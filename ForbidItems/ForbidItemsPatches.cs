/*
 * Copyright 2025 Peter Han
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
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System;
using UnityEngine;

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Patches which will be applied via annotations for Forbid Items.
	/// </summary>
	public sealed class ForbidItemsPatches : KMod.UserMod2 {
		internal static readonly Tag Forbidden = new Tag("Forbidden");

		internal static StatusItem ForbiddenStatus;
		
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.MISC));
			LocString.CreateLocStringKeys(typeof(ForbidItemsStrings.UI));
			ForbiddenStatus = Db.Get().MiscStatusItems.Add(new StatusItem(Forbidden.Name,
				"MISC", "status_item_building_disabled", StatusItem.IconType.Custom,
				NotificationType.Neutral, false, OverlayModes.None.ID));
		}
		
		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
			AllowPrefabID.Cleanup();
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(ForbidItemsPatches));
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
			[HarmonyPriority(Priority.Low)]
			internal static void Postfix(ChoreConsumerState ___consumerState,
					IApproachable approachable, ref bool __result) {
				if (__result && approachable is Pickupable pickupable && pickupable != null)
					__result = ___consumerState.prefabid.HasTag(AllowForbiddenItems.
						AllowForbiddenUse) || !pickupable.KPrefabID.HasTag(Forbidden);
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
		/// Extracts the original body of FetchableMonitor.IsFetchable before the patch is added.
		/// </summary>
		[HarmonyPatch]
		public static class IsFetchableOriginal {
			[HarmonyReversePatch]
			[HarmonyPatch(typeof(FetchableMonitor.Instance), nameof(FetchableMonitor.Instance.
				IsFetchable))]
			public static bool IsFetchable(FetchableMonitor.Instance instance) =>
				throw new NotImplementedException("Reverse patch stub");
		}

		/// <summary>
		/// Applied to FetchableMonitor.Instance to make forbidden items unfetchable.
		/// </summary>
		[HarmonyPatch(typeof(FetchableMonitor.Instance), nameof(FetchableMonitor.Instance.
			IsFetchable))]
		public static class FetchableMonitor_Instance_IsFetchable_Patch {
			/// <summary>
			/// Applied after IsFetchable runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static void Postfix(FetchableMonitor.Instance __instance,
					ref bool __result) {
				if (__result)
					__result = !__instance.pickupable.KPrefabID.HasTag(Forbidden);
			}
		}

		/// <summary>
		/// Applied to SolidTransferArmConfig to add a checkbox for forbidden item use.
		/// </summary>
		[HarmonyPatch(typeof(SolidTransferArmConfig), nameof(SolidTransferArmConfig.
			ConfigureBuildingTemplate))]
		public static class SolidTransferArmConfig_ConfigureBuildingTemplate_Patch {
			/// <summary>
			/// Applied after ConfigureBuildingTemplate runs. The prefab needs the component
			/// for it to load/save properly!
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.AddOrGet<AllowForbiddenItems>();
				go.AddOrGet<AllowPrefabID>();
			}
		}

		/// <summary>
		/// Applied to Pickupable to ban collection of forbidden items by Auto-Sweepers.
		/// Auto-Sweepers now properly check if the item is actually fetchable!
		/// </summary>
		[HarmonyPatch(typeof(Pickupable), "CouldBePickedUpCommon", typeof(int))]
		public static class Pickupable_CouldBePickedUpCommon_Patch {
			/// <summary>
			/// Applied after CouldBePickedUpCommon runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static void Postfix(Pickupable __instance, int carrierID,
					ref bool __result) {
				if (__result)
					__result = !__instance.KPrefabID.HasTag(Forbidden) ||
						AllowPrefabID.CanUseForbidden(carrierID);
			}
		}

		/// <summary>
		/// Applied to Pickupable to ban collection of forbidden items by Auto-Sweepers.
		/// Auto-Sweepers now properly check if the item is actually fetchable!
		/// </summary>
		[HarmonyPatch(typeof(Pickupable), nameof(Pickupable.CouldBePickedUpByTransferArm),
			typeof(int))]
		public static class Pickupable_CouldBePickedUpByTransferArm_Patch {
			/// <summary>
			/// Applied before CouldBePickedUpByTransferArm runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(Pickupable __instance, int carrierID,
					ref bool __result) {
				var fm = __instance.fetchable_monitor;
				__result = __instance.CouldBePickedUpCommon(carrierID) && (fm == null ||
					(AllowPrefabID.CanUseForbidden(carrierID) ? IsFetchableOriginal.
					IsFetchable(fm) : fm.IsFetchable()));
				return false;
			}
		}
	}
}
