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
		/// <param name="source">The object triggering the event.</param>
		/// <param name="hash">The event hash to trigger.</param>
		/// <param name="data">The event parameter (if available).</param>
		internal static void TriggerAndQueue(GameObject source, int hash, object data) {
			var inst = Instance;
			if (inst == null)
				source.Trigger(hash, data);
			else
				inst.Queue(source, hash, data);
		}

		/// <summary>
		/// The events which should be triggered. Only used on the foreground thread.
		/// </summary>
		private readonly Queue<TriggerEvent> pending;

		private DeferAnimQueueTrigger() {
			pending = new Queue<TriggerEvent>();
		}

		public void Dispose() {
			pending.Clear();
		}

		/// <summary>
		/// Empties the queue of all pending anim events.
		/// </summary>
		internal void Process() {
			while (pending.Count > 0) {
				var evt = pending.Dequeue();
				var src = evt.source;
				if (src != null)
					src.Trigger(evt.hash, evt.data);
			}
		}

		/// <summary>
		/// Queues an event.
		/// </summary>
		/// <param name="source">The object triggering the event.</param>
		/// <param name="hash">The event hash to trigger.</param>
		/// <param name="data">The event parameter (if available).</param>
		internal void Queue(GameObject source, int hash, object data) {
			pending.Enqueue(new TriggerEvent(source, hash, data));
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
			/// The object triggering the event.
			/// </summary>
			internal readonly GameObject source;

			public TriggerEvent(GameObject source, int hash, object data) {
				this.data = data;
				this.hash = hash;
				this.source = source;
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
		/// Transpiles TriggerStop to instead queue up the event until the start of the next
		/// frame.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCall(instructions, typeof(EventExtensions).
				GetMethodSafe(nameof(EventExtensions.Trigger), true, typeof(GameObject),
				typeof(int), typeof(object)), typeof(DeferAnimQueueTrigger).GetMethodSafe(
				nameof(DeferAnimQueueTrigger.TriggerAndQueue), true, typeof(GameObject),
				typeof(int), typeof(object)));
		}
	}
}
