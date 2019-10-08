/*
 * Copyright 2019 Peter Han
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
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastSave {
	/// <summary>
	/// Patches which will be applied via annotations for Fast Save.
	/// </summary>
	public sealed class FastSavePatches {
		/// <summary>
		/// The options read from the config file.
		/// </summary>
		private static FastSaveOptions options;

		public static void OnLoad() {
			PUtil.InitLibrary();
			options = new FastSaveOptions();
			POptions.RegisterOptions(typeof(FastSaveOptions));
#if false
			PUtil.RegisterPostload(OnPostLoad);
#endif
		}

#if false
		/// <summary>
		/// Cleans old time entries from the logs.
		/// </summary>
		/// <param name="values">The logged time entries.</param>
		/// <param name="time">The current game time.</param>
		private static void CleanTimes(List<Operational.TimeEntry> values, float time) {
			float threshold = time;
			// Select the threshold based on settings
			switch (options.Mode) {
			case FastSaveOptions.FastSaveMode.Aggressive:
				threshold -= FastSaveOptions.USAGE_AGGRESSIVE;
				break;
			case FastSaveOptions.FastSaveMode.Moderate:
				threshold -= FastSaveOptions.USAGE_MODERATE;
				break;
			case FastSaveOptions.FastSaveMode.Safe:
			default:
				threshold -= FastSaveOptions.USAGE_SAFE;
				break;
			}
			// Delete old entries
			var newEntries = ListPool<Operational.TimeEntry, FastSavePatches>.Allocate();
			foreach (var entry in values)
				if (entry.endTime > threshold || entry.startTime > threshold)
					newEntries.Add(entry);
#if DEBUG
			PUtil.LogDebug("Deleted time entries: {0:D}".F(values.Count - newEntries.Count));
#endif
			values.Clear();
			values.AddRange(newEntries);
			newEntries.Recycle();
		}

		/// <summary>
		/// Invoked after all other mods load.
		/// </summary>
		/// <param name="hInst">The Harmony instance to use for patching.</param>
		private static void OnPostLoad(HarmonyInstance hInst) {
			try {
				hInst.Patch(typeof(Operational), "OnSerializing", new HarmonyMethod(
					typeof(FastSavePatches), "OnSerializing_Prefix"), null);
			} catch (Exception e) {
				PUtil.LogWarning("Caught {0}, disabling Operational history trimming".F(e.
					GetType()));
			}
		}

		/// <summary>
		/// Applied before OnSerializing runs.
		/// </summary>
		internal static void OnSerializing_Prefix(List<Operational.TimeEntry> ___activeTimes,
				List<Operational.TimeEntry> ___inactiveTimes) {
			float now = GameClock.Instance.GetTime();
			CleanTimes(___activeTimes, now);
			CleanTimes(___inactiveTimes, now);
		}
#endif

		/// <summary>
		/// Applied to Game to load settings when the mod starts up.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied before OnPrefabInit runs.
			/// </summary>
			internal static void Prefix() {
				options = POptions.ReadSettings<FastSaveOptions>() ?? new FastSaveOptions();
				PUtil.LogDebug("FastSave in mode: {0}".F(options.Mode));
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
				switch (options.Mode) {
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
					int minReport = (reports == null) ? 0 : reports[0].day;
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
