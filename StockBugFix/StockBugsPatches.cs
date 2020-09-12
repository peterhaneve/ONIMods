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

using Database;
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
		/// Base divisor is 10000, so 6000/10000 = 0.6 priority.
		/// </summary>
		public const int JOY_PRIORITY_MOD = 6000;

#if false
		/// <summary>
		/// Applies rocket damage. Since the amount is known to be instant, the method can be
		/// simplified greatly.
		/// </summary>
		private static float ApplyRocketDamage(WorldDamage instance, int cell, float amount,
				int _, int destroyCallback, string sourceName, string popText) {
			if (Grid.Solid[cell]) {
				bool hadBuilding = false;
				// Destroy the cell immediately
				var obj = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
				if (obj != null) {
					// Break down the building on that cell
					var hp = obj.GetComponent<BuildingHP>();
					if (hp != null) {
						// Damage for all it has left
						obj.Trigger((int)GameHashes.DoBuildingDamage, new BuildingHP.
								DamageSourceInfo {
							damage = hp.HitPoints,
							source = sourceName,
							popString = popText
						});
						if (!hp.destroyOnDamaged)
							hadBuilding = true;
					}
				}
				Grid.Damage[cell] = 1.0f;
				if (hadBuilding)
					// Destroy tile completely
					SimMessages.ReplaceElement(cell, SimHashes.Vacuum, CellEventLogger.
						Instance.SimCellOccupierDestroySelf, 0.0f, 0.0f, 255, 0,
						destroyCallback);
				else
					// Regular tile, break it normally
					instance.DestroyCell(cell, destroyCallback);
			}
			return amount;
		}
#endif

		public static void PostPatch(HarmonyInstance instance) {
			var steamMod = PPatchTools.GetTypeSafe("KMod.Steam");
			if (steamMod != null) {
#if DEBUG
				PUtil.LogDebug("Transpiling Steam.UpdateMods()");
#endif
				// Transpile UpdateMods only for Steam versions (not EGS)
				instance.Patch(steamMod.GetMethodSafe("UpdateMods", false, PPatchTools.
					AnyArguments), transpiler: new HarmonyMethod(typeof(StockBugsPatches),
					nameof(TranspileUpdateMods)));
				instance.Patch(typeof(MainMenu).GetMethodSafe("Update", false), postfix:
					new HarmonyMethod(typeof(StockBugsPatches), nameof(PostfixMenuUpdate)));
			}
		}

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
		/// Applied to GourmetCookingStationConfig to make the CO2 output in the right place.
		/// </summary>
		[HarmonyPatch(typeof(GourmetCookingStationConfig), nameof(GourmetCookingStationConfig.
			ConfigureBuildingTemplate))]
		public static class GourmetCookingStationConfig_ConfigureBuildingTemplate_Patch {
			/// <summary>
			/// Applied after ConfigureBuildingTemplate runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				var elements = go.GetComponentSafe<ElementConverter>()?.outputElements;
				if (elements != null) {
					// ElementConverter.OutputElement is a struct!
					int n = elements.Length;
					for (int i = 0; i < n; i++) {
						var outputElement = elements[i];
						var offset = outputElement.outputElementOffset;
						if (offset.y > 2.0f) {
							outputElement.outputElementOffset = new Vector2(offset.x, 2.0f);
							elements[i] = outputElement;
						}
					}
				}
			}
		}

		/// <summary>
		/// Applied to PolymerizerConfig to fix a symmetry error when emitting the plastic.
		/// </summary>
		[HarmonyPatch(typeof(PolymerizerConfig), nameof(PolymerizerConfig.
			ConfigureBuildingTemplate))]
		public static class PolymerizerConfig_ConfigureBuildingTemplate_Patch {
			/// <summary>
			/// Applied after ConfigureBuildingTemplate runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				go.GetComponentSafe<Polymerizer>().emitOffset = new Vector3(-1.75f, 1.0f, 0.0f);
			}
		}

		/// <summary>
		/// Applied to FuelTank's property setter to properly update the chore when its
		/// capacity is changed.
		/// </summary>
		[HarmonyPatch]
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
				obj.GetComponentSafe<Storage>()?.Trigger((int)GameHashes.OnStorageChange, obj);
			}
		}

		/// <summary>
		/// Applied to OxidizerTank's property setter to properly update the chore when its
		/// capacity is changed.
		/// </summary>
		[HarmonyPatch]
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
				obj.GetComponentSafe<Storage>()?.Trigger((int)GameHashes.OnStorageChange, obj);
			}
		}

		/// <summary>
		/// Applied to ChoreTypes to bump the priority of overjoyed reactions.
		/// </summary>
		[HarmonyPatch]
		public static class ChoreTypes_Add_Patch {
			/// <summary>
			/// Calculates the correct Add overload to patch.
			/// </summary>
			internal static MethodInfo TargetMethod() {
				var methods = typeof(ChoreTypes).GetMethods(BindingFlags.DeclaredOnly |
					BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				foreach (var method in methods)
					if (method.Name == nameof(ChoreTypes.Add))
						return method;
				PUtil.LogWarning("Unable to find ChoreTypes.Add method!");
				return typeof(ChoreTypes).GetMethodSafe(nameof(ChoreTypes.Add), false,
					PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Applied before Add runs.
			/// </summary>
			internal static void Prefix(string id, ref int explicit_priority) {
				if (id == "JoyReaction") {
					explicit_priority = JOY_PRIORITY_MOD;
#if DEBUG
					PUtil.LogDebug("Changed priority of {1} to {0:D}".F(JOY_PRIORITY_MOD, id));
#endif
				}
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
		[HarmonyPatch(typeof(CircuitManager), nameof(CircuitManager.
			GetMaxSafeWattageForCircuit))]
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
		[HarmonyPatch(typeof(ElectricalUtilityNetwork), nameof(ElectricalUtilityNetwork.
			UpdateOverloadTime))]
		public static class ElectricalUtilityNetwork_UpdateOverloadTime_Patch {
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
				__instance.gameObject.Subscribe((int)GameHashes.LaunchRocket, (_) => {
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
				var storage = __instance.storage;
				__instance.gameObject.Subscribe((int)GameHashes.LaunchRocket, (_) => {
					// Clear the contents
					foreach (var item in storage.items)
						Util.KDestroyGameObject(item);
					storage.items.Clear();
				});
			}
		}

#if false
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
				__instance.gameObject.AddOrGet<Notifier>().Add(notification);
				return false;
			}
		}
#endif

		/// <summary>
		/// Applied to GeneShuffler to fix a bug where it would not update after recharging.
		/// </summary>
		[HarmonyPatch(typeof(GeneShuffler), "Recharge")]
		public static class GeneShuffler_Recharge_Patch {
			/// <summary>
			/// Applied after Recharge runs.
			/// </summary>
			internal static void Postfix(GeneShuffler.GeneShufflerSM.Instance
					___geneShufflerSMI) {
				if (___geneShufflerSMI != null)
					___geneShufflerSMI.GoTo(___geneShufflerSMI.sm.recharging);
			}
		}

		/// <summary>
		/// Applied to MainMenu to display a queued Steam mod status report if pending.
		/// </summary>
		internal static void PostfixMenuUpdate(MainMenu __instance) {
			if (__instance != null)
				QueuedReportManager.Instance.CheckQueuedReport(__instance.gameObject);
		}

		/// <summary>
		/// Applied to Steam to avoid dialog spam on startup if many mods are updated or
		/// installed.
		/// </summary>
		internal static IEnumerable<CodeInstruction> TranspileUpdateMods(
				IEnumerable<CodeInstruction> method) {
			return PPatchTools.ReplaceMethodCall(method, new Dictionary<MethodInfo,
					MethodInfo>() {
				{ typeof(KMod.Manager).GetMethodSafe(nameof(KMod.Manager.Report), false,
					typeof(GameObject)), typeof(QueuedReportManager).GetMethodSafe(nameof(
					QueuedReportManager.QueueDelayedReport), true, typeof(KMod.Manager),
					typeof(GameObject)) },
				{ typeof(KMod.Manager).GetMethodSafe(nameof(KMod.Manager.Sanitize), false,
					typeof(GameObject)), typeof(QueuedReportManager).GetMethodSafe(nameof(
					QueuedReportManager.QueueDelayedSanitize), true, typeof(KMod.Manager),
					typeof(GameObject)) }
			});
		}

		/// <summary>
		/// Applied to WorldInspector to fix the integer overflow error on huge masses.
		/// </summary>
		[HarmonyPatch(typeof(WorldInspector), nameof(WorldInspector.MassStringsReadOnly))]
		public static class WorldInspector_MassStringsReadOnly_Patch {
			/// <summary>
			/// Applied after MassStringsReadOnly runs.
			/// </summary>
			internal static void Postfix(string[] __result, Element ___cachedElement,
					float ___cachedMass) {
				var id = ___cachedElement.id;
				float mass = ___cachedMass;
				if (id != SimHashes.Vacuum && id != SimHashes.Unobtanium) {
					if (mass < 5.0f)
						// kg => g
						mass *= 1000.0f;
					if (mass < 5.0f)
						// g => mg
						mass *= 1000.0f;
					if (mass < 5.0f)
						mass = Mathf.Floor(1000.0f * mass);
					// Base game hardcodes dots so we will too
					string formatted = mass.ToString("F1", System.Globalization.CultureInfo.
						InvariantCulture);
					int index = formatted.IndexOf('.');
					if (index > 0) {
						__result[0] = formatted.Substring(0, index);
						__result[1] = formatted.Substring(index);
					} else {
						__result[0] = formatted;
						__result[1] = "";
					}
				}
			}
		}

#if false
		/// <summary>
		/// Applied to LaunchableRocket to not dupe materials when launched through a door.
		/// </summary>
		[HarmonyPatch(typeof(LaunchableRocket.States), "DoWorldDamage")]
		public static class LaunchableRocket_States_DoWorldDamage_Patch {
			/// <summary>
			/// Transpiles DoWorldDamage to destroy tiles more intelligently.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				// There are 2 overloads, so types must be specified
				return PPatchTools.ReplaceMethodCall(method, typeof(WorldDamage).GetMethodSafe(
					nameof(WorldDamage.ApplyDamage), false, typeof(int), typeof(float),
					typeof(int), typeof(int), typeof(string), typeof(string)),
					typeof(StockBugsPatches).GetMethodSafe(nameof(ApplyRocketDamage), true,
					PPatchTools.AnyArguments));
			}
		}

		/// <summary>
		/// Applied to LaunchableRocket to make rockets correctly disappear if loaded while
		/// underway.
		/// </summary>
		[HarmonyPatch(typeof(LaunchableRocket), "OnSpawn")]
		public static class LaunchableRocket_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(LaunchableRocket __instance) {
				var smi = __instance.smi;
				const string name = nameof(LaunchableRocket.States.not_grounded.space);
				if (smi != null && (smi.GetCurrentState().name?.EndsWith(name) ?? false)) {
#if DEBUG
					PUtil.LogDebug("Scheduling rocket fix task");
#endif
					GameScheduler.Instance.Schedule("FixRocketAnims", 0.5f, (_) => {
						var parts = smi.master.parts;
						if (smi.GetCurrentState().name?.EndsWith(name) ?? false)
							// Hide them!
							foreach (var part in parts)
								part.GetComponent<KBatchedAnimController>().enabled = false;
					});
				}
			}
		}
#endif
	}
}
