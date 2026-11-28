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
using PeterHan.PLib.Core;
using System;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to KAnimControllerBase to make trivial anims stop triggering updates.
	/// </summary>
	[HarmonyPatch(typeof(KAnimControllerBase), nameof(KAnimControllerBase.StartQueuedAnim))]
	public static class KAnimControllerBase_StartQueuedAnim_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Adjusts the play mode if the anim is trivial.
		/// </summary>
		internal static KAnim.PlayMode UpdateMode(KAnim.PlayMode mode,
				KAnimControllerBase controller) {
			var anim = controller.GetCurrentAnim();
			var inst = KAnimLoopOptimizer.Instance;
			if (anim != null && mode == KAnim.PlayMode.Loop && inst != null && controller.
					animQueue.Count == 0)
				mode = inst.GetAnimState(anim, mode);
			return mode;
		}

		/// <summary>
		/// Transpiles StartQueuedAnim to update the mode to Once on trivial anims.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = typeof(KAnimControllerBase).GetFieldSafe(nameof(KAnimControllerBase.
				mode), false);
			var injection = typeof(KAnimControllerBase_StartQueuedAnim_Patch).GetMethodSafe(
				nameof(UpdateMode), true, typeof(KAnim.PlayMode), typeof(KAnimControllerBase));
			if (target == null || injection == null) {
				PUtil.LogWarning("Unable to patch KAnimControllerBase.StartQueuedAnim");
				foreach (var instr in instructions)
					yield return instr;
			} else
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Stfld, target)) {
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, injection);
#if DEBUG
						PUtil.LogDebug("Patched KAnimControllerBase.StartQueuedAnim");
#endif
					}
					yield return instr;
				}
		}
	}

	/// <summary>
	/// Applied to KAnimSynchronizedController to not dirty rocket anims (among others)
	/// every frame.
	/// </summary>
	[HarmonyPatch(typeof(KAnimSynchronizedController), nameof(KAnimSynchronizedController.
		Dirty))]
	public static class KAnimSynchronizedController_Dirty_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AnimOpts;

		/// <summary>
		/// Applied before Dirty runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(KAnimSynchronizedController __instance) {
			var syncController = __instance.synchronizedController;
			var controller = __instance.controller;
			if (syncController != null && controller != null) {
				// These setters unconditionally dirty and deregister/register the anim
				if (syncController.Offset != controller.Offset)
					syncController.Offset = controller.Offset;
				if (syncController.Pivot != controller.Pivot)
					syncController.Pivot = controller.Pivot;
				if (!Mathf.Approximately(syncController.Rotation, controller.Rotation))
					syncController.Rotation = controller.Rotation;
				if (syncController.FlipX != controller.FlipX)
					syncController.FlipX = controller.FlipX;
				if (syncController.FlipY != controller.FlipY)
					syncController.FlipY = controller.FlipY;
			}
			return false;
		}
	}

	// The KBatchedAnimController.UpdateAnim replacement was removed for U60: anim batches
	// are no longer grouped by chunk (lastChunkXY, Register and DeRegister were removed in
	// favor of ValidateRegistration), so the old reimplementation no longer matches vanilla.
}
