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
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FoodTooltip {
	/// <summary>
	/// Patches which will be applied via annotations for Food Supply Tooltips.
	/// </summary>
	public sealed class FoodTooltipPatches : KMod.UserMod2 {
		/// <summary>
		/// Cleans up the recipe cache on close.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void CleanupCache() {
			PUtil.LogDebug("Destroying FoodRecipeCache");
			FoodRecipeCache.DestroyInstance();
		}

		/// <summary>
		/// Sets up the recipe cache on start.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void InitCache() {
			FoodRecipeCache.CreateInstance();
			PUtil.LogDebug("Created FoodRecipeCache");
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PLocalization().Register();
			new PPatchManager(harmony).RegisterPatchClass(typeof(FoodTooltipPatches));
		}

		/// <summary>
		/// Applied to CreatureCalorieMonitor.Def to include kcal per cycle information in the
		/// critter descriptors.
		/// </summary>
		[HarmonyPatch(typeof(CreatureCalorieMonitor.Def), nameof(CreatureCalorieMonitor.Def.
			GetDescriptors))]
		public static class CreatureCalorieMonitor_Def_GetDescriptors_Patch {
			/// <summary>
			/// Applied after GetDescriptors runs.
			/// </summary>
			internal static void Postfix(GameObject obj, List<Descriptor> __result) {
				if (__result != null && obj != null)
					FoodTooltipUtils.AddCritterDescriptors(obj, __result);
			}
		}

		/// <summary>
		/// Applied to Crop to include kcal per cycle information in the plant descriptors.
		/// </summary>
		[HarmonyPatch(typeof(Crop), nameof(Crop.InformationDescriptors))]
		public static class Crop_InformationDescriptors_Patch {
			/// <summary>
			/// Applied after InformationDescriptors runs.
			/// </summary>
			internal static void Postfix(Crop __instance, List<Descriptor> __result) {
				if (__result != null)
					FoodTooltipUtils.AddCropDescriptors(__instance, __result);
			}
		}

		/// <summary>
		/// Applied to MeterScreen.
		/// </summary>
		[HarmonyPatch(typeof(MeterScreen), "OnRationsTooltip")]
		public static class MeterScreen_OnRationsTooltip_Patch {
			/// <summary>
			/// Applied after OnRationsTooltip runs.
			/// </summary>
			internal static void Postfix(MeterScreen __instance) {
				FoodTooltipUtils.ShowFoodUseStats(__instance);
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to add a refresh component when it loads.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), "OnPrefabInit")]
		public static class SimpleInfoScreen_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(SimpleInfoScreen __instance) {
				__instance.gameObject.AddOrGet<InfoScreenRefresher>();
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to refresh the panel on plant wilt/recover or critter
		/// status changes.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnSelectTarget))]
		public static class SimpleInfoScreen_OnSelectTarget_Patch {
			/// <summary>
			/// Applied after OnSelectTarget runs.
			/// </summary>
			internal static void Postfix(SimpleInfoScreen __instance, GameObject target) {
				__instance.gameObject.GetComponentSafe<InfoScreenRefresher>()?.
					OnSelectTarget(target);
			}
		}

		/// <summary>
		/// Applied to SimpleInfoScreen to clean up after status changes.
		/// </summary>
		[HarmonyPatch(typeof(SimpleInfoScreen), nameof(SimpleInfoScreen.OnDeselectTarget))]
		public static class SimpleInfoScreen_OnDeselectTarget_Patch {
			/// <summary>
			/// Applied after OnDeselectTarget runs.
			/// </summary>
			internal static void Postfix(SimpleInfoScreen __instance, GameObject target) {
				__instance.gameObject.GetComponentSafe<InfoScreenRefresher>()?.
					OnDeselectTarget(target);
			}
		}
	}
}
