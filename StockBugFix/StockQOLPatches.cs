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

using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Applied to AlgaeDistilleryConfig to give it a sensible overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(AlgaeDistilleryConfig), "CreateBuildingDef")]
	public static class AlgaeDistilleryConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			// Overheat at 125 C
			__result.Overheatable = true;
			__result.OverheatTemperature = TUNING.BUILDINGS.OVERHEAT_TEMPERATURES.HIGH_2;
		}
	}

	/// <summary>
	/// Applied to EthanolDistilleryConfig to give it a sensible overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(EthanolDistilleryConfig), "CreateBuildingDef")]
	public static class EthanolDistilleryConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			// Overheat at 75 C (the product will break pipes at this temp anyways)
			__result.Overheatable = true;
			__result.OverheatTemperature = TUNING.BUILDINGS.OVERHEAT_TEMPERATURES.NORMAL;
		}
	}

	/// <summary>
	/// Applied to KilnConfig to give it a sensible overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(KilnConfig), "CreateBuildingDef")]
	public static class KilnConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			// Overheat at 125 C
			__result.Overheatable = true;
			__result.OverheatTemperature = TUNING.BUILDINGS.OVERHEAT_TEMPERATURES.HIGH_2;
		}
	}

	/// <summary>
	/// Applied to OxyliteRefineryConfig to give it a sensible overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(OxyliteRefineryConfig), "CreateBuildingDef")]
	public static class OxyliteRefineryConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			// Overheat at 125 C
			__result.Overheatable = true;
			__result.OverheatTemperature = TUNING.BUILDINGS.OVERHEAT_TEMPERATURES.HIGH_2;
		}
	}

	/// <summary>
	/// Applied to ParkSignConfig to remove its overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(ParkSignConfig), "CreateBuildingDef")]
	public static class ParkSignConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			__result.Overheatable = false;
		}
	}

	/// <summary>
	/// Applied to CreatureDeliveryPointConfig to remove its overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(CreatureDeliveryPointConfig), "CreateBuildingDef")]
	public static class CreatureDeliveryPointConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			__result.Overheatable = false;
		}
	}

	/// <summary>
	/// Applied to FishDeliveryPointConfig to remove its overheat temperature.
	/// </summary>
	[HarmonyPatch(typeof(FishDeliveryPointConfig), "CreateBuildingDef")]
	public static class FishDeliveryPointConfig_CreateBuildingDef_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOverheat;
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			__result.Overheatable = false;
		}
	}

	/// <summary>
	/// Applied to FoodDiagnostic.CheckEnoughFood to fix the calories calculation in the
	/// diagnostics panel.
	/// </summary>
	[HarmonyPatch(typeof(FoodDiagnostic), "CheckEnoughFood")]
	public static class FoodDiagnostic_CheckEnoughFood_Patch {
		/// <summary>
		/// Gets the number of calories required per cycle, adjusting for Duplicant
		/// attributes (including difficulty level).
		/// </summary>
		private static float GetRequiredFoodPerCycle(IEnumerable<MinionIdentity> dupes) {
			var totalCalories = 0.0f;
			if (dupes != null)
				foreach (var dupe in dupes) {
					var caloriesPerSecond = Db.Get().Amounts.Calories.Lookup(dupe).
						GetDelta();
					// "tummyless" attribute adds float.PositiveInfinity
					if (!float.IsInfinity(caloriesPerSecond))
						totalCalories += caloriesPerSecond * Constants.SECONDS_PER_CYCLE;
				}
			return Mathf.Abs(totalCalories);
		}

		/// <summary>
		/// Fix calories calculation.
		/// </summary>
		internal static bool Prefix(FoodDiagnostic __instance,
				ref ColonyDiagnostic.DiagnosticResult __result,
				float ___trackerSampleCountSeconds) {
			var result = new ColonyDiagnostic.DiagnosticResult(ColonyDiagnostic.
				DiagnosticResult.Opinion.Normal, STRINGS.UI.COLONY_DIAGNOSTICS.
				GENERIC_CRITERIA_PASS);
			if (__instance.tracker.GetDataTimeLength() < 10.0f) {
				result.opinion = ColonyDiagnostic.DiagnosticResult.Opinion.Normal;
				result.Message = STRINGS.UI.COLONY_DIAGNOSTICS.NO_DATA;
			} else {
				var dupes = Components.LiveMinionIdentities.GetWorldItems(__instance.worldID);
				float requiredCaloriesPerCycle = GetRequiredFoodPerCycle(dupes);
				// Show warning if food does not last for 3 days
				const float DAYS_TO_RESERVE = 3.0f;
				if (requiredCaloriesPerCycle * DAYS_TO_RESERVE > __instance.tracker.
						GetAverageValue(___trackerSampleCountSeconds)) {
					var currentValue = __instance.tracker.GetCurrentValue();
					var text = STRINGS.MISC.NOTIFICATIONS.FOODLOW.TOOLTIP;
					result.opinion = ColonyDiagnostic.DiagnosticResult.Opinion.Concern;
					text = text.Replace("{0}", GameUtil.GetFormattedCalories(
						currentValue)).Replace("{1}", GameUtil.GetFormattedCalories(
						requiredCaloriesPerCycle));
					result.Message = text;
				}
			}
			__result = result;
			return false;
		}
	}

	/// <summary>
	/// Applied to multiple types to add a Disease Source icon to Buddy Buds, Bristle
	/// Blossoms and Bammoths.
	/// </summary>
	internal static class DiseaseSourcesPatch {
		/// <summary>
		/// Since referencing the Bammoth causes localization to break through transitive
		/// string references, apply the patch manually after the Db is loaded.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void ApplyPatch(Harmony harmony) {
			const string METHOD_NAME = nameof(IEntityConfig.CreatePrefab);
			var bammothType = PPatchTools.GetTypeSafe("IceBellyConfig");
			var patchMethod = new HarmonyMethod(typeof(DiseaseSourcesPatch),
				nameof(PollenGermsPostfix));
			harmony.Patch(typeof(BulbPlantConfig), METHOD_NAME, postfix: patchMethod);
			harmony.Patch(typeof(PrickleFlowerConfig), METHOD_NAME, postfix: patchMethod);
			if (bammothType != null)
				harmony.Patch(bammothType, METHOD_NAME, postfix: patchMethod);
		}

		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void PollenGermsPostfix(GameObject __result) {
			__result.AddOrGet<DiseaseSourceVisualizer>().alwaysShowDisease = "PollenGerms";
		}
	}

	/// <summary>
	/// Applied to BeeConfig to add a Disease Source icon to Beetas.
	/// </summary>
	[HarmonyPatch(typeof(BeeConfig), nameof(BeeConfig.CreatePrefab))]
	public static class BeeConfig_CreatePrefab_Patch {
		internal static bool Prepare() {
			return DlcManager.FeatureRadiationEnabled();
		}

		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			__result.AddOrGet<DiseaseSourceVisualizer>().alwaysShowDisease =
				"RadiationSickness";
		}
	}

	/// <summary>
	/// Applied to EvilFlowerConfig to add a Disease Source icon to Sporechids.
	/// </summary>
	[HarmonyPatch(typeof(EvilFlowerConfig), nameof(EvilFlowerConfig.CreatePrefab))]
	public static class EvilFlowerConfig_CreatePrefab_Patch {
		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			__result.AddOrGet<DiseaseSourceVisualizer>().alwaysShowDisease = "ZombieSpores";
		}
	}
	
	/// <summary>
	/// Applied to multiple types to add a Disease Source icon to the Radbolt Engine,
	/// Radbolt Generator, Manual Radbolt Generator, Radiation Lamp and Research Reactor.
	/// </summary>
	[HarmonyPatch]
	public static class RadiationBuildingDiseaseSources_Patch {
		internal static bool Prepare() {
			return DlcManager.FeatureRadiationEnabled();
		}

		internal static IEnumerable<MethodBase> TargetMethods() {
			const string METHOD_NAME = nameof(IBuildingConfig.CreateBuildingDef);
			yield return typeof(HEPEngineConfig).GetMethodSafe(METHOD_NAME, false);
			yield return typeof(HighEnergyParticleSpawnerConfig).GetMethodSafe(METHOD_NAME,
				false);
			yield return typeof(ManualHighEnergyParticleSpawnerConfig).GetMethodSafe(
				METHOD_NAME, false);
			yield return typeof(NuclearReactorConfig).GetMethodSafe(METHOD_NAME, false);
			yield return typeof(RadiationLightConfig).GetMethodSafe(METHOD_NAME, false);
		}

		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			__result.DiseaseCellVisName = "RadiationSickness";
			// The disease cell vis is hardcoded to use the pipe output cell. None of these
			// buildings have a piped output yet, so this is fine to set to the origin.
			__result.UtilityOutputOffset = CellOffset.none;
		}
	}
}
