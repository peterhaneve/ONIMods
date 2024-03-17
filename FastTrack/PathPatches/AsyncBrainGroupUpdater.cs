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

using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using BrainPair = System.Collections.Generic.KeyValuePair<Brain, Navigator>;
using Fetch = GlobalChoreProvider.Fetch;
using FetchablesByPrefabId = FetchManager.FetchablesByPrefabId;
using Pickup = FetchManager.Pickup;
using SortedClearable = ClearableManager.SortedClearable;

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
		internal static bool allowFastListSwap;

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
		/// A non-mutating version of Navigator.GetNavigationCost that can be run on
		/// background threads.
		/// </summary>
		/// <param name="navigator">The navigator to calculate.</param>
		/// <param name="destination">The destination to find the cost.</param>
		/// <param name="cell">The workable's current cell.</param>
		/// <returns>The navigation cost to the destination.</returns>
		private static int GetNavigationCost(Navigator navigator, Workable destination,
				int cell) {
			CellOffset[] offsets = null;
			var offsetTracker = destination.offsetTracker;
			if (offsetTracker != null && (offsets = offsetTracker.offsets) == null) {
				offsetTracker.UpdateOffsets(cell);
				offsets = offsetTracker.offsets;
				if (offsetTracker.previousCell != cell)
					DeferredTriggers.Instance?.Queue(offsetTracker, cell);
			}
			return offsets == null ? navigator.GetNavigationCost(cell) : navigator.
				GetNavigationCost(cell, offsets);
		}
		
		/// <summary>
		/// Sorts the available Sweep errands.
		/// </summary>
		/// <param name="navigator">The consumer of the sweep chore.</param>
		/// <param name="clearableManager">The swept item manager to sort.</param>
		private static void SortClearables(Navigator navigator,
				ClearableManager clearableManager) {
			var sortedClearables = clearableManager.sortedClearables;
			var clearables = clearableManager.markedClearables.GetDataList();
			int n = clearables.Count;
			sortedClearables.Clear();
			for (int i = 0; i < n; i++) {
				var markedClearable = clearables[i];
				var pickupable = markedClearable.pickupable;
				int cost = pickupable.GetNavigationCost(navigator, pickupable.cachedCell);
				if (cost >= 0)
					sortedClearables.Add(new SortedClearable {
						pickupable = pickupable,
						masterPriority = markedClearable.prioritizable.GetMasterPriority(),
						cost = cost
					});
			}
			// Reuses Stock Bug Fix's transpile
			sortedClearables.Sort(SortedClearable.comparer);
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
		/// Since workable cells can no longer be obtained in the background, store the
		/// locations of fetchables here.
		/// </summary>
		private readonly ConcurrentDictionary<Workable, int> storageCells;

		private readonly IList<Workable> storageTemp;

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
			storageCells = new ConcurrentDictionary<Workable, int>(4, 256);
			storageTemp = new List<Workable>(256);
			// Must be initialized after byId
			updateOffsets = new UpdateOffsetTablesWork(this);
			updatingPickups = new List<CompilePickupsWork>(8);
		}

		/// <summary>
		/// Adds a brain to update asynchronously.
		/// </summary>
		/// <param name="brain">The rover or Duplicant brain to update.</param>
		internal void AddBrain(Brain brain) {
			Navigator nav;
			if (brain is MinionBrain mb)
				nav = mb.Navigator;
			else
				brain.TryGetComponent(out nav);
			if (nav != null) {
				// What PathProberSensor did
				nav.UpdateProbe();
				brainsToUpdate.Add(new BrainPair(brain, nav));
			}
		}

		/// <summary>
		/// Adds a fetch errand to the list, determining the target tile from the cache if
		/// possible.
		/// </summary>
		/// <param name="navigator">The navigator that is fetching items.</param>
		/// <param name="destination">The destination of the chore.</param>
		/// <param name="fetchChore">The chore to add.</param>
		/// <param name="fetches">The location where the errands will be populated.</param>
		private void AddFetch(Navigator navigator, Storage destination, FetchChore fetchChore,
				ICollection<Fetch> fetches) {
			// Get the storage cell from the cache
			if (storageCells.TryGetValue(destination, out int cell)) {
				int cost = GetNavigationCost(navigator, destination, cell);
				if (cost >= 0)
					fetches.Add(new Fetch {
						category = destination.fetchCategory, chore = fetchChore, cost = cost,
						priority = fetchChore.masterPriority, idsHash = fetchChore.
						tagsHash
					});
			} else
				storageCells.TryAdd(destination, Grid.InvalidCell);
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
			storageCells.Clear();
			updatingPickups.Clear();
		}

		/// <summary>
		/// Ends collection of Duplicant and rover brains.
		/// </summary>
		internal void EndBrainCollect() {
			var fm = Game.Instance.fetchManager;
			if (fm != null) {
				int n = fm.prefabIdToFetchables.Count, b = brainsToUpdate.Count, have =
					updatingPickups.Count;
				for (int i = 0; i < b; i++) {
					var brain = brainsToUpdate[i];
					if (i < have)
						updatingPickups[i].Begin(brain.Key, brain.Value, n, i == 0);
					else {
						// Add new entry
						var entry = new CompilePickupsWork(this);
						entry.Begin(brain.Key, brain.Value, n, i == 0);
						updatingPickups.Add(entry);
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
			// Wait out the pickups update - GC pauses always seem to occur during this time?
			bool updated = onFetchComplete.WaitAndMeasure(FastTrackMod.MAX_TIMEOUT, 1000);
			if (!updated)
				PUtil.LogWarning("Fetch updates did not complete within the timeout!");
			if (n > 0) {
				if (fm != null && inst != null && updated) {
					bool quickSwap = allowFastListSwap;
					var cm = inst.clearableManager;
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
						if (cm != null)
							SortClearables(entry.navigator, cm);
						// Calls into Sensors, but Pickupable and PathProber were bypassed
						entry.brain.UpdateBrain();
						entry.Cleanup();
					}
					if (quickSwap) {
						fm.pickups = pickups;
						inst.fetches = fetches;
					}
					UpdateStorageCells();
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
		/// Removes a storage from the cache.
		/// </summary>
		/// <param name="storage">The storage to remove.</param>
		internal void RemoveStorage(Workable storage) {
			if (storage != null)
				storageCells.TryRemove(storage, out _);
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
			var sau = GamePatches.SolidTransferArmUpdater.Instance;
			if (inst != null && fm != null) {
				int n = brainsToUpdate.Count;
				onFetchComplete.Reset();
				foreach (var pair in fm.prefabIdToFetchables)
					byId.Add(pair.Value);
				inst.Run(updateOffsets);
				if (n > 0) {
					// Wipe out cached items from the last run if still present (no sweepers
					// built, or game is paused)
					sau?.ClearCached();
					for (int i = 0; i < n; i++)
						inst.Run(updatingPickups[i]);
				} else if (sau != null) {
					sau.ClearCached();
					// If there are no Duplicants, run sweeper arms manually
					inst.Run(new GraveyardShift(this));
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
		/// <param name="chores">The eligible fetch chores.</param>
		/// <param name="fetches">The location where the errands will be populated.</param>
		private void UpdateFetches(Navigator navigator, IList<FetchChore> chores,
				List<Fetch> fetches) {
			fetches.Clear();
			if (chores != null) {
				int n = chores.Count;
				for (int i = 0; i < n; i++) {
					var fetchChore = chores[i];
					// Not already taken, allows manual use
					Storage destination;
					if (fetchChore.driver == null && (fetchChore.automatable == null ||
							!fetchChore.automatable.GetAutomationOnly()) && (destination =
							fetchChore.destination) != null)
						AddFetch(navigator, destination, fetchChore, fetches);
				}
				fetches.Sort(FetchComparer.Instance);
			}
		}

		/// <summary>
		/// Updates the cached cells of storage destinations that have been requested for a
		/// fetch chore at least once.
		/// </summary>
		private void UpdateStorageCells() {
			foreach (var pair in storageCells) {
				var storage = pair.Key;
				if (storage != null)
					storageTemp.Add(storage);
			}
			storageCells.Clear();
			int n = storageTemp.Count;
			for (int i = 0; i < n; i++) {
				var storage = storageTemp[i];
				int cell = Grid.PosToCell(storage.transform.position);
				storageCells.TryAdd(storage, cell);
				storage.GetOffsets(cell);
			}
			storageTemp.Clear();
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
			/// The eligible fetch chores for this navigator.
			/// </summary>
			internal IList<FetchChore> fetchChores;

			/// <summary>
			/// The location where the compiled fetch errands are stored.
			/// </summary>
			internal readonly List<Fetch> fetches;

			/// <summary>
			/// The Duplicant navigator that is trying to pick up items.
			/// </summary>
			internal Navigator navigator;
			
			/// <summary>
			/// If true, this task also updates Auto-Sweeper caches.
			/// </summary>
			private bool passToSweepers;

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
				passToSweepers = false;
			}

			/// <summary>
			/// Initializes the brain to be updated. This saves memory over reallocating new
			/// instances every frame.
			/// </summary>
			/// <param name="newBrain">The brain to update.</param>
			/// <param name="newNavigator">The navigator to compute paths for this brain.</param>
			/// <param name="n">The number of pickup prefab IDs to be updated.</param>
			/// <param name="updateSweepers">If true, Auto-Sweeper caches are also updated by this task.</param>
			public void Begin(Brain newBrain, Navigator newNavigator, int n,
					bool updateSweepers = false) {
				var gcp = GlobalChoreProvider.Instance;
				Count = n;
				brain = newBrain;
				int worldID = newNavigator.GetMyParentWorldId();
				if (gcp != null && gcp.fetchMap.TryGetValue(worldID, out var chores))
					fetchChores = chores;
				else
					fetchChores = null;
				navigator = newNavigator;
				passToSweepers = updateSweepers;
				worker = newNavigator.gameObject;
			}

			/// <summary>
			/// Clears the pickup and fetch lists of items that accumulated from the last
			/// frame.
			/// </summary>
			public void Cleanup() {
				fetchChores = null;
				fetches.Clear();
				pickups.Clear();
			}

			public void InternalDoWorkItem(int index) {
				if (index >= 0 && index < Count) {
					var thisPrefab = updater.byId[index];
					thisPrefab.UpdatePickups(navigator.PathProber, navigator, worker);
					// Help out our poor transfer arms in need
					if (passToSweepers)
						GamePatches.SolidTransferArmUpdater.Instance.UpdateCache(thisPrefab.
							fetchables.GetDataList());
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
					updater.UpdateFetches(task.navigator, task.fetchChores, task.fetches);
				}
			}

			public void TriggerAbort() {
				updater.FinishFetches();
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerComplete() {
				updater.FinishFetches();
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerStart() {
				OffsetTracker.isExecutingWithinJob = true;
			}
		}

		/// <summary>
		/// Updates only Auto-Sweepers if no Duplicants remain.
		/// </summary>
		private sealed class GraveyardShift : AsyncJobManager.IWork, IWorkItemCollection {
			/// <summary>
			/// The list of fetchables collected by prefab ID.
			/// </summary>
			private readonly IList<FetchablesByPrefabId> byId;

			public int Count => byId.Count;
			
			public IWorkItemCollection Jobs => this;

			internal GraveyardShift(AsyncBrainGroupUpdater updater) {
				byId = updater.byId;
			}

			public void InternalDoWorkItem(int index) {
				if (index >= 0 && index < byId.Count)
					GamePatches.SolidTransferArmUpdater.Instance?.UpdateCache(byId[index].
						fetchables.GetDataList());
			}

			public void TriggerAbort() {
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerComplete() {
				OffsetTracker.isExecutingWithinJob = false;
			}

			public void TriggerStart() {
				OffsetTracker.isExecutingWithinJob = true;
			}
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
					foreach (var item in byId[index].fetchables.GetDataList()) {
						var pickupable = item.pickupable;
						var tracker = pickupable.offsetTracker;
						int cachedCell = pickupable.cachedCell;
						if (tracker != null && tracker.previousCell != cachedCell)
							// If an update is actually being performed here, the cached cell
							// may need to be updated, to fix incubator related issues
							DeferredTriggers.Instance.Queue(pickupable);
						else
							pickupable.GetOffsets(cachedCell);
					}
			}

			public void TriggerAbort() {
				updater.FinishFetches();
			}

			public void TriggerComplete() { }

			public void TriggerStart() { }
		}
	}

	/// <summary>
	/// Compares fetchable items by priority and path cost.
	/// </summary>
	internal sealed class FetchComparer : IComparer<Fetch> {
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
