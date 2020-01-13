/*
 * Copyright 2020 Peter Han
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
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Patches which will be applied via annotations for Stock Bug Fix.
	/// </summary>
	public sealed class StockBugsPatches {
		/// <summary>
		/// Retrieves the specified property setter.
		/// </summary>
		/// <param name="baseType">The type with the property.</param>
		/// <param name="name">The property name to look up.</param>
		/// <returns>The set method for that property, or null if it was not found.</returns>
		private static MethodBase GetPropertySetter(Type baseType, string name) {
			var method = baseType.GetPropertySafe<float>(name, false)?.GetSetMethod();
			if (method == null)
				PUtil.LogError("Unable to find target method for {0}.{1}!".F(baseType.Name,
					name));
			return method;
		}

		/// <summary>
		/// Retrieves the wattage rating of an electrical network, with a slight margin to
		/// avoid overloading on rounding issues.
		/// </summary>
		/// <param name="rating">The wattage rating of the wire.</param>
		/// <returns>The wattage the wire can handle before overloading.</returns>
		private static float GetRoundedMaxWattage(Wire.WattageRating rating) {
			return Wire.GetMaxWattageAsFloat(rating) + 0.4f;
		}

		/// <summary>
		/// Correctly moves the room check point of CreatureDeliveryPoint to match the place
		/// where the critter spawns.
		/// </summary>
		/// <param name="deliveryPoint">The delivery point (fish release or dropoff).</param>
		/// <returns>The location to check for critters.</returns>
		private static int PosToCorrectedCell(KMonoBehaviour deliveryPoint) {
			int cell = Grid.PosToCell(deliveryPoint);
			if (deliveryPoint is CreatureDeliveryPoint cp) {
				int fixedCell = Grid.OffsetCell(cell, cp.spawnOffset);
				if (Grid.IsValidCell(fixedCell))
					cell = fixedCell;
			}
			return cell;
		}

		/// <summary>
		/// Applied to FuelTank's property setter to properly update the chore when its
		/// capacity is changed.
		/// </summary>
		public static class FuelTank_Set_UserMaxCapacity_Patch {
			/// <summary>
			/// Determines the target method to patch.
			/// </summary>
			/// <returns>The method which should be affected by this patch.</returns>
			internal static MethodBase TargetMethod() {
				return GetPropertySetter(typeof(FuelTank), nameof(FuelTank.UserMaxCapacity));
			}

			/// <summary>
			/// Applied after the setter runs.
			/// </summary>
			internal static void Postfix(FuelTank __instance) {
				var obj = __instance.gameObject;
				if (obj != null)
					obj.GetComponent<Storage>()?.Trigger((int)GameHashes.OnStorageChange, obj);
			}
		}

		/// <summary>
		/// Applied to OxidizerTank's property setter to properly update the chore when its
		/// capacity is changed.
		/// </summary>
		public static class OxidizerTank_Set_UserMaxCapacity_Patch {
			/// <summary>
			/// Determines the target method to patch.
			/// </summary>
			/// <returns>The method which should be affected by this patch.</returns>
			internal static MethodBase TargetMethod() {
				return GetPropertySetter(typeof(OxidizerTank), nameof(OxidizerTank.
					UserMaxCapacity));
			}

			/// <summary>
			/// Applied after the setter runs.
			/// </summary>
			internal static void Postfix(OxidizerTank __instance) {
				var obj = __instance.gameObject;
				if (obj != null)
					obj.GetComponent<Storage>()?.Trigger((int)GameHashes.OnStorageChange, obj);
			}
		}

		/// <summary>
		/// Applied to CreatureDeliveryPoint to make the fish release critter count correct.
		/// </summary>
		[HarmonyPatch(typeof(CreatureDeliveryPoint), "RefreshCreatureCount")]
		public static class CreatureDeliveryPoint_RefreshCreatureCount_Patch {
			/// <summary>
			/// Transpiles RefreshCreatureCount to check the right tile for creatures.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(Grid).GetMethodSafe(
					nameof(Grid.PosToCell), true, typeof(KMonoBehaviour)),
					typeof(StockBugsPatches).GetMethodSafe(nameof(PosToCorrectedCell), true,
					typeof(KMonoBehaviour)));
			}
		}

		/// <summary>
		/// Applied to CircuitManager to fix rounding errors in max wattage calculation.
		/// </summary>
		[HarmonyPatch(typeof(CircuitManager), "GetMaxSafeWattageForCircuit")]
		public static class CircuitManager_GetMaxSafeWattageForCircuit_Patch {
			/// <summary>
			/// Applied after GetMaxSafeWattageForCircuit runs.
			/// </summary>
			internal static void Postfix(ref float __result) {
				__result += 0.001953125f;
			}
		}

		/// <summary>
		/// Applied to ElectricalUtilityNetwork to fix rounding issues that would cause
		/// spurious overloads.
		/// </summary>
		[HarmonyPatch(typeof(ElectricalUtilityNetwork), "UpdateOverloadTime")]
		public static class ElectricalUtilityNetwork_UpdateOverloadTime_Patch {
			internal static bool Prefix(float watts_used) {
				PUtil.LogDebug("UpdateOverloadTime: Power = {0:F3}".F(watts_used));
				return true;
			}

			/// <summary>
			/// Transpiles UpdateOverloadTime to fix round off issues.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(Wire).GetMethodSafe(
					nameof(Wire.GetMaxWattageAsFloat), true, typeof(Wire.WattageRating)),
					typeof(StockBugsPatches).GetMethodSafe(nameof(GetRoundedMaxWattage), true,
					typeof(Wire.WattageRating)));
			}
		}

		/// <summary>
		/// Applied to FuelTank to empty the tank on launch to avoid duplicating mass.
		/// </summary>
		[HarmonyPatch(typeof(FuelTank), "OnSpawn")]
		public static class FuelTank_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(FuelTank __instance) {
				var obj = __instance?.gameObject;
				if (obj != null)
					obj.Subscribe((int)GameHashes.LaunchRocket, (_) => {
						// Clear the contents
						foreach (var item in __instance.items)
							Util.KDestroyGameObject(item);
						__instance.items.Clear();
					});
			}
		}

		/// <summary>
		/// Applied to OxidizerTank to empty the tank on launch to avoid duplicating mass.
		/// </summary>
		[HarmonyPatch(typeof(OxidizerTank), "OnSpawn")]
		public static class OxidizerTank_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(OxidizerTank __instance) {
				var obj = __instance?.gameObject;
				var storage = __instance?.storage;
				if (obj != null)
					obj.Subscribe((int)GameHashes.LaunchRocket, (_) => {
						// Clear the contents
						foreach (var item in storage.items)
							Util.KDestroyGameObject(item);
						storage.items.Clear();
					});
			}
		}

		/// <summary>
		/// Applied to SpacecraftManager to make the "Ready to land" message expire.
		/// </summary>
		[HarmonyPatch(typeof(SpacecraftManager), "PushReadyToLandNotification")]
		public static class SpacecraftManager_PushReadyToLandNotification_Patch {
			/// <summary>
			/// Applied before PushReadyToLandNotification runs.
			/// </summary>
			internal static bool Prefix(SpacecraftManager __instance, Spacecraft spacecraft) {
				var notification = new Notification(STRINGS.BUILDING.STATUSITEMS.
					SPACECRAFTREADYTOLAND.NOTIFICATION, NotificationType.Good, HashedString.
					Invalid, (notificationList, data) => STRINGS.BUILDING.STATUSITEMS.
					SPACECRAFTREADYTOLAND.NOTIFICATION_TOOLTIP + notificationList.
					ReduceMessages(false), spacecraft.launchConditions.GetProperName(), true);
				var obj = __instance.gameObject;
				if (obj != null)
					obj.AddOrGet<Notifier>().Add(notification);
				return false;
			}
		}

		/// <summary>
		/// Applied to GeneShuffler to fix a bug where it would not update after recharging.
		/// </summary>
		[HarmonyPatch(typeof(GeneShuffler), "Recharge")]
		public static class GeneShuffler_Recharge_Patch {
			/// <summary>
			/// Applied after Recharge runs.
			/// </summary>
			internal static void Postfix(GeneShuffler.GeneShufflerSM.Instance ___geneShufflerSMI) {
				if (___geneShufflerSMI != null) {
					var sm = ___geneShufflerSMI.sm;
					___geneShufflerSMI.GoTo(sm.recharging);
				}
			}
		}
	}
}
