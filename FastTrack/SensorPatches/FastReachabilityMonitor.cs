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
using ReachabilityMonitorState = GameStateMachine<ReachabilityMonitor, ReachabilityMonitor.
	Instance, Workable, object>.State;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// A faster version of ReachabilityMonitor that only updates if changes to pathing occur
	/// in its general area.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class FastReachabilityMonitor : KMonoBehaviour, ISim4000ms {
		/// <summary>
		/// The name of the layer used for the reachability scene partitioner.
		/// </summary>
		public const string REACHABILITY = nameof(FastReachabilityMonitor);

		/// <summary>
		/// The last set of extents from which this item was reachable.
		/// </summary>
		private Extents lastExtents;

		/// <summary>
		/// Registers a scene partitioner entry for nav cells changing.
		/// </summary>
		private HandleVector<int>.Handle partitionerEntry;

		/// <summary>
		/// The existing reachability monitor used to check and trigger events.
		/// </summary>
		private ReachabilityMonitor.Instance smi;

		internal FastReachabilityMonitor() {
			lastExtents = new Extents(int.MinValue, int.MinValue, 1, 1);
			partitionerEntry = HandleVector<int>.InvalidHandle;
		}

		/// <summary>
		/// Frees the scene partitioner entry for this workable.
		/// </summary>
		private void DestroyPartitioner() {
			var gsp = GameScenePartitioner.Instance;
			if (partitionerEntry.IsValid() && gsp != null)
				gsp.Free(ref partitionerEntry);
		}

		protected override void OnCleanUp() {
			DestroyPartitioner();
			base.OnCleanUp();
		}

		/// <summary>
		/// Only updates reachability if nav grid cells update nearby.
		/// </summary>
		private void OnReachableChanged(object _) {
			FastGroupProber.Instance?.Enqueue(smi);
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			smi = gameObject.GetSMI<ReachabilityMonitor.Instance>();
			UpdateOffsets();
		}

		public void Sim4000ms(float _) {
			UpdateOffsets();
		}

		/// <summary>
		/// Updates the scene partitioner entry if the offsets change.
		/// </summary>
		public void UpdateOffsets() {
			var inst = FastGroupProber.Instance;
			if (inst != null && smi != null) {
				var offsets = smi.master.GetOffsets();
				var extents = new Extents(Grid.PosToCell(transform.position), offsets);
				// Only if the extents actually changed
				if (extents.x != lastExtents.x || extents.y != lastExtents.y || extents.
						width != lastExtents.width || extents.height != lastExtents.height) {
					DestroyPartitioner();
					partitionerEntry = GameScenePartitioner.Instance.Add(
						"FastReachabilityMonitor.UpdateOffsets", this, extents, inst.mask,
						OnReachableChanged);
					// The offsets changed, fire a manual check if not the first time
					// (the smi constructor already updated it once)
					if (lastExtents.x >= 0)
						smi.UpdateReachability();
					lastExtents = extents;
				}
			} else
				PUtil.LogWarning("FastReachabilityMonitor scene partitioner is unavailable");
		}
	}

	/// <summary>
	/// Applied to GameScenePartitioner to create a mask for triggering reachability updates.
	/// </summary>
	[HarmonyPatch(typeof(GameScenePartitioner), "OnPrefabInit")]
	public static class GameScenePartitioner_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix(ScenePartitioner ___partitioner) {
			if (___partitioner != null)
				// XXX: There are only a few mask layers left
				FastGroupProber.Init(___partitioner.CreateMask(FastReachabilityMonitor.
					REACHABILITY));
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to use our check for reachability instead of its own.
	/// </summary>
	[HarmonyPatch]
	public static class MinionGroupProber_IsAllReachable_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Targets two methods that are essentially identical to save a patch.
		/// </summary>
		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(MinionGroupProber).GetMethodSafe(nameof(MinionGroupProber.
				IsAllReachable), false, typeof(int), typeof(CellOffset[]));
			yield return typeof(MinionGroupProber).GetMethodSafe(nameof(MinionGroupProber.
				IsReachable), false, typeof(int), typeof(CellOffset[]));
		}

		/// <summary>
		/// Applied before IsAllReachable runs.
		/// </summary>
		internal static bool Prefix(int cell, CellOffset[] offsets, ref bool __result) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				__result = Grid.IsValidCell(cell) && inst.IsReachable(cell, offsets);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to use our check for reachability instead of its own.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.IsReachable),
		typeof(int))]
	public static class MinionGroupProber_IsCellReachable_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before IsReachable runs.
		/// </summary>
		internal static bool Prefix(int cell, ref bool __result) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				__result = Grid.IsValidCell(cell) && inst.IsReachable(cell);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to use our check for reachability instead of its own.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), "IsReachable_AssumeLock")]
	public static class MinionGroupProber_IsReachableAssumeLock_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before IsReachable_AssumeLock runs.
		/// </summary>
		internal static bool Prefix(int cell, ref bool __result) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				__result = Grid.IsValidCell(cell) && inst.IsReachable(cell);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to mark cells as dirty when their prober status changes.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.Occupy))]
	public static class MinionGroupProber_Occupy_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before Occupy runs.
		/// </summary>
		internal static bool Prefix(object prober, IEnumerable<int> cells) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				inst.Occupy(prober, cells);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to deallocate references to destroyed path probers.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.ReleaseProber))]
	public static class MinionGroupProber_ReleaseProber_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before ReleaseProber runs.
		/// </summary>
		internal static bool Prefix(object prober) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				inst.Remove(prober);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to MinionGroupProber to obsolete invalid serial numbers when a prober
	/// completes.
	/// </summary>
	[HarmonyPatch(typeof(MinionGroupProber), nameof(MinionGroupProber.SetValidSerialNos))]
	public static class MinionGroupProber_SetValidSerialNos_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before SetValidSerialNos runs.
		/// </summary>
		internal static bool Prefix(object prober) {
			var inst = FastGroupProber.Instance;
			if (inst != null)
				inst.SetSerials(prober);
			return inst == null;
		}
	}

	/// <summary>
	/// Applied to ReachabilityMonitor to turn off periodic checks for reachability.
	/// </summary>
	[HarmonyPatch(typeof(ReachabilityMonitor), nameof(ReachabilityMonitor.InitializeStates))]
	public static class ReachabilityMonitor_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Replaces State.FastUpdate with a no-op method.
		/// </summary>
		private static ReachabilityMonitorState FastUpdate(ReachabilityMonitorState state,
				string name, UpdateBucketWithUpdater<ReachabilityMonitor.Instance>.IUpdater
				updater, UpdateRate update_rate, bool load_balance) {
			_ = name;
			_ = updater;
			_ = update_rate;
			_ = load_balance;
			return state;
		}

		/// <summary>
		/// Transpiles InitializeStates to remove the FastUpdate updater.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCall(instructions, typeof(ReachabilityMonitorState).
				GetMethodSafe(nameof(ReachabilityMonitorState.FastUpdate), false,
				typeof(string), typeof(UpdateBucketWithUpdater<ReachabilityMonitor.Instance>.
				IUpdater), typeof(UpdateRate), typeof(bool)), typeof(
				ReachabilityMonitor_InitializeStates_Patch).GetMethodSafe(nameof(FastUpdate),
				true, PPatchTools.AnyArguments));
		}
	}

	/// <summary>
	/// Applied to ReachabilityMonitor.Instance to add a fast reachability monitor on each
	/// object that needs reachability checks.
	/// </summary>
	[HarmonyPatch(typeof(ReachabilityMonitor.Instance), MethodType.Constructor,
		typeof(Workable))]
	public static class ReachabilityMonitor_Instance_Constructor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied after the constructor runs.
		/// </summary>
		internal static void Postfix(Workable workable) {
			if (workable != null)
				workable.gameObject.AddOrGet<FastReachabilityMonitor>();
		}
	}
}
