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

using Database;
using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Patches which will be applied via annotations for Stock Bug Fix.
	/// </summary>
	public sealed class StockBugsPatches : KMod.UserMod2 {
		/// <summary>
		/// Sets the default chore type of food storage depending on the user options. Also
		/// fixes (DLC) the trait exclusions.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			var db = Db.Get();
			var storeType = db.ChoreGroups?.Storage;
			var storeFood = db.ChoreTypes?.FoodFetch;
			if (StockBugFixOptions.Instance.StoreFoodChoreType == StoreFoodCategory.Store &&
					storeType != null && storeFood != null) {
				// Default is "supply"
				db.ChoreGroups.Hauling?.choreTypes?.Remove(storeFood);
				storeType.choreTypes.Add(storeFood);
				storeFood.groups[0] = storeType;
			}
			TraitsExclusionPatches.FixTraits();
		}

		[PLibMethod(RunAt.AfterModsLoad)]
		internal static void FixDiggable(Harmony instance) {
			const string BUG_KEY = "Bugs.DisableNeutroniumDig";
			if (!StockBugFixOptions.Instance.AllowNeutroniumDig && !PRegistry.GetData<bool>(
					BUG_KEY)) {
#if DEBUG
				PUtil.LogDebug("Disabling Neutronium digging");
#endif
				PRegistry.PutData(BUG_KEY, true);
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
		internal static MethodBase GetPropertySetter(Type baseType, string name) {
			var method = baseType.GetPropertySafe<float>(name, false)?.GetSetMethod();
			if (method == null)
				PUtil.LogError("Unable to find target method for {0}.{1}!".F(baseType.Name,
					name));
			return method;
		}

		/// <summary>
		/// Applied to MainMenu to display a queued Steam mod status report if pending.
		/// </summary>
		private static void PostfixMenuSpawn(MainMenu __instance) {
			GameObject go;
			if (__instance != null && (go = __instance.gameObject) != null)
				go.AddOrGet<QueuedModReporter>();
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
		private static TranspiledMethod TranspileUpdateMods(TranspiledMethod method) {
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
		/// Fixes the race condition in Steam.UpdateMods.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		private void FixModUpdateRace(Harmony instance) {
			var steamMod = PPatchTools.GetTypeSafe("KMod.Steam");
			const string BUG_KEY = "Bugs.ModUpdateRace";
			if (steamMod != null && !PRegistry.GetData<bool>(BUG_KEY)) {
				// Transpile UpdateMods only for Steam versions (not EGS)
#if DEBUG
				PUtil.LogDebug("Transpiling Steam.UpdateMods()");
#endif
				PRegistry.PutData(BUG_KEY, true);
				instance.Patch(steamMod.GetMethodSafe("UpdateMods", false, PPatchTools.
					AnyArguments), transpiler: new HarmonyMethod(typeof(StockBugsPatches),
					nameof(TranspileUpdateMods)));
				instance.Patch(typeof(MainMenu).GetMethodSafe("OnSpawn", false), postfix:
					new HarmonyMethod(typeof(StockBugsPatches), nameof(PostfixMenuSpawn)));
			}
		}

		public override void OnLoad(Harmony instance) {
			base.OnLoad(instance);
			PUtil.InitLibrary();
			var pm = new PPatchManager(instance);
			pm.RegisterPatchClass(typeof(StockBugsPatches));
			pm.RegisterPatchClass(typeof(SweepFixPatches));
			FixModUpdateRace(instance);
			PRegistry.PutData("Bugs.FishReleaseCount", true);
			PRegistry.PutData("Bugs.TepidizerPulse", true);
			PRegistry.PutData("Bugs.TraitExclusionSpacedOut", true);
			PRegistry.PutData("Bugs.JoyReactionFix", true);
			PRegistry.PutData("Bugs.AutosaveDragFix", true);
			new POptions().RegisterOptions(this, typeof(StockBugFixOptions));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Rotates the building placement offsets before building the offset table for
		/// construction or deconstruction.
		/// </summary>
		/// <param name="areaOffsets">The current building placement offsets.</param>
		/// <param name="table">The offset table to use for calculations.</param>
		/// <param name="filter">The filter to apply.</param>
		/// <param name="building">The building which would be built or demolished.</param>
		/// <returns>The adjusted offset table.</returns>
		internal static CellOffset[][] RotateAndBuild(CellOffset[] areaOffsets,
				CellOffset[][] table, CellOffset[] filter, KMonoBehaviour building) {
			var placementOffsets = areaOffsets;
			Rotatable rotator;
			if (building != null && (rotator = building.GetComponent<Rotatable>()) != null) {
				int n = areaOffsets.Length;
				placementOffsets = new CellOffset[n];
				for (int i = 0; i < n; i++)
					placementOffsets[i] = rotator.GetRotatedCellOffset(areaOffsets[i]);
			}
			return OffsetGroups.BuildReachabilityTable(placementOffsets, table, filter);
		}

		/// <summary>
		/// Rotates the building placement offsets.
		/// </summary>
		/// <param name="areaOffsets">The current building placement offsets.</param>
		/// <param name="building">The building which would be built or demolished.</param>
		/// <returns>The adjusted placement offsets.</returns>
		internal static CellOffset[] RotateAll(CellOffset[] areaOffsets,
				KMonoBehaviour building) {
			var placementOffsets = areaOffsets;
			Rotatable rotator;
			if (building != null && (rotator = building.GetComponent<Rotatable>()) != null) {
				int n = areaOffsets.Length;
				placementOffsets = new CellOffset[n];
				for (int i = 0; i < n; i++)
					placementOffsets[i] = rotator.GetRotatedCellOffset(areaOffsets[i]);
			}
			return placementOffsets;
		}
	}

	/// <summary>
	/// Applied to Constructable, Deconstructable, and Demolishable to fix the offsets
	/// based on building rotation.
	/// </summary>
	[HarmonyPatch]
	public static class Buildable_OnPrefabInit_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOffsetTables;
		}

		/// <summary>
		/// Calculates multiple target methods for one patch.
		/// </summary>
		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(Constructable).GetMethodSafe("OnPrefabInit", false);
			yield return typeof(Deconstructable).GetMethodSafe("OnPrefabInit", false);
			yield return typeof(Demolishable).GetMethodSafe("OnPrefabInit", false);
		}

		/// <summary>
		/// Transpiles OnPrefabInit to rotate the offset table before building it.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method,
				MethodBase __originalMethod) {
			var target = typeof(OffsetGroups).GetMethodSafe(nameof(OffsetGroups.
				BuildReachabilityTable), true, typeof(CellOffset[]),
				typeof(CellOffset[][]), typeof(CellOffset[]));
			var replacement = typeof(StockBugsPatches).GetMethodSafe(nameof(StockBugsPatches.
				RotateAndBuild), true, typeof(CellOffset[]), typeof(CellOffset[][]),
				typeof(CellOffset[]), typeof(KMonoBehaviour));
			bool patched = false;
			string sig = __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name;
			if (target != null && replacement != null)
				foreach (var instr in method) {
					if (instr.Is(OpCodes.Call, target)) {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						instr.operand = replacement;
#if DEBUG
						PUtil.LogDebug("Patched " + sig);
#endif
						patched = true;
					}
					yield return instr;
				}
			else
				foreach (var instr in method)
					yield return instr;
			if (!patched)
				PUtil.LogWarning("Unable to patch " + sig);
		}
	}

	/// <summary>
	/// Applied to ChoreTypes to bump the priority of overjoyed reactions.
	/// </summary>
	[HarmonyPatch]
	public static class ChoreTypes_Add_Patch {
		/// <summary>
		/// Base divisor is 10000, so 6000/10000 = 0.6 priority.
		/// The default -1 value means that the auto calculated priority value will be used.
		/// </summary>
		public const int JOY_PRIORITY_MOD = -1;

		/// <summary>
		/// Calculates the correct Add overload to patch.
		/// </summary>
		internal static MethodBase TargetMethod() {
			var methods = typeof(ChoreTypes).GetMethods(BindingFlags.DeclaredOnly |
				PPatchTools.BASE_FLAGS | BindingFlags.Instance);
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
		/// Transpiles RefreshCreatureCount to check the right tile for creatures.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCall(method, typeof(Grid).GetMethodSafe(
				nameof(Grid.PosToCell), true, typeof(KMonoBehaviour)),
				typeof(CreatureDeliveryPoint_RefreshCreatureCount_Patch).GetMethodSafe(
				nameof(PosToCorrectedCell), true, typeof(KMonoBehaviour)));
		}
	}

	/// <summary>
	/// Applied to Deconstructable to spawn items in positions that match the building's
	/// current orientation.
	/// </summary>
	[HarmonyPatch(typeof(Deconstructable), nameof(Deconstructable.SpawnItem))]
	public static class Deconstructable_SpawnItem_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixOffsetTables;
		}

		/// <summary>
		/// Transpiles SpawnItem to rotate the offset table before dropping items.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var target = typeof(Deconstructable).GetPropertySafe<CellOffset[]>(
				"placementOffsets", false)?.GetGetMethod(true);
			var modifier = typeof(StockBugsPatches).GetMethodSafe(nameof(StockBugsPatches.
				RotateAll), true, typeof(CellOffset[]), typeof(KMonoBehaviour));
			bool patched = false;
			if (target != null && modifier != null)
				foreach (var instr in method) {
					yield return instr;
					if (instr.Is(OpCodes.Call, target)) {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, modifier);
#if DEBUG
						PUtil.LogDebug("Patched Deconstructable.SpawnItem");
#endif
						patched = true;
					}
				}
			else
				foreach (var instr in method)
					yield return instr;
			if (!patched)
				PUtil.LogWarning("Unable to patch Deconstructable.SpawnItem");
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
		internal static bool Prefix(Diggable __instance, Worker worker, ref bool __result) {
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
	/// Applied to FishFeeder to fix a race condition with refilling the feeder when the
	/// Pacu eats the entire blob on the bottom of the feeder.
	/// </summary>
	[HarmonyPatch(typeof(FishFeeder), "OnStorageChange")]
	public static class FishFeeder_OnStorageChange_Patch {
		/// <summary>
		/// A coroutine used to fix the fish feeder by waiting a frame before refilling it
		/// after a Pacu eats.
		/// </summary>
		private static System.Collections.IEnumerator FishFeederFix(FishFeeder.Instance smi) {
			yield return null;
			if (smi != null && smi.gameObject != null) {
				smi.fishFeederTop.RefreshStorage();
				smi.fishFeederBot.RefreshStorage();
			}
		}

		/// <summary>
		/// Applied before OnStorageChange runs.
		/// </summary>
		internal static bool Prefix(FishFeeder.Instance smi, object data) {
			if (data is GameObject go && go != null)
				smi.Get<Storage>().StartCoroutine(FishFeederFix(smi));
			// Stop the bugged original method from running at all
			return false;
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
			return StockBugsPatches.GetPropertySetter(typeof(FuelTank), nameof(FuelTank.
				UserMaxCapacity));
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
	/// Applied to GlassForgeConfig to mitigate the zero mass object race condition on
	/// the Glass Forge by insulating its output storage.
	/// </summary>
	[HarmonyPatch(typeof(GlassForgeConfig), nameof(IBuildingConfig.
		ConfigureBuildingTemplate))]
	public static class GlassForgeConfig_ConfigureBuildingTemplate_Patch {
		/// <summary>
		/// Applied after ConfigureBuildingTemplate runs.
		/// </summary>
		internal static void Postfix(GameObject go) {
			var forge = go.GetComponentSafe<GlassForge>();
			if (forge != null)
				forge.outStorage.SetDefaultStoredItemModifiers(Storage.
					StandardInsulatedStorage);
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
	[HarmonyPatch(typeof(HoverTextHelper), "MassStringsReadOnly")]
	public static class MassStringsReadOnly_Patch {
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
			return StockBugsPatches.GetPropertySetter(typeof(OxidizerTank), nameof(
				OxidizerTank.UserMaxCapacity));
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
	/// Applied to PacuCleanerConfig to insulate its storage and prevent instantly
	/// entombing the critters.
	/// </summary>
	[HarmonyPatch(typeof(PacuCleanerConfig), "CreatePacu")]
	public static class PacuCleanerConfig_CreatePacu_Patch {
		/// <summary>
		/// Applied after CreatePacu runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			var storage = __result.GetComponentSafe<Storage>();
			if (storage != null)
				storage.SetDefaultStoredItemModifiers(Storage.StandardInsulatedStorage);
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
	/// Applied to SingleEntityReceptacle to fix the chore type away from the hardcoded
	/// farm fetch to ranching supply (incubator) or regular supply (pedestal).
	/// </summary>
	[HarmonyPatch(typeof(SingleEntityReceptacle), "CreateFetchChore")]
	public static class SingleEntityReceptacle_CreateFetchChore_Patch {
		/// <summary>
		/// Gets the appropriate chore type for a receptacle. Uses FarmFetch for farm tiles,
		/// RanchingFetch for incubators, and Fetch for all others.
		/// </summary>
		/// <param name="types">The Database location to fetch the chore type.</param>
		/// <param name="possibleTags">The tags that can be used for deposit.</param>
		/// <returns>The appropriate chore type.</returns>
		private static ChoreType GetChoreType(ChoreTypes types, IList<Tag> possibleTags) {
			bool farm = false, ranch = false;
			int n = possibleTags.Count;
			for (int i = 0; i < n && !farm && !ranch; i++) {
				var tag = possibleTags[i];
				if (tag == GameTags.Egg)
					ranch = true;
				else if (tag == GameTags.CropSeed)
					farm = true;
			}
			return ranch ? types.RanchingFetch : (farm ? types.FarmFetch : types.Fetch);
		}

		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var depositTags = typeof(SingleEntityReceptacle).GetFieldSafe(
				"possibleDepositTagsList", false);
			var farmFetch = typeof(ChoreTypes).GetFieldSafe(nameof(ChoreTypes.FarmFetch),
				false);
			var injection = typeof(SingleEntityReceptacle_CreateFetchChore_Patch).
				GetMethodSafe(nameof(GetChoreType), true, typeof(ChoreTypes),
				typeof(IList<Tag>));
			if (depositTags != null && farmFetch != null && injection != null)
				foreach (var instr in method) {
					if (instr.opcode == OpCodes.Ldfld && instr.operand is FieldInfo fi &&
							fi == farmFetch) {
						// Push the deposit tags list
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Ldfld, depositTags);
						instr.opcode = OpCodes.Call;
						instr.operand = injection;
					}
					yield return instr;
				}
			else {
				PUtil.LogWarning("Unable to patch SingleEntityReceptacle.CreateFetchChore!");
				foreach (var instr in method)
					yield return instr;
			}
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
		/// Allow this patch to be turned off in the config.
		/// </summary>
		internal static bool Prepare() {
			return !StockBugFixOptions.Instance.AllowTepidizerPulsing;
		}

		/// <summary>
		/// Transpiles MonitorHeating to replace the GetNonSolidCells call with one that
		/// only uses the appropriate building cells.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var instructionList = new List<CodeInstruction>(method);
			var targetMethod = typeof(GameUtil).GetMethodSafe("GetNonSolidCells",
				true, typeof(int), typeof(int), typeof(List<int>));
			int targetIndex = -1;
			for (int i = 0; i < instructionList.Count; i++)
				if (instructionList[i].Is(OpCodes.Call, targetMethod)) {
					targetIndex = i;
					break;
				}
			if (targetIndex == -1) {
				PUtil.LogWarning("Target method GetNonSolidCells not found.");
				return method;
			}
			instructionList[targetIndex].operand = typeof(SpaceHeater_MonitorHeating_Patch).
				GetMethodSafe(nameof(GetValidBuildingCells), true, PPatchTools.AnyArguments);
			instructionList.Insert(targetIndex, new CodeInstruction(OpCodes.Ldarg_0));
#if DEBUG
			PUtil.LogDebug("Patched SpaceHeater.MonitorHeating");
#endif
			return instructionList;
		}

		/// <summary>
		/// Correctly fill cells with the building placement cells according to the same
		/// conditions as GetNonSolidCells.
		/// </summary>
		/// <param name="cell">Unused, kept for compatibility.</param>
		/// <param name="radius">Unused, kept for compatibility.</param>
		/// <param name="cells">List of building cells matching conditions.</param>
		/// <param name="component">Caller of the method.</param>
		internal static void GetValidBuildingCells(int cell, int radius, List<int> cells,
				Component component) {
			var building = component.GetComponent<Building>();
			_ = cell;
			_ = radius;
			foreach (int targetCell in building.PlacementCells)
				if (Grid.IsValidCell(targetCell) && !Grid.Solid[targetCell] &&
						!Grid.DupePassable[targetCell])
					cells.Add(targetCell);
		}
	}

	/// <summary>
	/// Applied to SpaceHeater.States to fix the tepidizer pulsing and reload bug.
	/// </summary>
	[HarmonyPatch(typeof(SpaceHeater.States), nameof(SpaceHeater.States.InitializeStates))]
	public static class SpaceHeater_States_InitializeStates_Patch {
		/// <summary>
		/// Allow this patch to be turned off in the config.
		/// </summary>
		internal static bool Prepare() {
			return !StockBugFixOptions.Instance.AllowTepidizerPulsing;
		}

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		internal static void Postfix(SpaceHeater.States __instance) {
			var sm = __instance;
			var onUpdate = sm.online.updateActions;
			if (onUpdate.Count > 0) {
				foreach (var action in onUpdate) {
					var updater = action.updater as UpdateBucketWithUpdater<SpaceHeater.
						StatesInstance>.IUpdater;
					if (updater != null)
						// dt is not used by the handler!
						sm.online.Enter("CheckOverheatOnStart", (smi) => updater.Update(
							smi, 0.0f));
				}
			} else
				PUtil.LogWarning("No SpaceHeater update handler found");
		}
	}

	/// <summary>
	/// Applied to StaterpillarGeneratorConfig to prevent it from overheating, breaking,
	/// or in any way being damaged.
	/// </summary>
	[HarmonyPatch(typeof(StaterpillarGeneratorConfig), nameof(StaterpillarGeneratorConfig.
		CreateBuildingDef))]
	public static class StaterpillarGeneratorConfig_CreateBuildingDef_Patch {
		/// <summary>
		/// Applied after CreateBuildingDef runs.
		/// </summary>
		internal static void Postfix(BuildingDef __result) {
			__result.Invincible = true;
			__result.Overheatable = false;
			__result.OverheatTemperature = 9999.0f;
		}
	}

	/// <summary>
	/// Applied to Timelapser to cancel the current tool when autosave begins.
	/// </summary>
	[HarmonyPatch(typeof(Timelapser), "SaveScreenshot")]
	public static class Timelapser_SaveScreenshot_Patch {
		/// <summary>
		/// Applied after SaveScreenshot runs.
		/// </summary>
		internal static void Postfix() {
			PlayerController.Instance?.CancelDragging();
		}
	}
}
