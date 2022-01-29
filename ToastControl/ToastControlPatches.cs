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
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using Delivery = FetchAreaChore.StatesInstance.Delivery;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.ToastControl {
	/// <summary>
	/// Patches which will be applied via annotations for Popup Control.
	/// </summary>
	public sealed class ToastControlPatches : KMod.UserMod2 {
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
			"Storage:DropSome",
			"Storage:Store",
			"SuperProductive+<>c:<InitializeStates>b__3_0",
			"Toilet:Flush",
			"ToiletWorkableUse:OnCompleteWork",
			"UtilityBuildTool:ApplyPathToConduitSystem"
		};

		/// <summary>
		/// Methods to patch for the long form of PopFXManager.SpawnFX.
		/// </summary>
		private static ICollection<string> TargetsShort => new List<string>() {
			"BuildingHP:DoDamagePopFX",
			"Constructable:OnCompleteWork",
			"CreatureCalorieMonitor+Stomach:Poop",
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
			"NuclearResearchCenterWorkable:OnWorkTick",
			"PeeChore+States+<>c:<InitializeStates>b__2_6",
			"ReorderableBuilding:ConvertModule",
			"ResearchCenter:ConvertMassToResearchPoints",
			"ResearchPointObject:OnSpawn",
			"RotPile:ConvertToElement",
			"Rottable+<>c:<InitializeStates>b__10_6",
			"SandboxClearFloorTool:OnPaintCell",
			"SeedProducer:ProduceSeed",
			"SetLocker:DropContents",
			"SolidConsumerMonitor+Instance:OnEatSolidComplete",
			"VomitChore+States+<>c:<InitializeStates>b__7_4",
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
					var type = PPatchTools.GetTypeSafe(types[0]);
					for (int i = 1; i < n && type != null; i++)
						type = type.GetNestedType(types[i], PPatchTools.BASE_FLAGS |
							BindingFlags.Instance | BindingFlags.Static);
					// Access constructor or method
					if (type == null)
						PUtil.LogWarning("Unable to find type: " + mSpec);
					else
						result = LookupMethod(type, mSpec.Substring(index + 1));
					if (result != null)
						methods.Add(result);
				}
			}
			return methods;
		}

		/// <summary>
		/// Transpiler code shared between the pickup and delivery transpilers.
		/// </summary>
		/// <param name="method">The method code to transpile.</param>
		/// <param name="newInstructions">The new code instructions to insert - 
		/// should return a bool to determine if Store shows popups.</param>
		/// <returns>The transpiled method code.</returns>
		private static TranspiledMethod ExecuteChoreTranspiler(TranspiledMethod method,
				IList<CodeInstruction> newInstructions) {
			var instructions = new List<CodeInstruction>(method);
			int n = instructions.Count, toAdd = newInstructions.Count, previousCall = 0;
			bool patched = false;
			// Streaming this transpiler will be difficult since instructions before the call
			// to Store need to be changed
			var find = typeof(Storage).GetMethodSafe(nameof(Storage.Store), false,
				PPatchTools.AnyArguments);
			for (int i = 0; i < n; i++) {
				var instr = instructions[i];
				if (instr.opcode == OpCodes.Callvirt) {
					// ReplaceMethodCall will not work here since we need information not
					// available in the call to Store
					if (find != null && (instr.operand as MethodInfo) == find) {
						patched = SwapArgument(instructions, previousCall, i, newInstructions);
						break;
					}
					previousCall = i;
				}
			}
			if (!patched)
				PUtil.LogWarning("No calls to Storage.Store found!");
			return instructions;
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

		/// <summary>
		/// Looks up the specified method.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <param name="method">The method name to look up.</param>
		/// <returns>The method, or null if it was not found or ambiguous.</returns>
		private static MethodBase LookupMethod(Type type, string method) {
			MethodBase result = null;
			try {
				if (method == ".ctor") {
					// No way to specify which one
					var cons = type.GetConstructors(PPatchTools.BASE_FLAGS | BindingFlags.
						Instance);
					if (cons == null || cons.Length != 1)
						PUtil.LogWarning("No single constructor found for " + type);
					else
						result = cons[0];
				} else {
					result = type.GetMethod(method, PPatchTools.BASE_FLAGS |
						BindingFlags.Instance | BindingFlags.Static);
					if (result == null)
						PUtil.LogWarning("No match found for {0}.{1}".F(type, method));
				}
			} catch (AmbiguousMatchException e) {
				PUtil.LogWarning("Ambiguous match for {0}.{1}:".F(type, method));
				PUtil.LogExcWarn(e);
			}
			return result;
		}

		/// <summary>
		/// Determines if popups should be hidden from pick ups.
		/// </summary>
		/// <param name="worker">The worker who completed the chore.</param>
		private static bool ShouldHidePickupPopups(Worker worker) {
			var opts = ToastControlPopups.Options;
			bool pickupDupe = opts.PickedUp;
#pragma warning disable IDE0031 // Use null propagation
			var brain = (worker == null) ? null : worker.GetComponent<MinionBrain>();
#pragma warning restore IDE0031
			return (pickupDupe != opts.PickedUpMachine) ? (pickupDupe != (brain != null)) :
				!pickupDupe;
		}

		/// <summary>
		/// Common transpiled target method for each use of PopFXManager.SpawnFX.
		/// </summary>
		private static PopFX SpawnFXLong(PopFXManager instance, Sprite icon, string text,
				Transform targetTransform, Vector3 offset, float lifetime, bool track_target,
				bool force_spawn, object source) {
			PopFX popup = null;
			bool show = true;
			try {
				// Parameter count cannot be reduced - in order to conform with Klei method
				show = ToastControlPopups.ShowPopup(source, text);
			} catch (Exception e) {
				// Sometimes this gets executed on a background thread and unhandled exceptions
				// cause a CTD
				PUtil.LogException(e);
			}
			if (show)
				popup = instance.SpawnFX(icon, text, targetTransform, offset, lifetime,
					track_target, force_spawn);
			return popup;
		}

		/// <summary>
		/// Common transpiled target method for each use of PopFXManager.SpawnFX.
		/// </summary>
		private static PopFX SpawnFXShort(PopFXManager instance, Sprite icon, string text,
				Transform targetTransform, float lifetime, bool track_target, object source) {
			PopFX popup = null;
			bool show = true;
			try {
				// Parameter count cannot be reduced - in order to conform with Klei method
				show = ToastControlPopups.ShowPopup(source, text);
			} catch (Exception e) {
				// Sometimes this gets executed on a background thread and unhandled exceptions
				// cause a CTD
				PUtil.LogException(e);
			}
			if (show)
				popup = instance.SpawnFX(icon, text, targetTransform, lifetime, track_target);
			return popup;
		}

		/// <summary>
		/// A special case for ThreatMonitor+Grudge.Calm due to the source being a struct.
		/// </summary>
		private static PopFX SpawnFXThreat(PopFXManager instance, Sprite icon, string text,
				Transform targetTransform, float lifetime, bool track_target) {
			PopFX popup = null;
			if (ToastControlPopups.Options.Forgiveness)
				popup = instance.SpawnFX(icon, text, targetTransform, lifetime, track_target);
			return popup;
		}

		/// <summary>
		/// Swaps the first "false" (0) constant load in the specified range with the
		/// specified instructions.
		/// </summary>
		/// <param name="instructions">The instructions to modify.</param>
		/// <param name="start">The starting index to search (exclusive).</param>
		/// <param name="end">The ending index to search (exclusive).</param>
		/// <param name="newInstructions">The instructions to use as replacements.</param>
		/// <returns>true if instructions were replaced, or false otherwise.</returns>
		private static bool SwapArgument(List<CodeInstruction> instructions, int start,
				int end, ICollection<CodeInstruction> newInstructions) {
			bool patched = false;
			for (int j = start + 1; j < end && !patched; j++) {
				var instr = instructions[j];
				// "false"
				if (instr.opcode == OpCodes.Ldc_I4_0) {
#if DEBUG
					PUtil.LogDebug("Replacing " + instr + " with:\n" + newInstructions.
						Join("\n"));
#endif
					instructions.RemoveAt(j);
					instructions.InsertRange(j, newInstructions);
					// Copy the labels and blocks to the new instructions
					instructions[j].labels = instr.labels;
					instructions[j].blocks = instr.blocks;
					patched = true;
				}
			}
			return patched;
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PLocalization().Register();
			LocString.CreateLocStringKeys(typeof(ToastControlStrings.UI));
			new POptions().RegisterOptions(this, typeof(ToastControlOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(ToastControlPopups));
			ToastControlPopups.ReloadOptions();
			// No default key bind
			inGameSettings = new PActionManager().CreateAction(ToastControlStrings.ACTION_KEY,
				ToastControlStrings.ACTION_TITLE);
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to FetchAreaChore.StatesInstance.Delivery.Complete to determine whether
		/// store popups are shown.
		/// </summary>
		[HarmonyPatch(typeof(Delivery), nameof(Delivery.Complete))]
		public static class Delivery_Complete_Patch {
			/// <summary>
			/// Transpiles Complete to alter the "display popup" flag on Storage.Store
			/// depending on the options settings.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method) {
				var getChore = typeof(Delivery).GetPropertySafe<FetchChore>(nameof(Delivery.
					chore), false)?.GetGetMethod(true);
				var target = typeof(Delivery_Complete_Patch).GetMethodSafe(
					nameof(ShouldHidePopups), true, typeof(FetchChore));
				var result = method;
				if (getChore == null)
					// Unable to retrieve the chore, avoid crashing and just do not transpile
					PUtil.LogWarning("Unable to retrieve Delivery.chore property");
				else
					result = ExecuteChoreTranspiler(method, new List<CodeInstruction>(3) {
						new CodeInstruction(OpCodes.Ldarg_0),
						// Would have to call `Ldobj` on the struct to pass it directly to the
						// method, so might as well just grab the chore here
						new CodeInstruction(OpCodes.Call, getChore),
						new CodeInstruction(OpCodes.Call, target)
					});
				return result;
			}

			/// <summary>
			/// Determines if popups should be hidden from deliveries.
			/// </summary>
			/// <param name="chore">The chore that delivered the item.</param>
			private static bool ShouldHidePopups(FetchChore chore) {
				var opts = ToastControlPopups.Options;
				bool deliverDupe = opts.Delivered;
				return (deliverDupe != opts.DeliveredMachine) ? (deliverDupe != (chore.fetcher.
					GetComponent<MinionBrain>() != null)) : !deliverDupe;
			}
		}

		/// <summary>
		/// Applied to LiquidPumpingStation to determine whether pickup popups are shown.
		/// </summary>
		[HarmonyPatch(typeof(LiquidPumpingStation), "OnCompleteWork")]
		public static class LiquidPumpingStation_OnCompleteWork_Patch {
			/// <summary>
			/// Transpiles OnCompleteWork to alter the "display popup" flag on Storage.Store
			/// depending on the options settings.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method) =>
				ExecuteChoreTranspiler(method, new List<CodeInstruction>(2) {
					// Loads the first real Worker argument (arg 0 is this)
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Call, typeof(ToastControlPatches).
						GetMethodSafe(nameof(ShouldHidePickupPopups), true, typeof(Worker)))
				});
		}

		/// <summary>
		/// Applied to Pickupable.OnCompleteWork to determine whether pick up popups are shown.
		/// </summary>
		[HarmonyPatch(typeof(Pickupable), "OnCompleteWork")]
		public static class Pickupable_OnCompleteWork_Patch {
			/// <summary>
			/// Transpiles OnCompleteWork to alter the "display popup" flag on Storage.Store
			/// depending on the options settings.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method) =>
				ExecuteChoreTranspiler(method, new List<CodeInstruction>(2) {
					// Loads the first real Worker argument (arg 0 is this)
					new CodeInstruction(OpCodes.Ldarg_1),
					new CodeInstruction(OpCodes.Call, typeof(ToastControlPatches).
						GetMethodSafe(nameof(ShouldHidePickupPopups), true, typeof(Worker)))
				});
		}

		/// <summary>
		/// Applied to PopFX to disable the popups moving upward if necessary.
		/// </summary>
		[HarmonyPatch]
		public static class PopFX_Spawn_Patch {
			internal static MethodBase TargetMethod() {
				var options = typeof(PopFX).GetMethods(PPatchTools.BASE_FLAGS | BindingFlags.
					Instance | BindingFlags.DeclaredOnly);
				MethodBase target = null;
				// Look for a match that is not KMonoBehaviour.Spawn()
				foreach (var method in options)
					if (method.Name == nameof(PopFX.Spawn) && method.GetParameters().
							Length > 0) {
						target = method;
						break;
					}
				return target;
			}

			/// <summary>
			/// Applied after Spawn runs.
			/// </summary>
			internal static void Postfix(ref float ___Speed) {
				if (ToastControlPopups.Options.DisableMoving)
					___Speed = 0.0f;
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
			/// Transpiles SpawnFX to replace calls to SpawnFX with our handler.
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
			/// Transpiles SpawnFX to replace calls to SpawnFX with our handler.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method,
					MethodBase original) {
				return ExecuteTranspiler(method, SPAWN_FX_SHORT, typeof(ToastControlPatches).
					GetMethodSafe(nameof(SpawnFXShort), true, PPatchTools.AnyArguments),
					original);
			}
		}

		/// <summary>
		/// Applied to ThreatMonitor.Grudge as it uses a struct and therefore fails when used
		/// with the normal multi-patcher.
		/// </summary>
		[HarmonyPatch(typeof(ThreatMonitor.Grudge), nameof(ThreatMonitor.Grudge.Calm))]
		public static class ThreatMonitor_Grudge_Calm_Patch {
			/// <summary>
			/// Transpiles Calm to replace calls to SpawnFX with our handler.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod method,
					MethodBase original) {
				bool replaced = false;
				foreach (var instr in method) {
					if (instr.opcode == OpCodes.Callvirt && SPAWN_FX_SHORT != null && (instr.
							operand as MethodInfo) == SPAWN_FX_SHORT) {
						// Call our method instead
						instr.operand = typeof(ToastControlPatches).GetMethodSafe(nameof(
							SpawnFXThreat), true, PPatchTools.AnyArguments);
						replaced = true;
					}
					yield return instr;
				}
				if (!replaced)
					PUtil.LogWarning("No calls to SpawnFX found: ThreatMonitor+Grudge.Calm");
			}
		}

		/// <summary>
		/// Applied to ToolMenu to capture key binds to open the settings.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), nameof(ToolMenu.OnKeyDown))]
		public static class ToolMenu_OnKeyDown_Patch {
			/// <summary>
			/// Applied after OnKeyDown runs.
			/// </summary>
			internal static void Postfix(KButtonEvent e) {
				if (inGameSettings != null && !e.Consumed && e.TryConsume(inGameSettings.
						GetKAction()))
					POptions.ShowDialog(typeof(ToastControlOptions), onClose: (_) =>
						ToastControlPopups.ReloadOptions());
			}
		}
	}
}
