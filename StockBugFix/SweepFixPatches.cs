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
using PeterHan.PLib.Core;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Fixes a variety of bugs with Sweep chores.
	/// </summary>
	internal static class SweepFixPatches {
		private static readonly BindingFlags METHOD_FLAGS = BindingFlags.DeclaredOnly |
			BindingFlags.Instance | PPatchTools.BASE_FLAGS;

		/// <summary>
		/// The interrupt priority for Top Priority chores.
		/// </summary>
		private static int TOP_PRIORITY_IRQ;

		/// <summary>
		/// The SortedClearable class required for fixing the comparator.
		/// </summary>
		private static readonly Type SORTED_CLEARABLE = PPatchTools.GetTypeSafe(
			"ClearableManager")?.GetNestedType("SortedClearable", METHOD_FLAGS);

		/// <summary>
		/// Adds a sweep chore to the chore list, but fixes the interrupt priority to
		/// TopPriority if necessary.
		/// </summary>
		/// <param name="chores">The chore list where the chore will be placed.</param>
		/// <param name="toAdd">The sweep chore to add.</param>
		private static void AddAndFixPriority(List<Chore.Precondition.Context> chores,
				Chore.Precondition.Context toAdd) {
			if (toAdd.masterPriority.priority_class == PriorityScreen.PriorityClass.
					topPriority && TOP_PRIORITY_IRQ > 0)
				toAdd.interruptPriority = TOP_PRIORITY_IRQ;
			chores.Add(toAdd);
		}

		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			TOP_PRIORITY_IRQ = Db.Get().ChoreTypes.TopPriority.interruptPriority;
		}

		/// <summary>
		/// Transpiles the SortedClearable sorting method to properly use the priority class.
		/// </summary>
		private static void TranspileClearComparer(List<CodeInstruction> method,
				ILGenerator generator) {
			var mp = SORTED_CLEARABLE.GetFieldSafe("masterPriority", false);
			var pc = typeof(PrioritySetting).GetFieldSafe(nameof(PrioritySetting.
				priority_class), false);
			if (mp == null || pc == null)
				PUtil.LogWarning("Transpiler not run - priority field not found");
			else {
				int n = method.Count;
				// Find last "RET", or use last instruction if none found
				var lastRet = method[n - 1];
				for (int i = n - 1; i > 0; i--)
					if (method[i].opcode == OpCodes.Ret) {
						lastRet = method[i];
						break;
					}
				// Label that instruction
				var end = generator.DefineLabel();
				var endLabels = lastRet.labels;
				if (endLabels == null)
					lastRet.labels = endLabels = new List<Label>();
				endLabels.Add(end);
				method.InsertRange(0, new List<CodeInstruction>() {
					// Load: "b"
					new CodeInstruction(OpCodes.Ldarg_2),
					// Get field: "masterPriority"
					new CodeInstruction(OpCodes.Ldfld, mp),
					// Get field: "priority_class"
					new CodeInstruction(OpCodes.Ldfld, pc),
					// Load: "a"
					new CodeInstruction(OpCodes.Ldarg_1),
					// Get field: "masterPriority"
					new CodeInstruction(OpCodes.Ldfld, mp),
					// Get field: "priority_class"
					new CodeInstruction(OpCodes.Ldfld, pc),
					// Subtract: b - a (loading enum fields loads as their backing type)
					new CodeInstruction(OpCodes.Sub),
					// If nonzero
					new CodeInstruction(OpCodes.Dup),
					// Branch to end of method
					new CodeInstruction(OpCodes.Brtrue, end),
					// Otherwise, wipe it
					new CodeInstruction(OpCodes.Pop)
				});
			}
		}

		/// <summary>
		/// Applied to ClearableManager to fix sweep chores on yellow alert being ignored.
		/// </summary>
		[HarmonyPatch]
		public static class ClearableManager_CollectChores_Patch {
			/// <summary>
			/// Finds the private class and method to patch.
			/// </summary>
			internal static MethodBase TargetMethod() {
				return PPatchTools.GetTypeSafe("ClearableManager")?.GetMethodSafe(
					nameof(ChoreProvider.CollectChores), false, PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Transpiles CollectChores to interdict the interrupt priority and set it to
			/// Top Priority's interrupt if the chore itself is yellow alert.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				Type argType = typeof(Chore.Precondition.Context), listType = typeof(
					List<Chore.Precondition.Context>);
				var srcMethod = listType.GetMethodSafe(nameof(List<Chore.Precondition.Context>.
					Add), false, argType);
				if (srcMethod == null)
					throw new InvalidOperationException("Where is List.Add???");
				return PPatchTools.ReplaceMethodCall(method, srcMethod,
					typeof(SweepFixPatches).GetMethodSafe(nameof(AddAndFixPriority), true,
					listType, argType));
			}
		}

		/// <summary>
		/// Applied to ClearableManager to sort swept items with Top Priority ahead of other
		/// swept items.
		/// </summary>
		[HarmonyPatch]
		public static class ClearableManager_Compare_Patch {
			/// <summary>
			/// Finds the private class and method to patch.
			/// </summary>
			internal static MethodBase TargetMethod() {
				if (SORTED_CLEARABLE == null)
					throw new InvalidOperationException("SortedClearable type not found");
				return SORTED_CLEARABLE.GetNestedType("Comparer", METHOD_FLAGS)?.GetMethodSafe(
					nameof(IComparer<int>.Compare), false, SORTED_CLEARABLE, SORTED_CLEARABLE);
			}

			/// <summary>
			/// Transpiles Compare to sort chores with higher priority classes to the top.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator,
					IEnumerable<CodeInstruction> method) {
				var allInstr = new List<CodeInstruction>(method);
				TranspileClearComparer(allInstr, generator);
				return allInstr;
			}
		}

		/// <summary>
		/// Applied to FetchManager.FetchablesByPrefabId to avoid trashing the priority class
		/// when calculating fetchable priority.
		/// </summary>
		[HarmonyPatch]
		public static class FetchManager_FetchablesByPrefabId_Patch {
			/// <summary>
			/// Finds multiple methods to patch.
			/// </summary>
			internal static IEnumerable<MethodBase> TargetMethods() {
				var targetType = typeof(FetchManager.FetchablesByPrefabId);
				yield return targetType.GetMethodSafe(nameof(FetchManager.FetchablesByPrefabId.
					AddPickupable), false, typeof(Pickupable));
				yield return targetType.GetMethodSafe(nameof(FetchManager.FetchablesByPrefabId.
					UpdateStorage), false, typeof(HandleVector<int>.Handle), typeof(Storage));
			}

			/// <summary>
			/// Classifies yellow alert sweeps as 11 effective priority ahead of all others
			/// in the 1-9 range.
			/// </summary>
			/// <param name="setting">The current item priority setting.</param>
			/// <returns>The priority level to use for sorting.</returns>
			private static int GetComputedPriority(PrioritySetting setting) {
				int priority;
				if (setting.priority_class == PriorityScreen.PriorityClass.topPriority)
					priority = 11;
				else
					priority = setting.priority_value;
				return priority;
			}

			/// <summary>
			/// Transpiles these methods to replace the field access to PrioritySetting.
			/// priority_value with a composite lookup.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method, MethodBase __originalMethod) {
				var target = typeof(PrioritySetting).GetFieldSafe(nameof(PrioritySetting.
					priority_value), false);
				var replacement = typeof(FetchManager_FetchablesByPrefabId_Patch).
					GetMethodSafe(nameof(GetComputedPriority), true, typeof(PrioritySetting));
				if (target != null && replacement != null)
					foreach (var instr in method) {
						if (instr.opcode == OpCodes.Ldfld && instr.operand is FieldInfo fi &&
								fi == target) {
							instr.opcode = OpCodes.Call;
							instr.operand = replacement;
#if DEBUG
							PUtil.LogDebug("Patched " + __originalMethod.Name);
#endif
						}
						yield return instr;
					}
				else {
					PUtil.LogWarning("Unable to patch " + __originalMethod.Name);
					foreach (var instr in method)
						yield return instr;
				}
			}
		}
	}
}
