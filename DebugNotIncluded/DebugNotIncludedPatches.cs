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
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
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
		/// The KMod which describes this mod.
		/// </summary>
		internal static Mod ThisMod { get; private set; }

		/// <summary>
		/// Applied to ModsScreen to add our buttons and otherwise tweak the dialog.
		/// </summary>
		private static void BuildDisplay(ModsScreen __instance, object ___displayedMods) {
			// Must cast the type because ModsScreen.DisplayedMod is private
			foreach (var displayedMod in (System.Collections.IEnumerable)___displayedMods)
				ModDialogs.ConfigureRowInstance(Traverse.Create(displayedMod));
			__instance?.GetComponent<AllModsHandler>()?.UpdateCheckedState();
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
			PUtil.InitLibrary();
			if (DebugNotIncludedOptions.Instance?.DetailedBacktrace ?? true)
				DebugLogger.InstallExceptionLogger();
			POptions.RegisterOptions(typeof(DebugNotIncludedOptions));
			if (DebugNotIncludedOptions.Instance?.LogAsserts ?? true)
				LogAllFailedAsserts();
			// XXX There is an exception logger in StateMachine.2.cs (GenericInstance.
			// ExecuteActions) but open generic methods supposedly cannot be patched
			foreach (var mod in Global.Instance.modManager?.mods)
				if (mod.label.install_path == path) {
					ThisMod = mod;
					break;
				}
			if (ThisMod == null)
				DebugLogger.LogWarning("Unable to determine KMod instance!");
			// Must postload the mods dialog to come out after aki's mods, ony's mods, PLib
			// options, and so forth
			PUtil.RegisterPostload(PostloadHandler);
		}

		/// <summary>
		/// Runs the required postload patches after all other mods load.
		/// </summary>
		/// <param name="instance">The Harmony instance to execute patches.</param>
		private static void PostloadHandler(HarmonyInstance instance) {
			instance.Patch(typeof(ModsScreen), "BuildDisplay", postfix:
				new HarmonyMethod(typeof(DebugNotIncludedPatches), nameof(BuildDisplay)));
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
		/// Applied to Debug to log which methods are actually sending log messages.
		/// </summary>
		[HarmonyPatch(typeof(Debug), "TimeStamp")]
		public static class DebugUtil_TimeStamp_Patch {
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
				__result = DebugLogger.AddCallingLocation(__result, new System.Diagnostics.
					StackTrace(4));
			}
		}

		/// <summary>
		/// Applied to ModLoader to patch in our handling to LoadDLLs.
		/// </summary>
		[HarmonyPatch]
		public static class DLLLoader_LoadDLLs_Patch {
			/// <summary>
			/// Applied before OnPrefabInit runs.
			/// </summary>
			internal static MethodBase TargetMethod() {
				MethodBase target = null;
#if DEBUG
				DebugLogger.LogDebug("Transpiling LoadDLLs()");
#endif
				try {
					target = typeof(Mod).Assembly.GetType("KMod.DLLLoader", false)?.
						GetMethodSafe("LoadDLLs", true, typeof(string));
					if (target == null)
						DebugLogger.LogError("Unable to transpile LoadDLLs: Method not found");
				} catch (IOException e) {
					// This should theoretically be impossible since the type is loaded
					DebugLogger.BaseLogException(e, null);
				}
				return target;
			}

			/// <summary>
			/// Transpiles LoadDLLs to grab the exception information when a mod fails to load.
			/// </summary>
			private static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var instructions = new List<CodeInstruction>(method);
				// HarmonyInstance.Create and Assembly.LoadFrom will be wrapped
				var harmonyCreate = typeof(HarmonyInstance).GetMethodSafe(nameof(
					HarmonyInstance.Create), true, typeof(string));
				var loadFrom = typeof(Assembly).GetMethodSafe(nameof(Assembly.LoadFrom), true,
					typeof(string));
				bool patchException = false, patchAssembly = false, patchCreate = false;
				// Add call to our handler in exception block, and wrap harmony instances to
				// have more information on each mod
				for (int i = instructions.Count - 1; i > 0; i--) {
					var instr = instructions[i];
					if (instr.opcode == OpCodes.Pop && !patchException) {
						instr.opcode = OpCodes.Call;
						// Call our method instead
						instr.operand = typeof(ModLoadHandler).GetMethodSafe(nameof(
							ModLoadHandler.HandleModException), true, typeof(object));
						patchException = true;
					} else if (instr.opcode == OpCodes.Call) {
						var target = instr.operand as MethodInfo;
						if (target == harmonyCreate && harmonyCreate != null) {
							// Reroute HarmonyInstance.Create
							instr.operand = typeof(ModLoadHandler).GetMethodSafe(nameof(
								ModLoadHandler.CreateHarmonyInstance), true, typeof(string));
							patchCreate = true;
						} else if (target == loadFrom && loadFrom != null) {
							// Reroute Assembly.LoadFrom
							instr.operand = typeof(ModLoadHandler).GetMethodSafe(nameof(
								ModLoadHandler.LoadAssembly), true, typeof(string));
							patchAssembly = true;
						}
					}
				}
				if (!patchException)
					DebugLogger.LogError("Unable to transpile LoadDLLs: Could not find exception handler");
				if (!patchAssembly)
					DebugLogger.LogWarning("Unable to transpile LoadDLLs: No calls to Assembly.LoadFrom found");
				if (!patchCreate)
					DebugLogger.LogWarning("Unable to transpile LoadDLLs: No calls to HarmonyInstance.Create found");
				return instructions;
			}
		}

		/// <summary>
		/// Applied to KMonoBehaviour to modify InitializeComponent for better logging.
		/// </summary>
		[HarmonyPatch(typeof(KMonoBehaviour), "InitializeComponent")]
		public static class KMonoBehaviour_InitializeComponent_Patch {
			internal static bool Prepare() {
				return DebugNotIncludedOptions.Instance?.DetailedBacktrace ?? false;
			}

			/// <summary>
			/// Transpiles InitializeComponent to add more error logging.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
#if DEBUG
				DebugLogger.LogDebug("Transpiling InitializeComponent()");
#endif
				return TranspileSpawn(method);
			}
		}

		/// <summary>
		/// Applied to KMonoBehaviour to modify Spawn for better logging.
		/// </summary>
		[HarmonyPatch(typeof(KMonoBehaviour), "Spawn")]
		public static class KMonoBehaviour_Spawn_Patch {
			internal static bool Prepare() {
				return DebugNotIncludedOptions.Instance?.DetailedBacktrace ?? false;
			}

			/// <summary>
			/// Transpiles Spawn to add more error logging.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
#if DEBUG
				DebugLogger.LogDebug("Transpiling Spawn()");
#endif
				return TranspileSpawn(method);
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
					ModDialogs.CheckFirstMod(__instance?.gameObject);
			}
		}

		/// <summary>
		/// Applied to MainMenu to display a queued Steam mod status report if pending.
		/// </summary>
		[HarmonyPatch(typeof(MainMenu), "Update")]
		public static class MainMenu_Update_Patch {
			/// <summary>
			/// Applied after Update runs.
			/// </summary>
			internal static void Postfix(MainMenu __instance) {
				if (__instance != null)
					QueuedReportManager.Instance.CheckQueuedReport(__instance.gameObject);
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
		/// Applied to Mod to avoid disabling this mod on crash.
		/// </summary>
		[HarmonyPatch(typeof(Mod), "Crash")]
		public static class Mod_Crash_Patch {
			/// <summary>
			/// Applied before Crash runs.
			/// </summary>
			internal static bool Prefix(Mod __instance) {
				return ThisMod == null || !__instance.label.Match(ThisMod.label);
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
				if (__instance != null)
					ModLoadHandler.CurrentMod = ModDebugRegistry.Instance.GetDebugInfo(
						__instance);
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
			/// <param name="___entryPrefab"></param>
			internal static void Prefix(GameObject ___entryPrefab) {
				if (___entryPrefab != null)
					ModDialogs.ConfigureRowPrefab(___entryPrefab);
			}

			/// <summary>
			/// Applied after OnActivate runs.
			/// </summary>
			internal static void Postfix(KButton ___workshopButton, ModsScreen __instance) {
				if (___workshopButton != null && __instance != null) {
					// Hide the "STEAM WORKSHOP" button
					var obj = ___workshopButton.gameObject;
					obj.SetActive(false);
					// Drop a checkbox "All" there instead
					var parent = obj.GetParent();
					obj = __instance.gameObject;
					if (parent != null && obj != null)
						ModDialogs.AddExtraButtons(obj, parent);
				}
			}
		}

		/// <summary>
		/// Applied to Steam to avoid dialog spam on startup if many mods are updated or
		/// installed.
		/// </summary>
		[HarmonyPatch(typeof(Steam), "UpdateMods")]
		public static class Steam_UpdateMods_Patch {
			/// <summary>
			/// Transpiles UpdateMods to postpone the report.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
#if DEBUG
				DebugLogger.LogDebug("Transpiling Steam.UpdateMods()");
#endif
				var report = typeof(Manager).GetMethodSafe(nameof(Manager.Report), false,
					typeof(GameObject));
				var sanitize = typeof(Manager).GetMethodSafe(nameof(Manager.Sanitize), false,
					typeof(GameObject));
				var replacement = typeof(QueuedReportManager).GetMethodSafe(nameof(
					QueuedReportManager.QueueDelayedReport), true, typeof(Manager),
					typeof(GameObject));
				foreach (var instruction in method) {
					var callee = instruction.operand as MethodInfo;
					if (instruction.opcode == OpCodes.Callvirt && (callee == report ||
							callee == sanitize) && replacement != null) {
						instruction.opcode = OpCodes.Call;
						instruction.operand = replacement;
					}
					yield return instruction;
				}
			}
		}
	}
}
