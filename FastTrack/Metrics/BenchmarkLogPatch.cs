/*
 * Copyright 2026 Peter Han
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
using UnityEngine;
using System;
using System.Globalization;

namespace PeterHan.FastTrack.Metrics {
	/// <summary>
	/// Applied to Game to log frame time and GC stats to Player.log for A/B benchmark
	/// comparisons. Off by default; enabled only via the BenchmarkLog option.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.Update))]
	public static class BenchmarkLogPatch {
		private static int frame;

		internal static bool Prepare() => FastTrackOptions.Instance.BenchmarkLog;

		/// <summary>
		/// Applied after Update runs to sample frame time and GC stats.
		/// </summary>
		internal static void Postfix() {
			long mem = GC.GetTotalMemory(false);
			int gen0 = GC.CollectionCount(0);
			Debug.Log(string.Format(CultureInfo.InvariantCulture,
				"[FT-BENCH] frame={0} ms={1:F3} gcMB={2:F2} gen0={3}",
				frame++, Time.unscaledDeltaTime * 1000.0, mem / 1048576.0, gen0));
		}
	}
}
