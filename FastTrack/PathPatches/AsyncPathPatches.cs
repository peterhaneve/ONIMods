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
	/// Applied to BrainScheduler.BrainGroup to move the path probe updates to a fully
	/// asychronous task.
	/// </summary>
	[HarmonyPatch(typeof(BrainScheduler.BrainGroup), nameof(BrainScheduler.BrainGroup.
		AsyncPathProbe))]
	internal static class BrainScheduler_BrainGroup_AsyncPathProbe_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AsyncPathProbe;

		/// <summary>
		/// Transpiles AsyncPathProbe to use our job manager instead.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var workItemType = typeof(IWorkItemCollection);
			var cpuCharge = typeof(PathProbeJobManager).GetMethodSafe(nameof(
				PathProbeJobManager.SetCPUBudget), true, typeof(ICPULoad));
			return PPatchTools.ReplaceMethodCallSafe(instructions, new Dictionary<MethodInfo,
					MethodInfo> {
				{
					typeof(GlobalJobManager).GetMethodSafe(nameof(GlobalJobManager.Run),
						true, workItemType),
					typeof(PathProbeJobManager).GetMethodSafe(nameof(PathProbeJobManager.
						RunAsync), true, workItemType)
				},
				{
					typeof(CPUBudget).GetMethodSafe(nameof(CPUBudget.Start), true,
						typeof(ICPULoad)),
					cpuCharge
				},
				{
					typeof(CPUBudget).GetMethodSafe(nameof(CPUBudget.End), true,
						typeof(ICPULoad)),
					cpuCharge
				}
			});
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
			bool asyncProbe = __instance.isAsyncPathProbeEnabled;
			var inst = AsyncBrainGroupUpdater.Instance;
			if (!Game.IsQuitting() && !KMonoBehaviour.isLoadingScene && inst != null) {
				inst.StartBrainCollect();
				foreach (var brainGroup in __instance.brainGroups)
					PriorityBrainScheduler.Instance.UpdateBrainGroup(inst, asyncProbe,
						brainGroup);
				inst.EndBrainCollect();
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to CPUBudget to allow fetching the core count off the main thread.
	/// </summary>
	[HarmonyPatch]
	public static class CPUBudget_GetCoreCount_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		internal static MethodBase TargetMethod() {
			return typeof(CPUBudget).GetPropertySafe<int>(nameof(CPUBudget.coreCount), true)?.
				GetGetMethod(true);
		}

		/// <summary>
		/// Applied before coreCount's getter runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref int __result) {
			__result = FastTrackMod.CoreCount;
			return false;
		}
	}

	/// <summary>
	/// Applied to ScenePartitioner to make the GatherEntries family of methods partially
	/// thread safe.
	/// </summary>
	[HarmonyPatch(typeof(ScenePartitioner), nameof(ScenePartitioner.GatherEntries),
		typeof(int), typeof(int), typeof(int), typeof(int), typeof(ScenePartitionerLayer),
		typeof(object), typeof(List<ScenePartitionerEntry>), typeof(int))]
	public static class ScenePartitioner_GatherEntries_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// A slightly more thread safe version of HashSet.ExceptWith that at least avoids self
		/// races.
		/// </summary>
		/// <param name="set">The set to modify.</param>
		/// <param name="other">The entries to remove.</param>
		private static void ExceptWithLocked(SceneEntryHash set,
				IEnumerable<ScenePartitionerEntry> other) {
			lock (set) {
				set.ExceptWith(other);
			}
		}

		/// <summary>
		/// Transpiles GatherEntries to take out a lock before subtracting.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(SceneEntryHash).
				GetMethodSafe(nameof(SceneEntryHash.ExceptWith), false, typeof(
				IEnumerable<ScenePartitionerEntry>)), typeof(
				ScenePartitioner_GatherEntries_Patch).GetMethodSafe(nameof(ExceptWithLocked),
				true, typeof(SceneEntryHash), typeof(IEnumerable<ScenePartitionerEntry>)));
		}
	}

	/// <summary>
	/// Applied to ScenePartitioner to make the Add family of methods partially thread safe.
	/// </summary>
	[HarmonyPatch(typeof(ScenePartitioner), nameof(ScenePartitioner.Insert))]
	public static class ScenePartitioner_Insert_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// A slightly more thread safe version of List.Add that at least avoids self
		/// races.
		/// </summary>
		/// <param name="list">The list to modify.</param>
		/// <param name="entry">The entry to add.</param>
		private static void AddLocked(List<DirtyNode> list, DirtyNode entry) {
			lock (list) {
				list.Add(entry);
			}
		}

		/// <summary>
		/// A slightly more thread safe version of HashSet.Add that at least avoids self
		/// races.
		/// </summary>
		/// <param name="set">The set to modify.</param>
		/// <param name="entry">The entry to add.</param>
		/// <returns>true if the set was modified, or false otherwise.</returns>
		private static bool AddLocked(SceneEntryHash set, ScenePartitionerEntry entry) {
			bool result;
			lock (set) {
				result = set.Add(entry);
			}
			return result;
		}

		/// <summary>
		/// Transpiles Insert to take out a lock before adding.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, new Dictionary<MethodInfo,
					MethodInfo>() {
				{
					typeof(SceneEntryHash).GetMethodSafe(nameof(SceneEntryHash.Add), false,
						typeof(ScenePartitionerEntry)),
					typeof(ScenePartitioner_Insert_Patch).GetMethodSafe(nameof(AddLocked),
						true, typeof(SceneEntryHash), typeof(ScenePartitionerEntry))
				},
				{
					typeof(List<DirtyNode>).GetMethodSafe(nameof(List<DirtyNode>.Add), false,
						typeof(DirtyNode)),
					typeof(ScenePartitioner_Insert_Patch).GetMethodSafe(nameof(AddLocked),
						true, typeof(List<DirtyNode>), typeof(DirtyNode))
				}
			});
		}
	}

	/// <summary>
	/// Applied to ScenePartitioner to make the Free family of methods partially thread safe.
	/// </summary>
	[HarmonyPatch(typeof(ScenePartitioner), nameof(ScenePartitioner.Widthdraw))]
	public static class ScenePartitioner_Widthdraw_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// A slightly more thread safe version of HashSet.Remove that at least avoids self
		/// races.
		/// </summary>
		/// <param name="set">The set to modify.</param>
		/// <param name="entry">The entry to remove.</param>
		/// <returns>true if the set was modified, or false otherwise.</returns>
		private static bool RemoveLocked(SceneEntryHash set, ScenePartitionerEntry entry) {
			bool result;
			lock (set) {
				result = set.Remove(entry);
			}
			return result;
		}

		/// <summary>
		/// Transpiles Widthdraw (Clay please spelling) to take out a lock before removing.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(SceneEntryHash).
				GetMethodSafe(nameof(SceneEntryHash.Remove), false, typeof(
				ScenePartitionerEntry)), typeof(ScenePartitioner_Widthdraw_Patch).
				GetMethodSafe(nameof(RemoveLocked), true, typeof(SceneEntryHash), typeof(
				ScenePartitionerEntry)));
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
