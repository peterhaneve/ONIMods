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
		/// The statistic IDs that already were displayed -- do not display these again.
		/// </summary>
		internal static readonly ISet<string> ALREADY_DISPLAYED = new HashSet<string>();

		/// <summary>
		/// The last fetched statistic value, used as a quick and dirty way to hide unwanted
		/// stats.
		/// </summary>
		internal static int lastValue = int.MaxValue;

		/// <summary>
		/// Runs after the Db is initialized.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			FixStoreFood();
			FixRadiationSickness();
			if (StockBugFixOptions.Instance.FixTraits)
				TraitsExclusionPatches.FixTraits();
		}

		/// <summary>
		/// Fixes the integer overflow and incorrect rounding on large tile masses.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		private static void FixMassStringsReadOnly(Harmony instance) {
			if (!PRegistry.GetData<bool>("Bugs.MassStringsReadOnly")) {
#if DEBUG
				PUtil.LogDebug("Fixing tile mass renderer");
#endif
				instance.Patch(typeof(HoverTextHelper), "MassStringsReadOnly", postfix:
					new HarmonyMethod(typeof(StockBugsPatches), nameof(PostfixMassStrings)));
			}
		}

		/// <summary>
		/// Fixes the race condition in Steam.UpdateMods.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for patching.</param>
		private static void FixModUpdateRace(Harmony instance) {
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

		/// <summary>
		/// Fixes the radiation sickness cooldown trait to prevent endless loops of radiation
		/// sickness, as it was copy pasted but not modified from zombie spores.
		/// </summary>
		private static void FixRadiationSickness() {
			var ge = TUNING.GERM_EXPOSURE.TYPES;
			int n = ge.Length;
			for (int i = 0; i < n; i++) {
				var exposure = ge[i];
				if (exposure.germ_id == Klei.AI.RadiationSickness.ID) {
					exposure.excluded_effects.Add(Klei.AI.RadiationSickness.ID + "recovery");
					break;
				}
			}
		}

		/// <summary>
		/// Sets the default chore type of food storage depending on the user options.
		/// </summary>
		private static void FixStoreFood() {
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
		/// Gets the starting level of the Duplicant for the given statistic, or +0 if the
		/// stat was already displayed.
		/// </summary>
		/// <param name="startingLevels">The map of all starting attribute values for this Duplicant.</param>
		/// <param name="key">The starting attribtue ID to display.</param>
		/// <returns>The starting attribute level, or 0 if the attribute was not found or was
		/// already displayed on this iteration.</returns>
		internal static int GetStartingLevels(IDictionary<string, int> startingLevels,
				string key) {
			return (lastValue = ALREADY_DISPLAYED.Add(key) ? startingLevels[key] : 0);
		}
		
		/// <summary>
		/// Applied to HoverTextHelper to fix the integer overflow error on huge masses.
		/// </summary>
		internal static void PostfixMassStrings(int cell, ref string[] __result,
				float ___cachedMass, Element ___cachedElement) {
			SimHashes id;
			float mass = ___cachedMass;
			if (Grid.IsValidCell(cell) && ___cachedElement != null && (id = ___cachedElement.
					id) != SimHashes.Vacuum && id != SimHashes.Unobtanium) {
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

		/// <summary>
		/// Applied to MainMenu to display a queued Steam mod status report if pending.
		/// </summary>
		private static void PostfixMenuSpawn(Component __instance) {
			if (__instance != null && !__instance.TryGetComponent(out QueuedModReporter _))
				__instance.gameObject.AddComponent<QueuedModReporter>();
		}

		/// <summary>
		/// Sets the active flag of the attribute field only if the last value was nonzero
		/// (namely, the stat actually matters). Else, leaves it inactive.
		/// </summary>
		/// <param name="target">The target object to activate.</param>
		/// <param name="active">Always true, from the base game.</param>
		internal static void SetActiveIfNonzero(GameObject target, bool active) {
			if (lastValue != 0)
				target.SetActive(active);
		}

		/// <summary>
		/// Applied to Steam to avoid dialog spam on startup if many mods are updated or
		/// installed.
		/// </summary>
		private static TranspiledMethod TranspileUpdateMods(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCallSafe(method, new Dictionary<MethodInfo,
					MethodInfo> {
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

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			const string FIX_IRRIGATION = "Bugs.PlantIrrigation";
			base.OnAllModsLoaded(harmony, mods);
			DecorProviderRefreshFix.ApplyPatch(harmony);
			FixMassStringsReadOnly(harmony);
			if (PPatchTools.GetTypeSafe("BetterPlantTending.TendedPlant") == null &&
					!PRegistry.GetData<bool>(FIX_IRRIGATION)) {
				PlantIrrigationFixPatches.Apply(harmony);
				PRegistry.PutData(FIX_IRRIGATION, true);
			}
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
			/*
			 * For Sgt_Imalas:
			 *
			 * if (!PRegistry.GetData<bool>("Bugs.FreeGridSpace")) {
			 *     PRegistry.PutData("Bugs.FreeGridSpace", true);
			 *     instance.Patch(typeof(Grid), nameof(Grid.FreeGridSpace), prefix:
			 *         new HarmonyMethod(...));
			 * }
			 */
			PRegistry.PutData("Bugs.FreeGridSpace", true);
			new POptions().RegisterOptions(this, typeof(StockBugFixOptions));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			ALREADY_DISPLAYED.Clear();
		}
	}

	/// <summary>
	/// Applied to AggressiveChore.StatesInstance to exclude unbreakable objects from the
	/// Destructive stress reaction.
	/// </summary>
	[HarmonyPatch(typeof(AggressiveChore.StatesInstance), nameof(AggressiveChore.
		StatesInstance.FindBreakable))]
	public static class AggressiveChore_StatesInstance_FindBreakable_Patch {
		/// <summary>
		/// Applied after FindBreakable runs.
		/// </summary>
		internal static void Postfix(AggressiveChore.StatesInstance __instance) {
			var target = __instance.sm.breakable.Get(__instance);
			if (target != null && target.TryGetComponent(out BuildingHP hp) && hp.invincible)
				__instance.StopSM("Chosen building is unbreakable");
		}
	}

	/// <summary>
	/// Applied to ArtableSelectionSideScreen to fix a crash due to a missing null check.
	/// </summary>
	[HarmonyPatch]
	public static class ArtableSelectionSideScreen_Patch {
		/// <summary>
		/// The target method to patch.
		/// </summary>
		private static readonly MethodBase GENERATE_STATES = typeof(
			ArtableSelectionSideScreen).GetMethodSafe(nameof(ArtableSelectionSideScreen.
			GenerateStateButtons), false, PPatchTools.AnyArguments);

		internal static bool Prepare() {
			return GENERATE_STATES != null;
		}

		/// <summary>
		/// Determines the target methods to patch.
		/// </summary>
		/// <returns>The method which should be affected by this patch.</returns>
		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return GENERATE_STATES;
			PUtil.LogDebug("Patched ArtableSelectionSideScreen.GenerateStateButtons");
			var m = typeof(ArtableSelectionSideScreen).GetMethodSafe("RefreshButtons", false,
				PPatchTools.AnyArguments);
			if (m != null)
				yield return m;
		}

		/// <summary>
		/// Applied before GenerateStateButtons runs.
		/// </summary>
		internal static bool Prefix(Artable ___target) {
			return ___target != null && ___target.TryGetComponent(out KPrefabID _);
		}
	}

	/// <summary>
	/// Applied to CharacterContainer to fix duplicate displays of Duplicant attributes.
	/// </summary>
	[HarmonyPatch(typeof(CharacterContainer), "SetInfoText")]
	public static class CharacterContainer_SetInfoText_Patch {
		internal static bool Prepare() {
			return StockBugFixOptions.Instance.FixMultipleAttributes;
		}

		/// <summary>
		/// Applied before SetInfoText runs.
		/// </summary>
		internal static void Prefix() {
			StockBugsPatches.ALREADY_DISPLAYED.Clear();
			StockBugsPatches.lastValue = int.MaxValue;
		}

		/// <summary>
		/// Transpiles SetInfoText to hide the text box for duplicated attributes.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var getStats = typeof(Dictionary<string, int>).GetProperty("Item", PPatchTools.
				BASE_FLAGS | BindingFlags.Instance)?.GetGetMethod();
			var replaceStats = typeof(StockBugsPatches).GetMethodSafe(nameof(StockBugsPatches.
				GetStartingLevels), true, typeof(IDictionary<string, int>), typeof(string));
			var setActive = typeof(GameObject).GetMethodSafe(nameof(GameObject.SetActive),
				false, typeof(bool));
			var replaceActive = typeof(StockBugsPatches).GetMethodSafe(nameof(
				StockBugsPatches.SetActiveIfNonzero), true, typeof(GameObject), typeof(bool));
			int patched = 0;
			foreach (var instr in method) {
				if (instr.Is(OpCodes.Callvirt, getStats)) {
					instr.operand = replaceStats;
					patched = 1;
				} else if (instr.Is(OpCodes.Callvirt, setActive) && patched == 1) {
					instr.operand = replaceActive;
#if DEBUG
					PUtil.LogDebug("Patched CharacterContainer.SetInfoText");
#endif
					patched = 2;
				}
				yield return instr;
			}
			if (patched < 2)
				PUtil.LogWarning("Unable to patch CharacterContainer.SetInfoText");
		}
	}

	/// <summary>
	/// Applied to ClusterUtil to mark rocket interiors as having no printing pod.
	/// This patch applied at the request of asquared31415.
	/// </summary>
	[HarmonyPatch(typeof(ClusterUtil), nameof(ClusterUtil.ActiveWorldHasPrinter))]
	public static class ClusterUtil_ActiveWorldHasPrinter_Patch {
		/// <summary>
		/// Applied after ActiveWorldHasPrinter runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		internal static bool Prefix(ref bool __result) {
			var ci = ClusterManager.Instance;
			__result = ci != null && Components.Telepads.GetWorldItems(ci.activeWorldId).
				Count > 0;
			return false;
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
		/// Stores the rooms that are pending an update.
		/// </summary>
		private static readonly ISet<int> ROOMS_PENDING = new HashSet<int>();

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
			harmony.Patch(typeof(RoomProber), nameof(RoomProber.Sim1000ms), prefix:
				new HarmonyMethod(typeof(DecorProviderRefreshFix), nameof(PrefixRoomProbe)));
			ROOMS_PENDING.Clear();
		}

		/// <summary>
		/// Retriggers the conditions only when rooms would be rebuilt normally.
		/// </summary>
		[HarmonyPriority(Priority.HigherThanNormal)]
		private static void PrefixRoomProbe(RoomProber __instance) {
			foreach (int cell in ROOMS_PENDING) {
				var cavity = __instance.GetCavityForCell(cell);
				if (cavity != null)
					__instance.UpdateRoom(cavity);
				else
					__instance.SolidChangedEvent(cell, true);
			}
			ROOMS_PENDING.Clear();
		}

		/// <summary>
		/// Instead of triggering a full solid change of the room, merely retrigger the
		/// conditions.
		/// </summary>
		/// <param name="prober">The current room prober.</param>
		/// <param name="cell">The cell of the room that will be updated.</param>
		private static void SolidNotChangedEvent(RoomProber prober, int cell, bool _) {
			if (prober != null)
				ROOMS_PENDING.Add(cell);
		}

		/// <summary>
		/// Transpiles Refresh to change a solid change event into a condition retrigger.
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
	/// Applied to Edible to only display the yuck emote if there are actually no germs on the
	/// food. If the disease type is invalid, the germ count is an uninitialized variable and
	/// can be anything...
	/// </summary>
	[HarmonyPatch(typeof(Edible), "StopConsuming")]
	public static class Edible_StopConsuming_Patch {
		/// <summary>
		/// Returns the corrected disease count, account for if the disease is invalid.
		/// </summary>
		/// <param name="element">The potentially diseased item.</param>
		/// <returns>The number of active germs on the item.</returns>
		private static int GetRealDiseaseCount(PrimaryElement element) {
			return element.DiseaseIdx == Klei.SimUtil.DiseaseInfo.Invalid.idx ? 0 : element.
				DiseaseCount;
		}

		/// <summary>
		/// Transpiles StopConsuming to ignore disease if the handle is invalid.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCallSafe(method, typeof(PrimaryElement).
				GetPropertySafe<int>(nameof(PrimaryElement.DiseaseCount), false)?.
				GetGetMethod(true), typeof(Edible_StopConsuming_Patch).GetMethodSafe(
				nameof(GetRealDiseaseCount), true, typeof(PrimaryElement)));
		}
	}

	/// <summary>
	/// Applied to ExobaseHeadquartersConfig to ban them from being built in rockets. They
	/// already cannot be built there in the base game, but this greatly improves the
	/// diagnostic message.
	/// </summary>
	[HarmonyPatch(typeof(ExobaseHeadquartersConfig), nameof(ExobaseHeadquartersConfig.
		ConfigureBuildingTemplate))]
	public static class ExobaseHeadquartersConfig_ConfigureBuildingTemplate_Patch {
		/// <summary>
		/// Applied after ConfigureBuildingTemplate runs.
		/// </summary>
		internal static void Postfix(GameObject go) {
			if (go.TryGetComponent(out KPrefabID id))
				id.AddTag(GameTags.NotRocketInteriorBuilding);
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
			if (__instance != null && __instance.TryGetComponent(out Storage storage))
				storage.Trigger((int)GameHashes.OnStorageChange, __instance.gameObject);
		}
	}

	/// <summary>
	/// Applied to Grid to fix bugged grid spaces left behind after demolishing rockets.
	/// </summary>
	[HarmonyPatch(typeof(Grid), nameof(Grid.FreeGridSpace))]
	public static class Grid_FreeGridSpace_Patch {
		/// <summary>
		/// Applied before FreeGridSpace runs.
		/// </summary>
		internal static void Prefix(Vector2I size, Vector2I offset) {
			int cell = Grid.XYToCell(offset.x, offset.y), width = size.x, stride =
				Grid.WidthInCells - width;
			for (int y = size.y; y > 0; y--) {
				for (int x = width; x > 0; x--) {
					if (Grid.IsValidCell(cell))
						SimMessages.ReplaceElement(cell, SimHashes.Vacuum, null, 0.0f);
					cell++;
				}
				cell += stride;
			}
		}
	}

	/// <summary>
	/// Applied to MinionConfig to make it possible to recover Duplicants from radiation
	/// sickness.
	/// </summary>
	[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.CreatePrefab))]
	public static class MinionConfig_CreatePrefab_Patch {
		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			if (DlcManager.FeatureRadiationEnabled())
				__result.AddOrGet<RadiationRecoveryFix>();
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
			if (__instance != null && __instance.TryGetComponent(out Storage storage))
				storage.Trigger((int)GameHashes.OnStorageChange, __instance.gameObject);
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
			// outofrations is dead code
			var transitions = __instance.root?.transitions;
			if (transitions != null) {
				int n = transitions.Count, i = 0;
				while (i < n)
					if (transitions[i] is StateMachine.ParameterTransition) {
						transitions.RemoveAt(i);
						n--;
					} else
						i++;
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
			if (__instance != null && __instance.TryGetComponent(out Storage storage))
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
			var instructions = new List<CodeInstruction>(method);
			var targetMethod = typeof(GameUtil).GetMethodSafe("GetNonSolidCells",
				true, typeof(int), typeof(int), typeof(List<int>));
			int targetIndex = -1, n = instructions.Count;
			for (int i = 0; i < n; i++)
				if (instructions[i].Is(OpCodes.Call, targetMethod)) {
					targetIndex = i;
					break;
				}
			if (targetIndex == -1)
				PUtil.LogWarning("Target method GetNonSolidCells not found.");
			else {
				instructions[targetIndex].operand = typeof(SpaceHeater_MonitorHeating_Patch).
					GetMethodSafe(nameof(GetValidBuildingCells), true, typeof(int),
					typeof(int), typeof(List<int>), typeof(Component));
				instructions.Insert(targetIndex, new CodeInstruction(OpCodes.Ldarg_0));
#if DEBUG
				PUtil.LogDebug("Patched SpaceHeater.MonitorHeating");
#endif
			}
			return instructions;
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
			var online = __instance.online;
			var onUpdate = online.updateActions;
			foreach (var action in onUpdate)
				if (action.updater is UpdateBucketWithUpdater<SpaceHeater.StatesInstance>.
						IUpdater updater)
					// dt is not used by the handler!
					online.Enter("CheckOverheatOnStart", (smi) => updater.Update(smi, 0.0f));
			if (onUpdate.Count <= 0)
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
			__result.OverheatTemperature = Sim.MaxTemperature;
		}
	}

	/// <summary>
	/// Applied to Substance to fix the freezing into debris temperature reset bug, by
	/// actually using the set-temperature callback instead of modifying the internal
	/// temperature (which is unused by sim chunks).
	/// </summary>
	[HarmonyPatch(typeof(Substance), nameof(Substance.SpawnResource))]
	public static class Substance_SpawnResource_Patch {
		private static void SetTemperature(PrimaryElement element, float value) {
			if (value > 0.0f && value < Sim.MaxTemperature)
				element.Temperature = value;
		}

		/// <summary>
		/// Transpiles SpawnResource to use the right temperature setter.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCallSafe(method, typeof(PrimaryElement).
				GetPropertySafe<float>(nameof(PrimaryElement.InternalTemperature), false).
				GetSetMethod(true), typeof(Substance_SpawnResource_Patch).GetMethodSafe(
				nameof(SetTemperature), true, typeof(PrimaryElement), typeof(float)));
		}
	}

	/// <summary>
	/// Applied to Timelapser to squash a useless warning and fix timelapses not being saved.
	/// </summary>
	[HarmonyPatch(typeof(Timelapser), "OnNewDay")]
	public static class Timelapser_OnNewDay_Patch {
		private static bool NeedTimelapse(int cycle) {
			int cycle10 = cycle % 10;
			return cycle > 0 && (cycle <= 50 || (cycle < 100 && cycle10 == 5) || cycle10 == 0);
		}

		/// <summary>
		/// Applied before OnNewDay runs.
		/// </summary>
		internal static bool Prefix(IList<int> ___worldsToScreenshot,
				ref bool ___screenshotToday) {
			var ci = ClusterManager.Instance;
			if (___worldsToScreenshot != null && ci != null) {
				var containers = ci.WorldContainers;
				int n = containers.Count, cycle = GameClock.Instance.GetCycle();
				bool screenshot = false;
				if (___worldsToScreenshot.Count != 0)
					PUtil.LogWarning("Timelapser.OnNewDay was called, but worlds are still pending a screenshot");
				for (int i = 0; i < n; i++) {
					var world = containers[i];
					if (world.IsDiscovered && !world.IsModuleInterior && NeedTimelapse(cycle -
							(int)world.DiscoveryTimestamp)) {
						screenshot = true;
#if DEBUG
						PUtil.LogDebug("Requesting timelapse on cycle {0:D} for world {1:D}".F(
							cycle, world.id));
#endif
						___worldsToScreenshot.Add(world.id);
					}
				}
				if (screenshot)
					___screenshotToday = true;
			}
			return false;
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
			var pc = PlayerController.Instance;
			if (pc != null)
				pc.CancelDragging();
		}
	}

	/// <summary>
	/// Applied to Timelapser to fix the camera positioning on each timelapse.
	/// </summary>
	[HarmonyPatch(typeof(Timelapser), "SetPostionAndOrtho")]
	public static class Timelapser_SetPostionAndOrtho_Patch {
		/// <summary>
		/// Calculates the required size of the timelapse for the starting world.
		/// </summary>
		/// <param name="worldID">The world ID of the starting world.</param>
		/// <param name="screenRatio">The screen aspect ratio.</param>
		/// <param name="home">The world central position.</param>
		/// <returns>The orthographic size to set.</returns>
		private static float GetScreenshotSize(int worldID, float screenRatio, Vector3 home) {
			float size = 0f;
			foreach (var building in Components.BuildingCompletes.Items)
				if (building != null) {
					var pos = building.transform.position;
					int cell = Grid.PosToCell(pos);
					if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == worldID) {
						var diff = home - pos;
						float newSize = Mathf.Max(diff.x / screenRatio, diff.y);
						if (newSize > size) size = newSize;
					}
				}
			return Mathf.Max(size + 10.0f, 18.0f);
		}

		/// <summary>
		/// Applied before SetPostionAndOrtho runs.
		/// </summary>
		internal static bool Prefix(int world_id, ref Vector3 ___camPosition,
				ref float ___camSize, RenderTexture ___bufferRenderTexture) {
			var world = ClusterManager.Instance.GetWorld(world_id);
			var cc = CameraController.Instance;
			if (world != null && cc != null) {
				var overlayCamera = cc.overlayCamera;
				var cameraPos = cc.transform.position;
				float z = cameraPos.z;
				___camSize = overlayCamera.orthographicSize;
				___camPosition = cameraPos;
				if (world.IsStartWorld) {
					var telepad = GameUtil.GetTelepad(world_id);
					if (telepad != null) {
						var home = telepad.transform.position;
						cc.OrthographicSize = GetScreenshotSize(world_id, (float)
							___bufferRenderTexture.width / ___bufferRenderTexture.height,
							home);
						cc.SetPosition(new Vector3(home.x, home.y, z));
					}
				} else {
					var size = world.WorldSize;
					var offset = world.WorldOffset;
					float halfY = size.y * 0.5f;
					cc.OrthographicSize = halfY;
					cc.SetPosition(new Vector3(offset.x + size.x * 0.5f, offset.y + halfY, z));
				}
			}
			return false;
		}
	}
}
