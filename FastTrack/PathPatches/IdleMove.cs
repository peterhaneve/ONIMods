/*
 * Copyright 2026 Peter Han
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

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Prevent idle Duplicants that cannot be seen at the moment from moving 95% of the time.
	/// </summary>
	[HarmonyPatch(typeof(IdleChore.States), nameof(IdleChore.States.InitializeStates))]
	public static class IdleChore_States_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceDuplicantIdleMove;

		/// <summary>
		/// When the move state is entered, go to the idle state again if the Duplicant
		/// is not visible (95% of cases).
		/// </summary>
		internal static void Postfix(IdleChore.States __instance) {
			__instance.idle.move.Enter(delegate(IdleChore.StatesInstance smi) {
				if (!GridVisibleArea.GetVisibleArea().Contains(Grid.PosToCell(smi)) &&
						UnityEngine.Random.Range(1, 100) >= 96)
					smi.GoTo(smi.sm.idle);
			});
		}
	}

	/// <summary>
	/// Prevent idle critters that cannot be seen at the moment from moving 95% of the time.
	/// </summary>
	[HarmonyPatch(typeof(IdleStates), nameof(IdleStates.MoveToNewCell))]
	public static class IdleStates_MoveToNewCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceCritterIdleMove;

		/// <summary>
		/// Called when the move state is entered, go to the loop (=idle) state again if critter
		/// is not visible (95% of cases).
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(IdleStates.Instance smi, IdleStates.State ___loop) {
			bool skipMove = !GridVisibleArea.GetVisibleArea().Contains(Grid.PosToCell(smi)) &&
				UnityEngine.Random.Range(1, 100) >= 96;
			if (skipMove)
				smi.GoTo(___loop);
			return !skipMove;
		}
	}

	/// <summary>
	/// Prevent idle Beetas that cannot be seen at the moment from moving 95% of the time.
	/// </summary>
	[HarmonyPatch(typeof(BuzzStates), nameof(BuzzStates.MoveToNewCell))]
	public static class BuzzStates_MoveToNewCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceCritterIdleMove;

		/// <summary>
		/// Called when the move state is entered, go to the idle state again if Beeta
		/// is not visible (95% of cases).
		/// </summary>
		internal static bool Prefix(BuzzStates.Instance smi) {
			bool skipMove = !GridVisibleArea.GetVisibleArea().Contains(Grid.PosToCell(smi)) &&
				UnityEngine.Random.Range(1, 100) >= 96;
			if (skipMove)
				smi.GoTo(smi.sm.idle);
			return !skipMove;
		}
	}
}
