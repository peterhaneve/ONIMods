/*
 * Copyright 2026 Peter Han
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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// Patches for MD-535720: Hot Shots.
	/// </summary>
	internal static class MD535720 {
		/// <summary>
		/// Applied to multiple classes to remove all Laboratory references when MD-535720
		/// rooms are turned off.
		/// </summary>
		[HarmonyPatch]
		public static class BuildingConfig_DoPostConfigureComplete_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_DisableRooms;

			internal static IEnumerable<System.Reflection.MethodBase> TargetMethods() {
				yield return typeof(GeoTunerConfig).GetMethodSafe(nameof(
					IBuildingConfig.DoPostConfigureComplete), false, typeof(GameObject));
				yield return typeof(MissionControlClusterConfig).GetMethodSafe(nameof(
					IBuildingConfig.DoPostConfigureComplete), false, typeof(GameObject));
				yield return typeof(MissionControlConfig).GetMethodSafe(nameof(
					IBuildingConfig.DoPostConfigureComplete), false, typeof(GameObject));
			}

			internal static void Postfix(GameObject go) {
				if (go.TryGetComponent(out RoomTracker tracker))
					Object.Destroy(tracker);
			}
		}

		/// <summary>
		/// Applied to ContactConductivePipeBridgeConfig to disable it when MD-535720
		/// buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(ContactConductivePipeBridgeConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class ContactConductivePipeBridgeConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to ExteriorWallConfig to restore the old mass and build time.
		/// </summary>
		[HarmonyPatch(typeof(ExteriorWallConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class ExteriorWallConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_Drywall;

			internal static void Postfix(BuildingDef __result) {
				__result.ConstructionTime = TUNING.BUILDINGS.CONSTRUCTION_TIME_SECONDS.TIER2;
				__result.Mass = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER4;
			}
		}

		/// <summary>
		/// Applied to GeoTunerConfig to disable it when MD-535720 buildings are turned off.
		/// </summary>
		[HarmonyPatch(typeof(GeoTunerConfig), nameof(IBuildingConfig.CreateBuildingDef))]
		public static class GeoTunerConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to MissionControlConfig to disable it when MD-535720 buildings are turned
		/// off.
		/// </summary>
		[HarmonyPatch(typeof(MissionControlConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class MissionControlConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}
		
		/// <summary>
		/// Applied to RoomTypes to disable the Private Bedroom and Laboratory when MD-535720
		/// rooms are turned off.
		/// </summary>
		[HarmonyPatch(typeof(Database.RoomTypes), MethodType.Constructor,
			typeof(ResourceSet))]
		public static class RoomTypes_Constructor_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD535720_DisableRooms;

			internal static void Postfix(Database.RoomTypes __instance) {
				__instance.Remove(__instance.Laboratory);
				__instance.Remove(__instance.PrivateBedroom);
				// Only used with an Array.IndexOf, this is playing with fire but...
				__instance.Bedroom.upgrade_paths[0] = __instance.Bedroom;
				__instance.Barracks.upgrade_paths[1] = __instance.Barracks;
			}
		}
	}
}
