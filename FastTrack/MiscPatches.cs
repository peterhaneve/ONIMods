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
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack {
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
	/// Applied to Game to start property texture updates after Sim data arrives and
	/// fast Reachability updates before the sim cycle starts.
	/// </summary>
	[HarmonyPatch(typeof(Game), "Update")]
	[HarmonyPriority(Priority.Low)]
	public static class Game_Update_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.ReduceTileUpdates || options.FastReachability;
		}

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix() {
			SensorPatches.FastGroupProber.Instance?.Update();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix() {
			try {
				VisualPatches.PropertyTextureUpdater.Instance?.StartUpdate();
			} catch (System.Exception e) {
				PUtil.LogError(e);
			}
		}
	}

	/// <summary>
	/// Applied to Global to start up some expensive things before Game.LateUpdate runs.
	/// </summary>
	[HarmonyPatch(typeof(Global), "LateUpdate")]
	public static class Global_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ConduitOpts;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static void Prefix() {
			if (Game.Instance != null)
				ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
		}
	}

	/// <summary>
	/// Applied to Global to start up some expensive things before Game.Update runs.
	/// </summary>
	[HarmonyPatch(typeof(Global), "Update")]
	public static class Global_Update_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.ConduitOpts || options.ParallelInventory;
		}

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix() {
			if (Game.Instance != null) {
				var options = FastTrackOptions.Instance;
				if (options.ConduitOpts)
					ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
				if (options.ParallelInventory)
					UIPatches.BackgroundInventoryUpdater.Instance?.StartUpdateAll();
			}
		}
	}

	/// <summary>
	/// Applied to KAnimBatch to... actually clear the dirty flag when it updates.
	/// Unfortunately most anims are marked dirty every frame.
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

	/// <summary>
	/// Applied to MinionConfig to apply several patches from different areas of the mod.
	/// </summary>
	[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.OnSpawn))]
	public static class MinionConfig_OnSpawn_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.SensorOpts || options.NoSplash;
		}

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(GameObject go) {
			if (go != null) {
				var options = FastTrackOptions.Instance;
				var nav = go.GetComponentSafe<Navigator>();
				if (options.SensorOpts)
					SensorPatches.SensorPatches.RemoveBalloonArtistSensor(go);
				if (options.NoSplash && nav != null)
					nav.transitionDriver.overrideLayers.RemoveAll((layer) => layer is
						SplashTransitionLayer);
			}
		}
	}

	/// <summary>
	/// Applied to World to finish up expensive things after Game.LateUpdate has run.
	/// </summary>
	[HarmonyPatch(typeof(World), "LateUpdate")]
	public static class World_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static void Prefix() {
			PathPatches.DupeBrainGroupUpdater.Instance?.ReleaseFetches();
		}

		/// <summary>
		/// Applied after LateUpdate runs.
		/// </summary>
		internal static void Postfix() {
			PathPatches.DupeBrainGroupUpdater.Instance?.EndBrainUpdate();
		}
	}
}
