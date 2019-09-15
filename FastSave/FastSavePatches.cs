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
using System;
using System.Collections.Generic;

namespace PeterHan.FastSave {
	/// <summary>
	/// Patches which will be applied via annotations for Fast Save.
	/// </summary>
	public sealed class FastSavePatches {
		/// <summary>
		/// Time entries ending this many in-game seconds before the current time will be
		/// removed.
		/// </summary>
		private const float MAX_USAGE_RETAIN = 6200.0f;

		public static void OnLoad() {
			PUtil.LogModInit();
		}

		/// <summary>
		/// Cleans old time entries from the logs.
		/// </summary>
		/// <param name="values">The logged time entries.</param>
		/// <param name="time">The current game time.</param>
		private static void CleanTimes(List<Operational.TimeEntry> values, float time) {
			float threshold = time - MAX_USAGE_RETAIN;
			var newEntries = ListPool<Operational.TimeEntry, FastSavePatches>.Allocate();
			foreach (var entry in values) {
				if (entry.endTime > threshold || entry.startTime > threshold)
					newEntries.Add(entry);
			}
#if DEBUG
			PUtil.LogDebug("Deleted time entries: {0:D}".F(values.Count - newEntries.Count));
#endif
			values.Clear();
			values.AddRange(newEntries);
			newEntries.Recycle();
		}

		/// <summary>
		/// Applied to Operational to remove old time entries whenever the game is saved.
		/// </summary>
		[HarmonyPatch(typeof(Operational), "OnSerializing")]
		public static class Operational_OnSerializing_Patch {
			/// <summary>
			/// Applied before OnSerializing runs.
			/// </summary>
			internal static void Prefix(ref List<Operational.TimeEntry> ___activeTimes,
					ref List<Operational.TimeEntry> ___inactiveTimes) {
				float now = GameClock.Instance.GetTime();
				CleanTimes(___activeTimes, now);
				CleanTimes(___inactiveTimes, now);
			}
		}

#if false
		/// <summary>
		/// Applied to Operational to remove old time entries whenever entries are added.
		/// </summary>
		[HarmonyPatch(typeof(Operational), "AddTimeEntry")]
		public static class Operational_AddTimeEntry_Patch {
			/// <summary>
			/// Applied after AddTimeEntry runs.
			/// </summary>
			internal static void Postfix(ref List<Operational.TimeEntry> ___activeTimes,
					ref List<Operational.TimeEntry> ___inactiveTimes) {
				float now = GameClock.Instance.GetTime();
				CleanTimes(___activeTimes, now);
				CleanTimes(___inactiveTimes, now);
			}
		}
#endif
	}
}
