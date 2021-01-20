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

using Harmony;
using PeterHan.CritterInventory.NewResourceScreen;
using PeterHan.CritterInventory.OldResourceScreen;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Patches which will be applied via annotations for Critter Inventory.
	/// </summary>
	public static class CritterInventoryPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
			LocString.CreateLocStringKeys(typeof(CritterInventoryStrings.CREATURES));
			PLib.Datafiles.PLocalization.Register();
		}

		/// <summary>
		/// Applied to AllResourcesScreen to add a container for storing critter categories.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), "OnPrefabInit")]
		public static class AllResourcesScreen_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(AllResourcesScreen __instance) {
				__instance.gameObject.AddOrGet<CritterCategoryRows>();
			}
		}

		/// <summary>
		/// Applied to AllResourcesScreen to refresh the critter charts.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), "RefreshCharts")]
		public static class AllResourcesScreen_RefreshCharts_Patch {
			/// <summary>
			/// Applied after RefreshCharts runs.
			/// </summary>
			internal static void Postfix(AllResourcesScreen __instance) {
				var cr = __instance.GetComponent<CritterCategoryRows>();
				if (cr != null)
					cr.UpdateCharts();
			}
		}

		/// <summary>
		/// Applied to AllResourcesScreen to refresh the critter rows.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), nameof(AllResourcesScreen.RefreshRows))]
		public static class AllResourcesScreen_RefreshRows_Patch {
			/// <summary>
			/// Applied after RefreshRows runs.
			/// </summary>
			internal static void Postfix(AllResourcesScreen __instance) {
				var cr = __instance.GetComponent<CritterCategoryRows>();
				if (cr != null)
					cr.UpdateContents();
			}
		}

		/// <summary>
		/// Applied to AllResourcesScreen to filter critter rows on search query.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), "SearchFilter")]
		public static class AllResourcesScreen_SearchFilter_Patch {
			/// <summary>
			/// Applied before SearchFilter runs.
			/// </summary>
			internal static void Prefix(AllResourcesScreen __instance, string search) {
				var cr = __instance.GetComponent<CritterCategoryRows>();
				if (cr != null)
					cr.SearchFilter(search);
			}
		}

		/// <summary>
		/// Applied to AllResourcesScreen to show or hide critter rows when filtered.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), "SetRowsActive")]
		public static class AllResourcesScreen_SetRowsActive_Patch {
			/// <summary>
			/// Applied after SetRowsActive runs.
			/// </summary>
			internal static void Postfix(AllResourcesScreen __instance) {
				var cr = __instance.GetComponent<CritterCategoryRows>();
				if (cr != null)
					cr.SetRowsActive();
			}
		}

		/// <summary>
		/// Applied to AllResourcesScreen to add Critter rows to the list. To avoid many issues
		/// with compatibility, the rows are added at the end.
		/// </summary>
		[HarmonyPatch(typeof(AllResourcesScreen), "SpawnRows")]
		public static class AllResourcesScreen_SpawnRows_Patch {
			/// <summary>
			/// Applied after SpawnRows runs.
			/// </summary>
			internal static void Postfix(AllResourcesScreen __instance) {
				var cr = __instance.GetComponent<CritterCategoryRows>();
				if (cr != null)
					cr.SpawnRows();
			}
		}

		/// <summary>
		/// Applied to PinnedResourcesPanel to add critter rows to the display.
		/// </summary>
		[HarmonyPatch(typeof(PinnedResourcesPanel), "OnSpawn")]
		public static class PinnedResourcesPanel_OnSpawn_Patch {
			/// <summary>
			/// Applied before OnSpawn runs.
			/// </summary>
			internal static void Prefix(PinnedResourcesPanel __instance) {
				__instance.gameObject.AddOrGet<PinnedCritterManager>();
			}
		}

		/// <summary>
		/// Applied to PinnedResourcesPanel to refresh critter counts.
		/// </summary>
		[HarmonyPatch(typeof(PinnedResourcesPanel), nameof(PinnedResourcesPanel.Refresh))]
		public static class PinnedResourcesPanel_Refresh_Patch {
			/// <summary>
			/// Applied after Refresh runs.
			/// </summary>
			internal static void Postfix(PinnedResourcesPanel __instance) {
				var pcm = __instance.GetComponent<PinnedCritterManager>();
				if (pcm != null)
					pcm.UpdateContents();
			}
		}

		/// <summary>
		/// Applied to PinnedResourcesPanel to insert critter rows just after the rest of
		/// them, in sorted order.
		/// </summary>
		[HarmonyPatch(typeof(PinnedResourcesPanel), "SortRows")]
		public static class PinnedResourcesPanel_SortRows_Patch {
			/// <summary>
			/// Applied after SortRows runs.
			/// </summary>
			internal static void Postfix(PinnedResourcesPanel __instance) {
				var pcm = __instance.GetComponent<PinnedCritterManager>();
				if (pcm != null)
					pcm.PopulatePinnedRows();
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryHeader to highlight critters on hover.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryHeader), "Hover")]
		public static class ResourceCategoryHeader_Hover_Patch {
			/// <summary>
			/// Applied after Hover runs.
			/// </summary>
			internal static void Postfix(ResourceCategoryHeader __instance,
					bool is_hovering, Color ___highlightColour) {
				var info = __instance.GetComponent<CritterResourceHeader>();
				if (info != null)
					info.HighlightAllMatching(is_hovering ? ___highlightColour : Color.black);
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryHeader to properly chart critters.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryHeader), "RefreshChart")]
		public static class ResourceCategoryHeader_RefreshChart_Patch {
			/// <summary>
			/// Applied before RefreshChart runs.
			/// </summary>
			internal static bool Prefix(ResourceCategoryHeader __instance,
					GameObject ___sparkChart) {
				var info = __instance.GetComponent<CritterResourceHeader>();
				if (info != null)
					info.UpdateChart(___sparkChart);
				return info == null;
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryHeader to replace UpdateContents calls on critter
		/// resource headers with logic to update with critters.
		/// 
		/// As compared to the old approach of updating separately, this method preserves the
		/// semantics of the original UpdateContents, improving compatibility.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryHeader), nameof(ResourceCategoryHeader.
			UpdateContents))]
		public static class ResourceCategoryHeader_UpdateContents_Patch {
			/// <summary>
			/// Applied before UpdateContents runs.
			/// </summary>
			internal static bool Prefix(ResourceCategoryHeader __instance,
					ref bool ___anyDiscovered) {
				var info = __instance.GetComponent<CritterResourceHeader>();
				// UpdateContents adds spurious entries (e.g. babies when only adults ever
				// discovered) on critters so it must be skipped
				if (info != null)
					info.UpdateHeader(ref ___anyDiscovered);
				return info == null;
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryScreen to add a category for "Critter".
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryScreen), "OnActivate")]
		public static class ResourceCategoryScreen_OnActivate_Patch {
			/// <summary>
			/// Applied after OnActivate runs.
			/// </summary>
			internal static void Postfix(ResourceCategoryScreen __instance,
					GameObject ___Prefab_CategoryBar) {
				var headers = __instance.gameObject.AddOrGet<CritterHeaders>();
				foreach (var type in Enum.GetValues(typeof(CritterType)))
					if (type is CritterType ct)
						headers.Create(__instance, ___Prefab_CategoryBar, ct);
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryScreen to update the "Critter" category.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryScreen), "Update")]
		public static class ResourceCategoryScreen_Update_Patch {
			/// <summary>
			/// Applied after Update runs.
			/// </summary>
			internal static void Postfix(ResourceCategoryScreen __instance) {
				var headers = __instance.GetComponent<CritterHeaders>();
				if (ClusterManager.Instance.activeWorld.worldInventory != null && headers !=
						null)
					headers.UpdateContents();
			}
		}

		/// <summary>
		/// Applied to ResourceEntry to highlight critters on hover.
		/// </summary>
		[HarmonyPatch(typeof(ResourceEntry), "Hover")]
		public static class ResourceEntry_Hover_Patch {
			/// <summary>
			/// Applied after Hover runs.
			/// </summary>
			internal static void Postfix(ResourceEntry __instance, bool is_hovering,
					Color ___HighlightColor) {
				var entry = __instance.GetComponent<CritterResourceEntry>();
				if (entry != null)
					entry.HighlightAllMatching(is_hovering ? ___HighlightColor : Color.black);
			}
		}

		/// <summary>
		/// Applied to ResourceEntry to cycle through critters on click.
		/// </summary>
		[HarmonyPatch(typeof(ResourceEntry), "OnClick")]
		public static class ResourceEntry_OnClick_Patch {
			/// <summary>
			/// Applied before OnClick runs.
			/// </summary>
			internal static bool Prefix(ResourceEntry __instance, ref int ___selectionIdx) {
				var entry = __instance.gameObject.GetComponentSafe<CritterResourceEntry>();
				bool cont = entry == null;
				if (!cont) {
					var creaturesOfType = entry.CachedCritters;
					// Build list if empty
					entry.UpdateLastClick();
					if (creaturesOfType == null) {
						creaturesOfType = entry.PopulateCache();
						entry.StartCoroutine(entry.ClearCacheAfterThreshold());
					}
					int count = creaturesOfType.Count;
					if (count > 0)
						// Rotate through valid indexes
						PUtil.CenterAndSelect(creaturesOfType[___selectionIdx++ % count]);
				}
				return cont;
			}
		}

		/// <summary>
		/// Applied to ResourceEntry to properly chart critters.
		/// </summary>
		[HarmonyPatch(typeof(ResourceEntry), "RefreshChart")]
		public static class ResourceEntry_RefreshChart_Patch {
			/// <summary>
			/// Applied before RefreshChart runs.
			/// </summary>
			internal static bool Prefix(ResourceEntry __instance, GameObject ___sparkChart) {
				var entry = __instance.GetComponent<CritterResourceEntry>();
				if (entry != null)
					entry.UpdateChart(___sparkChart);
				return entry == null;
			}
		}

		/// <summary>
		/// Applied to TrackerTool to add trackers for critter counts.
		/// </summary>
		[HarmonyPatch(typeof(TrackerTool), "AddNewWorldTrackers")]
		public static class TrackerTool_AddNewWorldTrackers_Patch {
			/// <summary>
			/// Applied after AddNewWorldTrackers runs.
			/// </summary>
			internal static void Postfix(IList<WorldTracker> ___worldTrackers, int worldID) {
				var ctValues = Enum.GetValues(typeof(CritterType));
				foreach (var type in ctValues)
					if (type is CritterType ct)
						___worldTrackers.Add(new AllCritterTracker(worldID, ct));
				foreach (var prefab in Assets.GetPrefabsWithTag(GameTags.Creature)) {
					var species = prefab.PrefabID();
					foreach (var type in ctValues)
						if (type is CritterType ct)
							___worldTrackers.Add(new CritterTracker(worldID, species, ct));
				}
			}
		}

		/// <summary>
		/// Applied to WorldInventory to add critter inventories to each asteroid.
		/// </summary>
		[HarmonyPatch(typeof(WorldInventory), "OnPrefabInit")]
		public static class WorldInventory_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(WorldInventory __instance) {
				__instance.gameObject.AddOrGet<CritterInventory>();
			}
		}
	}
}
