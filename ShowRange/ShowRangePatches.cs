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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.ShowRange {
	/// <summary>
	/// Patches which will be applied via annotations for Show Building Ranges.
	/// </summary>
	public sealed class ShowRangePatches : KMod.UserMod2 {
		/// <summary>
		/// The type name to ignore to avoid a crash with Wall Pumps and Vents.
		/// </summary>
		private const string IGNORE_WALLPUMPS = "WallPumps.RotatableElementConsumer";

		/// <summary>
		/// Adds ElementConsumer range previews to the specified building def.
		/// </summary>
		/// <param name="def">The preview to add.</param>
		private static void AddConsumerPreview(BuildingDef def) {
			GameObject complete = def.BuildingComplete, preview = def.BuildingPreview,
				inBuild = def.BuildingUnderConstruction;
			var consumers = complete.GetComponents<ElementConsumer>();
			var existing = DictionaryPool<CellOffset, int, ElementConsumer>.Allocate();
			foreach (var consumer in consumers)
				// Avoid stomping the range preview of Wall Vents and Pumps
				if (consumer.GetType().FullName != IGNORE_WALLPUMPS) {
					int radius = consumer.consumptionRadius & 0xFF;
					var sco = consumer.sampleCellOffset;
					var offset = new CellOffset(Mathf.RoundToInt(sco.x), Mathf.RoundToInt(
						sco.y));
					if (!existing.TryGetValue(offset, out int oldRad) || radius != oldRad) {
						PUtil.LogDebug("Visualizer added to {0}, range {1:D}".F(def.PrefabID,
							radius));
						if (radius > oldRad)
							existing[offset] = radius;
					}
				}
			int n = existing.Count;
			if (n > 0) {
				// Build an array of the offsets and ranges
				var visualizers = new SimVisualizer[n];
				int index = 0, worstCaseRadius = 0;
				foreach (var pair in existing) {
					var offset = pair.Key;
					int radius = pair.Value, extentX = Mathf.Abs(offset.x) + radius,
						extentY = Mathf.Abs(offset.y) + radius;
					visualizers[index++] = new SimVisualizer(offset, radius);
					if (extentX > worstCaseRadius)
						worstCaseRadius = extentX;
					if (extentY > worstCaseRadius)
						worstCaseRadius = extentY;
				}
				SimRangeVisualizer.Create(complete, visualizers, worstCaseRadius);
				if (preview != null)
					SimRangeVisualizer.Create(preview, visualizers, worstCaseRadius);
				if (inBuild != null)
					SimRangeVisualizer.Create(inBuild, visualizers, worstCaseRadius);
			}
			existing.Recycle();
		}

		/// <summary>
		/// Adds components to visualize the range of buildings.
		/// </summary>
		/// <param name="def">The building def to add previews (if necessary).</param>
		private static void AddRangePreviews(BuildingDef def) {
			AddConsumerPreview(def);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to CameraController to add the range visualizer to the correct camera.
		/// </summary>
		[HarmonyPatch(typeof(CameraController), "OnPrefabInit")]
		public static class CameraController_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(CameraController __instance) {
				__instance.overlayNoDepthCamera.gameObject.AddComponent<SimRangeVisualizer>();
			}
		}

		/// <summary>
		/// Applied to GeneratedBuildings to add the visualizers for the Space Scanner and
		/// Telescope, along with automatic visualizers for all buildings that use elements
		/// from their environment like Liquid Pump, Deodorizer, and so forth.
		/// </summary>
		[HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.
			LoadGeneratedBuildings))]
		public static class GeneratedBuildings_LoadGeneratedBuildings_Patch {
			/// <summary>
			/// Applied after LoadGeneratedBuildings runs.
			/// </summary>
			internal static void Postfix() {
				foreach (var def in Assets.BuildingDefs)
					if (def != null && def.BuildingComplete != null)
						AddRangePreviews(def);
			}
		}
	}
}
