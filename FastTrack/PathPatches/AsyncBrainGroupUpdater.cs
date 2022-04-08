﻿/*
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

using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using BrainPair = System.Collections.Generic.KeyValuePair<Brain, Navigator>;
using FetchablesByPrefabId = FetchManager.FetchablesByPrefabId;
using Pickup = FetchManager.Pickup;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Wraps the Duplicant brain group with a singleton that allows smart and threaded updates
	/// to pathing and sensors.
	/// </summary>
	public sealed class AsyncBrainGroupUpdater : IDisposable {
		/// <summary>
		/// The singleton (if any) instance of this class.
		/// </summary>
		public static AsyncBrainGroupUpdater Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance.
		/// </summary>
		internal static void CreateInstance() {
			Instance?.Dispose();
			Instance = new AsyncBrainGroupUpdater();
		}

		/// <summary>
		/// Destroys and cleans up the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The brains to update asynchronously.
		/// </summary>
		private readonly IList<BrainPair> brainsToUpdate;

		/// <summary>
		/// The list of fetchables collected by prefab ID.
		/// </summary>
		private readonly IList<FetchablesByPrefabId> byId;

		/// <summary>
		/// The list of destinations (pickupables, workables, etc) that need offset table
		/// updates.
		/// </summary>
		private readonly ConcurrentQueue<Workable> needOffsetUpdate;

		/// <summary>
		/// Fired when it is safe to mutate the offset tables off the main thread.
		/// </summary>
		private readonly EventWaitHandle onFetchComplete;

		/// <summary>
		/// Contains the list of all pickup jobs that are currently running.
		/// </summary>
		private readonly IList<CompilePickupsWork> updatingPickups;

		private AsyncBrainGroupUpdater() {
			brainsToUpdate = new List<BrainPair>(32);
			byId = new List<FetchablesByPrefabId>(64);
			needOffsetUpdate = new ConcurrentQueue<Workable>();
			onFetchComplete = new AutoResetEvent(false);
			updatingPickups = new List<CompilePickupsWork>(8);
		}

		/// <summary>
		/// Adds a brain to update asynchronously.
		/// </summary>
		/// <param name="brain">The rover brain to update.</param>
		internal void AddBrain(CreatureBrain brain) {
			var nav = brain.GetComponent<Navigator>();
			if (nav != null)
				// What PathProberSensor did
				nav.UpdateProbe(false);
			brainsToUpdate.Add(new BrainPair(brain, nav));
		}

		/// <summary>
		/// Adds a brain to update asynchronously.
		/// </summary>
		/// <param name="brain">The Duplicant brain to update.</param>
		internal void AddBrain(MinionBrain brain) {
			var nav = brain.Navigator;
			if (nav != null)
				// What PathProberSensor did
				nav.UpdateProbe(false);
			brainsToUpdate.Add(new BrainPair(brain, nav));
		}

		/// <summary>
		/// Cleans up any pickup jobs that are still running, as they leak memory if not
		/// disposed.
		/// </summary>
		private void Cleanup() {
			foreach (var entry in updatingPickups)
				entry.Dispose();
		}

		public void Dispose() {
			FinishFetches();
			Cleanup();
			brainsToUpdate.Clear();
			onFetchComplete.Dispose();
			byId.Clear();
			updatingPickups.Clear();
		}

		/// <summary>
		/// Ends collection of Duplicant and rover brains.
		/// </summary>
		internal void EndBrainCollect() {
			var fm = Game.Instance.fetchManager;
			updatingPickups.Clear();
			if (fm != null) {
				int n = fm.prefabIdToFetchables.Count;
				foreach (var brain in brainsToUpdate)
					updatingPickups.Add(new CompilePickupsWork(this, brain.Key, brain.Value,
						n));
			}
		}

		/// <summary>
		/// Ends a Duplicant and rover brain update cycle.
		/// </summary>
		internal void EndBrainUpdate() {
			var fm = Game.Instance.fetchManager;
			var inst = GlobalChoreProvider.Instance;
			if (updatingPickups.Count > 0) {
				// Wait out the pickups update
				bool updated = onFetchComplete.WaitOne(FastTrackMod.MAX_TIMEOUT);
				if (!updated)
					PUtil.LogWarning("Fetch updates did not complete within the timeout!");
				if (fm != null && inst != null && updated) {
					var fetches = inst.fetches;
					var pickups = fm.pickups;
					foreach (var entry in updatingPickups) {
						// Copy fetch list
						fetches.Clear();
						foreach (var fetchInfo in entry.fetches)
							fetches.Add(new GlobalChoreProvider.Fetch {
								category = fetchInfo.category, chore = fetchInfo.chore,
								cost = fetchInfo.cost, priority = fetchInfo.chore.
								masterPriority, tagBitsHash = fetchInfo.tagBitsHash
							});
						// Copy pickup list
						pickups.Clear();
						pickups.AddRange(entry.pickups);
						entry.Dispose();
						// Calls into Sensors, but Pickupable and PathProber were bypassed
						entry.brain.UpdateBrain();
					}
				} else
					Cleanup();
				updatingPickups.Clear();
				// Update anything that needs an offset update
				while (needOffsetUpdate.TryDequeue(out Workable workable))
					workable.GetOffsets();
			}
			byId.Clear();
		}

		/// <summary>
		/// Called when all fetches are up to date.
		/// </summary>
		private void FinishFetches() {
			onFetchComplete.Set();
		}

		/// <summary>
		/// Starts a Duplicant and rover brain update cycle.
		/// </summary>
		internal void StartBrainCollect() {
			int n = updatingPickups.Count;
			if (n > 0) {
				PUtil.LogWarning("{0:D} pickup collection jobs did not finish in time!".F(n));
				Cleanup();
			}
			brainsToUpdate.Clear();
		}

		/// <summary>
		/// Releases the task to collect fetch errands, which should only be run during
		/// kanim updates (other RenderEveryTicks apparently can update pickupables!)
		/// </summary>
		internal void StartBrainUpdate() {
			var fm = Game.Instance.fetchManager;
			var inst = AsyncJobManager.Instance;
			if (inst != null && fm != null) {
				if (updatingPickups.Count > 0) {
					onFetchComplete.Reset();
					foreach (var pair in fm.prefabIdToFetchables)
						byId.Add(pair.Value);
					inst.Run(new UpdateOffsetTablesWork(this));
					foreach (var task in updatingPickups)
						inst.Run(task);
				}
				// This will not start until all the updatingPickups are completed
				inst.Run(new FinishFetchesWork(this));
			}
		}

		/// <summary>
		/// A more efficient (slightly) version of GlobalChoreProber.UpdateFetches.
		/// </summary>
		/// <param name="navigator">The navigator that is fetching items.</param>
		/// <param name="fetches">The location where the errands will be populated.</param>
		private void UpdateFetches(Navigator navigator, List<FetchInfo> fetches) {
			var gcp = GlobalChoreProvider.Instance;
			fetches.Clear();
			if (gcp != null) {
				Storage destination;
				foreach (var fetchChore in gcp.fetchChores)
					// Not already taken, allows manual use
					if (fetchChore.driver == null && (fetchChore.automatable == null ||
							!fetchChore.automatable.GetAutomationOnly()) && (destination =
							fetchChore.destination) != null) {
						// If storage needs offsets updated, queue it up
						if (navigator.GetNavigationCostNU(destination, destination.GetCell(),
								out int cost))
							needOffsetUpdate.Enqueue(destination);
						if (cost >= 0)
							fetches.Add(new FetchInfo(fetchChore, cost, destination));
					}
				fetches.Sort();
			}
		}

		/// <summary>
		/// Updates the offset tables in a thread-safe way, enqueuing any that need to be
		/// changed onto the queue to get their SCPs updated later.
		/// </summary>
		/// <param name="fetchables">The items whose tables need updating.</param>
		private void UpdateOffsetTables(List<FetchManager.Fetchable> fetchables) {
			// Fetching the count over and over again is required here in the uncommon case
			// of a fetchable being removed so late in the frame
			for (int i = 0; i < fetchables.Count; i++) {
				var pickupable = fetchables[i].pickupable;
				pickupable.GetOffsets(pickupable.cachedCell);
			}
		}

		/// <summary>
		/// In parallel, compiles debris pickups on the background job queue.
		/// </summary>
		private sealed class CompilePickupsWork : AsyncJobManager.IWork, IWorkItemCollection,
				IDisposable {
			public int Count { get; }

			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The brain to update when this task is done.
			/// </summary>
			internal readonly Brain brain;

			/// <summary>
			/// The location where the compiled fetch errands are stored.
			/// </summary>
			internal readonly ListPool<FetchInfo, CompilePickupsWork>.PooledList fetches;

			/// <summary>
			/// The Duplicant navigator that is trying to pick up items.
			/// </summary>
			internal readonly Navigator navigator;

			/// <summary>
			/// The location where the compiled fetch errands are stored.
			/// </summary>
			internal readonly ListPool<Pickup, CompilePickupsWork>.PooledList pickups;

			/// <summary>
			/// The parent object to notify when this completes.
			/// </summary>
			private readonly AsyncBrainGroupUpdater updater;

			/// <summary>
			/// The worker that is trying to pick up items.
			/// </summary>
			private readonly GameObject worker;

			internal CompilePickupsWork(AsyncBrainGroupUpdater updater, Brain brain,
					Navigator navigator, int n) {
				this.brain = brain;
				Count = n;
				fetches = ListPool<FetchInfo, CompilePickupsWork>.Allocate();
				pickups = ListPool<Pickup, CompilePickupsWork>.Allocate();
				this.navigator = navigator;
				this.updater = updater;
				worker = navigator.gameObject;
			}

			public void Dispose() {
				fetches.Recycle();
				pickups.Recycle();
			}

			public void InternalDoWorkItem(int index) {
				if (index >= 0 && index < Count) {
					var thisPrefab = updater.byId[index];
					var solidArmUpdater = GamePatches.SolidTransferArmUpdater.Instance;
					thisPrefab.UpdatePickups(navigator.PathProber, navigator, worker);
					// Help out our poor transfer arms in need
					if (solidArmUpdater != null)
						solidArmUpdater.UpdateCache(thisPrefab.fetchables.GetDataList());
				}
			}

			public void TriggerAbort() {
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerComplete() {
				pickups.Clear();
				foreach (var pair in updater.byId)
					pickups.AddRange(pair.finalPickups);
				pickups.Sort(FetchManager.ComparerNoPriority);
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerStart() {
				OffsetTracker.isExecutingWithinJob = true;
			}
		}

		/// <summary>
		/// Updates the fetch errands for all Duplicants that are queued.
		/// </summary>
		private sealed class FinishFetchesWork : AsyncJobManager.IWork, IWorkItemCollection {
			public int Count { get; }

			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The parent object to notify when this completes.
			/// </summary>
			private readonly AsyncBrainGroupUpdater updater;

			internal FinishFetchesWork(AsyncBrainGroupUpdater updater) {
				Count = updater.updatingPickups.Count;
				this.updater = updater;
			}

			public void InternalDoWorkItem(int index) {
				if (index >= 0 && index < Count) {
					var task = updater.updatingPickups[index];
					updater.UpdateFetches(task.navigator, task.fetches);
				}
			}

			public void TriggerAbort() {
				updater.FinishFetches();
			}

			public void TriggerComplete() {
				updater.FinishFetches();
			}

			public void TriggerStart() { }
		}

		/// <summary>
		/// In parallel, updates offset tables on the background job queue.
		/// </summary>
		private sealed class UpdateOffsetTablesWork : AsyncJobManager.IWork,
				IWorkItemCollection {
			public int Count => byId.Count;

			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The prefab IDs that will be updated.
			/// </summary>
			private readonly IList<FetchablesByPrefabId> byId;

			/// <summary>
			/// The parent object to notify when this completes.
			/// </summary>
			private readonly AsyncBrainGroupUpdater updater;

			internal UpdateOffsetTablesWork(AsyncBrainGroupUpdater updater) {
				byId = updater.byId;
				this.updater = updater;
			}

			public void InternalDoWorkItem(int index) {
				// Few offset tables should be updated here, as the offset tables are already
				// recalculated when the pickupable's cached cell is updated
				if (index >= 0 && index < byId.Count)
					updater.UpdateOffsetTables(byId[index].fetchables.GetDataList());
			}

			public void TriggerAbort() {
				updater.FinishFetches();
			}

			public void TriggerComplete() { }

			public void TriggerStart() { }
		}
	}
}