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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
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
				if (mod.label.install_path == path) {
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
			var st = new System.Diagnostics.StackTrace(6);
			Assembly assembly = null;
			if (st.FrameCount > 0)
				assembly = st.GetFrame(0).GetMethod()?.DeclaringType?.Assembly;
			PUtil.LogDebug(assembly?.FullName ?? "none");
			RunningPLibAssembly = assembly ?? Assembly.GetCallingAssembly();
			// Log which mod is running PLib
			var latest = ModDebugRegistry.Instance.OwnerOfAssembly(RunningPLibAssembly);
			if (latest != null)
				DebugLogger.LogDebug("Executing version of PLib is from: " + latest.ModName);
			HarmonyPatchInspector.Check();
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
		/// Applied to BuildingConfigManager to catch and report errors when initializing
		/// buildings.
		/// </summary>
		[HarmonyPatch(typeof(BuildingConfigManager), "RegisterBuilding")]
		public static class BuildingConfigManager_RegisterBuilding_Patch {
			/// <summary>
			/// Transpiles RegisterBuilding to catch exceptions and log them.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator,
					IEnumerable<CodeInstruction> method) {
#if DEBUG
				DebugLogger.LogDebug("Transpiling BuildingConfigManager.RegisterBuilding()");
#endif
				var logger = typeof(DebugLogger).GetMethodSafe(nameof(DebugLogger.
					LogBuildingException), true, typeof(Exception), typeof(IBuildingConfig));
				var ee = method.GetEnumerator();
				// Emit all but the last instruction
				if (ee.MoveNext()) {
					CodeInstruction last;
					bool hasNext, isFirst = true;
					var endMethod = generator.DefineLabel();
					do {
						last = ee.Current;
						if (isFirst)
							last.blocks.Add(new ExceptionBlock(ExceptionBlockType.
								BeginExceptionBlock, null));
						hasNext = ee.MoveNext();
						isFirst = false;
						if (hasNext)
							yield return last;
					} while (hasNext);
					// Preserves the labels "ret" might have had
					last.opcode = OpCodes.Nop;
					last.operand = null;
					yield return last;
					// Add a "leave"
					yield return new CodeInstruction(OpCodes.Leave, endMethod);
					// The exception is already on the stack
					var startHandler = new CodeInstruction(OpCodes.Ldarg_1);
					startHandler.blocks.Add(new ExceptionBlock(ExceptionBlockType.
						BeginCatchBlock, typeof(Exception)));
					yield return startHandler;
					yield return new CodeInstruction(OpCodes.Call, logger);
					// End catch block, quash the exception
					var endCatch = new CodeInstruction(OpCodes.Leave, endMethod);
					endCatch.blocks.Add(new ExceptionBlock(ExceptionBlockType.
						EndExceptionBlock, null));
					yield return endCatch;
					// Actual new ret
					var ret = new CodeInstruction(OpCodes.Ret);
					ret.labels.Add(endMethod);
					yield return ret;
				}
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
				public int eventHash;
				public int handlerIndex;
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

#if true
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
