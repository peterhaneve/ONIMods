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

using Harmony;
using Ionic.Zlib;
using PeterHan.PLib;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

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

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BackgroundAutosave Instance { get; } = new BackgroundAutosave();

		/// <summary>
		/// Writes the image to a PNG in the background.
		/// </summary>
		/// <param name="instance">The time lapser that is recording the image.</param>
		/// <param name="texture">The texture to write.</param>
		internal static void BackgroundWritePng(Timelapser instance, RenderTexture texture) {
			instance.WriteToPng(texture);
		}

		/// <summary>
		/// Enables save/load and removes the warning about quitting.
		/// </summary>
		internal static void EnableSaving() {
			var instance = PauseScreen.Instance;
			var buttons = (instance == null) ? null : Traverse.Create(instance).
				GetField<IList<ButtonInfo>>("buttons");
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
			var buttons = (instance == null) ? null : Traverse.Create(instance).
				GetField<IList<ButtonInfo>>("buttons");
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
					"Autosave failed!"));
				break;
			case SaveStatus.IOError:
				// I/O error, raise a dialog
				done = true;
				PUIElements.ShowMessageDialog(GameScreenManager.Instance.ssOverlayCanvas.
					gameObject, string.Format(STRINGS.UI.CRASHSCREEN.SAVEFAILED,
					"IOException. You may not have enough free space!"));
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
		/// <param name="filename">The file name that is being saved.</param>
		private void CleanAutosaves(string filename) {
			if (!Klei.GenericGameSettings.instance.keepAllAutosaves) {
				var saveFiles = SaveLoader.GetSaveFiles(Path.GetDirectoryName(filename));
				// Clean up old autosaves and their preview images
				for (int i = saveFiles.Count - 1; i >= SaveLoader.MAX_AUTOSAVE_FILES - 1; i--) {
					string autoName = saveFiles[i], autoImage = Path.ChangeExtension(
						autoName, ".png");
					try {
						PUtil.LogDebug("Deleting old autosave: " + autoName);
						File.Delete(autoName);
					} catch (Exception e) {
						PUtil.LogWarning("Problem deleting old autosave: " + autoName);
						PUtil.LogExcWarn(e);
					}
					try {
						if (File.Exists(autoImage))
							File.Delete(autoImage);
					} catch (Exception e) {
						PUtil.LogWarning("Problem deleting old screenshot: " + autoImage);
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
						// SaveLoader.CompressContents is now private
						using (var compressor = new ZlibStream(writer.BaseStream,
								CompressionMode.Compress, CompressionLevel.BestSpeed)) {
							stream.CopyTo(compressor);
							compressor.Flush();
						}
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
				} catch (Exception e) {
					PUtil.LogException(e);
				}
			}
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
				var trLoader = Traverse.Create(inst);
				KSerialization.Manager.Clear();
#if DEBUG
				PUtil.LogDebug("Starting serialization of save");
#endif
				bool compress = true;
				// This field is currently always true
				try {
					compress = trLoader.GetField<bool>("compressSaveData");
				} catch { }
				// Keep this part on the foreground
				try {
					trLoader.CallMethod("Save", new BinaryWriter(buffer));
				} catch (Exception e) {
					buffer.Dispose();
					PUtil.LogError("Error when saving game:");
					PUtil.LogException(e);
					save = false;
				}
				// In Unity 4 GetComponent no longer works on background threads
				RetireColonyUtility.SaveColonySummaryData();
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
						task.Priority = System.Threading.ThreadPriority.BelowNormal;
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
