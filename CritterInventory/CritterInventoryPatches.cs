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
		/// The cached category for "Critter (Tame)". Since ResourceCategoryScreen also has
		/// only one static instance for now, this non-reentrant reference is safe.
		/// </summary>
		private static ResourceCategoryHeader critterTame;
		/// <summary>
		/// The cached category for "Critter (Wild)". Since ResourceCategoryScreen also has
		/// only one static instance for now, this non-reentrant reference is safe.
		/// </summary>
		private static ResourceCategoryHeader critterWild;

		public static void OnLoad() {
			PUtil.InitLibrary();
		}

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
			/// <param name="___HighlightColor">The highlight color from the instance.</param>
			internal static void Postfix(ref ResourceEntry __instance, bool is_hovering,
					ref Color ___HighlightColor) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				if (info != null) {
					var hlc = ___HighlightColor;
					CritterType type = info.CritterType;
					Tag species = __instance.Resource;
					CritterInventoryUtils.IterateCreatures((creature) => {
						if (creature.PrefabID() == species && type.Matches(creature))
							PUtil.HighlightEntity(creature, is_hovering ? hlc : Color.
								black);
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
			/// <param name="___selectionIdx">The current selection index.</param>
			internal static void Postfix(ref ResourceEntry __instance, ref int ___selectionIdx)
			{
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
					if (count > 0)
						// Rotate through valid indexes
						// Select the object and center it
						PUtil.CenterAndSelect(creaturesOfType[___selectionIdx++ % count]);
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
			/// <param name="___highlightColour">The highlight color from the instance.</param>
			internal static void Postfix(ref ResourceCategoryHeader __instance,
					bool is_hovering, ref Color ___highlightColour) {
				var info = __instance.gameObject.GetComponent<CritterResourceInfo>();
				if (info != null) {
					var hlc = ___highlightColour;
					CritterType type = info.CritterType;
					// It is a creature header, highlight all matching
					CritterInventoryUtils.IterateCreatures((creature) => {
						if (type.Matches(creature))
							PUtil.HighlightEntity(creature, is_hovering ? hlc :
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
			internal static bool Prefix(ref ResourceCategoryHeader __instance) {
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
			/// <param name="___Prefab_CategoryBar">The category bar prefab used to create categories.</param>
			internal static void Postfix(ref ResourceCategoryScreen __instance,
					ref GameObject ___Prefab_CategoryBar) {
				critterTame = CritterResourceHeader.Create(__instance, ___Prefab_CategoryBar,
					CritterType.Tame);
				critterWild = CritterResourceHeader.Create(__instance, ___Prefab_CategoryBar,
					CritterType.Wild);
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
			internal static void Postfix() {
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
