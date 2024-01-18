/*
 * Copyright 2024 Peter Han
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
using System;
using UnityEngine;
using UnityEngine.UI;

using DiagnosticRow = ColonyDiagnosticScreen.DiagnosticRow;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to Bouncer to turn off notification bounces.
	/// </summary>
	[HarmonyPatch(typeof(Bouncer), nameof(Bouncer.Bounce))]
	public static class Bouncer_Bounce_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// Applied before Bounce runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to ColonyDiagnosticScreen.DiagnosticRow to turn off the bouncing effect.
	/// </summary>
	[HarmonyPatch(typeof(DiagnosticRow), nameof(DiagnosticRow.TriggerVisualNotification))]
	public static class DiagnosticRow_TriggerVisualNotification_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// A replacement coroutine that waits 3 seconds and then resolves it with no bounce.
		/// </summary>
		private static System.Collections.IEnumerator NoMoveRoutine(DiagnosticRow row) {
			// Wait for 3 seconds unscaled
			yield return new WaitForSeconds(3.0f);
			if (row.gameObject != null)
				try {
					row.ResolveNotificationRoutine();
				} catch (Exception) {
					// Ignore exception if the notification cannot be resolved
				}
		}

		/// <summary>
		/// Transpiles TriggerVisualNotification to remove calls to the bouncy coroutine.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(DiagnosticRow).
				GetMethodSafe(nameof(DiagnosticRow.VisualNotificationRoutine), false),
				typeof(DiagnosticRow_TriggerVisualNotification_Patch).GetMethodSafe(nameof(
				NoMoveRoutine), true, typeof(DiagnosticRow)));
		}
	}


	/// <summary>
	/// Applied to NotificationAnimator to turn off the bouncing effect.
	/// </summary>
	[HarmonyPatch(typeof(NotificationAnimator), nameof(NotificationAnimator.Begin))]
	public static class NotificationAnimator_Begin_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

		/// <summary>
		/// Applied before Begin runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(NotificationAnimator __instance, ref bool ___animating,
				ref LayoutElement ___layoutElement) {
			if (__instance.TryGetComponent(out LayoutElement le))
				le.minWidth = 0.0f;
			___layoutElement = le;
			___animating = false;
			return false;
		}
	}
}
