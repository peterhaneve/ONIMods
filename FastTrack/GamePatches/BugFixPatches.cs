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

using IndirectionData = AnimEventManager.IndirectionData;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to Accumulators to fill the first value if the average has yet to be
	/// calculated.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.Accumulate))]
	public static class Accumulators_Accumulate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before Accumulate runs.
		/// </summary>
		internal static bool Prefix(Accumulators __instance, HandleVector<int>.Handle handle,
				float amount) {
			var accumulated = __instance.accumulated;
			var average = __instance.average;
			float data = accumulated.GetData(handle);
			accumulated.SetData(handle, data + amount);
			// Prime the pump
			if (float.IsNaN(average.GetData(handle)))
				average.SetData(handle, amount);
			return false;
		}
	}

	/// <summary>
	/// Applied to Accumulators to preload an invalid value, for notifying the averaging
	/// system that it needs initialization.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.Add))]
	public static class Accumulators_Add_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before Add runs.
		/// </summary>
		internal static bool Prefix(Accumulators __instance, ref HandleVector<int>.
				Handle __result) {
			__result = __instance.accumulated.Allocate(0f);
			__instance.average.Allocate(float.NaN);
			return false;
		}
	}

	/// <summary>
	/// Applied to Accumulators to substitute zero if no samples have been accumulated at all.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.GetAverageRate))]
	public static class Accumulators_GetAverageRate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied after GetAverageRate runs.
		/// </summary>
		internal static void Postfix(ref float __result) {
			float r = __result;
			if (float.IsNaN(r) || float.IsInfinity(r))
				__result = 0.0f;
		}
	}

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
			MethodInfo target = null, setData = null;
			target = typeof(KCompactedVector<IndirectionData>).GetMethodSafe(nameof(
				KCompactedVector<IndirectionData>.Free), false, typeof(HandleVector<int>.
				Handle));
			setData = typeof(KCompactedVector<IndirectionData>).GetMethodSafe(nameof(
				KCompactedVector<IndirectionData>.SetData), false, typeof(HandleVector<int>.
				Handle), typeof(IndirectionData));
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
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

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
}
