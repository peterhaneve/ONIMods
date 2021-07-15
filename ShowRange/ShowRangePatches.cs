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
		private static readonly string IGNORE_WALLPUMPS = "WallPumps.RotatableElementConsumer";

		/// <summary>
		/// Adds ElementConsumer range previews to the specified building def.
		/// </summary>
		/// <param name="def">The preview to add.</param>
		private static void AddConsumerPreview(BuildingDef def) {
			GameObject complete = def.BuildingComplete, preview = def.BuildingPreview,
				inBuild = def.BuildingUnderConstruction;
			var consumers = complete.GetComponents<ElementConsumer>();
			int n = consumers.Length;
			var existing = DictionaryPool<CellOffset, int, ElementConsumer>.Allocate();
			foreach (var consumer in consumers)
				// Avoid stomping the range preview of Wall Vents and Pumps
				if (consumer.GetType().FullName != IGNORE_WALLPUMPS) {
					int radius = consumer.consumptionRadius & 0xFF;
					var sco = consumer.sampleCellOffset;
					var color = Color.white;
					var offset = new CellOffset(Mathf.RoundToInt(sco.x), Mathf.RoundToInt(
						sco.y));
					// Make secondary consumers a color related to their element
					if (n > 1 && consumer.configuration == ElementConsumer.Configuration.
							Element) {
						var target = ElementLoader.FindElementByHash(consumer.
							elementToConsume);
						if (target != null)
							color = target.substance.conduitColour;
					}
					if (!existing.TryGetValue(offset, out int oldRad) || radius != oldRad) {
						PUtil.LogDebug("Visualizer added to {0}, range {1:D}".F(def.PrefabID,
							radius));
						ElementConsumerVisualizer.Create(complete, offset, radius, color);
						// Consumer found, update the preview and under construction versions
						if (preview != null)
							// Previews should always be white as other colors are hard to see
							// on overlays
							ElementConsumerVisualizer.Create(preview, offset, radius);
						if (inBuild != null)
							ElementConsumerVisualizer.Create(inBuild, offset, radius, color);
						existing[offset] = radius;
					}
				}
			existing.Recycle();
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PVersionCheck().Register(this, new SteamVersionChecker());
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
					if (def?.BuildingComplete != null) {
						AddConsumerPreview(def);
						// After it runs, the telescope and space scanner should have defs
						if (def.PrefabID == TelescopeConfig.ID) {
							PUtil.LogDebug("Telescope visualizer added");
							TelescopeVisualizer.Create(def.BuildingComplete);
							TelescopeVisualizer.Create(def.BuildingPreview);
							TelescopeVisualizer.Create(def.BuildingUnderConstruction);
						}
#if SPACEDOUT
						if (def.PrefabID == "ClusterTelescope") {
							// Not reachable in Vanilla
							PUtil.LogDebug("Cluster Telescope visualizer added");
							ClusterTelescopeVisualizer.Create(def.BuildingComplete);
							ClusterTelescopeVisualizer.Create(def.BuildingPreview);
							ClusterTelescopeVisualizer.Create(def.BuildingUnderConstruction);
						}
#endif
						if (def.PrefabID == CometDetectorConfig.ID) {
							PUtil.LogDebug("Space scanner visualizer added");
							SpaceScannerVisualizer.Create(def.BuildingComplete);
							SpaceScannerVisualizer.Create(def.BuildingPreview);
							SpaceScannerVisualizer.Create(def.BuildingUnderConstruction);
						}
					}
			}
		}
	}
}
