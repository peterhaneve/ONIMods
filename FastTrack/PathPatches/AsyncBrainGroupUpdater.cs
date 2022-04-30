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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using BrainPair = System.Collections.Generic.KeyValuePair<Brain, Navigator>;
using Fetch = GlobalChoreProvider.Fetch;
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
		/// Whether a dangerous but very fast optimization to avoid list copies is used.
		/// </summary>
		internal static bool AllowFastListSwap;

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
		/// A singleton instance of FinishFetchesWork to avoid allocation.
		/// </summary>
		private readonly FinishFetchesWork finishFetches;

		/// <summary>
		/// Fired when it is safe to mutate the offset tables off the main thread.
		/// </summary>
		private readonly EventWaitHandle onFetchComplete;

		/// <summary>
		/// A singleton instance of UpdateOffsetTablesWork to avoid allocation.
		/// </summary>
		private readonly UpdateOffsetTablesWork updateOffsets;

		/// <summary>
		/// Contains the list of all pickup jobs that are currently running.
		/// </summary>
		private readonly List<CompilePickupsWork> updatingPickups;

		private AsyncBrainGroupUpdater() {
			brainsToUpdate = new List<BrainPair>(32);
			byId = new List<FetchablesByPrefabId>(64);
			finishFetches = new FinishFetchesWork(this);
			onFetchComplete = new AutoResetEvent(false);
			// Must be initialized after byId
			updateOffsets = new UpdateOffsetTablesWork(this);
			updatingPickups = new List<CompilePickupsWork>(8);
		}

		/// <summary>
		/// Adds a brain to update asynchronously.
		/// </summary>
		/// <param name="brain">The rover brain to update.</param>
		internal void AddBrain(CreatureBrain brain) {
			if (brain.TryGetComponent(out Navigator nav))
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
		/// Cleans up any pickup jobs that are still running.
		/// </summary>
		private void Cleanup() {
			foreach (var entry in updatingPickups)
				entry.Cleanup();
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
			if (fm != null) {
				var toUpdate = updatingPickups;
				int n = fm.prefabIdToFetchables.Count, b = brainsToUpdate.Count, have =
					toUpdate.Count;
				for (int i = 0; i < b; i++) {
					var brain = brainsToUpdate[i];
					if (i < have)
						toUpdate[i].Begin(brain.Key, brain.Value, n);
					else {
						// Add new entry
						var entry = new CompilePickupsWork(this);
						entry.Begin(brain.Key, brain.Value, n);
						toUpdate.Add(entry);
					}
				}
			}
		}

		/// <summary>
		/// Ends a Duplicant and rover brain update cycle.
		/// </summary>
		internal void EndBrainUpdate() {
			var fm = Game.Instance.fetchManager;
			var inst = GlobalChoreProvider.Instance;
			int n = brainsToUpdate.Count;
			if (n > 0) {
				// Wait out the pickups update - GC pauses always seem to occur during this
				// time?
				bool updated = onFetchComplete.WaitAndMeasure(FastTrackMod.MAX_TIMEOUT, 1000),
					quickSwap = AllowFastListSwap;
				if (!updated)
					PUtil.LogWarning("Fetch updates did not complete within the timeout!");
				if (fm != null && inst != null && updated) {
					var fetches = inst.fetches;
					var pickups = fm.pickups;
					for (int i = 0; i < n; i++) {
						var entry = updatingPickups[i];
						if (quickSwap) {
							// Danger Will Robinson!
							inst.fetches = entry.fetches;
							fm.pickups = entry.pickups;
						} else {
							fetches.Clear();
							fetches.AddRange(entry.fetches);
							pickups.Clear();
							pickups.AddRange(entry.pickups);
						}
						// Calls into Sensors, but Pickupable and PathProber were bypassed
						entry.brain.UpdateBrain();
						entry.Cleanup();
					}
					if (quickSwap) {
						fm.pickups = pickups;
						inst.fetches = fetches;
					}
				} else
					Cleanup();
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
				int n = brainsToUpdate.Count;
				if (n > 0) {
					onFetchComplete.Reset();
					foreach (var pair in fm.prefabIdToFetchables)
						byId.Add(pair.Value);
					inst.Run(updateOffsets);
					for (int i = 0; i < n; i++)
						inst.Run(updatingPickups[i]);
				}
				// This will not start until all the updatingPickups are completed
				finishFetches.Begin(n);
				inst.Run(finishFetches);
			}
		}

		/// <summary>
		/// A more efficient (slightly) version of GlobalChoreProber.UpdateFetches.
		/// </summary>
		/// <param name="navigator">The navigator that is fetching items.</param>
		/// <param name="fetches">The location where the errands will be populated.</param>
		private void UpdateFetches(Navigator navigator, List<Fetch> fetches) {
			var chores = GlobalChoreProvider.Instance?.fetchChores;
			fetches.Clear();
			if (chores != null) {
				Storage destination;
				int cost, n = chores.Count;
				for (int i = 0; i < n; i++) {
					var fetchChore = chores[i];
					// Not already taken, allows manual use
					if (fetchChore.driver == null && (fetchChore.automatable == null ||
							!fetchChore.automatable.GetAutomationOnly()) && (destination =
							fetchChore.destination) != null && (cost = navigator.
							GetNavigationCost(destination)) >= 0)
						fetches.Add(new Fetch {
							category = destination.fetchCategory, chore = fetchChore,
							cost = cost, priority = fetchChore.masterPriority, tagBitsHash =
							fetchChore.tagBitsHash
						});
				}
				fetches.Sort(FetchComparer.Instance);
			}
		}

		/// <summary>
		/// In parallel, compiles debris pickups on the background job queue.
		/// </summary>
		private sealed class CompilePickupsWork : AsyncJobManager.IWork, IWorkItemCollection {
			public int Count { get; private set; }

			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The brain to update when this task is done.
			/// </summary>
			internal Brain brain;

			/// <summary>
			/// The location where the compiled fetch errands are stored.
			/// </summary>
			internal readonly List<Fetch> fetches;

			/// <summary>
			/// The Duplicant navigator that is trying to pick up items.
			/// </summary>
			internal Navigator navigator;

			/// <summary>
			/// The location where the compiled fetch errands are stored.
			/// </summary>
			internal readonly List<Pickup> pickups;

			/// <summary>
			/// The parent object to notify when this completes.
			/// </summary>
			private readonly AsyncBrainGroupUpdater updater;

			/// <summary>
			/// The worker that is trying to pick up items.
			/// </summary>
			private GameObject worker;

			internal CompilePickupsWork(AsyncBrainGroupUpdater updater) {
				fetches = new List<Fetch>(64);
				pickups = new List<Pickup>(128);
				this.updater = updater;
			}

			/// <summary>
			/// Initializes the brain to be updated. This saves memory over reallocating new
			/// instances every frame.
			/// </summary>
			/// <param name="brain">The brain to update.</param>
			/// <param name="navigator">The navigator to compute paths for this brain.</param>
			/// <param name="n">The number of pickup prefab IDs to be updated.</param>
			public void Begin(Brain brain, Navigator navigator, int n) {
				Count = n;
				this.brain = brain;
				this.navigator = navigator;
				worker = navigator.gameObject;
			}

			/// <summary>
			/// Clears the pickup and fetch lists of items that accumulated from the last
			/// frame.
			/// </summary>
			public void Cleanup() {
				fetches.Clear();
				pickups.Clear();
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
				var byId = updater.byId;
				int n = byId.Count;
				pickups.Clear();
				for (int i = 0; i < n; i++)
					pickups.AddRange(byId[i].finalPickups);
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
			public int Count { get; private set; }

			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The parent object to notify when this completes.
			/// </summary>
			private readonly AsyncBrainGroupUpdater updater;

			internal FinishFetchesWork(AsyncBrainGroupUpdater updater) {
				this.updater = updater;
			}

			/// <summary>
			/// Initializes the number of fetch errand updates to perform.
			/// </summary>
			/// <param name="count">The number of updates to run (one per active Duplicant brain).</param>
			public void Begin(int count) {
				if (count < 0)
					throw new ArgumentOutOfRangeException(nameof(count));
				Count = count;
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
					byId[index].UpdateOffsetTables();
			}

			public void TriggerAbort() {
				updater.FinishFetches();
			}

			public void TriggerComplete() { }

			public void TriggerStart() { }
		}

		/// <summary>
		/// Compares fetchable items by priority and path cost.
		/// </summary>
		private sealed class FetchComparer : IComparer<Fetch> {
			/// <summary>
			/// The singleton instance of this class.
			/// </summary>
			internal static readonly IComparer<Fetch> Instance = new FetchComparer();

			private FetchComparer() { }

			public int Compare(Fetch x, Fetch y) {
				int result = y.chore.masterPriority.CompareTo(x.chore.masterPriority);
				if (result == 0)
					result = x.cost.CompareTo(y.cost);
				return result;
			}
		}
	}
}
