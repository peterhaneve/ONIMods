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
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

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
#if DEBUG
				DebugLogger.LogDebug("Logging all failed asserts");
#endif
			} catch (Exception e) {
				DebugLogger.BaseLogException(e, null);
			}
		}
		
		public static void OnLoad() {
			PUtil.InitLibrary();
			DebugLogger.InstallExceptionLogger();
			LogAllFailedAsserts();
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
		/// Applied to ModLoader to patch in our handling to LoadDLLs.
		/// </summary>
		[HarmonyPatch]
		public static class Game_OnPrefabInit_Patch {
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
					PUtil.LogException(e);
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
					DebugLogger.LogError("Unable to transpile LoadDLLs: No calls to Assembly.LoadFrom found");
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
	}
}
