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
using KMod;
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
	public sealed class StockBugsPatches : UserMod2 {
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
		/// Applied to Steam to avoid dialog spam on startup if many mods are updated or
		/// installed.
		/// </summary>
		private static TranspiledMethod TranspileUpdateMods(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCallSafe(method, new Dictionary<MethodInfo,
					MethodInfo>() {
				{
					typeof(Manager).GetMethodSafe(nameof(Manager.Report), false,
						typeof(GameObject)),
					typeof(QueuedReportManager).GetMethodSafe(nameof(QueuedReportManager.
						QueueDelayedReport), true, typeof(Manager), typeof(GameObject))
				},
				{
					typeof(Manager).GetMethodSafe(nameof(Manager.Sanitize), false,
						typeof(GameObject)),
					typeof(QueuedReportManager).GetMethodSafe(nameof(QueuedReportManager.
						QueueDelayedSanitize), true, typeof(Manager), typeof(GameObject))
				}
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

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			base.OnAllModsLoaded(harmony, mods);
			DecorProviderRefreshFix.ApplyPatch(harmony);
		}

		public override void OnLoad(Harmony instance) {
			base.OnLoad(instance);
			PUtil.InitLibrary();
			var pm = new PPatchManager(instance);
			pm.RegisterPatchClass(typeof(StockBugsPatches));
			pm.RegisterPatchClass(typeof(SweepFixPatches));
			FixModUpdateRace(instance);
			PRegistry.PutData("Bugs.TepidizerPulse", true);
			PRegistry.PutData("Bugs.TraitExclusionSpacedOut", true);
			PRegistry.PutData("Bugs.TropicalPacuRooms", true);
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
	/// Applied to DecorProvider to reduce the effect of the Tropical Pacu bug by instead of
	/// triggering a full room rebuild, just refreshing the room constraints.
	/// 
	/// If Decor Reimagined is installed, it will override the auto patch, the conditional one
	/// will be used instead.
	/// </summary>
	public static class DecorProviderRefreshFix {
		/// <summary>
		/// Attempts to also patch the Decor Reimagined implementation of DecorProvider.
		/// Refresh.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void ApplyPatch(Harmony harmony) {
			var patchMethod = new HarmonyMethod(typeof(DecorProviderRefreshFix), nameof(
				TranspileRefresh));
			var targetMethod = PPatchTools.GetTypeSafe(
				"ReimaginationTeam.DecorRework.DecorSplatNew", "DecorReimagined")?.
				GetMethodSafe("RefreshDecor", false, PPatchTools.AnyArguments);
			if (targetMethod != null) {
				PUtil.LogDebug("Patching Decor Reimagined for DecorProvider.RefreshDecor");
				harmony.Patch(targetMethod, transpiler: patchMethod);
			}
			PUtil.LogDebug("Patching DecorProvider.Refresh");
			harmony.Patch(typeof(DecorProvider).GetMethodSafe(nameof(DecorProvider.Refresh),
				false, PPatchTools.AnyArguments), transpiler: patchMethod);
		}

		/// <summary>
		/// Instead of triggering a full solid change of the room, merely retrigger the
		/// conditions.
		/// </summary>
		/// <param name="prober">The current room prober.</param>
		/// <param name="cell">The cell of the room that will be updated.</param>
		private static void SolidNotChangedEvent(RoomProber prober, int cell, bool _) {
			if (prober != null) {
				var cavity = prober.GetCavityForCell(cell);
				if (cavity != null)
					prober.UpdateRoom(cavity);
				// else: If the critter is not currently in any cavity, they will be added
				// when the cavity is created by OvercrowdingMonitor, at which point the room
				// conditions will be evaluated again anyways
			}
		}

		/// <summary>
		/// Transpiles the constructor to resize the array to 26 slots by default.
		/// Most decor providers have 1 or 2 radius which is 1 and 9 tiles respectively,
		/// 26 handles up to 3 without a resize.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		internal static TranspiledMethod TranspileRefresh(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(RoomProber).
				GetMethodSafe(nameof(RoomProber.SolidChangedEvent), false, typeof(int),
				typeof(bool)), typeof(DecorProviderRefreshFix).GetMethodSafe(nameof(
				SolidNotChangedEvent), true, typeof(RoomProber), typeof(int), typeof(bool)));
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
