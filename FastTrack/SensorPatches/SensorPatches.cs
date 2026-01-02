/*
 * Copyright 2025 Peter Han
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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// Applied to PickupableSensor to shut it off unconditionally if pickup opts have been
	/// moved to a background task.
	/// </summary>
	[HarmonyPatch(typeof(PickupableSensor), nameof(PickupableSensor.Update))]
	public static class PickupableSensor_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Patch the Occupy family of methods in MinionGroupProber to trigger a reachability
	/// update if the cell is freshly occupied.
	/// </summary>
	[HarmonyPatch]
	public static class MinionGroupProber_Occupy_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		internal static IEnumerable<MethodBase> TargetMethods() {
			const string occupy = nameof(MinionGroupProber.Occupy);
			yield return typeof(MinionGroupProber).GetMethodSafe(occupy, false,
				typeof(List<int>));
			yield return typeof(MinionGroupProber).GetMethodSafe(occupy, false, typeof(int));
		}

		/// <summary>
		/// Enqueues a dirty path grid cell, only if Interlocked.Increment returned 1 (which
		/// means it was previously zero).
		/// </summary>
		/// <param name="value">The incremented value.</param>
		private static void EnqueueIfOne(int value) {
			if (value == 1)
				FastGroupProber.Instance?.AddDirtyCell(value);
		}

		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var inc = typeof(Interlocked).GetMethodSafe(nameof(Interlocked.Increment), true,
				typeof(int).MakeByRefType());
			var target = typeof(MinionGroupProber_Occupy_Patch).GetMethodSafe(
				nameof(EnqueueIfOne), true, typeof(int));
			int state = 0;
			foreach (var instr in instructions) {
				var opcode = instr.opcode;
				// Replace the pop after the call to Interlocked.Increment
				if (opcode == OpCodes.Call && instr.operand is MethodBase method &&
						method == inc)
					state = 1;
				if (opcode == OpCodes.Pop && state == 1) {
					yield return new CodeInstruction(OpCodes.Call, target);
					state = 2;
				} else
					yield return instr;
			}
			if (state == 2) {
#if DEBUG
				PUtil.LogDebug("Patched MinionGroupProber.Occupy");
#endif
			} else
				PUtil.LogWarning("Unable to patch MinionGroupProber.Occupy");
		}
	}
	
	/// <summary>
	/// Applied to MinionGroupProber to queue dirty path grid cells if cells are freshly
	/// occupied.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.OccupyST))]
	public static class MinionGroupProber_OccupyST_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before OccupyST runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(List<int> cells, List<int> ___cells) {
			var fgp = FastGroupProber.Instance;
			int n = cells.Count;
			for (int i = 0; i < n; i++) {
				int cell = cells[i];
				if (0 == ___cells[cell]++ && fgp != null)
					fgp.AddDirtyCell(cell);
			}
			return false;
		}
	}

	/// <summary>
	/// Patch the Vacate family of methods in MinionGroupProber to trigger a reachability
	/// update if the cell is freshly vacated.
	/// </summary>
	[HarmonyPatch]
	public static class MinionGroupProber_Vacate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		internal static IEnumerable<MethodBase> TargetMethods() {
			const string vacate = nameof(MinionGroupProber.Vacate);
			yield return typeof(MinionGroupProber).GetMethodSafe(vacate, false,
				typeof(List<int>));
			yield return typeof(MinionGroupProber).GetMethodSafe(vacate, false, typeof(int));
		}

		/// <summary>
		/// Enqueues a dirty path grid cell, only if Interlocked.Decrement returned 0.
		/// </summary>
		/// <param name="value">The incremented value.</param>
		private static void EnqueueIfZero(int value) {
			if (value == 0)
				FastGroupProber.Instance?.AddDirtyCell(value);
		}

		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var dec = typeof(Interlocked).GetMethodSafe(nameof(Interlocked.Decrement), true,
				typeof(int).MakeByRefType());
			var target = typeof(MinionGroupProber_Vacate_Patch).GetMethodSafe(
				nameof(EnqueueIfZero), true, typeof(int));
			int state = 0;
			foreach (var instr in instructions) {
				var opcode = instr.opcode;
				// Replace the pop after the call to Interlocked.Decrement
				if (opcode == OpCodes.Call && instr.operand is MethodBase method &&
						method == dec)
					state = 1;
				if (opcode == OpCodes.Pop && state == 1) {
					yield return new CodeInstruction(OpCodes.Call, target);
					state = 2;
				} else
					yield return instr;
			}
			if (state == 2) {
#if DEBUG
				PUtil.LogDebug("Patched MinionGroupProber.Vacate");
#endif
			} else
				PUtil.LogWarning("Unable to patch MinionGroupProber.Vacate");
		}
	}
	
	/// <summary>
	/// Applied to MinionGroupProber to queue dirty path grid cells if cells are freshly
	/// vacated.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.VacateST))]
	public static class MinionGroupProber_VacateST_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before VacateST runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(List<int> cells, List<int> ___cells) {
			var fgp = FastGroupProber.Instance;
			int n = cells.Count;
			for (int i = 0; i < n; i++) {
				int cell = cells[i];
				if (0 == --___cells[cell] && fgp != null)
					fgp.AddDirtyCell(cell);
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to SafeCellQuery to dramatically speed it up by cancelling the query once
	/// a target cell has been found with all criteria.
	/// </summary>
	[HarmonyPatch(typeof(SafeCellQuery), nameof(SafeCellQuery.IsMatch))]
	public static class SafeCellQuery_IsMatch_Patch {
		/// <summary>
		/// SafeFlags is not declared as a [Flags] enum, so manually calculate the OR of all
		/// the flags added together
		/// </summary>
		private const SafeCellQuery.SafeFlags MAX_FLAGS = (SafeCellQuery.SafeFlags)(
			(int)SafeCellQuery.SafeFlags.IsNotLiquid * 2 - 1);

		internal static bool Prepare() => FastTrackOptions.Instance.SensorOpts;

		/// <summary>
		/// Applied after IsMatch runs.
		/// </summary>
		internal static void Postfix(SafeCellQuery __instance, ref bool __result, int cost) {
			if (cost >= __instance.targetCost && __instance.targetCellFlags >= MAX_FLAGS)
				__result = true;
		}
	}
}
