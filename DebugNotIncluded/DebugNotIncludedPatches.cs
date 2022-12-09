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
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Patches which will be applied via annotations for Debug Not Included.
	/// </summary>
	public sealed class DebugNotIncludedPatches : UserMod2 {
		/*
		 * Spawned prefabs at launch initialization:
		 * KObjectManager
		 * KScreenManager
		 * ScreenPrefabs
		 * Global
		 * MusicManager
		 * InputInit
		 * Audio
		 * EffectPrefabs
		 * EntityPrefabs
		 * GlobalAssets
		 * GameAssets
		 * CustomGameSettings
		 */

		/// <summary>
		/// The assembly which is running the current version of PLib.
		/// </summary>
		internal static Assembly RunningPLibAssembly { get; private set; }

		/// <summary>
		/// The KMod which describes this mod.
		/// </summary>
		internal static Mod ThisMod { get; private set; }

		[PLibMethod(RunAt.AfterLayerableLoad)]
		internal static void AfterModsLoad() {
			// Input manager is not set up until this time
			KInputHandler.Add(Global.GetInputManager().GetDefaultController(),
				new UISnapshotHandler(), 1024);
		}

		/// <summary>
		/// Applied to ModsScreen to add our buttons and otherwise tweak the dialog.
		/// </summary>
		private static void BuildDisplay(ModsScreen __instance, object ___displayedMods) {
			// Must cast the type because ModsScreen.DisplayedMod is private
			foreach (var displayedMod in (System.Collections.IEnumerable)___displayedMods)
				ModDialogs.ConfigureRowInstance(displayedMod, __instance);
#if ALL_MODS_CHECKBOX
			__instance.GetComponent<AllModsHandler>()?.UpdateCheckedState();
#endif
		}

		/// <summary>
		/// Applied to ModsScreen to hide any popups from this mod before the rows get
		/// destroyed.
		/// </summary>
		private static void HidePopups(ModsScreen __instance) {
			__instance.gameObject.AddOrGet<MoreModActions>().HidePopup();
		}

		/// <summary>
		/// Logs all failed asserts to the error log.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		private static void LogAllFailedAsserts(Harmony harmony) {
			var handler = new HarmonyMethod(typeof(DebugLogger), nameof(DebugLogger.
				OnAssertFailed));
			try {
				// Assert(bool)
				var assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool));
				if (assert != null)
					harmony.Patch(assert, handler);
				// Assert(bool, object)
				assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool), typeof(
					object));
				if (assert != null)
					harmony.Patch(assert, handler);
				// Assert(bool, object, UnityEngine.Object)
				assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool), typeof(
					object), typeof(UnityEngine.Object));
				if (assert != null)
					harmony.Patch(assert, handler);
				// Assert(bool, string)
				assert = typeof(KCrashReporter).GetMethodSafe("Assert", true, typeof(bool),
					typeof(string));
				if (assert != null)
					harmony.Patch(assert, handler);
#if DEBUG
				DebugLogger.LogDebug("Logging all failed asserts");
#endif
			} catch (Exception e) {
				DebugLogger.BaseLogException(e, null);
			}
		}

		/// <summary>
		/// Handles a mod crash and bypasses disabling the mod if it is this mod.
		/// </summary>
		private static bool OnModCrash(Mod __instance) {
			return ThisMod == null || !__instance.label.Match(ThisMod.label);
		}

		/// <summary>
		/// Runs the required postload patches after all other mods load.
		/// </summary>
		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			var options = DebugNotIncludedOptions.Instance;
			if (options?.PowerUserMode == true)
				harmony.Patch(typeof(ModsScreen), "BuildDisplay",
					new HarmonyMethod(typeof(DebugNotIncludedPatches), nameof(HidePopups)),
					new HarmonyMethod(typeof(DebugNotIncludedPatches), nameof(BuildDisplay)));
			if (mods != null)
				ModDebugRegistry.Instance.Populate(mods);
			else
				DebugLogger.LogWarning("Mods list is empty! Attribution will not work");

			var runningCore = PRegistry.Instance.GetLatestVersion(
				"PeterHan.PLib.Core.PLibCorePatches")?.GetOwningAssembly();
			if (runningCore != null)
				RunningPLibAssembly = runningCore;
			// Log which mod is running PLib
			var latest = ModDebugRegistry.Instance.OwnerOfAssembly(RunningPLibAssembly);
			if (latest != null)
				DebugLogger.LogDebug("Executing version of PLib is from: " + latest.ModName);

			HarmonyPatchInspector.Check();
#if DEBUG
			harmony.ProfileMethod(typeof(SaveLoader).GetMethodSafe("Load", false, typeof(
				IReader)));
			harmony.ProfileMethod(typeof(SaveLoader).GetMethodSafe("Save", false, typeof(
				BinaryWriter)));
			harmony.ProfileMethod(typeof(SaveManager).GetMethodSafe("Load", false,
				PPatchTools.AnyArguments));
			harmony.ProfileMethod(typeof(SaveManager).GetMethodSafe("Save", false,
				PPatchTools.AnyArguments));
#endif
			if (options?.LocalizeMods == true)
				typeof(PLocalization).GetMethodSafe("DumpAll", false)?.Invoke(loc, null);
		}

		/// <summary>
		/// Used to localize this mod.
		/// </summary>
		private readonly PLocalization loc;

		public DebugNotIncludedPatches() {
			loc = new PLocalization();
		}

		public override void OnLoad(Harmony harmony) {
			_ = ModDebugRegistry.Instance;

			var method = typeof(Mod).GetMethodSafe("Crash", false);
			if (method == null)
				method = typeof(Mod).GetMethodSafe("SetCrashed", false);
			if (method != null)
				harmony.Patch(method, prefix: new HarmonyMethod(typeof(
					DebugNotIncludedPatches), nameof(OnModCrash)));

			base.OnLoad(harmony);
			RunningPLibAssembly = typeof(PUtil).Assembly;
			PUtil.InitLibrary();
			if (DebugNotIncludedOptions.Instance?.DetailedBacktrace ?? true)
				DebugLogger.InstallExceptionLogger();
			new POptions().RegisterOptions(this, typeof(DebugNotIncludedOptions));

			LocString.CreateLocStringKeys(typeof(DebugNotIncludedStrings.UI));
			LocString.CreateLocStringKeys(typeof(DebugNotIncludedStrings.INPUT_BINDINGS));
			loc.Register();

			if (DebugNotIncludedOptions.Instance?.LogAsserts ?? true)
				LogAllFailedAsserts(harmony);
			ThisMod = mod;
			if (ThisMod == null)
				DebugLogger.LogWarning("Unable to determine KMod instance!");

			new PPatchManager(harmony).RegisterPatchClass(typeof(DebugNotIncludedPatches));
			// Force class initialization to avoid crashes on the background thread if
			// an Assert fails later
			DebugUtils.RegisterUIDebug();
			new PLib.AVC.PVersionCheck().Register(this, new PLib.AVC.SteamVersionChecker());
		}
	}

	/// <summary>
	/// Applied to AudioSheets to log audio event information.
	/// </summary>
	[HarmonyPatch(typeof(AudioSheets), "CreateSound")]
	public static class AudioSheets_CreateSound_Patch {
		internal static bool Prepare() {
			return DebugNotIncludedOptions.Instance?.LogSounds ?? false;
		}

		/// <summary>
		/// Applied after CreateSound runs.
		/// </summary>
		internal static void Postfix(string file_name, string anim_name, string sound_name) {
			// Add sound "GasPump_intake" to anim pumpgas_kanim.working_loop
			DebugLogger.LogDebug("Add sound \"{0}\" to anim {1}.{2}".F(sound_name,
				file_name, anim_name));
		}
	}

	/// <summary>
	/// Applied to Debug to log which methods are actually sending log messages.
	/// </summary>
	[HarmonyPatch(typeof(Debug), "TimeStamp")]
	public static class Debug_TimeStamp_Patch {
		internal static bool Prepare() {
			return DebugNotIncludedOptions.Instance?.ShowLogSenders ?? false;
		}

		/// <summary>
		/// Applied after TimeStamp runs.
		/// </summary>
		internal static void Postfix(ref string __result) {
			/*
			 * Postfix()
			 * TimeStamp_Patch1()
			 * WriteTimeStamped
			 * Log/LogFormat/...
			 */
			__result = DebugLogger.AddCallingLocation(__result, new StackTrace(4));
		}
	}

	/// <summary>
	/// Applied to DebugUtil to log errors more cleanly.
	/// </summary>
	[HarmonyPatch(typeof(DebugUtil), nameof(DebugUtil.LogErrorArgs), typeof(object[]))]
	public static class DebugUtil_LogErrorArgs_Patch {
		/// <summary>
		/// Applied after LogErrorArgs runs.
		/// </summary>
		internal static void Postfix() {
			DebugLogger.DumpStack();
		}
	}

	/// <summary>
	/// Applied to DebugUtil to log exceptions more cleanly.
	/// </summary>
	[HarmonyPatch(typeof(DebugUtil), nameof(DebugUtil.LogException))]
	public static class DebugUtil_LogException_Patch {
		/// <summary>
		/// Applied before LogException runs.
		/// </summary>
		internal static bool Prefix(Exception e, string errorMessage) {
			DebugLogger.LogError(errorMessage);
			DebugLogger.LogException(e);
			return false;
		}
	}

#if DEBUG
	/// <summary>
	/// Applied to EggConfig to allow eggs to be instantly hatched.
	/// </summary>
	[HarmonyPatch(typeof(EggConfig), nameof(EggConfig.CreateEgg))]
	public static class EggConfig_CreateEgg_Patch {
		/// <summary>
		/// Applied after CreateEgg runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			__result.AddOrGet<InstantGrowable>();
		}
	}

	/// <summary>
	/// Applied to EntityTemplates to allow things to be instantly tamed in sandbox mode.
	/// </summary>
	[HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.ExtendEntityToWildCreature))]
	public static class EntityTemplates_ExtendEntityToWildCreature_Patch {
		/// <summary>
		/// Applied after ExtendEntityToWildCreature runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			__result.AddOrGet<InstantGrowable>();
		}
	}
#endif

#if DEBUG
	/// <summary>
	/// Applied to EventSystem to debug the cause of the pesky "Not subscribed to event"
	/// log spam.
	/// </summary>
	[HarmonyPatch(typeof(EventSystem), nameof(EventSystem.Unsubscribe), typeof(int),
		typeof(int), typeof(bool))]
	public static class EventSystem_Unsubscribe_Patch {
		/// <summary>
		/// Applied before Unsubscribe runs.
		/// </summary>
		internal static void Prefix(ArrayRef<IntraObjectRoute> ___intraObjectRoutes,
				int eventName, int subscribeHandle, bool suppressWarnings) {
			if (!suppressWarnings && ___intraObjectRoutes.FindIndex((route) => route.
					eventHash == eventName && route.handlerIndex == subscribeHandle) < 0)
				DebugLogger.DumpStack();
		}

		// Mirror struct to the private struct EventSystem.IntraObjectRoute
		internal struct IntraObjectRoute {
#pragma warning disable CS0649
			public int eventHash;
			public int handlerIndex;
#pragma warning restore CS0649

			public override string ToString() {
				return "IntraObjectRoute[hash={0:D},index={1:D}]".F(eventHash,
					handlerIndex);
			}
		}
	}
#endif

#if DEBUG
	/// <summary>
	/// Applied to Growing to allow things to be instantly grown in sandbox mode.
	/// </summary>
	[HarmonyPatch(typeof(Growing), "OnPrefabInit")]
	public static class Growing_OnPrefabInit_Patch {
		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(Growing __instance) {
			__instance.gameObject.AddOrGet<InstantGrowable>();
		}
	}
#endif

	/// <summary>
	/// Applied to KCrashReporter to crash to desktop instead of showing the crash dialog.
	/// </summary>
	[HarmonyPatch(typeof(KCrashReporter), nameof(KCrashReporter.ShowDialog))]
	public static class KCrashReporter_ShowDialog_Patch {
		/// <summary>
		/// Applied before ShowDialog runs.
		/// </summary>
		internal static bool Prefix(ref bool __result) {
			bool ctd = DebugNotIncludedOptions.Instance.DisableCrashDialog;
			if (ctd) {
				__result = false;
				Application.Quit();
			}
			return !ctd;
		}
	}

	/// <summary>
	/// Applied to MainMenu to check and optionally move this mod to the top.
	/// </summary>
	[HarmonyPatch(typeof(MainMenu), "OnSpawn")]
	public static class MainMenu_OnSpawn_Patch {
		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(MainMenu __instance) {
			if (DebugNotIncludedOptions.Instance?.SkipFirstModCheck != true)
				ModDialogs.CheckFirstMod(__instance.gameObject);
		}
	}

	/// <summary>
	/// Applied to Manager to make the mod events dialog more user friendly.
	/// </summary>
	[HarmonyPatch(typeof(Manager), "MakeEventList")]
	public static class Manager_MakeEventList_Patch {
		/// <summary>
		/// Applied after MakeEventList runs.
		/// </summary>
		internal static void Postfix(List<Event> events, ref string __result) {
			string result = ModEvents.Describe(events);
			if (!string.IsNullOrEmpty(result))
				__result = result;
		}
	}

	/// <summary>
	/// Applied to Manager to make the Subscribe function not log.
	/// </summary>
	[HarmonyPatch(typeof(Manager), nameof(Manager.Subscribe))]
	public static class Manager_Subscribe_Patch {
		/// <summary>
		/// Transpiles Subscribe to remove all debug log calls.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.RemoveMethodCall(method, typeof(Debug).GetMethodSafe(nameof(
				Debug.LogFormat), true, typeof(string), typeof(object[])));
		}
	}

	/// <summary>
	/// Applied to MinionConfig to add buttons for triggering stress and joy reactions.
	/// </summary>
	[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.CreatePrefab))]
	public static class MinionConfig_CreatePrefab_Patch {
		/// <summary>
		/// Applied after CreatePrefab runs.
		/// </summary>
		internal static void Postfix(GameObject __result) {
			__result.AddOrGet<InstantEmotable>();
		}
	}

	/// <summary>
	/// Applied to Mod to reduce logging when checking for archived versions.
	/// </summary>
	[HarmonyPatch(typeof(Mod), "GetMostSuitableArchive")]
	public static class Mod_GetMostSuitableArchive_Patch {
		/// <summary>
		/// Transpiles GetMostSuitableArchive to remove all debug log calls.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.RemoveMethodCall(method, typeof(Mod).GetMethodSafe(nameof(
				Mod.ModDevLog), false, typeof(string)));
		}
	}

	/// <summary>
	/// Applied to Mod to make the ScanContent method log less.
	/// </summary>
	[HarmonyPatch(typeof(Mod), nameof(Mod.ScanContent))]
	public static class Mod_ScanContent_Patch {
		/// <summary>
		/// Logs mod load information to the debug log.
		/// </summary>
		/// <param name="mod">The mod that was loaded.</param>
		private static void LogModScanned(Mod mod) {
			if (mod != null) {
				string path = mod.relative_root;
				DebugLogger.LogDebug("{3} ({0}): Successfully loaded {2} from '{1}'".F(
					mod.label.title, string.IsNullOrEmpty(path) ? "root" : path,
					mod.available_content.ToString(), mod.staticID));
			}
		}

		/// <summary>
		/// Transpiles ScanContent to remove the logs and add a call to a consolidated log
		/// method.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var devLog = typeof(Mod).GetMethodSafe(nameof(Mod.ModDevLog), false,
				typeof(string));
			var debugLog = typeof(Debug).GetMethodSafe(nameof(Debug.Log), true,
				typeof(object));
			var postfix = typeof(Mod_ScanContent_Patch).GetMethodSafe(nameof(LogModScanned),
				true, typeof(Mod));
			int replaced = 0;
			foreach (var instr in method) {
				if (instr.Is(OpCodes.Call, devLog)) {
					yield return new CodeInstruction(OpCodes.Pop);
					instr.opcode = OpCodes.Pop;
					instr.operand = null;
				} else if (debugLog != null && instr.Is(OpCodes.Call, debugLog)) {
					instr.opcode = OpCodes.Pop;
					instr.operand = null;
					if (++replaced == 3 && postfix != null) {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, postfix);
					}
				}
				yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to ModUtil to log animations loaded.
	/// </summary>
	[HarmonyPatch(typeof(ModUtil), nameof(ModUtil.AddKAnimMod))]
	public static class ModUtil_AddKAnimMod_Patch {
		/// <summary>
		/// Applied after AddKAnimMod runs.
		/// </summary>
		internal static void Postfix(string name) {
			DebugLogger.LogDebug("Adding anim \"{0}\"", name);
		}
	}

	/// <summary>
	/// Applied to ModsScreen to add UI for performing more actions to mods.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), "OnActivate")]
	[HarmonyPriority(Priority.Last)]
	public static class ModsScreen_OnActivate_Patch {
		/// <summary>
		/// Applied before OnActivate runs.
		/// </summary>
		internal static void Prefix(GameObject ___entryPrefab) {
			if (___entryPrefab != null)
				ModDialogs.ConfigureRowPrefab(___entryPrefab);
		}

		internal static bool Prepare() {
			return DebugNotIncludedOptions.Instance?.PowerUserMode ?? false;
		}

		/// <summary>
		/// Applied after OnActivate runs.
		/// </summary>
		internal static void Postfix(KButton ___workshopButton, ModsScreen __instance) {
			if (___workshopButton != null) {
				// Hide the "STEAM WORKSHOP" button
				var obj = ___workshopButton.gameObject;
				obj.SetActive(false);
				var parent = obj.GetParent();
				if (parent != null)
					ModDialogs.AddExtraButtons(__instance.gameObject, parent);
			}
		}
	}

#if ALL_MODS_CHECKBOX
	/// <summary>
	/// Applied to ModsScreen to update the All checkbox when mods are toggled.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), "OnToggleClicked")]
	public static class ModsScreen_OnToggleClicked_Patch {
		internal static bool Prepare() {
			return DebugNotIncludedOptions.Instance?.PowerUserMode ?? false;
		}

		/// <summary>
		/// Applied after OnToggleClicked runs.
		/// </summary>
		internal static void Postfix(ModsScreen __instance) {
			__instance?.GetComponent<AllModsHandler>()?.UpdateCheckedState();
		}
	}
#endif

	/// <summary>
	/// Applied to SaveLoader to try and get rid of a duplicate Sim initialization.
	/// </summary>
	[HarmonyPatch(typeof(SaveLoader), "OnSpawn")]
	public static class SaveLoader_OnSpawn_Patch {
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceMethodCallSafe(method, new Dictionary<MethodInfo,
				MethodInfo>() {
				{
					typeof(Sim).GetMethodSafe(nameof(Sim.SIM_Initialize), true,
						PPatchTools.AnyArguments), PPatchTools.RemoveCall
				},
				{
					typeof(SimMessages).GetMethodSafe(nameof(SimMessages.
						CreateSimElementsTable), true, PPatchTools.AnyArguments),
					PPatchTools.RemoveCall
				},
				{
					typeof(SimMessages).GetMethodSafe(nameof(SimMessages.CreateDiseaseTable),
						true, PPatchTools.AnyArguments), PPatchTools.RemoveCall
				}
			});
		}
	}

	/// <summary>
	/// Applied to ScheduleManager to sort schedules upon game load.
	/// </summary>
	[HarmonyPatch(typeof(ScheduleManager), "OnSpawn")]
	public static class ScheduleManager_OnSpawn_Patch {
		internal static bool Prepare() {
			return DebugNotIncludedOptions.Instance?.SortSchedules ?? false;
		}

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(ScheduleManager __instance) {
			DebugLogger.LogDebug("Sorting schedules");
			__instance.GetSchedules().Sort((a, b) => string.Compare(a.name, b.name,
				StringComparison.CurrentCultureIgnoreCase));
		}
	}

#if DEBUG
	/// <summary>
	/// Applied to SelectToolHoverTextCard to show the cell coordinates, number, and other
	/// attributes in the hover card.
	/// </summary>
	[HarmonyPatch(typeof(SelectToolHoverTextCard), nameof(SelectToolHoverTextCard.
		UpdateHoverElements))]
	public static class UpdateHoverElements_Patch {
		/// <summary>
		/// Adds the coordinates and cell number to the select tool.
		/// </summary>
		private static HoverTextDrawer DrawCoordinates(HoverTextDrawer drawer,
				HoverTextConfiguration instance) {
			int cell = Grid.PosToCell(Camera.main.ScreenToWorldPoint(
				KInputManager.GetMousePos()));
			if (Grid.IsValidCell(cell)) {
				Grid.CellToXY(cell, out int x, out int y);
				drawer.BeginShadowBar();
				drawer.DrawText(string.Format(DebugNotIncludedStrings.UI.TOOLTIPS.DNI_CELL,
					cell, x, y), instance.Styles_BodyText.Standard);
				drawer.EndShadowBar();
			}

			return drawer;
		}

		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var targetMethod = typeof(HoverTextDrawer).GetMethodSafe(nameof(HoverTextDrawer.
				EndDrawing), false);
			var addition = typeof(UpdateHoverElements_Patch).GetMethodSafe(nameof(
				DrawCoordinates), true, typeof(HoverTextDrawer), typeof(
				HoverTextConfiguration));
			if (targetMethod != null && addition != null)
				foreach (var instr in method) {
					if (instr.Is(OpCodes.Callvirt, targetMethod)) {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, addition);
					}
					yield return instr;
				}
			else {
				DebugLogger.LogWarning("Unable to patch UpdateHoverElements");
				foreach (var instr in method)
					yield return instr;
			}
		}
	}
#endif
}
