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

using Database;
using Harmony;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Patches which will be applied via annotations for Stock Bug Fix.
	/// </summary>
	public static class StockBugsPatches {
		/// <summary>
		/// Base divisor is 10000, so 6000/10000 = 0.6 priority.
		/// </summary>
		public const int JOY_PRIORITY_MOD = 6000;

		[PLibMethod(RunAt.AfterModsLoad)]
		internal static void FixDiggable(HarmonyInstance instance) {
			const string BUG_KEY = "Bugs.DisableNeutroniumDig";
			if (!StockBugFixOptions.Instance.AllowNeutroniumDig && !PSharedData.GetData<bool>(
					BUG_KEY)) {
#if DEBUG
				PUtil.LogDebug("Disabling Neutronium digging");
#endif
				PSharedData.PutData(BUG_KEY, true);
				instance.Patch(typeof(Diggable).GetMethodSafe("OnSolidChanged", false,
					PPatchTools.AnyArguments), prefix: new HarmonyMethod(
					typeof(StockBugsPatches), nameof(PrefixSolidChanged)));
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
		/// Applied to MainMenu to display a queued Steam mod status report if pending.
		/// </summary>
		private static void PostfixMenuUpdate(MainMenu __instance) {
			if (__instance != null)
				QueuedReportManager.Instance.CheckQueuedReport(__instance.gameObject);
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

		public static void PostPatch(HarmonyInstance instance) {
			var steamMod = PPatchTools.GetTypeSafe("KMod.Steam");
			const string BUG_KEY = "Bugs.ModUpdateRace";
			PUtil.InitLibrary();
			PUtil.RegisterPatchClass(typeof(StockBugsPatches));
			if (steamMod != null && !PSharedData.GetData<bool>(BUG_KEY)) {
#if DEBUG
				PUtil.LogDebug("Transpiling Steam.UpdateMods()");
#endif
				// Transpile UpdateMods only for Steam versions (not EGS)
				PSharedData.PutData(BUG_KEY, true);
				instance.Patch(steamMod.GetMethodSafe("UpdateMods", false, PPatchTools.
					AnyArguments), transpiler: new HarmonyMethod(typeof(StockBugsPatches),
					nameof(TranspileUpdateMods)));
				instance.Patch(typeof(MainMenu).GetMethodSafe("Update", false), postfix:
					new HarmonyMethod(typeof(StockBugsPatches), nameof(PostfixMenuUpdate)));
			}
			PSharedData.PutData("Bugs.FishReleaseCount", true);
		}

		/// <summary>
		/// Applied to Diggable to cancel the chore if neutronium digging is not allowed.
		/// </summary>
		internal static bool PrefixSolidChanged(Diggable __instance) {
			GameObject go;
			bool cont = true;
			if (__instance != null && (go = __instance.gameObject) != null) {
				int cell = Grid.PosToCell(go);
				Element element;
				// Immediately cancel dig chores placed on 255 hardness items
				if (Grid.IsValidCell(cell) && (element = Grid.Element[cell]) != null &&
						element.hardness > 254) {
					go.Trigger((int)GameHashes.Cancel, null);
					cont = false;
				}
			}
			return cont;
		}

		/// <summary>
		/// Applied to Steam to avoid dialog spam on startup if many mods are updated or
		/// installed.
		/// </summary>
		private static IEnumerable<CodeInstruction> TranspileUpdateMods(
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
		/// Applied to ChoreTypes to bump the priority of overjoyed reactions.
		/// </summary>
		[HarmonyPatch]
		public static class ChoreTypes_Add_Patch {
			/// <summary>
			/// Calculates the correct Add overload to patch.
			/// </summary>
			internal static MethodBase TargetMethod() {
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
		/// Applied to Diggable to prevent maximum experience overflow if Super Productive
		/// manages to complete on Neutronium.
		/// </summary>
		[HarmonyPatch(typeof(Diggable), nameof(Diggable.InstantlyFinish))]
		public static class Diggable_InstantlyFinish_Patch {
			/// <summary>
			/// Applied before InstantlyFinish runs.
			/// </summary>
			internal static bool Prefix(Diggable __instance, Worker worker, ref bool __result)
			{
				bool cont = true;
				if (__instance != null) {
					int cell = Grid.PosToCell(__instance);
					Element element;
					// Complete by removing the cell instantaneously
					if (Grid.IsValidCell(cell) && (element = Grid.Element[cell]) != null &&
							element.hardness > 254) {
						if (worker != null)
							// Give some experience
							worker.Work(1.0f);
						SimMessages.Dig(cell);
						__result = true;
						cont = false;
					}
				}
				return cont;
			}
		}

		/// <summary>
		/// Applied to EntombedItemManager to remove the "tower of terror" that spawns on load.
		/// </summary>
		[HarmonyPatch(typeof(EntombedItemManager), "AddMassToWorlIfPossible")]
		public static class EntombedItemManager_AddMassToWorlIfPossible_Patch {
			/// <summary>
			/// Transpiles AddMassToWorlIfPossible to flip the flag that Klei forgot about.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				const int SEARCH_LIMIT = 16;
				var messages = typeof(SimMessages).GetMethods(BindingFlags.DeclaredOnly |
					BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				var targets = new List<MethodBase>(4);
				// Match any of the overloads, no matter what their parameters contain, in
				// case new optional parameters are added
				foreach (var target in messages)
					if (target.Name == nameof(SimMessages.AddRemoveSubstance))
						targets.Add(target);
				var opcodes = new List<CodeInstruction>(method);
				for (int i = opcodes.Count - 1; i > 0; i--) {
					var instr = opcodes[i];
					// Is it a call to SimMessages.AddRemoveSubstance?
					if (instr.opcode == OpCodes.Call && instr.operand is MethodBase target &&
							targets.Contains(target)) {
						int cutoff = Math.Max(0, i - SEARCH_LIMIT);
						// Backward search for the first "ldc.i4.1"
						for (int j = i - 1; j > cutoff; j--) {
							instr = opcodes[j];
							// Switch "true" for DoVerticalSolidDisplacement to "false"
							if (instr.opcode == OpCodes.Ldc_I4_1) {
								instr.opcode = OpCodes.Ldc_I4_0;
#if DEBUG
								PUtil.LogDebug("Patched EntombedItemManager flag");
#endif
								break;
							}
						}
					}
				}
				return opcodes;
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
		/// Applied to HoverTextHelper to fix the integer overflow error on huge masses.
		/// </summary>
		[HarmonyPatch]
		public static class MassStringsReadOnly_Patch {
			private const string METHOD = "MassStringsReadOnly";

			internal static MethodBase TargetMethod() {
				// Target HoverTextHelper in DLC, and WorldInspector in vanilla
				// TODO Vanilla/DLC code
				var type = PPatchTools.GetTypeSafe("HoverTextHelper");
				if (type == null)
					type = PPatchTools.GetTypeSafe("WorldInspector");
				return type?.GetMethodSafe(METHOD, true, typeof(int));
			}

			/// <summary>
			/// Applied after MassStringsReadOnly runs.
			/// </summary>
			internal static void Postfix(int cell, ref string[] __result, float ___cachedMass,
					Element ___cachedElement) {
				var element = ___cachedElement;
				SimHashes id;
				float mass = ___cachedMass;
				if (Grid.IsValidCell(cell) && element != null && (id = element.id) !=
						SimHashes.Vacuum && id != SimHashes.Unobtanium) {
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
		/// Applied to RationMonitor to stop dead code from cancelling Eat chores at new day.
		/// </summary>
		[HarmonyPatch(typeof(RationMonitor), nameof(RationMonitor.InitializeStates))]
		public static class RationMonitor_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(RationMonitor __instance) {
				var root = __instance.root;
				if (root != null)
					// outofrations is dead code
					root.parameterTransitions?.Clear();
			}
		}

		/// <summary>
		/// Applied to SolidTransferArm to prevent offgassing of materials inside its
		/// storage during transfer.
		/// </summary>
		[HarmonyPatch(typeof(SolidTransferArm), "OnSpawn")]
		public static class SolidTransferArm_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(SolidTransferArm __instance) {
				Storage storage;
				if (__instance != null && (storage = __instance.GetComponent<Storage>()) !=
						null)
					storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
			}
		}

		/// <summary>
		/// Applied to SpaceHeater to fix Tepidizer target temperature area being too large.
		/// </summary>
		[HarmonyPatch(typeof(SpaceHeater), "MonitorHeating")]
		public static class SpaceHeater_MonitorHeating_Patch {
			/// <summary>
			/// Transpiles MonitorHeating to replace the GetNonSolidCells call.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> method) {
				List<CodeInstruction> instructionList = method.ToList();
				MethodInfo targetMethod = typeof(GameUtil).GetMethodSafe("GetNonSolidCells",
					true, typeof(int), typeof(int), typeof(List<int>));
				int targetIndex = -1;
				for (int i = 0; i < instructionList.Count; i++) {
					CodeInstruction instruction = instructionList[i];
					if (instruction.opcode == OpCodes.Call && instruction.operand != null &&
						instruction.operand.Equals(targetMethod)) {
						targetIndex = i;
						break;
					}
				}
				if (targetIndex == -1) {
#if DEBUG
					PUtil.LogError("Target method GetNonSolidCells not found.");
#endif
					return method;
				}
				instructionList[targetIndex].operand = typeof(SpaceHeater_MonitorHeating_Patch)
					.GetMethodSafe("GetValidBuildingCells", true, PPatchTools.AnyArguments);
				instructionList.Insert(targetIndex, new CodeInstruction(OpCodes.Ldarg_0));
#if DEBUG
				PUtil.LogDebug("Patched SpaceHeater.MonitorHeating");
#endif
				return instructionList.AsEnumerable();
			}

			/// <summary>
			/// Correctly fill cells with the building placement cells according to the same
			/// conditions as GetNonSolidCells.
			/// </summary>
			/// <param name="cell">Unused, kept for compatibility.</param>
			/// <param name="radius">Unused, kept for compatibility.</param>
			/// <param name="cells">List of building cells matching conditions.</param>
			/// <param name="component">Caller of the method.</param>
			public static void GetValidBuildingCells(int cell, int radius, List<int> cells,
				Component component) {
				Building building = component.GetComponent<Building>();
				foreach (int targetCell in building.PlacementCells) {
					if (Grid.IsValidCell(targetCell) && !Grid.Solid[targetCell] &&
						!Grid.DupePassable[targetCell]) {
						cells.Add(targetCell);
					}
				}
			}
		}
	}
}
