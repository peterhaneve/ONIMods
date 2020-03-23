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
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<Harmony.CodeInstruction>;

namespace PeterHan.ToastControl {
	/// <summary>
	/// Patches which will be applied via annotations for Popup Control.
	/// </summary>
	public static class ToastControlPatches {
		/// <summary>
		/// The action triggered when the user wants to change settings in game.
		/// </summary>
		private static PAction inGameSettings;

		/// <summary>
		/// The method used to resolve typeof() calls from handles.
		/// </summary>
		private static readonly MethodInfo RESOLVE_TYPE = typeof(Type).GetMethodSafe(nameof(
			Type.GetTypeFromHandle), true, typeof(RuntimeTypeHandle));

		/// <summary>
		/// The long form method of PopFXManager.SpawnFX to replace.
		/// </summary>
		private static readonly MethodInfo SPAWN_FX_LONG = typeof(PopFXManager).GetMethodSafe(
			nameof(PopFXManager.SpawnFX), false, typeof(Sprite), typeof(string),
			typeof(Transform), typeof(Vector3), typeof(float), typeof(bool), typeof(bool));

		/// <summary>
		/// The short form method of PopFXManager.SpawnFX to replace.
		/// </summary>
		private static readonly MethodInfo SPAWN_FX_SHORT = typeof(PopFXManager).GetMethodSafe(
			nameof(PopFXManager.SpawnFX), false, typeof(Sprite), typeof(string),
			typeof(Transform), typeof(float), typeof(bool));

		/// <summary>
		/// Methods to patch for the long form of PopFXManager.SpawnFX.
		/// </summary>
		private static ICollection<string> TargetsLong => new List<string>() {
			"BaseUtilityBuildTool:BuildPath", // 2 hits
			"BuildTool:TryBuild",
			"CaptureTool:MarkForCapture",
			"CopyBuildingSettings:ApplyCopy",
			"DebugHandler:SpawnMinion",
			"DebugHandler:OnKeyDown",
			"FlushToilet:Flush",
			"Klei.AI.AttributeLevel:LevelUp",
			"MinionResume:OnSkillPointGained",
			"MopTool:OnDragTool",
			"SandboxSampleTool:OnLeftClickDown",
			"SuperProductive+<>c:<InitializeStates>b__3_0",
			"Toilet:Flush",
			"UtilityBuildTool:ApplyPathToConduitSystem"
		};

		/// <summary>
		/// Methods to patch for the long form of PopFXManager.SpawnFX.
		/// </summary>
		private static ICollection<string> TargetsShort => new List<string>() {
			"BuildingHP:DoDamagePopFX",
			"Constructable:OnCompleteWork",
			"CreatureCalorieMonitor+Stomach:Poop",
			"ElementDropper:OnStorageChanged",
			"ElementDropperMonitor+Instance:DropElement",
			"ElementEmitter:ForceEmit",
			"FleeStates+<>c:<InitializeStates>b__8_2",
			"HarvestDesignatable:<OnRefreshUserMenu>b__34_0",
			"HarvestDesignatable:<OnRefreshUserMenu>b__34_1",
			"Klei.AI.EffectInstance:.ctor",
			"Klei.AI.SicknessInstance+StatesInstance:Infect",
			"Klei.AI.SicknessInstance+StatesInstance:Cure",
			"Klei.AI.SlimeSickness+SlimeLungComponent+StatesInstance:ProduceSlime",
			"Moppable:Sim1000ms",
			"ResearchCenter:ConvertMassToResearchPoints",
			"ResearchPointObject:OnSpawn",
			"RotPile:ConvertToElement",
			"Rottable+<>c:<InitializeStates>b__10_6",
			"SandboxClearFloorTool:OnPaintCell",
			"SeedProducer:ProduceSeed",
			"SetLocker:DropContents",
			"SolidConsumerMonitor+Instance:OnEatSolidComplete",
			"Storage:Store",
			"ThreatMonitor+Grudge:Calm",
			"WorldDamage:OnDigComplete"
		};

		/// <summary>
		/// Enumerates a list of methods and resolves their types. This method handles
		/// constructors as well.
		/// 
		/// This method is very slow. Only execute when necessary.
		/// </summary>
		/// <param name="specs">The methods to look up.</param>
		/// <returns>The resolved methods or constructors.</returns>
		private static IEnumerable<MethodBase> CreateMethodList(ICollection<string> specs) {
			var methods = new List<MethodBase>(specs.Count);
			foreach (string mSpec in specs) {
				int index = mSpec.IndexOf(':');
				if (index > 0) {
					string[] types = mSpec.Substring(0, index).Split('+');
					int n = types.Length;
					MethodBase result = null;
					// Resolve the type, descending by '+' if needed
					var type = AccessTools.TypeByName(types[0]);
					for (int i = 1; i < n && type != null; i++)
						type = type.GetNestedType(types[i], BindingFlags.NonPublic |
							BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
					// Access constructor or method
					if (type != null) {
						string method = mSpec.Substring(index + 1);
						if (method == ".ctor")
							// No way to specify which one
							result = AccessTools.GetDeclaredConstructors(type)[0];
						else
							result = AccessTools.Method(type, method);
					}
					if (result == null)
						PUtil.LogWarning("Unable to find method: " + mSpec);
					else
						methods.Add(result);
				}
			}
			return methods;
		}

		/// <summary>
		/// Transpiler code shared between the long and short form PopFXManager.SpawnFX
		/// handler methods.
		/// </summary>
		/// <param name="method">The method code to transpile.</param>
		/// <param name="find">The method to find.</param>
		/// <param name="replace">The replacement method, with a Component added as the last
		/// argument to denote the source.</param>
		/// <param name="original">The method being patched.</param>
		/// <returns>The transpiled method code.</returns>
		private static TranspiledMethod ExecuteTranspiler(TranspiledMethod method,
				MethodBase find, MethodBase replace, MethodBase original) {
			bool replaced = false;
			if (replace == null)
				throw new ArgumentNullException("replace");
			foreach (var instr in method) {
				if (instr.opcode == OpCodes.Callvirt && find != null && (instr.operand as
						MethodInfo) == find) {
					if (original.IsStatic) {
						// Equivalent to typeof with original.DeclaringType
						yield return new CodeInstruction(OpCodes.Ldtoken, original.
							DeclaringType);
						yield return new CodeInstruction(OpCodes.Call, RESOLVE_TYPE);
					} else
						// First push "this" onto the stack as last argument
						yield return new CodeInstruction(OpCodes.Ldarg_0);
					// Call our method instead
					instr.operand = replace;
					replaced = true;
				}
				yield return instr;
			}
			if (!replaced)
				PUtil.LogWarning("No calls to SpawnFX found: {0}.{1}".F(original.DeclaringType.
					FullName, original.Name));
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			LocString.CreateLocStringKeys(typeof(ToastControlStrings.UI));
			PLocalization.Register();
			POptions.RegisterOptions(typeof(ToastControlOptions));
			ToastControlPopups.ReloadOptions();
			// No default key bind
			inGameSettings = PAction.Register(ToastControlStrings.ACTION_KEY,
				ToastControlStrings.ACTION_TITLE);
		}

		/// <summary>
		/// Common transpiled target method for each use of PopFXManager.SpawnFX.
		/// </summary>
		private static PopFX SpawnFXLong(PopFXManager instance, Sprite icon, string text,
				Transform targetTransform, Vector3 offset, float lifetime, bool track_target,
				bool force_spawn, object source) {
			PopFX popup = null;
			// Parameter count cannot be reduced - in order to conform with Klei method
			if (ToastControlPopups.ShowPopup(source, text))
				popup = instance.SpawnFX(icon, text, targetTransform, offset, lifetime,
					track_target, force_spawn);
			return popup;
		}

		/// <summary>
		/// Common transpiled target method for each use of PopFXManager.SpawnFX.
		/// </summary>
		private static PopFX SpawnFXShort(PopFXManager instance, Sprite icon, string text,
			Transform targetTransform, float lifetime, bool track_target, object source)
		{
			PopFX popup = null;
			// Parameter count cannot be reduced - in order to conform with Klei method
			if (ToastControlPopups.ShowPopup(source, text))
				popup = instance.SpawnFX(icon, text, targetTransform, lifetime, track_target);
			return popup;
		}

		/// <summary>
		/// Applied to Game to load settings when the user starts a game.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnSpawn")]
		public static class Game_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix() {
				ToastControlPopups.ReloadOptions();
			}
		}

		/// <summary>
		/// Applied to each base game location that uses the long form overload of
		/// PopFXManager.SpawnFX.
		/// </summary>
		[HarmonyPatch]
		public static class PopFXManager_SpawnFXLong_Patch {
			/// <summary>
			/// Determines which methods need to be patched
			/// </summary>
			/// <returns>A list of everything to patch for the long form.</returns>
			internal static IEnumerable<MethodBase> TargetMethods() {
				return CreateMethodList(TargetsLong);
			}

			/// <summary>
			/// Applied before SpawnFX runs.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method,
					MethodBase original) {
				return ExecuteTranspiler(method, SPAWN_FX_LONG, typeof(ToastControlPatches).
					GetMethodSafe(nameof(SpawnFXLong), true, PPatchTools.AnyArguments),
					original);
			}
		}

		/// <summary>
		/// Applied to each base game location that uses the short form overload of
		/// PopFXManager.SpawnFX.
		/// </summary>
		[HarmonyPatch]
		public static class PopFXManager_SpawnFXShort_Patch {
			/// <summary>
			/// Determines which methods need to be patched
			/// </summary>
			/// <returns>A list of everything to patch for the long form.</returns>
			internal static IEnumerable<MethodBase> TargetMethods() {
				return CreateMethodList(TargetsShort);
			}

			/// <summary>
			/// Applied before SpawnFX runs.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method,
					MethodBase original) {
				return ExecuteTranspiler(method, SPAWN_FX_SHORT, typeof(ToastControlPatches).
					GetMethodSafe(nameof(SpawnFXShort), true, PPatchTools.AnyArguments),
					original);
			}
		}

		/// <summary>
		/// Applied to ToolMenu to capture key binds to open the settings.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "OnKeyDown")]
		public static class ToolMenu_OnKeyDown_Patch {
			/// <summary>
			/// Applied after OnKeyDown runs.
			/// </summary>
			internal static void Postfix(KButtonEvent e) {
				if (inGameSettings != null && !e.Consumed && e.TryConsume(inGameSettings.
						GetKAction()))
					POptions.ShowNow(typeof(ToastControlOptions), onClose: (_) =>
						ToastControlPopups.ReloadOptions());
			}
		}
	}
}
