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
using System.Reflection.Emit;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to ClusterCometDetector.Instance to avoid sending logic signals every 200ms
	/// unless something has actually changed.
	/// </summary>
	[HarmonyPatch(typeof(ClusterCometDetector.Instance), nameof(ClusterCometDetector.Instance.
		UpdateDetectionState))]
	public static class ClusterCometDetector_Instance_UpdateDetectionState_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.LogicUpdates;

		/// <summary>
		/// Applied before UpdateDetectionState runs.
		/// </summary>
		internal static bool Prefix(ClusterCometDetector.Instance __instance,
				bool currentDetection) {
			if (__instance != null) {
				var prefabID = __instance.GetComponent<KPrefabID>();
				bool hasTag = prefabID.HasTag(GameTags.Detecting);
				if (currentDetection && !hasTag)
					prefabID.AddTag(GameTags.Detecting, false);
				else if (!currentDetection && hasTag)
					prefabID.RemoveTag(GameTags.Detecting);
				if (currentDetection != hasTag)
					// State changed, this would trigger a state transition anyways
					__instance.SetLogicSignal(currentDetection);
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to SmartReservoir to force update the state once on spawn.
	/// </summary>
	[HarmonyPatch(typeof(SmartReservoir), nameof(SmartReservoir.OnSpawn))]
	public static class SmartReservoir_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.LogicUpdates;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(SmartReservoir __instance) {
			__instance.logicPorts.SendSignal(SmartReservoir.PORT_ID, __instance.activated ?
				1 : 0);
		}
	}

	/// <summary>
	/// Applied to SmartReservoir to stop sending logic updates every 200ms even if nothing
	/// has changed.
	/// </summary>
	[HarmonyPatch(typeof(SmartReservoir), nameof(SmartReservoir.UpdateLogicCircuit))]
	public static class SmartReservoir_UpdateLogicCircuit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.LogicUpdates;

		/// <summary>
		/// Only sends the logic signal update if the new value has changed from the last
		/// value.
		/// </summary>
		/// <param name="ports">The logic port object to which to send the signal.</param>
		/// <param name="portID">The logic port ID to update.</param>
		/// <param name="newValue">The new value of the port.</param>
		/// <param name="lastValue">The previous value of the activated flag.</param>
		private static void SendSignalIf(LogicPorts ports, HashedString portID, int newValue,
				bool lastValue) {
			int lastIntValue = lastValue ? 1 : 0;
			if (newValue != lastIntValue)
				ports.SendSignal(portID, newValue);
		}

		/// <summary>
		/// Transpiles UpdateLogicCircuit to only conditionally send the signal by swapping
		/// out the calls to SendSignal.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var target = typeof(LogicPorts).GetMethodSafe(nameof(LogicPorts.SendSignal),
				false, typeof(HashedString), typeof(int));
			var replacement = typeof(SmartReservoir_UpdateLogicCircuit_Patch).GetMethodSafe(
				nameof(SendSignalIf), true, PPatchTools.AnyArguments);
			var activatedField = typeof(SmartReservoir).GetFieldSafe(nameof(SmartReservoir.
				activated), false);
			if (target == null || replacement == null || activatedField == null) {
				PUtil.LogWarning("Unable to patch SmartReservoir.UpdateLogicCircuit");
				foreach (var instr in instructions)
					yield return instr;
			} else {
				var oldActivated = generator.DeclareLocal(typeof(bool));
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, activatedField);
				yield return new CodeInstruction(OpCodes.Stloc_S, (byte)oldActivated.
					LocalIndex);
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Callvirt, target)) {
						// Call the replacement
						instr.opcode = OpCodes.Ldloc_S;
						instr.operand = (byte)oldActivated.LocalIndex;
						yield return instr;
						// Push the old value
						yield return new CodeInstruction(OpCodes.Call, replacement);
#if DEBUG
						PUtil.LogDebug("Patched SmartReservoir.UpdateLogicCircuit");
#endif
					} else
						yield return instr;
				}
			}
		}
	}
}
