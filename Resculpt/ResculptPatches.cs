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

namespace PeterHan.Resculpt {
	/// <summary>
	/// Patches which will be applied via annotations for Resculpt.
	/// </summary>
	public static class ResculptPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
		}

		/// <summary>
		/// Applied to Artable to allow repainting.
		/// </summary>
		[HarmonyPatch(typeof(Artable), "OnSpawn")]
		public static class Artable_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Artable __instance) {
				var go = __instance.gameObject;
				if (go != null) {
					var rs = go.AddOrGet<Resculptable>();
					// Is it a painting?
					if (__instance is Painting)
						rs.ButtonText = ResculptStrings.REPAINT_BUTTON;
				}
			}
		}
	}
}
