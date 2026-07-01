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
using UnityEngine;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches for MinionTodoSideScreen (the Duplicant "Errands" tab).
	///
	/// PopulateElements rebuilds the colony-wide chore list (every pending errand tested
	/// against this dupe's precondition snapshot via GlobalChoreProvider.CollectChores) and
	/// is idempotent - it always rebuilds fully from ChoreConsumer.GetLastPreconditionSnapshot,
	/// with no side effect that depends on being called on every single invocation. But it is
	/// invoked on TWO redundant paths while the tab is open: unconditionally every frame from
	/// ScreenUpdate, and again from a self-rescheduling 0.1s UIScheduler tick that
	/// PopulateElements itself arms. Candidate 0005: throttle so the colony-scaled rebuild
	/// runs at most 10x/sec (matching the existing 0.1s cadence) instead of ~60x/sec.
	/// </summary>
	public static class MinionTodoSideScreenPatches {
		/// <summary>
		/// Minimum real time between two "real" PopulateElements rebuilds, in seconds. Matches
		/// the 0.1s cadence that the base game's own UIScheduler.Schedule("RefreshToDoList", ...)
		/// call already uses, so this only removes redundant same-window calls - it does not
		/// slow down the panel's effective refresh rate.
		/// </summary>
		private const float THROTTLE = 0.1f;

		/// <summary>
		/// The unscaled time (Time.unscaledTime) at which PopulateElements last actually ran.
		/// MinionTodoSideScreen is a singleton-per-DetailsScreen side screen (one instance
		/// active at a time), so a single static field is sufficient here.
		/// </summary>
		private static float lastPopulate;

		/// <summary>
		/// Applied to MinionTodoSideScreen.PopulateElements to throttle the colony-wide chore
		/// list rebuild. PopulateElements is called both per-frame (ScreenUpdate) and from its
		/// own 0.1s re-arming UIScheduler callback; skipping the redundant in-window calls here
		/// collapses both paths down to the intended ~10 Hz cadence.
		/// </summary>
		[HarmonyPatch(typeof(MinionTodoSideScreen), "PopulateElements")]
		public static class PopulateElements_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before PopulateElements runs. Skips the rebuild (and the scheduler
			/// re-arm inside it) if it already ran within the last THROTTLE seconds; the
			/// still-armed 0.1s scheduler handle from the last real call will fire on time.
			/// </summary>
			internal static bool Prefix() {
				float now = Time.unscaledTime;
				bool runNow = now - lastPopulate >= THROTTLE;
				if (runNow)
					lastPopulate = now;
				return runNow;
			}
		}
	}
}
