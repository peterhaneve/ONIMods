/*
 * Copyright 2023 Peter Han
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

using PeterHan.PLib.Core;
using System;
using System.IO;
using System.Threading;

namespace PeterHan.FastSave {
	/// <summary>
	/// Manages taking the colony timelapse in the background.
	/// </summary>
	internal sealed class BackgroundTimelapser {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BackgroundTimelapser Instance { get; } = new BackgroundTimelapser();
		
		private BackgroundTimelapser() { }

		/// <summary>
		/// Starts saving the colony preview image in the background.
		/// </summary>
		/// <param name="previewPath">The path to save the preview; ignored if saving a colony summary / timelapse image.</param>
		/// <param name="rawData">The image data to save, encoded as PNG.</param>
		/// <param name="worldID">The ID of the world to write.</param>
		/// <param name="preview">true if the image is a colony preview, or false otherwise.</param>
		public void Start(string previewPath, byte[] rawData, int worldID, bool preview) {
#if DEBUG
			PUtil.LogDebug("Encoding preview image");
#endif
			var data = new BackgroundTimelapseData(previewPath, rawData, worldID, preview);
			var task = new Thread(data.DoSave);
			Util.ApplyInvariantCultureToThread(task);
			task.Priority = ThreadPriority.BelowNormal;
			task.Name = "Background Timelapser";
			Thread.MemoryBarrier();
			task.Start();
		}
	}

	/// <summary>
	/// Stores information for the handoff to PNG file writing in the background.
	/// </summary>
	internal sealed class BackgroundTimelapseData {
		/// <summary>
		/// Whether the screenshot is a colony preview.
		/// </summary>
		internal bool Preview { get; }

		/// <summary>
		/// The timelapse preview path to be written. Only valid if Preview is true.
		/// </summary>
		internal string SaveGamePath { get; }

		/// <summary>
		/// The screenshot data.
		/// </summary>
		internal byte[] RawData { get; }

		/// <summary>
		/// The ID of the world being written.
		/// </summary>
		internal int WorldID { get; }

		/// <summary>
		/// The name of the world being written.
		/// </summary>
		internal string WorldName { get; }

		public BackgroundTimelapseData(string savePath, byte[] rawData, int worldID,
				bool preview) {
			if (string.IsNullOrEmpty(savePath) && preview)
				throw new ArgumentNullException(nameof(savePath));
			Preview = preview;
			SaveGamePath = savePath;
			RawData = rawData ?? throw new ArgumentNullException(nameof(rawData));
			WorldID = worldID;
			if (worldID >= 0) {
				var world = ClusterManager.Instance.GetWorld(worldID);
				WorldName = world == null ? "" : world.GetComponent<ClusterGridEntity>().Name;
			} else
				WorldName = "";
		}

		/// <summary>
		/// Saves a preview image to disk in the background.
		/// </summary>
		internal void DoSave() {
			try {
				string retiredPath = Path.Combine(Util.RootFolder(), Util.
					GetRetiredColoniesFolderName());
				if (!Directory.Exists(retiredPath))
					// This call is recursive
					Directory.CreateDirectory(retiredPath);
				string saveName = RetireColonyUtility.StripInvalidCharacters(SaveGame.Instance.
					BaseName), path;
				if (Preview)
					// Colony preview
					path = Path.ChangeExtension(SaveGamePath, ".png");
				else {
					string saveFolder = Path.Combine(retiredPath, saveName);
					if (!string.IsNullOrWhiteSpace(WorldName)) {
						saveFolder = Path.Combine(saveFolder, WorldID.ToString("D5"));
						saveFolder = Path.Combine(saveFolder, WorldName);
						saveName = WorldName;
					}
					if (!Directory.Exists(saveFolder))
						Directory.CreateDirectory(saveFolder);
					// Suffix file name with the current cycle
					saveName += "_cycle_" + GameClock.Instance.GetCycle().ToString("0000.##") +
						".png";
					// debugScreenShot is always false
					path = Path.Combine(saveFolder, saveName);
				}
				PUtil.LogDebug("Saving screenshot to " + path);
				File.WriteAllBytes(path, RawData);
#if DEBUG
				PUtil.LogDebug("Background screenshot save complete");
#endif
			} catch (IOException e) {
				PUtil.LogWarning("Unable to save colony timelapse screenshot:");
				PUtil.LogExcWarn(e);
			} catch (UnauthorizedAccessException e) {
				PUtil.LogWarning("Unable to save colony timelapse screenshot:");
				PUtil.LogExcWarn(e);
			}
		}
	}
}
