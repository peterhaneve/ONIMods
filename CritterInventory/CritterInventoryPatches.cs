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

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Patches which will be applied via annotations for Critter Inventory.
	/// 
	/// This code took inspiration from https://code.ecool.ws/ecool/FavoritesCategory/
	/// </summary>
	public static class CritterInventoryPatches {
		/// <summary>
		/// The cached category for "Critter (Wild)". Since ResourceCategoryScreen also has
		/// only one static instance for now, this non-reentrant reference is safe.
		/// </summary>
		private static ResourceCategoryHeader critterTame;
		/// <summary>
		/// The cached category for "Critter (Wild)". Since ResourceCategoryScreen also has
		/// only one static instance for now, this non-reentrant reference is safe.
		/// </summary>
		private static ResourceCategoryHeader critterWild;

		/// <summary>
		/// Applied to ResourceEntry to highlight critters on hover.
		/// </summary>
		[HarmonyPatch(typeof(ResourceEntry), "Hover")]
		public static class ResourceEntry_Hover_Patch {
			/// <summary>
			/// Applied after Hover runs.
			/// </summary>
			/// <param name="__instance">The current resource entry.</param>
			/// <param name="is_hovering">true if the user is hovering, or false otherwise</param>
			private static void Postfix(ResourceEntry __instance, bool is_hovering) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				if (info != null) {
					var hlc = Traverse.Create(__instance).GetField<Color>("highlightColor");
					CritterType type = info.CritterType;
					Tag species = __instance.Resource;
					// This should work, but highlightColor is always #00000000 even for
					// regular items!
					CritterInventoryUtils.IterateCreatures((creature) => {
						if (creature.PrefabID() == species && type.Matches(creature))
							PLibUtil.HighlightEntity(creature, is_hovering ? hlc :
								Color.black);
					});
				}
			}
		}

		/// <summary>
		/// Applied to ResourceEntry to cycle through critters on click
		/// </summary>
		[HarmonyPatch(typeof(ResourceEntry), "OnClick")]
		public static class ResourceEntry_OnClick_Patch {
			/// <summary>
			/// Applied after OnClick runs.
			/// </summary>
			/// <param name="__instance">The current resource entry.</param>
			private static void Postfix(ResourceEntry __instance) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				if (info != null) {
					var creaturesOfType = ListPool<CreatureBrain, ResourceCategoryHeader>.
						Allocate();
					CritterType type = info.CritterType;
					var species = __instance.Resource;
					// Get a list of creatures that match this type
					CritterInventoryUtils.IterateCreatures((creature) => {
						if (creature.PrefabID() == species && type.Matches(creature))
							creaturesOfType.Add(creature);
					});
					int count = creaturesOfType.Count;
					if (count > 0) {
						var trEntry = Traverse.Create(__instance);
						// Rotate through valid indexes
						int selectionIdx = trEntry.GetField<int>("selectionIdx");
						// Select the object and center it
						PLibUtil.CenterAndSelect(creaturesOfType[selectionIdx % count]);
						trEntry.SetField("selectionIdx", selectionIdx + 1);
					}
					creaturesOfType.Recycle();
				}
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
			/// <param name="__instance">The current resource category header.</param>
			/// <param name="is_hovering">true if the user is hovering, or false otherwise.</param>
			private static void Postfix(ResourceCategoryHeader __instance, bool is_hovering) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				if (info != null) {
					var hlc = Traverse.Create(__instance).GetField<Color>("highlightColour");
					CritterType type = info.CritterType;
					// It is a creature header, highlight all matching
					CritterInventoryUtils.IterateCreatures((creature) => {
						if (type.Matches(creature))
							PLibUtil.HighlightEntity(creature, is_hovering ? hlc :
								Color.black);
					});
				}
			}
		}
		
		/// <summary>
		/// Applied to ResourceCategoryHeader to replace UpdateContents calls on critter
		/// resource headers with logic to update with critters.
		/// 
		/// As compared to the old approach of updating separately, this method preserves the
		/// semantics of the original UpdateContents, improving compatibility.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryHeader), "UpdateContents")]
		public static class ResourceCategoryHeader_UpdateContents_Patch {
			/// <summary>
			/// Applied before UpdateContents runs.
			/// </summary>
			/// <param name="__instance">The current resource category header.</param>
			private static bool Prefix(ResourceCategoryHeader __instance) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				// UpdateContents adds spurious entries (e.g. babies when only adults ever
				// discovered) on critters
				if (info != null)
					__instance.Update(info.CritterType);
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
			/// <param name="__instance">The current resource category list.</param>
			private static void Postfix(ResourceCategoryScreen __instance) {
				critterTame = CritterResourceHeader.Create(__instance, CritterType.Tame);
				critterWild = CritterResourceHeader.Create(__instance, CritterType.Wild);
			}
		}

		/// <summary>
		/// Applied to ResourceCategoryScreen to update the "Critter" category.
		/// </summary>
		[HarmonyPatch(typeof(ResourceCategoryScreen), "Update")]
		public static class ResourceCategoryScreen_Update_Patch {
			/// <summary>
			/// Alternates tame and wild critter updating to reduce CPU load.
			/// </summary>
			private static int critterUpdatePacer = 0;

			/// <summary>
			/// Applied after Update runs.
			/// </summary>
			/// <param name="__instance">The current resource category list.</param>
			private static void Postfix(ResourceCategoryScreen __instance) {
				if (WorldInventory.Instance != null) {
					if (critterTame != null) {
						// Tame critter update
						critterTame.Activate();
						if (critterUpdatePacer == 0)
							critterTame.UpdateContents();
					}
					if (critterWild != null) {
						// Wild critter update
						critterWild.Activate();
						if (critterUpdatePacer == 1)
							critterWild.UpdateContents();
					}
					critterUpdatePacer = (critterUpdatePacer + 1) & 0x1;
				}
			}
		}
	}
}
