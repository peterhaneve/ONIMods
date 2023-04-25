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

using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeterHan.FastSave {
	/// <summary>
	/// Patches which will be applied via annotations for Fast Save.
	/// </summary>
	public sealed class FastSavePatches : KMod.UserMod2 {
		/// <summary>
		/// Waits in a coroutine for the GPU to complete uploading the timelapse image, and
		/// then starts a background task to save it.
		/// </summary>
		/// <param name="rt">The texture where the timelapse image was rendered.</param>
		/// <param name="savePath">The path to save the image.</param>
		/// <param name="worldID">The ID of the world to write.</param>
		/// <param name="preview">true if the image is a colony preview.</param>
		private static System.Collections.IEnumerator TimelapseCoroutine(RenderTexture rt,
				string savePath, int worldID, bool preview) {
			int width = rt.width, height = rt.height;
			if (width > 0 && height > 0) {
				var request = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
				// Wait for texture to be read back from the GPU
				while (!request.done)
					yield return null;
				if (request.hasError) {
					PUtil.LogWarning("Error saving background timelapse image!");
					var oldRT = RenderTexture.active;
					RenderTexture.active = rt;
					Game.Instance.timelapser.WriteToPng(rt, worldID);
					RenderTexture.active = oldRT;
				} else {
					byte[] rawARGB = request.GetData<byte>().ToArray();
					if (rawARGB != null)
						BackgroundTimelapser.Instance.Start(savePath, TextureToPNG(rawARGB,
							width, height), worldID, preview);
				}
			}
		}

		/// <summary>
		/// Converts raw image data in ARGB32 format (the format used by the game for the
		/// camera render texture) to PNG image data.
		/// </summary>
		/// <param name="rawData">The raw texture data.</param>
		/// <param name="width">The image width.</param>
		/// <param name="height">The image height.</param>
		/// <returns>The image encoded as PNG.</returns>
		private static byte[] TextureToPNG(byte[] rawData, int width, int height) {
			// NOTE: The game uses ARGB32 as the RenderTexture format, but for some reason
			// the returned data is RGBA32...
			var pngTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
#if DEBUG
			PUtil.LogDebug("Copying texture {0:D}x{1:D} to render".F(width, height));
#endif
			byte[] data;
			try {
				pngTexture.LoadRawTextureData(rawData);
				pngTexture.Apply();
				data = pngTexture.EncodeToPNG();
			} finally {
				Object.Destroy(pngTexture);
			}
			return data;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(FastSaveOptions));
			// Sorry, Fast Save now requires a restart to take effect because of background!
			PUtil.LogDebug("FastSave in mode: {0}".F(FastSaveOptions.Instance.Mode));
			new PLocalization().Register();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to Game to move part of the autosave to a background thread.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DelayedSave")]
		public static class Game_DelayedSave_Patch {
			internal static bool Prepare() {
				// Only enable if background save is on
				return FastSaveOptions.Instance.BackgroundSave;
			}

			/// <summary>
			/// Applied before DelayedSave runs.
			/// </summary>
			internal static System.Collections.IEnumerator Postfix(
					System.Collections.IEnumerator result, string filename, bool isAutoSave,
					Game.SavingPostCB ___activatePostCB, bool updateSavePointer,
					Game.SavingActiveCB ___activateActiveCB) {
				var inst = PlayerController.Instance;
				if (isAutoSave) {
					var bi = BackgroundAutosave.Instance;
					// Wait for player to stop dragging
					while (inst.IsDragging())
						yield return null;
					inst.CancelDragging();
					inst.AllowDragging(false);
					BackgroundAutosave.DisableSaving();
					try {
						yield return null;
						if (___activateActiveCB != null) {
							___activateActiveCB();
							yield return null;
						}
						var t = Game.Instance.timelapser;
						// Save in the background
						if (t != null)
							t.SaveColonyPreview(filename);
						bi.StartSave(filename);
						yield return null;
						RetireColonyUtility.SaveColonySummaryData();
						// Wait asynchronously for it
						while (!bi.CheckSaveStatus())
							yield return null;
						if (updateSavePointer)
							SaveLoader.SetActiveSaveFilePath(filename);
						___activatePostCB?.Invoke();
						for (int i = 0; i < 5; i++)
							yield return null;
					} finally {
						BackgroundAutosave.EnableSaving();
						inst.AllowDragging(true);
					}
				} else
					// Original method
					while (result.MoveNext())
						yield return result.Current;
			}
		}

		/// <summary>
		/// Applied to ReportManager to remove old daily reports.
		/// </summary>
		[HarmonyPatch(typeof(ReportManager), "OnNightTime")]
		public static class ReportManager_OnNightTime_Patch {
			/// <summary>
			/// Applied after OnNightTime runs.
			/// </summary>
			internal static void Postfix(List<ReportManager.DailyReport> ___dailyReports) {
				int keep, n = ___dailyReports.Count;
				// Select the threshold based on settings
				switch (FastSaveOptions.Instance.Mode) {
				case FastSaveOptions.FastSaveMode.Aggressive:
					keep = FastSaveOptions.SUMMARY_AGGRESSIVE;
					break;
				case FastSaveOptions.FastSaveMode.Moderate:
					keep = FastSaveOptions.SUMMARY_MODERATE;
					break;
				case FastSaveOptions.FastSaveMode.Safe:
				default:
					keep = int.MaxValue;
					break;
				}
				if (n > keep) {
					// Take last N reports
					var newReports = ListPool<ReportManager.DailyReport, FastSavePatches>.
						Allocate();
					int start = n - keep;
					for (int i = 0; i < keep; i++)
						newReports.Add(___dailyReports[i + start]);
					___dailyReports.Clear();
					___dailyReports.AddRange(newReports);
					newReports.Recycle();
#if DEBUG
					PUtil.LogDebug("Cleared {0:D} daily report(s)".F(start));
#endif
				}
			}
		}

		/// <summary>
		/// Applied to ReportScreen to avoid crashing when deleted reports are shown.
		/// </summary>
		[HarmonyPatch(typeof(ReportScreen), "Refresh")]
		public static class ReportScreen_Refresh_Patch {
			/// <summary>
			/// Applied after Refresh runs.
			/// </summary>
			internal static void Postfix(ReportManager.DailyReport ___currentReport,
					KButton ___prevButton) {
				int prevDay = ___currentReport.day - 1;
				if (ReportManager.Instance.FindReport(prevDay) == null)
					// Do not allow previous day if it cannot be found
					___prevButton.isInteractable = false;
			}
		}

		/// <summary>
		/// Applied to RetiredColonyInfoScreen to resize the charts to available statistics.
		/// </summary>
		[HarmonyPatch(typeof(RetiredColonyInfoScreen), "ConfigureGraph")]
		public static class RetiredColonyInfoScreen_ConfigureGraph_Patch {
			/// <summary>
			/// Applied after ConfigureGraph runs.
			/// </summary>
			internal static void Postfix(RetiredColonyData.RetiredColonyStatistic statistic,
					Dictionary<string, GameObject> ___activeColonyWidgets,
					Dictionary<string, Color> ___statColors) {
				var ri = ReportManager.Instance;
				var reports = (ri == null) ? null : ri.reports;
				if (___activeColonyWidgets.TryGetValue(statistic.name, out var obj)) {
					// Find first remaining report's cycle index
					int minReport = (reports == null || reports.Count < 1) ? 0 : reports[0].
						day;
					var graph = obj.GetComponentInChildren<GraphBase>();
					var lineLayer = obj.GetComponentInChildren<LineLayer>();
					if (graph != null && lineLayer != null) {
						// Update the graph min X value to that cycle #
						graph.axis_x.min_value = minReport;
						graph.RefreshGuides();
						lineLayer.ClearLines();
						// Recreate the line with the correct scale
						var graphedLine = lineLayer.NewLine(statistic.value, statistic.id);
						var lineFormatting = lineLayer.line_formatting;
						int lastIndex = lineFormatting.Length - 1;
						// Reassign the color (yes lots of duplicate work but a transpiler
						// would be super annoying)
						if (___statColors.TryGetValue(statistic.id, out Color color))
							lineFormatting[lastIndex].color = color;
						graphedLine.line_renderer.color = lineFormatting[lastIndex].color;
					}
				}
			}
		}

		/// <summary>
		/// Applied to Timelapser to save the PNG on a background thread.
		/// </summary>
		[HarmonyPatch(typeof(Timelapser), "RenderAndPrint")]
		public static class Timelapser_RenderAndPrint_Patch {
			internal static bool Prepare() {
				// Only enable if background save is on
				return FastSaveOptions.Instance.BackgroundSave;
			}

			/// <summary>
			/// Applied before RenderAndPrint runs.
			/// </summary>
			internal static bool Prefix(RenderTexture ___bufferRenderTexture, float ___camSize,
					string ___previewSaveGamePath, bool ___previewScreenshot,
					Vector3 ___camPosition, int world_id) {
				var world = ClusterManager.Instance.GetWorld(world_id);
				var rt = ___bufferRenderTexture;
				var inst = CameraController.Instance;
				if (world != null && rt != null && inst != null) {
					float z = inst.transform.position.z;
					if (world.IsStartWorld) {
						var telepad = GameUtil.GetTelepad(world_id);
						if (telepad == null)
							Debug.Log("No telepad present, aborting screenshot.");
						else {
							var centerPos = telepad.transform.position;
							centerPos.z = z;
							inst.SetPosition(centerPos);
						}
					} else
						inst.SetPosition(new Vector3(world.WorldOffset.x + world.WorldSize.x *
							0.5f, world.WorldOffset.y + world.WorldSize.y * 0.5f, z));
					var oldRT = RenderTexture.active;
					// Center camera on the printing pod
					RenderTexture.active = rt;
					inst.RenderForTimelapser(ref rt);
					inst.StartCoroutine(TimelapseCoroutine(rt, ___previewSaveGamePath,
						world_id, ___previewScreenshot));
					inst.OrthographicSize = ___camSize;
					inst.SetPosition(___camPosition);
					RenderTexture.active = oldRT;
				}
				return false;
			}
		}
	}
}
