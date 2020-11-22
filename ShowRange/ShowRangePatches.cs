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

namespace PeterHan.ShowRange {
	/// <summary>
	/// Patches which will be applied via annotations for Show Building Ranges.
	/// </summary>
	public static class ShowRangePatches {
		/// <summary>
		/// The type name to ignore to avoid a crash with Wall Pumps and Vents.
		/// </summary>
		private static readonly string IGNORE_WALLPUMPS = "WallPumps.RotatableElementConsumer";

		/// <summary>
		/// The color for the secondary (water) consumer of the Algae Terrarium.
		/// </summary>
		private static readonly Color TERRARIUM_SECONDARY = new Color(0.5f, 0.5f, 1.0f);

		/// <summary>
		/// Adds ElementConsumer range previews to the specified building def.
		/// </summary>
		/// <param name="def">The preview to add.</param>
		private static void AddConsumerPreview(BuildingDef def) {
			GameObject complete = def.BuildingComplete, preview = def.BuildingPreview,
				inBuild = def.BuildingUnderConstruction;
			// Check the complete building for a consumer
			foreach (var consumer in complete.GetComponents<ElementConsumer>())
				// Avoid stomping the range preview of Wall Vents and Pumps
				if (consumer.GetType().FullName != IGNORE_WALLPUMPS) {
					int radius = consumer.consumptionRadius & 0xFF;
					var sco = consumer.sampleCellOffset;
					var color = Color.white;
					var offset = new CellOffset(Mathf.RoundToInt(sco.x), Mathf.RoundToInt(
						sco.y));
					// Special case: make the algae terrarium's secondary consumer blue
					if (radius == 1 && def.PrefabID == AlgaeHabitatConfig.ID)
						color = TERRARIUM_SECONDARY;
					PUtil.LogDebug("Visualizer added to {0}, range {1:D}".F(def.PrefabID,
						radius));
					ElementConsumerVisualizer.Create(complete, offset, radius, color);
					// Consumer found, update the preview and under construction versions
					if (preview != null)
						ElementConsumerVisualizer.Create(preview, offset, radius, color);
					if (inBuild != null)
						ElementConsumerVisualizer.Create(inBuild, offset, radius, color);
				}
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
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
						if (def.PrefabID == ClusterTelescopeConfig.ID) {
							PUtil.LogDebug("Cluster Telescope visualizer added");
							ClusterTelescopeVisualizer.Create(def.BuildingComplete);
							ClusterTelescopeVisualizer.Create(def.BuildingPreview);
							ClusterTelescopeVisualizer.Create(def.BuildingUnderConstruction);
						}
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
