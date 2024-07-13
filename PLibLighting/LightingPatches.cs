/*
 * Copyright 2024 Peter Han
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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;

using LightGridEmitter = LightGridManager.LightGridEmitter;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Contains all patches (many!) required by the PLib Lighting subsystem. Only applied by
	/// the latest version of PLightManager.
	/// </summary>
	internal static class LightingPatches {
		private static readonly IDetouredField<Light2D, int> ORIGIN = PDetours.
			DetourFieldLazy<Light2D, int>("origin");

		private static readonly IDetouredField<LightShapePreview, int> PREVIOUS_CELL =
			PDetours.DetourFieldLazy<LightShapePreview, int>("previousCell");

		private static bool ComputeExtents_Prefix(Light2D __instance, ref Extents __result) {
			var lm = PLightManager.Instance;
			bool cont = true;
			if (lm != null && __instance != null) {
				var shape = __instance.shape;
				int rad = Mathf.CeilToInt(__instance.Range), cell;
				lm.AddLight(__instance.emitter, __instance.gameObject);
				if (shape > LightShape.Cone && rad > 0 && Grid.IsValidCell(cell = ORIGIN.Get(
						__instance))) {
					Grid.CellToXY(cell, out int x, out int y);
					// Better safe than sorry, check whole possible radius
					__result = new Extents(x - rad, y - rad, 2 * rad, 2 * rad);
					cont = false;
				}
			}
			return cont;
		}

		/// <summary>
		/// Applies the required lighting related patches.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching.</param>
		public static void ApplyPatches(Harmony plibInstance) {
			// Light2D
			plibInstance.Patch(typeof(Light2D), "ComputeExtents",
				prefix: PatchMethod(nameof(ComputeExtents_Prefix)));
			plibInstance.Patch(typeof(Light2D), nameof(Light2D.FullRemove), postfix:
				PatchMethod(nameof(Light2D_FullRemove_Postfix)));
			plibInstance.Patch(typeof(Light2D), nameof(Light2D.RefreshShapeAndPosition),
				postfix: PatchMethod(nameof(Light2D_RefreshShapeAndPosition_Postfix)));

			// LightBuffer
			try {
				plibInstance.PatchTranspile(typeof(LightBuffer), "LateUpdate", PatchMethod(
					nameof(LightBuffer_LateUpdate_Transpile)));
			} catch (Exception e) {
				// Only visual, log as warning
				PUtil.LogExcWarn(e);
			}

			// LightGridEmitter
			plibInstance.Patch(typeof(LightGridEmitter), "ComputeLux", prefix:
				PatchMethod(nameof(ComputeLux_Prefix)));
			plibInstance.Patch(typeof(LightGridEmitter), nameof(LightGridEmitter.
				UpdateLitCells), prefix: PatchMethod(nameof(UpdateLitCells_Prefix)));

			// LightGridManager
			plibInstance.Patch(typeof(LightGridManager).GetOverloadWithMostArguments(nameof(
				LightGridManager.CreatePreview), true, typeof(int), typeof(float),
				typeof(LightShape), typeof(int)),
				prefix: PatchMethod(nameof(CreatePreview_Prefix)));

			// LightShapePreview
			plibInstance.Patch(typeof(LightShapePreview), "Update", prefix:
				PatchMethod(nameof(LightShapePreview_Update_Prefix)));

			// Rotatable
			plibInstance.Patch(typeof(Rotatable), "OrientVisualizer", postfix:
				PatchMethod(nameof(OrientVisualizer_Postfix)));
		}

		private static bool ComputeLux_Prefix(LightGridEmitter __instance, int cell,
				LightGridEmitter.State ___state, ref int __result) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.GetBrightness(__instance, cell, ___state, out __result);
		}

		private static bool CreatePreview_Prefix(int origin_cell, float radius,
				LightShape shape, int lux) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.PreviewLight(origin_cell, radius, shape, lux);
		}

		private static void Light2D_FullRemove_Postfix(Light2D __instance) {
			var lm = PLightManager.Instance;
			if (lm != null && __instance != null)
				lm.DestroyLight(__instance.emitter);
		}

		private static void Light2D_RefreshShapeAndPosition_Postfix(Light2D __instance,
				Light2D.RefreshResult __result) {
			var lm = PLightManager.Instance;
			if (lm != null && __instance != null && __result == Light2D.RefreshResult.Updated)
				lm.AddLight(__instance.emitter, __instance.gameObject);
		}

		private static TranspiledMethod LightBuffer_LateUpdate_Transpile(
				TranspiledMethod body) {
			var target = typeof(Light2D).GetPropertySafe<LightShape>(nameof(Light2D.shape),
				false)?.GetGetMethod(true);
			return (target == null) ? body : PPatchTools.ReplaceMethodCallSafe(body, target,
				typeof(PLightManager).GetMethodSafe(nameof(PLightManager.LightShapeToRayShape),
				true, typeof(Light2D)));
		}

		private static void LightShapePreview_Update_Prefix(LightShapePreview __instance) {
			var lm = PLightManager.Instance;
			// Pass the preview object into LightGridManager
			if (lm != null && __instance != null)
				lm.PreviewObject = __instance.gameObject;
		}

		private static void OrientVisualizer_Postfix(Rotatable __instance) {
			// Force regeneration on next Update()
			if (__instance != null && __instance.TryGetComponent(out LightShapePreview
					preview))
				PREVIOUS_CELL.Set(preview, -1);
		}

		/// <summary>
		/// Gets a HarmonyMethod instance for manual patching using a method from this class.
		/// </summary>
		/// <param name="name">The method name.</param>
		/// <returns>A reference to that method as a HarmonyMethod for patching.</returns>
		private static HarmonyMethod PatchMethod(string name) {
			return new HarmonyMethod(typeof(LightingPatches), name);
		}

		private static bool UpdateLitCells_Prefix(LightGridEmitter __instance,
				List<int> ___litCells, LightGridEmitter.State ___state) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.UpdateLitCells(__instance, ___state, ___litCells);
		}
	}
}
