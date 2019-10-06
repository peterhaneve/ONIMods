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
using UnityEngine;

namespace PeterHan.QueueForSinks {
	/// <summary>
	/// Patches which will be applied via annotations for Queue For Sinks.
	/// </summary>
	public sealed class QueueForSinkPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Applied to HandSanitizerConfig to add a checkpoint for hand sanitizers.
		/// </summary>
		[HarmonyPatch(typeof(HandSanitizerConfig), "DoPostConfigureComplete")]
		public static class HandSanitizerConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddComponent<SinkCheckpoint>();
			}
		}

		/// <summary>
		/// Applied to OreScrubberConfig to add a checkpoint for ore scrubbers.
		/// </summary>
		[HarmonyPatch(typeof(OreScrubberConfig), "DoPostConfigureComplete")]
		public static class OreScrubberConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddComponent<ScrubberCheckpoint>();
			}
		}

		/// <summary>
		/// Applied to WashBasinConfig to add a checkpoint for wash basins.
		/// </summary>
		[HarmonyPatch(typeof(WashBasinConfig), "DoPostConfigureComplete")]
		public static class WashBasinConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddComponent<SinkCheckpoint>();
			}
		}

		/// <summary>
		/// Applied to WashSinkConfig to add a checkpoint for sinks.
		/// </summary>
		[HarmonyPatch(typeof(WashSinkConfig), "DoPostConfigureComplete")]
		public static class WashSinkConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddComponent<SinkCheckpoint>();
			}
		}
	}
}
