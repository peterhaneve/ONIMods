/*
 * Copyright 2023 Peter Han
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
using Klei.AI;
using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using AmountInstanceBucket = UpdateBucketWithUpdater<ISim200ms>.Entry;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A singleton class that updates Amount instances in the background quickly.
	/// </summary>
	internal sealed class AsyncAmountsUpdater : AsyncJobManager.IWork, IWorkItemCollection,
			IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static AsyncAmountsUpdater Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void CreateInstance() {
			DestroyInstance();
			Instance = new AsyncAmountsUpdater();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		public int Count { get; }

		public IWorkItemCollection Jobs => this;

		/// <summary>
		/// Stores the full list of amounts from the batch updater.
		/// </summary>
		private IList<AmountInstanceBucket> allAmounts;

		/// <summary>
		/// The time delta elapsed since the last update.
		/// </summary>
		private float deltaTime;

		/// <summary>
		/// Fired when the sweepers are all updated.
		/// </summary>
		private readonly EventWaitHandle onComplete;

		/// <summary>
		/// Stores the amounts that actually changed.
		/// </summary>
		private readonly ConcurrentQueue<AmountUpdated> results;

		/// <summary>
		/// Queues slices of the job list fairly.
		/// </summary>
		private readonly RangeInt[] slices;

		private AsyncAmountsUpdater() {
			deltaTime = 0.0f;
			onComplete = new AutoResetEvent(false);
			Count = AsyncJobManager.Instance.ThreadCount;
			results = new ConcurrentQueue<AmountUpdated>();
			slices = new RangeInt[Count];
		}

		/// <summary>
		/// Starts updating all amounts.
		/// </summary>
		/// <param name="entries">The amount instances to update.</param>
		/// <param name="dt">The time that has passed since the last update.</param>
		internal void BatchUpdate(IList<AmountInstanceBucket> entries, float dt) {
			var inst = AsyncJobManager.Instance;
			int n = entries.Count;
			if (inst != null && n > 0 && dt > 0.0f) {
				// In case multiple updates get run in one frame, finish the previous one before
				// starting a new one
				Finish();
				allAmounts = entries;
				int perBucketInt = n / Count;
				int cutoff = n - Count * perBucketInt, index = 0;
				deltaTime = dt;
				// CFQ
				for (int i = 0; i < Count; i++) {
					int bin = perBucketInt + ((i < cutoff) ? 1 : 0);
					slices[i] = new RangeInt(index, bin);
					index += bin;
				}
				onComplete.Reset();
				inst.Run(this);
				Finish();
			} else {
				allAmounts = null;
				deltaTime = 0.0f;
			}
		}

		public void Dispose() {
			onComplete.Dispose();
		}

		/// <summary>
		/// Waits for the job to complete, then posts the update to the main thread.
		/// </summary>
		internal void Finish() {
			if (deltaTime > 0.0f && AsyncJobManager.Instance != null) {
				if (!onComplete.WaitOne(FastTrackMod.MAX_TIMEOUT))
					PUtil.LogWarning("Unable to post Amounts updates within the timeout!");
				// Make best effort even if the amounts did not post in time
				while (results.TryDequeue(out var result))
					result.instance.Publish(result.delta, result.lastValue);
				deltaTime = 0.0f;
				allAmounts = null;
			}
		}

		public void InternalDoWorkItem(int index) {
			float dt = deltaTime;
			if (index >= 0 && index < slices.Length && allAmounts != null && dt > 0.0f) {
				var range = slices[index];
				int n = range.length;
				if (n > 0) {
					int start = range.start, end = start + n;
					float delta;
					for (int i = start; i < end; i++)
						if (allAmounts[i].data is AmountInstance instance && (delta = instance.
								GetDelta() * dt) != 0.0f) {
							float lastValue = instance.value;
							instance.SetValue(lastValue + delta);
							results.Enqueue(new AmountUpdated(instance, delta, lastValue));
						}
				}
			}
		}

		public void TriggerAbort() {
			onComplete.Set();
		}

		public void TriggerComplete() {
			onComplete.Set();
		}

		public void TriggerStart() { }

		/// <summary>
		/// Stores the results of amount updates.
		/// </summary>
		private readonly struct AmountUpdated {
			/// <summary>
			/// The total change.
			/// </summary>
			internal readonly float delta;

			/// <summary>
			/// The amount instance that changed.
			/// </summary>
			internal readonly AmountInstance instance;

			/// <summary>
			/// The value before the update.
			/// </summary>
			internal readonly float lastValue;

			public AmountUpdated(AmountInstance instance, float delta, float lastValue) {
				this.delta = delta;
				this.instance = instance;
				this.lastValue = lastValue;
			}

			public override string ToString() {
				return "{0}: {1:F2} to {2:F2} ({3:F2})".F(instance.name, lastValue, instance.
					value, delta);
			}
		}
	}

	/// <summary>
	/// Applied to AmountInstance to handle the batch updating ourselves in the background.
	/// </summary>
	[HarmonyPatch(typeof(AmountInstance), nameof(AmountInstance.BatchUpdate))]
	public static class AmountInstance_BatchUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before BatchUpdate runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(List<AmountInstanceBucket> amount_instances,
				float time_delta) {
			var inst = AsyncAmountsUpdater.Instance;
			bool run = inst == null;
			if (!run)
				inst.BatchUpdate(amount_instances, time_delta);
			return run;
		}
	}
}
