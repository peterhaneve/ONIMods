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
	}
}
