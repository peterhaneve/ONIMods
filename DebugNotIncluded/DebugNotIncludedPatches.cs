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
using Harmony.ILCopying;
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Patches which will be applied via annotations for Debug Not Included.
	/// </summary>
	public static class DebugNotIncludedPatches {
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

		/// <summary>
		/// The Action used when "UI Debug" is pressed.
		/// </summary>
		internal static PAction UIDebugAction { get; private set; }

		/// <summary>
		/// Applied to ModsScreen to add our buttons and otherwise tweak the dialog.
		/// </summary>
		private static void BuildDisplay(ModsScreen __instance, object ___displayedMods) {
			// Must cast the type because ModsScreen.DisplayedMod is private
			foreach (var displayedMod in (System.Collections.IEnumerable)___displayedMods)
				ModDialogs.ConfigureRowInstance(Traverse.Create(displayedMod), __instance);
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
		/// Applied to DLLLoader to patch in our handling to LoadDLLs.
		/// </summary>
		[PLibPatch(RunAt.Immediately, "LoadDLLs", RequireType = "KMod.DLLLoader")]
		internal static void LoadDLLs_Postfix(object __result) {
			// LoadedModData is not declared in old versions
			if (__result != null)
				ModLoadHandler.LoadAssemblies(__result);
		}

		/// <summary>
		/// Logs all failed asserts to the error log.
		/// </summary>
		private static void LogAllFailedAsserts() {
			var handler = new HarmonyMethod(typeof(DebugLogger), nameof(DebugLogger.
				OnAssertFailed));
			var inst = ModDebugRegistry.Instance.DebugInstance;
			MethodInfo assert;
			try {
				// Assert(bool)
				assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool));
				if (assert != null)
					inst.Patch(assert, handler);
				// Assert(bool, object)
				assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool), typeof(
					object));
				if (assert != null)
					inst.Patch(assert, handler);
				// Assert(bool, object, UnityEngine.Object)
				assert = typeof(Debug).GetMethodSafe("Assert", true, typeof(bool), typeof(
					object), typeof(UnityEngine.Object));
				if (assert != null)
					inst.Patch(assert, handler);
				// Assert(bool, string)
				assert = typeof(KCrashReporter).GetMethodSafe("Assert", true, typeof(bool),
					typeof(string));
				if (assert != null)
					inst.Patch(assert, handler);
#if DEBUG
				DebugLogger.LogDebug("Logging all failed asserts");
#endif
			} catch (Exception e) {
				DebugLogger.BaseLogException(e, null);
			}
		}

		public static void OnLoad(string path) {
			var inst = ModDebugRegistry.Instance;
			RunningPLibAssembly = typeof(PUtil).Assembly;
			PUtil.InitLibrary();
			if (DebugNotIncludedOptions.Instance?.DetailedBacktrace ?? true)
				DebugLogger.InstallExceptionLogger();
			POptions.RegisterOptions(typeof(DebugNotIncludedOptions));
			// Set up strings
			LocString.CreateLocStringKeys(typeof(DebugNotIncludedStrings.UI));
			LocString.CreateLocStringKeys(typeof(DebugNotIncludedStrings.INPUT_BINDINGS));
			PLocalization.Register();
			if (DebugNotIncludedOptions.Instance?.LogAsserts ?? true)
				LogAllFailedAsserts();
			foreach (var mod in Global.Instance.modManager?.mods)
				if (mod.GetModBasePath() == path) {
					ThisMod = mod;
					break;
				}
			if (ThisMod == null)
				DebugLogger.LogWarning("Unable to determine KMod instance!");
			else
				inst.RegisterModAssembly(Assembly.GetExecutingAssembly(), inst.GetDebugInfo(
					ThisMod));
			// Default UI debug key is ALT+U
			UIDebugAction = PAction.Register("DebugNotIncluded.UIDebugAction",
				DebugNotIncludedStrings.INPUT_BINDINGS.DEBUG.SNAPSHOT, new PKeyBinding(
				KKeyCode.U, Modifier.Alt));
			// Must postload the mods dialog to come out after aki's mods, ony's mods, PLib
			// options, and so forth
			PUtil.RegisterPatchClass(typeof(DebugNotIncludedPatches));
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
		/// <param name="instance">The Harmony instance to execute patches.</param>
		[PLibMethod(RunAt.AfterModsLoad)]
		private static void PostloadHandler(HarmonyInstance instance) {
			if (DebugNotIncludedOptions.Instance?.PowerUserMode ?? false)
				instance.Patch(typeof(ModsScreen), "BuildDisplay",
					new HarmonyMethod(typeof(DebugNotIncludedPatches), nameof(HidePopups)),
					new HarmonyMethod(typeof(DebugNotIncludedPatches), nameof(BuildDisplay)));
			KInputHandler.Add(Global.Instance.GetInputManager().GetDefaultController(),
				new UISnapshotHandler(), 1024);
			// New postload architecture requires going back a little ways
			var st = new StackTrace(6);
			Assembly assembly = null;
			if (st.FrameCount > 0)
				assembly = st.GetFrame(0).GetMethod()?.DeclaringType?.Assembly;
			RunningPLibAssembly = assembly ?? Assembly.GetCallingAssembly();
			// Log which mod is running PLib
			var latest = ModDebugRegistry.Instance.OwnerOfAssembly(RunningPLibAssembly);
			if (latest != null)
				DebugLogger.LogDebug("Executing version of PLib is from: " + latest.ModName);
			HarmonyPatchInspector.Check();
#if DEBUG
			// SaveManager.Load:: 13831 ms
			instance.ProfileMethod(typeof(SaveLoader).GetMethodSafe("Load", false, typeof(
				IReader)));
			instance.ProfileMethod(typeof(SaveLoader).GetMethodSafe("Save", false, typeof(
				BinaryWriter)));
			instance.ProfileMethod(typeof(SaveManager).GetMethodSafe("Load", false,
				PPatchTools.AnyArguments));
			instance.ProfileMethod(typeof(SaveManager).GetMethodSafe("Save", false,
				PPatchTools.AnyArguments));
#endif
		}

		/// <summary>
		/// Invoked by the game before our patches, so we get a chance to patch Mod.Crash.
		/// </summary>
		public static void PrePatch(HarmonyInstance instance) {
			var method = typeof(Mod).GetMethodSafe("Crash", false);
			if (method == null)
				method = typeof(Mod).GetMethodSafe("SetCrashed", false);
			if (method != null)
				instance.Patch(method, prefix: new HarmonyMethod(typeof(
					DebugNotIncludedPatches), nameof(OnModCrash)));
		}

		/// <summary>
		/// Profiles a method, outputting how many milliseconds it took to run on each use.
		/// </summary>
		/// <param name="instance">The Harmony instance to use for the patch.</param>
		/// <param name="target">The method to profile.</param>
		internal static void ProfileMethod(this HarmonyInstance instance, MethodBase target) {
			if (target == null)
				PUtil.LogWarning("No method specified to profile!");
			else {
				instance.Patch(target, new HarmonyMethod(typeof(DebugNotIncludedPatches),
					nameof(ProfilerPrefix)), new HarmonyMethod(typeof(DebugNotIncludedPatches),
					nameof(ProfilerPostfix)));
				DebugLogger.LogDebug("Profiling method {0}.{1}".F(target.DeclaringType, target.
					Name));
			}
		}

		/// <summary>
		/// A postfix method for instrumenting methods in the code base. Logs the total time
		/// taken in milliseconds.
		/// </summary>
		private static void ProfilerPostfix(MethodBase __originalMethod, Stopwatch __state) {
			DebugLogger.LogDebug("{1}.{2}:: {0:D} ms".F(__state.ElapsedMilliseconds,
				__originalMethod.DeclaringType, __originalMethod.Name));
		}

		/// <summary>
		/// A prefix method for instrumenting methods in the code base.
		/// </summary>
		private static void ProfilerPrefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Transpiles the Spawn and InitializeComponent methods of KMonoBehaviour to better
		/// handle debug messages.
		/// </summary>
		private static IEnumerable<CodeInstruction> TranspileSpawn(
				IEnumerable<CodeInstruction> method) {
			var instructions = new List<CodeInstruction>(method);
			var target = typeof(DebugLogger).GetMethodSafe(nameof(DebugLogger.
				LogKMonoException), true, typeof(Exception));
			// Find last "throw"
			for (int i = instructions.Count - 1; i > 0; i--) {
				var instr = instructions[i];
				if (instr.opcode == OpCodes.Throw) {
					// Insert "dup" and call before it
					instructions.Insert(i, new CodeInstruction(OpCodes.Call, target));
					instructions.Insert(i, new CodeInstruction(OpCodes.Dup));
					break;
				}
			}
			return instructions;
		}

#if false
		/// <summary>
		/// Applied to Assets to fix a bug with anim loading.
		/// </summary>
		[HarmonyPatch(typeof(Assets), "LoadAnims")]
		public static class Assets_LoadAnims_Patch {
			internal static bool Prepare() {
				return !string.IsNullOrEmpty(DlcManager.GetActiveDlcId());
			}

			/// <summary>
			/// Applied after LoadAnims runs.
			/// </summary>
			internal static void Postfix() {
				var animTable = Traverse.Create(typeof(Assets)).
					GetField<IDictionary<HashedString, KAnimFile>>("AnimTable");
				foreach (var modAnim in Assets.ModLoadedKAnims)
					if (modAnim != null)
						animTable[modAnim.name] = modAnim;
			}

			/// <summary>
			/// Transpiles LoadAnims to swap some key instructions.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator,
					IEnumerable<CodeInstruction> method) {
				var remove = typeof(Manager).GetMethodSafe(nameof(Manager.Load), false,
					typeof(Content));
				var globalInstance = typeof(Global).GetPropertySafe<Global>(nameof(Global.
					Instance), true);
				var managerField = typeof(Global).GetFieldSafe(nameof(Global.modManager),
					false);
				var insertAt = typeof(KAnimGroupFile).GetMethodSafe(nameof(KAnimGroupFile.
					LoadAll), true, PPatchTools.AnyArguments);
				foreach (var instr in method) {
					var opcode = instr.opcode;
					var operand = instr.operand;
					if (opcode == OpCodes.Callvirt && (operand as MethodBase) == remove &&
							globalInstance != null && managerField != null) {
						// Comment out the first call and pop the argument and the instance
						instr.operand = null;
						instr.opcode = OpCodes.Pop;
						yield return new CodeInstruction(OpCodes.Pop);
					}
					yield return instr;
					if (opcode == OpCodes.Call && (operand as MethodBase) == insertAt &&
							globalInstance != null && managerField != null) {
						// Re-add the call after the groups are set
						yield return new CodeInstruction(OpCodes.Call, globalInstance.
							GetGetMethod());
						yield return new CodeInstruction(OpCodes.Ldfld, managerField);
						yield return new CodeInstruction(OpCodes.Ldc_I4, (int)Content.Animation);
						yield return new CodeInstruction(OpCodes.Callvirt, remove);
					}
				}
			}
		}
#endif

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
		/// Applied to DebugUtil to log exceptions more cleanly.
		/// </summary>
		[HarmonyPatch(typeof(DebugUtil), "LogException")]
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
		/// Applied to EntityTemplates to allow things to be instantly tamed in sandbox mode.
		/// </summary>
		[HarmonyPatch(typeof(EntityTemplates), "ExtendEntityToWildCreature")]
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
		[HarmonyPatch(typeof(EventSystem), "Unsubscribe", typeof(int), typeof(int),
			typeof(bool))]
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
		[HarmonyPatch(typeof(KCrashReporter), "ShowDialog")]
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
		/// Applied to MainMenu to check and move this mod to the top.
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
		/// Applied to Manager to make the crash and restart dialog better.
		/// </summary>
		[HarmonyPatch(typeof(Manager), "DevRestartDialog")]
		public static class Manager_DevRestartDialog_Patch {
			/// <summary>
			/// Applied before DevRestartDialog runs.
			/// </summary>
			internal static bool Prefix(Manager __instance, GameObject parent, bool is_crash) {
				var events = __instance.events;
				bool cont = true;
				if (events != null && events.Count > 0 && is_crash) {
					ModDialogs.BlameFailedMod(parent);
					events.Clear();
					cont = false;
				}
				return cont;
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

#if DEBUG
		/// <summary>
		/// Applied to Memory to warn about suspicious patches that target empty methods.
		/// 
		/// DEBUG ONLY.
		/// </summary>
		[HarmonyPatch(typeof(Memory), "DetourMethod")]
		public static class Memory_DetourMethod_Patch {
			private const int MIN_METHOD_SIZE = 8;

			/// <summary>
			/// Applied before DetourMethod runs.
			/// </summary>
			internal static void Prefix(MethodBase original, MethodBase replacement) {
				var body = original.GetMethodBody();
				if (body.GetILAsByteArray().Length < MIN_METHOD_SIZE)
					PUtil.LogWarning("Patch {0} targets empty method {1}.{2}".F(replacement.
						Name, original.DeclaringType, original.Name));
			}
		}
#endif

		/// <summary>
		/// Applied to MinionConfig to add buttons for triggering stress and joy reactions.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), "CreatePrefab")]
		public static class MinionConfig_CreatePrefab_Patch {
			/// <summary>
			/// Applied after CreatePrefab runs.
			/// </summary>
			internal static void Postfix(GameObject __result) {
				__result.AddOrGet<InstantEmotable>();
			}
		}

		/// <summary>
		/// Applied to Mod to set the active mod when loading.
		/// </summary>
		[HarmonyPatch(typeof(Mod), "Load")]
		public static class Mod_Load_Patch {
			/// <summary>
			/// Applied before Load runs.
			/// </summary>
			internal static void Prefix(Mod __instance) {
				ModLoadHandler.CurrentMod = ModDebugRegistry.Instance.GetDebugInfo(__instance);
			}
		}

		/// <summary>
		/// Applied to ModUtil to log animations loaded.
		/// </summary>
		[HarmonyPatch(typeof(ModUtil), "AddKAnimMod")]
		public static class ModUtil_AddKAnimMod_Patch {
			/// <summary>
			/// Applied after AddKAnimMod runs.
			/// </summary>
			internal static void Postfix(string name) {
				DebugLogger.LogDebug("Adding anim \"{0}\"", name);
			}
		}

		/// <summary>
		/// Applied to ModsScreen to add UI for saving and restoring mod lists.
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

#if DEBUG
		/// <summary>
		/// Applied to PatchProcessor to warn about suspicious patches that end up targeting
		/// a method in another class.
		/// 
		/// DEBUG ONLY.
		/// </summary>
		[HarmonyPatch(typeof(PatchProcessor), "GetOriginalMethod")]
		public static class PatchProcessor_GetOriginalMethod_Patch {
			/// <summary>
			/// Applied after GetOriginalMethod runs.
			/// </summary>
			internal static void Postfix(HarmonyMethod ___containerAttributes,
					Type ___container, MethodBase __result) {
				if (__result != null && ___containerAttributes != null)
					HarmonyPatchInspector.CheckHarmonyMethod(___containerAttributes,
						___container);
			}
		}
#endif

		/// <summary>
		/// Applied to SaveLoader to try and get rid of a duplicate Sim initialization.
		/// </summary>
		[HarmonyPatch(typeof(SaveLoader), "OnSpawn")]
		public static class SaveLoader_OnSpawn_Patch {
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, new Dictionary<MethodInfo, MethodInfo>() {
					{ typeof(Sim).GetMethodSafe(nameof(Sim.SIM_Initialize), true, PPatchTools.AnyArguments), null },
					{ typeof(SimMessages).GetMethodSafe(nameof(SimMessages.CreateSimElementsTable), true, PPatchTools.AnyArguments), null },
					{ typeof(SimMessages).GetMethodSafe(nameof(SimMessages.CreateDiseaseTable), true, PPatchTools.AnyArguments), null }
				});
			}
		}

#if false
		private static ConcurrentDictionary<string, int> hitCount;
		private static ConcurrentDictionary<int, int> threadCount;
		private static long timeInPath;

		/// <summary>
		/// Dumps profiling information every second.
		/// </summary>
		[HarmonyPatch(typeof(Navigator.PathProbeTask), "Run")]
		internal sealed class ProfilingComponent : KMonoBehaviour, IRender1000ms {
			protected override void OnPrefabInit() {
				base.OnPrefabInit();
				hitCount = new ConcurrentDictionary<string, int>(64, 4);
				threadCount = new ConcurrentDictionary<int, int>(32, 4);
				timeInPath = 0L;
			}

			internal static void Postfix() {
				var stackTrace = new StackTrace(2);
				int id = Thread.CurrentThread.ManagedThreadId;
				if (stackTrace.FrameCount > 0) {
					var method = stackTrace.GetFrame(0).GetMethod();
					hitCount.AddOrUpdate(method.DeclaringType.FullName + "." + method.Name,
						1, (key, value) => value + 1);
				}
				threadCount.AddOrUpdate(id, 1, (key, value) => value + 1);
			}

			public void Render1000ms(float dt) {
				PUtil.LogDebug("Spent {0:D} us in pathfinding the last second".F(
					timeInPath / 1000L));
				foreach (var pair in hitCount)
					PUtil.LogDebug("{0:D} hits from {1}".F(pair.Value, pair.Key));
				foreach (var pair in threadCount)
					PUtil.LogDebug("{0:D} hits from T#{1:D}".F(pair.Value, pair.Key));
				timeInPath = 0L;
				hitCount.Clear();
				threadCount.Clear();
			}
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void InitTimers() {
			Game.Instance.gameObject.AddOrGet<ProfilingComponent>();
		}

		[HarmonyPatch(typeof(PathProber), "UpdateProbe")]
		public static class TimePatch {
			internal static void Prefix(ref Stopwatch __state) {
				__state = Stopwatch.StartNew();
			}

			internal static void Postfix(Stopwatch __state) {
				long ticks = __state.ElapsedTicks * 1000000000L / Stopwatch.Frequency,
					oldValue, newValue;
				// Thread safe, lockless increment
				do {
					oldValue = Interlocked.Read(ref timeInPath);
					newValue = oldValue + ticks;
				} while (Interlocked.CompareExchange(ref timeInPath, newValue, oldValue) !=
					oldValue);
			}
		}
#endif
	}
}
