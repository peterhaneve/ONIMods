/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.Core;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// Patches which will be applied via annotations for Queue For Sinks.
	/// </summary>
	public sealed class QueueForSinkPatches : KMod.UserMod2 {
		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Applied to HandSanitizer.Work to add a checkpoint for hand sanitizers, sinks, and
		/// wash basins.
		/// </summary>
		[HarmonyPatch(typeof(HandSanitizer.Work), "OnPrefabInit")]
		public static class HandSanitizer_Work_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(HandSanitizer.Work __instance) {
				__instance.gameObject.AddOrGet<SinkCheckpoint>();
			}
		}

		/// <summary>
		/// Applied to OreScrubber.Work to add a checkpoint for ore scrubbers.
		/// </summary>
		[HarmonyPatch(typeof(OreScrubber.Work), "OnPrefabInit")]
		public static class OreScrubberConfig_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(OreScrubber.Work __instance) {
				__instance.gameObject.AddOrGet<ScrubberCheckpoint>();
			}
		}
	}
}
