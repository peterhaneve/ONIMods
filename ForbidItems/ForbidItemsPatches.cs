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

using System.Collections.Generic;
using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.Database;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Patches which will be applied via annotations for Forbid Items.
	/// </summary>
	public sealed class ForbidItemsPatches : KMod.UserMod2 {
		internal static readonly Tag Forbidden = new Tag("Forbidden");

		internal static StatusItem ForbiddenStatus;
		
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			ForbiddenStatus = Db.Get().MiscStatusItems.Add(new StatusItem(Forbidden.Name,
				"MISC", "status_item_building_disabled", StatusItem.IconType.Custom,
				NotificationType.Neutral, false, OverlayModes.None.ID));
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(ForbidItemsPatches));
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.MISC));
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.UI));
			new PLocalization().Register();
		}

		/// <summary>
		/// Applied to ChoreConsumer to properly ban sweepers from multi-fetching partially
		/// forbidden item stacks, and to deny forbidden items from Take Medicine and Equip
		/// chores.
		/// </summary>
		[HarmonyPatch(typeof(ChoreConsumer), nameof(ChoreConsumer.CanReach))]
		public static class ChoreConsumer_CanReach_Patch {
			/// <summary>
			/// Applied after CanReach runs.
			/// </summary>
			internal static void Postfix(IApproachable approachable, ref bool __result) {
				if (__result && approachable is Pickupable pickupable && pickupable != null)
					__result = !pickupable.KPrefabID.HasTag(Forbidden);
			}
		}

		/// <summary>
		/// Applied to EntityTemplates to make dropped items forbiddable.
		/// </summary>
		[HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.
			CreateBaseOreTemplates))]
		public static class EntityTemplates_CreateBaseOreTemplates_Patch {
			/// <summary>
			/// Applied after CreateBaseOreTemplates runs.
			/// </summary>
			internal static void Postfix(GameObject ___baseOreTemplate) {
				___baseOreTemplate.AddOrGet<Forbiddable>();
			}
		}

		/// <summary>
		/// Applied to EntityTemplates to make artifacts, food, and so forth forbiddable.
		/// </summary>
		[HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.CreateLooseEntity))]
		public static class EntityTemplates_CreateLooseEntity_Patch {
			/// <summary>
			/// Applied after CreateLooseEntity runs.
			/// </summary>
			internal static void Postfix(GameObject __result) {
				__result.AddOrGet<Forbiddable>();
			}
		}

		/// <summary>
		/// Applied to FetchableMonitor.Instance to make forbidden items unfetchable.
		/// </summary>
		[HarmonyPatch(typeof(FetchableMonitor.Instance), nameof(FetchableMonitor.Instance.
			IsFetchable))]
		public static class FetchableMonitor_IsFetchable_Patch {
			/// <summary>
			/// Applied after IsFetchable runs.
			/// </summary>
			internal static void Postfix(FetchableMonitor.Instance __instance,
					ref bool __result) {
				if (__result)
					__result = !__instance.pickupable.KPrefabID.HasTag(Forbidden);
			}
		}

		/// <summary>
		/// Applied to Pickupable to ban collection of forbidden items by Auto-Sweepers.
		/// Auto-Sweepers do not check if the item is actually fetchable.
		/// </summary>
		[HarmonyPatch(typeof(Pickupable), "CouldBePickedUpCommon")]
		public static class Pickupable_CouldBePickedUpCommon_Patch {
			/// <summary>
			/// Applied after CouldBePickedUpCommon runs.
			/// </summary>
			internal static void Postfix(Pickupable __instance, ref bool __result) {
				if (__result)
					__result = !__instance.KPrefabID.HasTag(Forbidden);
			}
		}
	}
}
