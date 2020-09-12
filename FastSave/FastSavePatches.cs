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
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastSave {
	/// <summary>
	/// Patches which will be applied via annotations for Fast Save.
	/// </summary>
	public sealed class FastSavePatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
			POptions.RegisterOptions(typeof(FastSaveOptions));
			CleanUsageLogs.RegisterPostload();
			// Sorry, Fast Save now requires a restart to take effect because of background!
			PUtil.LogDebug("FastSave in mode: {0}".F(FastSaveOptions.Instance.Mode));
			PLocalization.Register();
		}

		/// <summary>
		/// Applied to Game.
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
				if (isAutoSave) {
					// Wait for player to stop dragging
					while (PlayerController.Instance.IsDragging())
						yield return null;
					PlayerController.Instance.AllowDragging(false);
					BackgroundAutosave.DisableSaving();
					try {
						yield return null;
						if (___activateActiveCB != null) {
							___activateActiveCB();
							yield return null;
						}
						// Save in the background
						Game.Instance.timelapser.SaveColonyPreview(filename);
						BackgroundAutosave.Instance.StartSave(filename);
						// Wait asynchronously for it
						while (!BackgroundAutosave.Instance.CheckSaveStatus())
							yield return null;
						if (updateSavePointer)
							SaveLoader.SetActiveSaveFilePath(filename);
						___activatePostCB?.Invoke();
						for (int i = 0; i < 5; i++)
							yield return null;
					} finally {
						BackgroundAutosave.EnableSaving();
						PlayerController.Instance.AllowDragging(true);
					}
					yield break;
				} else
					// Original method
					while (result.MoveNext())
						yield return result.Current;
			}
		}

		/// <summary>
		/// Applied to Timelapser to save the PNG on a background thread.
		/// </summary>
		[HarmonyPatch(typeof(Timelapser), nameof(Timelapser.WriteToPng))]
		public static class Timelapser_WriteToPng_Patch {
			internal static bool Prepare() {
				// Only enable if background save is on
				return FastSaveOptions.Instance.BackgroundSave;
			}

			/// <summary>
			/// Applied before WriteToPng runs.
			/// </summary>
			internal static bool Prefix(RenderTexture renderTex, string ___previewSaveGamePath,
					bool ___previewScreenshot) {
				if (renderTex != null) {
					int width = renderTex.width, height = renderTex.height;
					// Read pixels from the screen
					var screenData = new Texture2D(width, height, TextureFormat.ARGB32, false);
					screenData.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
					screenData.Apply();
					BackgroundTimelapser.Instance.Start(___previewSaveGamePath, screenData,
						___previewScreenshot);
					Object.Destroy(screenData);
				}
				return false;
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
				var reports = ReportManager.Instance?.reports;
				if (___activeColonyWidgets.TryGetValue(statistic.name, out GameObject obj)) {
					// Find first remaining report's cycle index
					int minReport = ((reports?.Count ?? 0) < 1) ? 0 : reports[0].day;
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
	}
}
