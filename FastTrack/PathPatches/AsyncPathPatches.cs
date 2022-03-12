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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

using FetchablesByPrefabId = FetchManager.FetchablesByPrefabId;
using Pickup = FetchManager.Pickup;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Wraps the Duplicant brain group with a singleton that allows smart and threaded updates
	/// to pathing and sensors.
	/// </summary>
	public sealed class DupeBrainGroupUpdater : IDisposable {
		/// <summary>
		/// The singleton (if any) instance of this class.
		/// </summary>
		public static DupeBrainGroupUpdater Instance { get; private set; }

		/// <summary>
		/// Destroys and cleans up the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The BrainScheduler.BrainGroup nested type is private.
		/// </summary>
		private static readonly Type BRAIN_GROUP = typeof(BrainScheduler).GetNestedType(
			"BrainGroup", PPatchTools.BASE_FLAGS | BindingFlags.Instance);

		/// <summary>
		/// Runs the multithreaded path probe (possibly async).
		/// </summary>
		private static readonly MethodInfo ASYNC_PATH_PROBE = BRAIN_GROUP?.GetMethodSafe(
			"AsyncPathProbe", false);

		/// <summary>
		/// Retrieves the comparator used to sort pickups.
		/// </summary>
		private static readonly IDetouredField<FetchManager, IComparer<Pickup>>
			COMPARER_NO_PRIORITY = PDetours.DetourField<FetchManager, IComparer<Pickup>>(
			"ComparerNoPriority");

		/// <summary>
		/// A delegate that accesses the next brain to update.
		/// </summary>
		private readonly Func<object, IList<Brain>> GET_BRAIN_LIST = BRAIN_GROUP?.
			GenerateGetter<IList<Brain>>("brains");

		private const string NEXT_UPDATE_BRAIN = "nextUpdateBrain";

		/// <summary>
		/// A delegate that accesses the next brain to update.
		/// </summary>
		private readonly Func<object, int> GET_NEXT_UPDATE_BRAIN = BRAIN_GROUP?.
			GenerateGetter<int>(NEXT_UPDATE_BRAIN);

		/// <summary>
		/// Retrieves the list of pickups at runtime from FetchManager.
		/// </summary>
		private readonly IDetouredField<FetchManager, List<Pickup>> GET_PICKUPS = PDetours.
			DetourField<FetchManager, List<Pickup>>("pickups");

		/// <summary>
		/// A delegate that modifies the next brain to update.
		/// </summary>
		private readonly Action<object, int> SET_NEXT_UPDATE_BRAIN = BRAIN_GROUP?.
			GenerateSetter<int>(NEXT_UPDATE_BRAIN);

		/// <summary>
		/// A more efficient (slightly) version of GlobalChoreProber.UpdateFetches.
		/// </summary>
		/// <param name="navigator">The navigator that is fetching items.</param>
		/// <param name="fetches">The location where the errands will be populated.</param>
		private static void UpdateFetches(Navigator navigator, List<FetchInfo> fetches) {
			var sortFetches = DictionaryPool<FetchInfo, FetchInfo, DupeBrainGroupUpdater>.
				Allocate();
			var gcp = GlobalChoreProvider.Instance;
			if (gcp != null) {
				Storage destination;
				int cost;
				foreach (var fetchChore in gcp.fetchChores)
					// Not already taken, allows manual use - GetNavigationCost could actually
					// mutate the offset table (CLAY PLEASE) so FG thread only
					if (fetchChore.driver == null && (fetchChore.automatable == null ||
							!fetchChore.automatable.GetAutomationOnly()) && (destination =
							fetchChore.destination) != null && (cost = navigator.
							GetNavigationCost(destination)) >= 0) {
						var info = new FetchInfo(fetchChore, cost, destination);
						// Replace the older one if better
						if (!sortFetches.TryGetValue(info, out FetchInfo oldInfo) || info.
								CompareTo(oldInfo) > 0)
							sortFetches[info] = info;
					}
			}
			fetches.Clear();
			// Enumerate the dictionary, then sort it - sounds like extra work, but with
			// it already de-duplicated by item type it is way faster than before
			foreach (var pair in sortFetches)
				fetches.Add(pair.Value);
			sortFetches.Recycle();
			fetches.Sort();
		}

		/// <summary>
		/// The list of fetchables collected by prefab ID.
		/// </summary>
		private readonly IList<FetchablesByPrefabId> byId;

		/// <summary>
		/// The pickup that is currently being updated.
		/// </summary>
		private volatile int currentIndex;

		/// <summary>
		/// The current DupeBrainGroup used for updates.
		/// </summary>
		private readonly object dupeBrainGroup;

		/// <summary>
		/// Called to get the current probe count per frame.
		/// </summary>
		private readonly Func<int> getInitialProbeCount;

		/// <summary>
		/// Fired when the pickups are all compiled.
		/// </summary>
		private readonly EventWaitHandle onComplete;

		/// <summary>
		/// Compares pickups for sorting.
		/// </summary>
		private readonly IComparer<Pickup> pickupComparer;

		/// <summary>
		/// Runs the "asynchronous" path probe, which is either main thread (AsyncPathProbe =
		/// false) or background thread (AsyncPathProbe = true).
		/// </summary>
		private readonly System.Action runAsyncPathProbe;

		/// <summary>
		/// Contains the list of all pickup jobs that are currently running.
		/// </summary>
		private readonly IList<CompilePickupsWork> updatingPickups;

		private DupeBrainGroupUpdater(object dupeBrainGroup) {
			byId = new List<FetchablesByPrefabId>(64);
			currentIndex = -1;
			this.dupeBrainGroup = dupeBrainGroup ?? throw new ArgumentNullException(nameof(
				dupeBrainGroup));
			getInitialProbeCount = BRAIN_GROUP.CreateDelegate<Func<int>>("InitialProbeCount",
				dupeBrainGroup);
			if (getInitialProbeCount == null)
				PUtil.LogError("InitialProbeCount not found!");
			onComplete = new AutoResetEvent(false);
			pickupComparer = COMPARER_NO_PRIORITY.Get(null);
			if (pickupComparer == null)
				PUtil.LogError("ComparerNoPriority not found!");
			runAsyncPathProbe = ASYNC_PATH_PROBE?.CreateDelegate<System.Action>(
				dupeBrainGroup);
			if (runAsyncPathProbe == null)
				PUtil.LogDebug("AsyncPathProbe not found!");
			updatingPickups = new List<CompilePickupsWork>(8);
		}

		public void Dispose() {
			// These need to be cleaned up, or memory gets leaked
			foreach (var entry in updatingPickups)
				entry.Dispose();
			byId.Clear();
			updatingPickups.Clear();
		}

		/// <summary>
		/// Ends a Duplicant brain update cycle.
		/// </summary>
		internal void EndBrainUpdate() {
			var fm = Game.Instance.fetchManager;
			var fetchPool = ListPool<FetchInfo, CompilePickupsWork>.Allocate();
			var inst = GlobalChoreProvider.Instance;
			if (fm != null && inst != null) {
				var fetches = inst.fetches;
				var pickups = GET_PICKUPS.Get(fm);
				// Wait out the pickups update
				onComplete.WaitOne(Timeout.Infinite);
				foreach (var entry in updatingPickups) {
					UpdateFetches(entry.navigator, fetchPool);
					// Copy fetch list
					fetches.Clear();
					foreach (var fetchInfo in fetchPool)
						fetches.Add(new GlobalChoreProvider.Fetch {
							category = fetchInfo.category, chore = fetchInfo.chore,
							cost = fetchInfo.cost, priority = fetchInfo.chore.masterPriority,
							tagBitsHash = fetchInfo.tagBitsHash
						});
					// Copy pickup list
					pickups.Clear();
					pickups.AddRange(entry.pickups);
					entry.Dispose();
					// Calls into Sensors, but Pickupable and PathProber were bypassed
					entry.brain.UpdateBrain();
				}
			} else {
				// Clean up anyways
				onComplete.WaitOne(Timeout.Infinite);
				foreach (var entry in updatingPickups)
					entry.Dispose();
			}
			byId.Clear();
			fetchPool.Recycle();
			updatingPickups.Clear();
		}

		/// <summary>
		/// Populates the list of Duplicant brains to be updated.
		/// </summary>
		/// <param name="toUpdate">The location where the brains to update will be populated.</param>
		/// <returns>The number of brains that will be updated.</returns>
		internal int GetBrainsToUpdate(ICollection<MinionBrain> toUpdate)
		{
			var brains = GET_BRAIN_LIST.Invoke(dupeBrainGroup);
			int count = getInitialProbeCount.Invoke(), index = GET_NEXT_UPDATE_BRAIN.Invoke(
				dupeBrainGroup), n = brains.Count;
			if (toUpdate == null)
				throw new ArgumentNullException(nameof(toUpdate));
			toUpdate.Clear();
			while (count-- > 0) {
				var brain = brains[index];
				if (brain.IsRunning() && brain is MinionBrain mb && mb != null)
					// Always should be true, this is a dupe brain group
					toUpdate.Add(mb);
				index = (index + 1) % n;
			}
			SET_NEXT_UPDATE_BRAIN.Invoke(dupeBrainGroup, index);
			return toUpdate.Count;
		}

		/// <summary>
		/// Starts the next pickup compilation. These are run in series in the background,
		/// with each one spawning parallel tasks.
		/// </summary>
		private void NextPickup() {
			int n = updatingPickups.Count, toRun = Interlocked.Increment(ref currentIndex);
			if (toRun < n)
				AsyncJobManager.Instance?.Run(updatingPickups[toRun]);
			else
				onComplete.Set();
		}

		/// <summary>
		/// Starts a Duplicant brain update cycle.
		/// </summary>
		/// <param name="asyncPathProbe">true to start path probes asynchronously, or false
		/// to run them "synchronously" in the sensor methods.</param>
		internal void StartBrainUpdate(bool asyncPathProbe) {
			var update = ListPool<MinionBrain, DupeBrainGroupUpdater>.Allocate();
			var fm = Game.Instance.fetchManager;
			if (asyncPathProbe)
				runAsyncPathProbe.Invoke();
			if (GetBrainsToUpdate(update) > 0 && fm != null) {
				currentIndex = -1;
				updatingPickups.Clear();
				// Brains.... Brains!!!!
				foreach (var brain in update) {
					var nav = brain.GetComponent<Navigator>();
					if (nav != null) {
						// What PathProberSensor did
						nav.UpdateProbe(false);
						updatingPickups.Add(new CompilePickupsWork(this, brain, nav));
					}
				}
				if (updatingPickups.Count > 0) {
					onComplete.Reset();
					// Add a task to update the offset tables first
					foreach (var pair in fm.prefabIdToFetchables)
						byId.Add(pair.Value);
					AsyncJobManager.Instance?.Run(new UpdateOffsetTablesWork(this));
				} else
					onComplete.Set();
			} else
				// Avoid sitting and waiting
				onComplete.Set();
			update.Recycle();
		}

		/// <summary>
		/// Allows much faster fetch handling by dictionary sorting using priority value and
		/// class. This class has a natural ordering inconsistent with Equals().
		/// </summary>
		private struct FetchInfo : IComparable<FetchInfo> {
			/// <summary>
			/// The category of the errand.
			/// </summary>
			internal readonly Storage.FetchCategory category;

			/// <summary>
			/// The chore to be executed.
			/// </summary>
			internal readonly FetchChore chore;

			/// <summary>
			/// The navigation cost to the errand.
			/// </summary>
			internal readonly int cost;

			/// <summary>
			/// The hash of the tag bits for the errand item.
			/// </summary>
			internal readonly int tagBitsHash;

			internal FetchInfo(FetchChore fetchChore, int cost, Storage destination) {
				category = destination.fetchCategory;
				this.cost = cost;
				chore = fetchChore;
				tagBitsHash = fetchChore.tagBitsHash;
			}

			public int CompareTo(FetchInfo other) {
				int result = other.chore.masterPriority.CompareTo(chore.masterPriority);
				if (result == 0)
					result = cost.CompareTo(other.cost);
				return result;
			}

			public override bool Equals(object obj) {
				return obj is FetchInfo other && category == other.category && chore.
					choreType == other.chore.choreType && tagBitsHash == other.tagBitsHash;
			}

			public override int GetHashCode() {
				return tagBitsHash;
			}

			public override string ToString() {
				var p = chore.masterPriority;
				return "FetchInfo[category={0},cost={1:D},priority={2},{3:D}]".F(category,
					cost, p.priority_class, p.priority_value);
			}
		}

		/// <summary>
		/// In parallel, compiles debris pickups on the background job queue.
		/// </summary>
		private sealed class CompilePickupsWork : AsyncJobManager.IWork, IWorkItemCollection,
				IDisposable {
			public int Count => byId.Count;
			
			public IWorkItemCollection Jobs => this;

			/// <summary>
			/// The brain to update when this task is done.
			/// </summary>
			internal readonly MinionBrain brain;

			/// <summary>
			/// The prefab IDs that will be compiled.
			/// </summary>
			private readonly IList<FetchablesByPrefabId> byId;

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
			private readonly DupeBrainGroupUpdater updater;

			/// <summary>
			/// The worker that is trying to pick up items.
			/// </summary>
			private readonly GameObject worker;

			internal CompilePickupsWork(DupeBrainGroupUpdater updater, MinionBrain brain,
					Navigator navigator) {
				this.brain = brain;
				byId = updater.byId;
				pickups = ListPool<Pickup, CompilePickupsWork>.Allocate();
				this.navigator = navigator;
				this.updater = updater;
				worker = navigator.gameObject;
			}

			public void Dispose() {
				pickups.Recycle();
			}

			public void InternalDoWorkItem(int index) {
				int n = byId.Count;
				if (index >= 0 && index < n)
					byId[index].UpdatePickups(navigator.PathProber, navigator, worker);
			}

			public void TriggerComplete() {
				pickups.Clear();
				foreach (var pair in byId)
					pickups.AddRange(pair.finalPickups);
				pickups.Sort(updater.pickupComparer);
				OffsetTracker.isExecutingWithinJob = false;
				updater.NextPickup();
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
			private readonly DupeBrainGroupUpdater updater;

			internal UpdateOffsetTablesWork(DupeBrainGroupUpdater updater) {
				byId = updater.byId;
				this.updater = updater;
			}

			public void InternalDoWorkItem(int index) {
				if (index >= 0 && index < byId.Count)
					byId[index].UpdateOffsetTables();
			}

			public void TriggerComplete() {
				updater.NextPickup();
			}

			public void TriggerStart() { }
		}

		/// <summary>
		/// Applied to BrainScheduler.BrainGroup to move the path probe updates to a fully
		/// asychronous task.
		/// </summary>
		[HarmonyPatch]
		internal static class AsyncPathProbe_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AsyncPathProbe;

			internal static MethodBase TargetMethod() {
				// Private type with private method
				return BRAIN_GROUP?.GetMethodSafe("AsyncPathProbe", false);
			}

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
		[HarmonyPatch(typeof(BrainScheduler), "OnPrefabInit")]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(System.Collections.IList ___brainGroups) {
				DestroyInstance();
				if (ASYNC_PATH_PROBE != null)
					foreach (object brainGroup in ___brainGroups)
						if (brainGroup.GetType().Name.EndsWith("DupeBrainGroup")) {
#if DEBUG
							PUtil.LogDebug("Created DupeBrainGroupUpdater");
#endif
							Instance = new DupeBrainGroupUpdater(brainGroup);
							break;
						}
			}
		}

		/// <summary>
		/// Applied to BrainScheduler.BrainGroup to only start up the sensors if the pickup
		/// optimizations are being backgrounded.
		/// </summary>
		[HarmonyPatch]
		internal static class RenderEveryTick_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

			internal static MethodBase TargetMethod() {
				return BRAIN_GROUP?.GetMethodSafe("RenderEveryTick", false, typeof(float),
					typeof(bool));
			}

			internal static bool Prefix(object __instance, bool isAsyncPathProbeEnabled) {
				var inst = Instance;
				if (inst != null && AsyncJobManager.Instance != null && __instance == inst.
						dupeBrainGroup)
					inst.StartBrainUpdate(isAsyncPathProbeEnabled);
				return false;
			}
		}
	}
}
