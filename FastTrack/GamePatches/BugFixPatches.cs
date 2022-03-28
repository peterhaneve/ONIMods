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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to AnimEventManager to fix a bug where the anim indirection data list grows
	/// without bound, consuming memory, causing GC pauses, and eventually crashing when it
	/// overflows.
	/// </summary>
	[HarmonyPatch(typeof(AnimEventManager), nameof(AnimEventManager.StopAnim))]
	public static class AnimEventManager_StopAnim_Patch {
		/// <summary>
		/// Transpiles StopAnim to throw away the event handle properly.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var indirectionDataType = typeof(AnimEventManager).GetNestedType("IndirectionData",
				PPatchTools.BASE_FLAGS | BindingFlags.Instance);
			MethodInfo target = null, setData = null;
			if (indirectionDataType != null) {
				var kcv = typeof(KCompactedVector<>).MakeGenericType(indirectionDataType);
				target = kcv?.GetMethodSafe(nameof(KCompactedVector<AnimEventManager.
					EventPlayerData>.Free), false, typeof(HandleVector<int>.Handle));
				setData = kcv?.GetMethodSafe(nameof(KCompactedVector<AnimEventManager.
					EventPlayerData>.SetData), false, typeof(HandleVector<int>.Handle),
					indirectionDataType);
			}
			if (target != null && setData != null)
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Callvirt, setData)) {
						// Remove "data"
						yield return new CodeInstruction(OpCodes.Pop);
						// Call Free
						yield return new CodeInstruction(OpCodes.Callvirt, target);
						// Pop the retval
						instr.opcode = OpCodes.Pop;
						instr.operand = null;
#if DEBUG
						PUtil.LogDebug("Patched AnimEventManager.StopAnim");
#endif
					}
					yield return instr;
				}
			else {
				PUtil.LogWarning("Unable to patch AnimEventManager.StopAnim");
				foreach (var instr in instructions)
					yield return instr;
			}
		}
	}

	/// <summary>
	/// Applied to ClusterManager to reduce memory allocations when accessing
	/// WorldContainers.
	/// </summary>
	[HarmonyPatch(typeof(ClusterManager), nameof(ClusterManager.WorldContainers),
		MethodType.Getter)]
	public static class ClusterManager_WorldContainers_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Transpiles the WorldContainers getter to remove the AsReadOnly call.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var targetMethod = typeof(List<>).MakeGenericType(typeof(WorldContainer))?.
				GetMethodSafe(nameof(List<WorldContainer>.AsReadOnly), false);
			var method = new List<CodeInstruction>(instructions);
			if (targetMethod != null) {
				method.RemoveAll((instr) => instr.Is(OpCodes.Callvirt, targetMethod));
#if DEBUG
				PUtil.LogDebug("Patched ClusterManager.WorldContainers");
#endif
			} else
				PUtil.LogWarning("Unable to patch ClusterManager.WorldContainers");
			return method;
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to... actually clear the dirty flag when it updates.
	/// Unfortunately most anims are marked dirty every frame anyways.
	/// </summary>
	[HarmonyPatch(typeof(KAnimBatch), "ClearDirty")]
	public static class KAnimBatch_ClearDirty_Patch {
		/// <summary>
		/// Applied after ClearDirty runs.
		/// </summary>
		internal static void Postfix(ref bool ___needsWrite) {
			___needsWrite = false;
		}
	}
}
