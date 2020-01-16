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
using System;
using System.IO;
using System.Threading;
using UnityEngine;

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
		/// Saves a preview image to disk in the background.
		/// </summary>
		/// <param name="data">The uncompressed preview image data.</param>
		private void DoSave(BackgroundTimelapseData data) {
			var screenData = data.TextureData;
			string previewPath = data.SaveGamePath;
			bool preview = data.Preview;
			byte[] rawPNG;
			// Encode PNG (this is the woofy part on high resolution)
#if DEBUG
			PUtil.LogDebug("Encoding preview image");
#endif
			try {
				rawPNG = screenData.EncodeToPNG();
			} finally {
				data.Dispose();
			}
			try {
				string retiredPath = Path.Combine(Util.RootFolder(), Util.
					GetRetiredColoniesFolderName());
				if (!Directory.Exists(retiredPath))
					// This call is recursive
					Directory.CreateDirectory(retiredPath);
				string saveName = RetireColonyUtility.StripInvalidCharacters(SaveGame.Instance.
					BaseName), path;
				if (preview)
					// Colony preview
					path = Path.ChangeExtension(previewPath, ".png");
				else {
					string saveFolder = Path.Combine(retiredPath, saveName);
					if (!Directory.Exists(saveFolder))
						Directory.CreateDirectory(saveFolder);
					// Suffix file name with the current cycle
					saveName += "_cycle" + GameClock.Instance.GetCycle().ToString("0000.##");
					// debugScreenShot is always false
					path = Path.Combine(saveFolder, saveName);
				}
				PUtil.LogDebug("Saving screenshot to " + path);
				File.WriteAllBytes(path, rawPNG);
				rawPNG = null;
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

		/// <summary>
		/// Starts saving the colony preview image in the background.
		/// </summary>
		/// <param name="preview">true if the image is a colony preview, or false otherwise.</param>
		/// <param name="previewPath">The path to save the preview; ignored if saving a colony summary / timelapse image.</param>
		/// <param name="textureData">The texture data to save.</param>
		public void Start(string previewPath, Texture2D textureData, bool preview) {
#if DEBUG
			PUtil.LogDebug("Starting preview image save");
#endif
			var task = new Thread(() => DoSave(new BackgroundTimelapseData(previewPath,
				textureData, preview)));
			Util.ApplyInvariantCultureToThread(task);
			task.Priority = System.Threading.ThreadPriority.BelowNormal;
			task.Name = "Background Timelapser";
			Thread.MemoryBarrier();
			task.Start();
		}
	}

	/// <summary>
	/// Stores information for the handoff to PNG file writing in the background.
	/// </summary>
	internal sealed class BackgroundTimelapseData : IDisposable {
		/// <summary>
		/// The timelapse preview path to be written. Only valid if Preview is true.
		/// </summary>
		internal string SaveGamePath { get; }

		/// <summary>
		/// Whether the screenshot is a colony preview.
		/// </summary>
		internal bool Preview { get; }

		/// <summary>
		/// The screenshot data.
		/// </summary>
		internal Texture2D TextureData { get; }

		public BackgroundTimelapseData(string savePath, Texture2D textureData, bool preview) {
			if (string.IsNullOrEmpty(savePath) && preview)
				throw new ArgumentNullException("savePath");
			SaveGamePath = savePath;
			Preview = preview;
			TextureData = textureData ?? throw new ArgumentNullException("textureData");
		}

		public void Dispose() {
			UnityEngine.Object.Destroy(TextureData);
		}
	}
}
