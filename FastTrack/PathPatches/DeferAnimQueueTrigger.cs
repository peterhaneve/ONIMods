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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Defers triggers that would occur during hazardous portions of the frame to a safer
	/// location.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class DeferAnimQueueTrigger : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static DeferAnimQueueTrigger Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void CreateInstance() {
			DestroyInstance();
			Instance = new DeferAnimQueueTrigger();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// Instead of immediately triggering the event, queues it for next frame.
		/// </summary>
		/// <param name="source">The controller triggering the event.</param>
		/// <param name="hash">The event hash to trigger.</param>
		/// <param name="data">The event parameter (if available).</param>
		internal static void TriggerAndQueue(KAnimControllerBase source, int hash,
				object data) {
			var inst = Instance;
			if (inst == null || source.overrideAnimFiles.Count > 0) {
				source.gameObject.Trigger(hash, data);
				if (source.destroyOnAnimComplete)
					source.DestroySelf();
			} else
				inst.Queue(source, hash, data);
		}

		/// <summary>
		/// The offsets which should be updated.
		/// </summary>
		private readonly ConcurrentQueue<UpdateOffset> offsetPending;

		/// <summary>
		/// The events which should be triggered. Only used on the foreground thread.
		/// </summary>
		private readonly Queue<TriggerEvent> animPending;

		private DeferAnimQueueTrigger() {
			offsetPending = new ConcurrentQueue<UpdateOffset>();
			animPending = new Queue<TriggerEvent>();
		}

		public void Dispose() {
			animPending.Clear();
		}

		/// <summary>
		/// Empties the queue of all pending anim events.
		/// </summary>
		internal void Process() {
			while (animPending.Count > 0) {
				var evt = animPending.Dequeue();
				var src = evt.source;
				if (src != null) {
					src.gameObject.Trigger(evt.hash, evt.data);
					// Destroy it now
					if (src.destroyOnAnimComplete)
						src.DestroySelf();
				}
			}
			while (offsetPending.TryDequeue(out UpdateOffset offset))
				offset.offsets.GetOffsets(offset.newCell);
		}

		/// <summary>
		/// Queues an event.
		/// </summary>
		/// <param name="source">The controller triggering the event.</param>
		/// <param name="hash">The event hash to trigger.</param>
		/// <param name="data">The event parameter (if available).</param>
		internal void Queue(KAnimControllerBase source, int hash, object data) {
			animPending.Enqueue(new TriggerEvent(source, hash, data));
		}

		/// <summary>
		/// Queues offsets to be updated synchronously. This is a fairly rare code path to
		/// avoid an assert.
		/// </summary>
		/// <param name="offsets">The offsets to update.</param>
		/// <param name="newCell">The new cell that the offsets occupy.</param>
		internal void Queue(OffsetTracker offsets, int newCell) {
			offsetPending.Enqueue(new UpdateOffset(offsets, newCell));
		}

		/// <summary>
		/// Saves information about events to trigger later.
		/// </summary>
		private struct TriggerEvent {
			/// <summary>
			/// The event parameter (if available).
			/// </summary>
			internal readonly object data;

			/// <summary>
			/// The event hash to trigger.
			/// </summary>
			internal readonly int hash;

			/// <summary>
			/// The controller triggering the event.
			/// </summary>
			internal readonly KAnimControllerBase source;

			public TriggerEvent(KAnimControllerBase source, int hash, object data) {
				this.data = data;
				this.hash = hash;
				this.source = source;
			}
		}

		/// <summary>
		/// Saves information about offsets to update later.
		/// </summary>
		private struct UpdateOffset {
			/// <summary>
			/// The new cell that the offsets will use.
			/// </summary>
			internal readonly int newCell;

			/// <summary>
			/// The offsets to update.
			/// </summary>
			internal readonly OffsetTracker offsets;

			public UpdateOffset(OffsetTracker offsets, int newCell) {
				this.newCell = newCell;
				this.offsets = offsets;
			}
		}
	}

	/// <summary>
	/// Applied to KBatchedAnimController to move all anim queue triggers to start of frame,
	/// as those could interfere with async pickups.
	/// </summary>
	[HarmonyPatch(typeof(KBatchedAnimController), nameof(KBatchedAnimController.TriggerStop))]
	public static class KBatchedAnimController_TriggerStop_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before TriggerStop runs.
		/// </summary>
		internal static bool Prefix(KBatchedAnimController __instance) {
			var anim = __instance.CurrentAnim;
			if (__instance.animQueue.Count > 0) {
				__instance.StartQueuedAnim();
			} else if (anim != null && __instance.mode == KAnim.PlayMode.Once) {
				__instance.currentFrame = anim.numFrames - 1;
				__instance.Stop();
				DeferAnimQueueTrigger.TriggerAndQueue(__instance, (int)GameHashes.
					AnimQueueComplete, null);
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to OffsetTracker to make it defer offset computations if they occur during
	/// unsafe times instead of asserting.
	/// </summary>
	[HarmonyPatch(typeof(OffsetTracker), nameof(OffsetTracker.GetOffsets))]
	public static class OffsetTracker_GetOffsets_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before GetOffsets runs.
		/// </summary>
		internal static bool Prefix(OffsetTracker __instance, int current_cell,
				ref CellOffset[] __result) {
			var offsets = __instance.offsets;
			int lastCell = __instance.previousCell;
			if (current_cell != lastCell) {
				if (OffsetTracker.isExecutingWithinJob)
					DeferAnimQueueTrigger.Instance.Queue(__instance, current_cell);
				else {
					__instance.UpdateCell(lastCell, current_cell);
					__instance.previousCell = lastCell = current_cell;
				}
			}
			if (offsets == null)
				lock (__instance) {
					__instance.UpdateOffsets(lastCell);
					offsets = __instance.offsets;
				}
			__result = offsets;
			return false;
		}
	}
}
