/*
 * Copyright 2020 Peter Han
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or
 * substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using PeterHan.PLib;
using PeterHan.PLib.Detours;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using ButtonInfo = KButtonMenu.ButtonInfo;
using PAUSE_SCREEN = STRINGS.UI.FRONTEND.PAUSE_SCREEN;

namespace PeterHan.FastSave {
	/// <summary>
	/// Manages autosaving in the background.
	/// </summary>
	internal sealed class BackgroundAutosave {
		/// <summary>
		/// The initial buffer size for the uncompressed save file.
		/// </summary>
		private const int BUFFER_SIZE = 1024 * 1024 * 4;

		private delegate void CompressContents(BinaryWriter writer, byte[] data, int length);

		private delegate List<string> GetSaveFiles(string save_dir);

		private static readonly IDetouredField<KButtonMenu, IList<ButtonInfo>> BUTTONS =
			PDetours.DetourField<KButtonMenu, IList<ButtonInfo>>("buttons");

		private static readonly CompressContents COMPRESS_CONTENTS = typeof(SaveLoader).
			Detour<CompressContents>();

		private static readonly IDetouredField<SaveLoader, bool> COMPRESS_SAVE_DATA =
			PDetours.DetourField<SaveLoader, bool>("compressSaveData");

		private static readonly GetSaveFiles GET_SAVE_FILES = typeof(SaveLoader).
			Detour<GetSaveFiles>();

		private static readonly Action<SaveLoader, BinaryWriter> SAVE =
			typeof(SaveLoader).Detour<Action<SaveLoader, BinaryWriter>>("Save");

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BackgroundAutosave Instance { get; } = new BackgroundAutosave();

		/// <summary>
		/// Enables save/load and removes the warning about quitting.
		/// </summary>
		internal static void EnableSaving() {
			var instance = PauseScreen.Instance;
			var buttons = (instance == null) ? null : BUTTONS.Get(instance);
			if (buttons != null && buttons.Count > 0) {
				foreach (var button in buttons) {
					// Enable "save", "save as", "load", "quit to menu"
					button.isEnabled = true;
					button.toolTip = null;
				}
				instance.RefreshButtons();
			}
		}

		/// <summary>
		/// Disables save/load and adds a warning about quitting.
		/// </summary>
		internal static void DisableSaving() {
			var instance = PauseScreen.Instance;
			var buttons = (instance == null) ? null : BUTTONS.Get(instance);
			if (buttons != null && buttons.Count > 0) {
				foreach (var button in buttons) {
					string text = button.text;
					// Disable "save", "save as", "load", "quit to menu"
					if (text == PAUSE_SCREEN.SAVE || text == PAUSE_SCREEN.SAVEAS || text ==
							PAUSE_SCREEN.LOAD || text == PAUSE_SCREEN.QUIT) {
						button.isEnabled = false;
						button.toolTip = FastSaveStrings.AUTOSAVE_PROGRESS;
					}
					if (text == PAUSE_SCREEN.DESKTOPQUIT)
						button.toolTip = FastSaveStrings.DESKTOP_QUIT_WARNING;
				}
				instance.RefreshButtons();
			}
		}

		/// <summary>
		/// Synchronizes starting and stopping threads.
		/// </summary>
		private readonly object startLock;

		/// <summary>
		/// The current save status.
		/// </summary>
		private SaveStatus status;

		private BackgroundAutosave() {
			startLock = new object();
			status = SaveStatus.NotStarted;
		}

		/// <summary>
		/// Checks the autosave status on the UI thread.
		/// </summary>
		/// <returns>true if save completed, or false otherwise.</returns>
		public bool CheckSaveStatus() {
			var status = this.status;
			bool done = false;
			switch (status) {
			case SaveStatus.Done:
				done = true;
				break;
			case SaveStatus.Failed:
				// Generic error
				done = true;
				PUIElements.ShowMessageDialog(GameScreenManager.Instance.ssOverlayCanvas.
					gameObject, string.Format(STRINGS.UI.CRASHSCREEN.SAVEFAILED,
					FastSaveStrings.AUTOSAVE_FAILED));
				break;
			case SaveStatus.IOError:
				// I/O error, raise a dialog
				done = true;
				PUIElements.ShowMessageDialog(GameScreenManager.Instance.ssOverlayCanvas.
					gameObject, string.Format(STRINGS.UI.CRASHSCREEN.SAVEFAILED,
					FastSaveStrings.IO_ERROR));
				break;
			case SaveStatus.InProgress:
				break;
			default:
				// NotStarted, time to throw
				throw new InvalidOperationException("Save not started");
			}
			return done;
		}

		/// <summary>
		/// Cleans up old autosaves.
		/// </summary>
		/// <param name="filename">The path to the current save.</param>
		private void CleanAutosaves(string filename) {
			if (!Klei.GenericGameSettings.instance.keepAllAutosaves) {
				string autoSavePath = PUtil.GameVersion > 420700 ? GetActiveAutoSavePath() :
					Path.GetDirectoryName(filename);
				// SearchOption.AllDirectories is the default value for Cloud Save Preview
				var saveFiles = GET_SAVE_FILES.Invoke(autoSavePath);
				// Clean up old autosaves and their preview images
				for (int i = saveFiles.Count - 1; i >= SaveLoader.MAX_AUTOSAVE_FILES - 1; i--) {
					string autoName = saveFiles[i], autoImage = Path.ChangeExtension(
						autoName, ".png");
					try {
						PUtil.LogDebug("Deleting old autosave: " + autoName);
						File.Delete(autoName);
					} catch (Exception e) {
						PUtil.LogWarning("Error deleting old autosave: " + autoName);
						PUtil.LogExcWarn(e);
					}
					try {
						if (File.Exists(autoImage))
							File.Delete(autoImage);
					} catch (Exception e) {
						PUtil.LogWarning("Error deleting old screenshot: " + autoImage);
						PUtil.LogExcWarn(e);
					}
				}
			}
		}

		/// <summary>
		/// Saves the game to disk in the background.
		/// </summary>
		/// <param name="data">The uncompressed save data.</param>
		private void DoSave(BackgroundSaveData data) {
			try {
				var stream = data.Stream;
				PUtil.LogDebug("Background save to: " + data.FileName);
				stream.Seek(0L, SeekOrigin.Begin);
				CleanAutosaves(data.FileName);
				// Write the file header
				using (var writer = new BinaryWriter(File.Open(data.FileName, FileMode.
						Create))) {
					var saveHeader = SaveGame.Instance.GetSaveHeader(true, data.Compress,
						out SaveGame.Header header);
					writer.Write(header.buildVersion);
					writer.Write(header.headerSize);
					writer.Write(header.headerVersion);
					writer.Write(header.compression);
					writer.Write(saveHeader);
					KSerialization.Manager.SerializeDirectory(writer);
					writer.Flush();
					if (data.Compress)
						COMPRESS_CONTENTS.Invoke(writer, stream.GetBuffer(), (int)stream.
							Length);
					else
						stream.CopyTo(writer.BaseStream);
				}
				status = SaveStatus.Done;
				PUtil.LogDebug("Background save complete");
			} catch (IOException e) {
				// Autosave error!
				PUtil.LogExcWarn(e);
				status = SaveStatus.IOError;
			} catch (Exception e) {
				// Allowing it to continue here will crash with a simdll error
				PUtil.LogException(e);
				status = SaveStatus.Failed;
			} finally {
				try {
					// Cannot throw during a finally, or it will discard the original exception
					data.Dispose();
				} catch { }
			}
		}

		/// <summary>
		/// Gets the path to the current autosave folder.
		/// </summary>
		/// <returns>The location where autosaves are saved.</returns>
		private static string GetActiveAutoSavePath() {
			string filename = SaveLoader.GetActiveSaveFilePath();
			bool flag = filename == null;
			string result;
			if (flag) {
				result = SaveLoader.GetAutoSavePrefix();
			} else {
				string root = Path.GetDirectoryName(filename);
				result = Path.Combine(root, "auto_save");
			}
			return result;
		}

		/// <summary>
		/// Starts saving the game in the background. This function is not reentrant!
		/// </summary>
		/// <param name="filename">The file name where the save should be stored.</param>
		public void StartSave(string filename) {
			var buffer = new MemoryStream(BUFFER_SIZE);
			var inst = SaveLoader.Instance;
			bool save = true;
			if (inst != null) {
				KSerialization.Manager.Clear();
#if DEBUG
				PUtil.LogDebug("Starting serialization of save");
#endif
				bool compress = true;
				// This field is currently always true
				if (COMPRESS_SAVE_DATA != null)
					try {
						compress = COMPRESS_SAVE_DATA.Get(inst);
					} catch { }
				// Keep this part on the foreground
				try {
					SAVE.Invoke(inst, new BinaryWriter(buffer));
				} catch (Exception e) {
					buffer.Dispose();
					PUtil.LogError("Error when saving game:");
					PUtil.LogException(e);
					save = false;
				}
				// In Unity 4 GetComponent no longer works on background threads
				if (save)
					StartSave(new BackgroundSaveData(buffer, compress, filename));
			}
		}

		/// <summary>
		/// Starts saving the game in the background.
		/// </summary>
		/// <param name="data">The uncompressed save data.</param>
		private void StartSave(BackgroundSaveData data) {
			// Wait until current save is complete (best effort)
			while (true)
				lock (startLock) {
					if (status != SaveStatus.InProgress) {
#if DEBUG
						PUtil.LogDebug("Switching save to background task");
#endif
						var task = new Thread(() => DoSave(data));
						status = SaveStatus.InProgress;
						Util.ApplyInvariantCultureToThread(task);
						task.Priority = ThreadPriority.BelowNormal;
						task.Name = "Background Autosave";
						Thread.MemoryBarrier();
						task.Start();
						break;
					}
				}
		}
	}

	/// <summary>
	/// Stores information for the handoff to save file writing in the background.
	/// </summary>
	internal sealed class BackgroundSaveData : IDisposable {
		/// <summary>
		/// Whether the save data should be compressed.
		/// </summary>
		internal bool Compress { get; }

		/// <summary>
		/// The target save file name.
		/// </summary>
		internal string FileName { get; }

		/// <summary>
		/// The uncompressed save data.
		/// </summary>
		internal MemoryStream Stream { get; }

		public BackgroundSaveData(MemoryStream stream, bool compress, string filename) {
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException("filename");
			Compress = compress;
			Stream = stream ?? throw new ArgumentNullException("stream");
			FileName = filename;
		}

		public void Dispose() {
			Stream.Dispose();
		}
	}

	/// <summary>
	/// The possible status results for autosave.
	/// </summary>
	internal enum SaveStatus {
		NotStarted, InProgress, Done, Failed, IOError
	}
}
