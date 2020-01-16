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
using System;
using System.Collections.Generic;

namespace PeterHan.FastSave {
	/// <summary>
	/// Cleans out old machine usage logs. This class is no longer required.
	/// </summary>
	internal static class CleanUsageLogs {
		/// <summary>
		/// Registers a postload patch to clean up the usage logs.
		/// </summary>
		internal static void RegisterPostload() {
#if CLEAN_USAGE_LOGS
			PUtil.RegisterPostload(OnPostLoad);
#endif
		}

#if CLEAN_USAGE_LOGS
		/// <summary>
		/// Cleans old time entries from the logs.
		/// </summary>
		/// <param name="values">The logged time entries.</param>
		/// <param name="time">The current game time.</param>
		private static void CleanTimes(List<Operational.TimeEntry> values, float time) {
			float threshold = time;
			// Select the threshold based on settings
			switch (FastSaveOptions.Instance.Mode) {
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
	}
#endif
	}
}
