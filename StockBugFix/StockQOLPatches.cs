/*
 * Copyright 2022 Peter Han
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
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Patches which will be applied via annotations for Stock Bug Fix.
	/// </summary>
	public sealed class StockQOLPatches {
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
		/// Applied to IceMachineConfig to give it a sensible overheat temperature.
		/// </summary>
		[HarmonyPatch(typeof(IceMachineConfig), "CreateBuildingDef")]
		public static class IceMachineConfig_CreateBuildingDef_Patch {
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
		/// Applied to FoodDiagnostic.CheckEnoughFood to fix the calories calculation in the diagnostics panel.
		/// </summary>
		[HarmonyPatch(typeof(FoodDiagnostic), "CheckEnoughFood")]
		public static class FoodDiagnostic_CheckEnoughFood_Patch {
			/// <summary>
			/// Fix calories calculation.
			/// </summary>
			internal static bool Prefix(FoodDiagnostic __instance, ref ColonyDiagnostic.DiagnosticResult __result, float ___trackerSampleCountSeconds) {
				__result = new ColonyDiagnostic.DiagnosticResult(ColonyDiagnostic.DiagnosticResult.Opinion.Normal, STRINGS.UI.COLONY_DIAGNOSTICS.GENERIC_CRITERIA_PASS);
				if (__instance.tracker.GetDataTimeLength() < 10f) {
					__result.opinion = ColonyDiagnostic.DiagnosticResult.Opinion.Normal;
					__result.Message = STRINGS.UI.COLONY_DIAGNOSTICS.NO_DATA;
				} else {
					var dupes = Components.LiveMinionIdentities.GetWorldItems(__instance.worldID);
					var requiredCaloriesPerCycle = GetRequiredFoodPerCycleByAttributeModifier(dupes);
					// show warning if food doesn't last for 3 days
					var daysReserve = 3;
					if (requiredCaloriesPerCycle * daysReserve > __instance.tracker.GetAverageValue(___trackerSampleCountSeconds)) {
						__result.opinion = ColonyDiagnostic.DiagnosticResult.Opinion.Concern;
						var currentValue = __instance.tracker.GetCurrentValue();
						var text = STRINGS.MISC.NOTIFICATIONS.FOODLOW.TOOLTIP;
						text = text.Replace("{0}", GameUtil.GetFormattedCalories(currentValue));
						text = text.Replace("{1}", GameUtil.GetFormattedCalories(requiredCaloriesPerCycle));
						__result.Message = text;
					}
				}
				return false;
			}

			private static float ToCaloriesPerCycle(float caloriesPerSec) {
				return caloriesPerSec * 600f;
			}

			/// <summary>
			///  Get required calories per cycle from minion attributes
			/// </summary>
			private static float GetRequiredFoodPerCycleByAttributeModifier(List<MinionIdentity> dupes) {
				var totalCalories = 0f;
				if (dupes != null) {
					foreach (var dupe in dupes) {
						var caloriesPerSecond = Db.Get().Amounts.Calories.Lookup(dupe).GetDelta();
						// "tummyless" attribute adds float.PositiveInfinity
						if (caloriesPerSecond != float.PositiveInfinity) {
							totalCalories += ToCaloriesPerCycle(caloriesPerSecond);
						}
					}
				}
				return Mathf.Abs(totalCalories);
			}
		}
	}
}
