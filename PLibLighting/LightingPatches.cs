/*
 * Copyright 2022 Peter Han
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

using IntHandle = HandleVector<int>.Handle;
using LightGridEmitter = LightGridManager.LightGridEmitter;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Contains all patches (many!) required by the PLib Lighting subsystem. Only applied by
	/// the latest version of PLightManager.
	/// </summary>
	internal static class LightingPatches {
		private delegate IntHandle AddToLayerDelegate(Light2D instance, Extents ext,
			ScenePartitionerLayer layer);

		private static readonly DetouredMethod<AddToLayerDelegate> ADD_TO_LAYER =
			typeof(Light2D).DetourLazy<AddToLayerDelegate>("AddToLayer");

		private static readonly IDetouredField<Light2D, int> ORIGIN = PDetours.
			DetourFieldLazy<Light2D, int>("origin");

		private static readonly IDetouredField<LightShapePreview, int> PREVIOUS_CELL =
			PDetours.DetourFieldLazy<LightShapePreview, int>("previousCell");

		/// <summary>
		/// Applied to LightGridEmitter to unattribute lighting sources.
		/// </summary>
		private static void AddToGrid_Postfix() {
			var lm = PLightManager.Instance;
			if (lm != null)
				lm.CallingObject = null;
		}

		/// <summary>
		/// Applied to Light2D to properly attribute lighting sources.
		/// </summary>
		private static bool AddToScenePartitioner_Prefix(Light2D __instance,
				ref IntHandle ___solidPartitionerEntry,
				ref IntHandle ___liquidPartitionerEntry) {
			var lm = PLightManager.Instance;
			bool cont = true;
			if (lm != null) {
				// Replace the whole method since the radius could use different algorithms
				lm.CallingObject = __instance.gameObject;
				cont = !AddScenePartitioner(__instance, ref ___solidPartitionerEntry,
					ref ___liquidPartitionerEntry);
			}
			return cont;
		}

		/// <summary>
		/// Replaces the scene partitioner method to register lights for tile changes in
		/// their active radius.
		/// </summary>
		/// <param name="instance">The light to register.</param>
		/// <param name="solidPart">The solid partitioner registered.</param>
		/// <param name="liquidPart">The liquid partitioner registered.</param>
		/// <returns>true if registered, or false if not.</returns>
		internal static bool AddScenePartitioner(Light2D instance, ref IntHandle solidPart,
				ref IntHandle liquidPart) {
			bool handled = false;
			var shape = instance.shape;
			int rad = Mathf.CeilToInt(instance.Range);
			// Avoid interfering with vanilla lights
			if (shape != LightShape.Cone && shape != LightShape.Circle && ORIGIN.Get(instance)
					is int cell && rad > 0 && Grid.IsValidCell(cell)) {
				var origin = Grid.CellToXY(cell);
				var extents = new Extents(origin.x - rad, origin.y - rad, 2 * rad, 2 * rad);
				// Better safe than sorry, check whole possible radius
				var gsp = GameScenePartitioner.Instance;
				solidPart = AddToLayer(instance, extents, gsp.solidChangedLayer);
				liquidPart = AddToLayer(instance, extents, gsp.liquidChangedLayer);
				handled = true;
			}
			return handled;
		}

		/// <summary>
		/// Adds a light's scene change partitioner to a layer.
		/// </summary>
		/// <param name="instance">The light to add.</param>
		/// <param name="extents">The extents that this light occupies.</param>
		/// <param name="layer">The layer to add it on.</param>
		/// <returns>A handle to the change partitioner, or InvalidHandle if it could not be
		/// added.</returns>
		private static IntHandle AddToLayer(Light2D instance, Extents extents,
				ScenePartitionerLayer layer) {
			return ADD_TO_LAYER.Invoke(instance, extents, layer);
		}

		/// <summary>
		/// Applies the required lighting related patches.
		/// </summary>
		/// <param name="plibInstance">The Harmony instance to use for patching.</param>
		public static void ApplyPatches(Harmony plibInstance) {
			// DiscreteShadowCaster
			plibInstance.Patch(typeof(DiscreteShadowCaster), nameof(DiscreteShadowCaster.
				GetVisibleCells), prefix: PatchMethod(nameof(GetVisibleCells_Prefix)));

			// Light2D
			plibInstance.Patch(typeof(Light2D), "AddToScenePartitioner",
				prefix: PatchMethod(nameof(AddToScenePartitioner_Prefix)));
			plibInstance.Patch(typeof(Light2D), nameof(Light2D.RefreshShapeAndPosition),
				postfix: PatchMethod(nameof(RefreshShapeAndPosition_Postfix)));

			// LightBuffer
			try {
				plibInstance.PatchTranspile(typeof(LightBuffer), "LateUpdate", PatchMethod(
					nameof(LightBuffer_LateUpdate_Transpile)));
			} catch (Exception e) {
				// Only visual, log as warning
				PUtil.LogExcWarn(e);
			}

			// LightGridEmitter
			plibInstance.Patch(typeof(LightGridEmitter), nameof(LightGridEmitter.AddToGrid),
				postfix: PatchMethod(nameof(AddToGrid_Postfix)));
			plibInstance.Patch(typeof(LightGridEmitter), "ComputeLux", prefix:
				PatchMethod(nameof(ComputeLux_Prefix)));
			plibInstance.Patch(typeof(LightGridEmitter), nameof(LightGridEmitter.
				RemoveFromGrid), postfix: PatchMethod(nameof(RemoveFromGrid_Postfix)));
			plibInstance.Patch(typeof(LightGridEmitter), nameof(LightGridEmitter.
				UpdateLitCells), prefix: PatchMethod(nameof(UpdateLitCells_Prefix)));

			// LightGridManager
			plibInstance.Patch(typeof(LightGridManager), nameof(LightGridManager.
				CreatePreview), prefix: PatchMethod(nameof(CreatePreview_Prefix)));

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

		private static bool GetVisibleCells_Prefix(int cell, List<int> visiblePoints,
				int range, LightShape shape) {
			bool exec = true;
			var lm = PLightManager.Instance;
			if (shape != LightShape.Circle && shape != LightShape.Cone && lm != null)
				// This is not a customer scenario
				exec = !lm.GetVisibleCells(cell, visiblePoints, range, shape);
			return exec;
		}

		private static IEnumerable<CodeInstruction> LightBuffer_LateUpdate_Transpile(
				IEnumerable<CodeInstruction> body) {
			var target = typeof(Light2D).GetPropertySafe<LightShape>(nameof(Light2D.shape),
				false)?.GetGetMethod(true);
			return (target == null) ? body : PPatchTools.ReplaceMethodCall(body, target,
				typeof(PLightManager).GetMethodSafe(nameof(PLightManager.LightShapeToRayShape),
				true, typeof(Light2D)));
		}

		private static void LightShapePreview_Update_Prefix(LightShapePreview __instance) {
			var lm = PLightManager.Instance;
			if (lm != null)
				lm.CallingObject = __instance.gameObject;
		}

		private static void OrientVisualizer_Postfix(Rotatable __instance) {
			var preview = __instance.gameObject.GetComponentSafe<LightShapePreview>();
			// Force regeneration on next Update()
			if (preview != null)
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

		private static void RefreshShapeAndPosition_Postfix(Light2D __instance) {
			var lm = PLightManager.Instance;
			if (lm != null)
				lm.CallingObject = __instance.gameObject;
		}

		private static void RemoveFromGrid_Postfix(LightGridEmitter __instance) {
			PLightManager.Instance?.DestroyLight(__instance);
		}

		private static bool UpdateLitCells_Prefix(LightGridEmitter __instance,
				List<int> ___litCells, LightGridEmitter.State ___state) {
			var lm = PLightManager.Instance;
			return lm == null || !lm.UpdateLitCells(__instance, ___state, ___litCells);
		}
	}
}
