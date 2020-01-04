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
using UnityEngine;

namespace PeterHan.Resculpt {
	/// <summary>
	/// Patches which will be applied via annotations for Resculpt.
	/// </summary>
	public static class ResculptPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Applied to CanvasConfig to allow repainting.
		/// </summary>
		[HarmonyPatch(typeof(CanvasConfig), "DoPostConfigureComplete")]
		public static class CanvasConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>().ButtonText = ResculptStrings.REPAINT_BUTTON;
			}
		}

		/// <summary>
		/// Applied to CanvasTallConfig to allow repainting.
		/// </summary>
		[HarmonyPatch(typeof(CanvasTallConfig), "DoPostConfigureComplete")]
		public static class CanvasTallConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>().ButtonText = ResculptStrings.REPAINT_BUTTON;
			}
		}

		/// <summary>
		/// Applied to CanvasWideConfig to allow repainting.
		/// </summary>
		[HarmonyPatch(typeof(CanvasWideConfig), "DoPostConfigureComplete")]
		public static class CanvasWideConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>().ButtonText = ResculptStrings.REPAINT_BUTTON;
			}
		}

		/// <summary>
		/// Applied to MarbleSculptureConfig to allow resculpting.
		/// </summary>
		[HarmonyPatch(typeof(MarbleSculptureConfig), "DoPostConfigureComplete")]
		public static class MarbleSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>();
			}
		}

		/// <summary>
		/// Applied to MetalSculptureConfig to allow resculpting.
		/// </summary>
		[HarmonyPatch(typeof(MetalSculptureConfig), "DoPostConfigureComplete")]
		public static class MetalSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>();
			}
		}

		/// <summary>
		/// Applied to IceSculptureConfig to allow resculpting.
		/// </summary>
		[HarmonyPatch(typeof(IceSculptureConfig), "DoPostConfigureComplete")]
		public static class IceSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>();
			}
		}

		/// <summary>
		/// Applied to SculptureConfig to allow resculpting.
		/// </summary>
		[HarmonyPatch(typeof(SculptureConfig), "DoPostConfigureComplete")]
		public static class SculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>();
			}
		}

		/// <summary>
		/// Applied to SmallSculptureConfig to allow resculpting.
		/// </summary>
		[HarmonyPatch(typeof(SmallSculptureConfig), "DoPostConfigureComplete")]
		public static class SmallSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<Resculptable>();
			}
		}

		/// <summary>
		/// Applied to Artable to refresh the user menu when work is completed to show the
		/// resculpt button.
		/// </summary>
		[HarmonyPatch(typeof(Artable), "OnCompleteWork")]
		public static class Artable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied after OnCompleteWork runs.
			/// </summary>
			internal static void Postfix(Artable __instance) {
				var obj = __instance.gameObject;
				if (obj != null)
					Game.Instance?.userMenu?.Refresh(obj);
			}
		}
	}
}
