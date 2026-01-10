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
using System.Collections.Generic;
using System.Reflection;

using DirtyNode = ScenePartitioner.DirtyNode;
using SceneEntryHash = System.Collections.Generic.HashSet<ScenePartitionerEntry>;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Applied to Brain to stop chore selection until reachability updates.
	/// </summary>
	[HarmonyPatch(typeof(Brain), nameof(Brain.UpdateChores))]
	public static class Brain_UpdateChores_Patch {
		internal static bool Prepare() {
			var opts = FastTrackOptions.Instance;
			return opts.PickupOpts && opts.FastReachability && opts.ChorePriorityMode ==
				FastTrackOptions.NextChorePriority.Delay;
		}

		/// <summary>
		/// Applied before UpdateChores runs.
		/// </summary>
		internal static bool Prefix(Brain __instance) {
			var consumer = __instance.choreConsumer;
			var inst = PriorityBrainScheduler.Instance;
			return consumer == null || consumer.choreDriver.HasChore() || !inst.updateFirst.
				Contains(__instance);
		}
	}

	/// <summary>
	/// Applied to BrainScheduler to initialize the singleton instance with the current
	/// Duplicant brain group.
	/// </summary>
	[HarmonyPatch(typeof(BrainScheduler), nameof(BrainScheduler.OnPrefabInit))]
	internal static class BrainScheduler_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix() {
			AsyncBrainGroupUpdater.CreateInstance();
#if DEBUG
			PUtil.LogDebug("Created AsyncBrainGroupUpdater");
#endif
		}
	}

	/// <summary>
	/// Applied to BrainScheduler to gather brains to update.
	/// </summary>
	[HarmonyPatch(typeof(BrainScheduler), nameof(BrainScheduler.RenderEveryTick))]
	internal static class BrainScheduler_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(BrainScheduler __instance) {
			var inst = AsyncBrainGroupUpdater.Instance;
			if (!Game.IsQuitting() && !KMonoBehaviour.isLoadingScene && inst != null) {
				inst.StartBrainCollect();
				foreach (var brainGroup in __instance.brainGroups)
					PriorityBrainScheduler.Instance.UpdateBrainGroup(inst, brainGroup);
				inst.EndBrainCollect();
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to Storage to remove it from the cache when it is destroyed.
	/// </summary>
	[HarmonyPatch(typeof(Storage), nameof(Storage.OnCleanUp))]
	public static class Storage_OnCleanUp_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied after OnCleanUp runs.
		/// </summary>
		internal static void Postfix(Storage __instance) {
			AsyncBrainGroupUpdater.Instance?.RemoveStorage(__instance);
		}
	}
}
