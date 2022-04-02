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

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
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
			return PPatchTools.ReplaceMethodCall(instructions, new Dictionary<MethodInfo,
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
		internal static void Postfix(IList<BrainScheduler.BrainGroup> ___brainGroups) {
			DupeBrainGroupUpdater.DestroyInstance();
			foreach (var brainGroup in ___brainGroups)
				if (brainGroup.GetType() == typeof(BrainScheduler.DupeBrainGroup)) {
					DupeBrainGroupUpdater.CreateInstance(brainGroup);
#if DEBUG
					PUtil.LogDebug("Created DupeBrainGroupUpdater");
#endif
					break;
				}
		}
	}

	/// <summary>
	/// Applied to BrainScheduler.BrainGroup to only start up the sensors if the pickup
	/// optimizations are being backgrounded.
	/// </summary>
	[HarmonyPatch(typeof(BrainScheduler.BrainGroup), nameof(BrainScheduler.BrainGroup.
		RenderEveryTick))]
	internal static class RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		internal static bool Prefix(BrainScheduler.BrainGroup __instance,
				bool isAsyncPathProbeEnabled) {
			var inst = DupeBrainGroupUpdater.Instance;
			bool update = true;
			if (inst != null && __instance == inst.dupeBrainGroup) {
				update = AsyncJobManager.Instance == null;
				if (!update)
					inst.StartBrainUpdate(isAsyncPathProbeEnabled);
			}
			return update;
		}
	}
}
