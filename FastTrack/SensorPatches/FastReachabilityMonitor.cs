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
using UnityEngine;

using RMI = ReachabilityMonitor.Instance;
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
		// Avoid allocating closures by using a shared Action
		private static readonly System.Action<object> ON_MOVED = (target) => {
			if (target is FastReachabilityMonitor rm)
				rm.UpdateOffsets();
		};

		/// <summary>
		/// The event subscription ID when the object moves.
		/// </summary>
		private int cellChangedHash;

		/// <summary>
		/// The event subscription ID when the object lands.
		/// </summary>
		private int landedHash;

		/// <summary>
		/// The last set of extents from which this item was reachable.
		/// </summary>
		private Extents lastExtents;

		/// <summary>
		/// The target game object to monitor.
		/// </summary>
		private GameObject master;

		/// <summary>
		/// Registers a scene partitioner entry for nav cells changing.
		/// </summary>
		private ThreadsafePartitionerEntry partitionerEntry;

		/// <summary>
		/// The existing reachability monitor used to check and trigger events.
		/// </summary>
		private RMI smi;

		internal FastReachabilityMonitor() {
			cellChangedHash = -1;
			landedHash = -1;
			lastExtents = new Extents(int.MinValue, int.MinValue, 1, 1);
			master = null;
			partitionerEntry = null;
		}

		public override void OnCleanUp() {
			if (partitionerEntry != null) {
				partitionerEntry.Dispose();
				partitionerEntry = null;
			}
			if (master != null) {
				master.Unsubscribe(landedHash);
				master.Unsubscribe(cellChangedHash);
			}
			base.OnCleanUp();
		}

		/// <summary>
		/// Only updates reachability if nav grid cells update nearby.
		/// </summary>
		private void OnReachableChanged(object _) {
			FastGroupProber.Instance?.Enqueue(smi);
		}

		public override void OnSpawn() {
			Workable workable;
			base.OnSpawn();
			if (TryGetComponent(out StateMachineController smc) && (smi = smc.GetSMI<RMI>()) !=
					null && (workable = smi.master) != null) {
				master = workable.gameObject;
				// It looks like Klei frequently forgets to set the isMovable flag on anims
				// that can move anyways. Please!
				cellChangedHash = master.Subscribe((int)GameHashes.CellChanged, ON_MOVED);
				landedHash = master.Subscribe((int)GameHashes.Landed, ON_MOVED);
				UpdateOffsets();
				FastGroupProber.Instance?.Enqueue(smi);
			} else {
				master = null;
			}
		}

		public void Sim4000ms(float _) {
			UpdateOffsets();
		}

		/// <summary>
		/// Updates the scene partitioner entry if the offsets change.
		/// </summary>
		public void UpdateOffsets() {
			var inst = FastGroupProber.Instance;
			if (inst != null && master != null) {
				int cell = Grid.PosToCell(master.transform.position);
				if (Grid.IsValidCell(cell) && cell > 0) {
					var extents = new Extents(cell, smi.master.GetOffsets(cell));
					// Only if the extents actually changed
					if (extents.x != lastExtents.x || extents.y != lastExtents.y || extents.
							width != lastExtents.width || extents.height != lastExtents.
							height) {
						if (partitionerEntry != null)
							partitionerEntry.UpdatePosition(extents);
						else
							partitionerEntry = inst.Mask.Add(extents, this,
								OnReachableChanged);
						inst.Enqueue(smi);
						lastExtents = extents;
					}
				} else if (lastExtents.x >= 0 || lastExtents.y >= 0) {
					// Payloads and worn items are sometimes moved to (0, 0) or cell -1
					if (partitionerEntry != null) {
						partitionerEntry.Dispose();
						partitionerEntry = null;
					}
					smi.sm.isReachable.Set(false, smi);
					lastExtents.x = int.MinValue;
					lastExtents.y = int.MinValue;
					lastExtents.width = 1;
					lastExtents.height = 1;
				}
			}
		}
	}
	
	/// <summary>
	/// Applied to ReachabilityMonitor to turn off periodic checks for reachability.
	/// </summary>
	[HarmonyPatch(typeof(ReachabilityMonitor), nameof(ReachabilityMonitor.InitializeStates))]
	public static class ReachabilityMonitor_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Transpiles InitializeStates to remove the FastUpdate updater.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref StateMachine.BaseState default_state,
				ReachabilityMonitor __instance) {
			// Existing states
			ReachabilityMonitorState reachable = __instance.reachable, unreachable =
				__instance.unreachable;
			var param = __instance.isReachable;
			// New state: avoid an extra event on startup by only transitioning after the
			// first successful reachability update
			var tbd = __instance.CreateState("tbd");
			default_state = tbd;
			__instance.serializable = StateMachine.SerializeType.Never;
			tbd.ParamTransition(param, unreachable, ReachabilityMonitor.IsFalse).
				ParamTransition(param, reachable, ReachabilityMonitor.IsTrue);
			reachable.ToggleTag(GameTags.Reachable).
				Enter("TriggerEvent", TriggerEvent).
				ParamTransition(param, unreachable, ReachabilityMonitor.IsFalse);
			unreachable.Enter("TriggerEvent", TriggerEvent).
				ParamTransition(param, reachable, ReachabilityMonitor.IsTrue);
			return false;
		}

		/// <summary>
		/// Triggers an event when reachability changes.
		/// </summary>
		/// <param name="smi">The reachability monitor whose state changed.</param>
		private static void TriggerEvent(RMI smi) {
			smi.TriggerEvent();
		}
	}

	/// <summary>
	/// Applied to ReachabilityMonitor.Instance to add a fast reachability monitor on each
	/// object that needs reachability checks.
	/// </summary>
	[HarmonyPatch(typeof(RMI), MethodType.Constructor, typeof(Workable))]
	public static class ReachabilityMonitor_Instance_Constructor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Adds a fast reachability monitor to the target workable, but does not immediately
		/// check for reachability, instead queuing it for a check on the group prober.
		/// </summary>
		/// <param name="smi">The existing reachability monitor state.</param>
		private static void UpdateReachability(RMI smi) {
			var workable = smi.master;
			if (workable != null)
				workable.gameObject.AddOrGet<FastReachabilityMonitor>();
		}

		/// <summary>
		/// Transpiles the constructor to enqueue the item onto the reachability queue instead
		/// of checking immediately (where, on load, the result would be invalid).
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(RMI).GetMethodSafe(
				nameof(RMI.UpdateReachability), false),
				typeof(ReachabilityMonitor_Instance_Constructor_Patch).GetMethodSafe(nameof(
				UpdateReachability), true, typeof(RMI)));
		}
	}
}
