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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using InventoryDict = System.Collections.Generic.IDictionary<Tag, System.Collections.Generic.
	HashSet<Pickupable>>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Compiles WorldInventory updates in the background.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class BackgroundWorldInventory : KMonoBehaviour {
		/// <summary>
		/// The tags which are not stored in the world inventory. These are primarily meta
		/// tags that are not actually requested.
		/// </summary>
		private static readonly ISet<Tag> BANNED_TAGS = new HashSet<Tag>();

		/// <summary>
		/// Initializes the banned tags list after the Db is loaded.
		/// </summary>
		internal static void Init() {
			BANNED_TAGS.Add(GameTags.Pickupable);
			BANNED_TAGS.Add(GameTags.PedestalDisplayable);
			BANNED_TAGS.Add(GameTags.Garbage);
			BANNED_TAGS.Add(GameTags.HasChores);
			BANNED_TAGS.Add(GameTags.Stored);
		}

		/// <summary>
		/// Checks to see if the tag is valid for addition/removal. These tags are applied to
		/// every single chunk, but are never requested.
		/// </summary>
		/// <param name="tag">The category tag being used.</param>
		internal static bool IsAcceptable(Tag tag) {
			return !BANNED_TAGS.Contains(tag);
		}

		/// <summary>
		/// Adds up the total mass of available items.
		/// </summary>
		/// <param name="items">The reachable items across all worlds.</param>
		/// <param name="worldId">The current world ID.</param>
		/// <returns>The total mass of items on this world.</returns>
		private static float SumTotal(IEnumerable<Pickupable> items, int worldId) {
			float total = 0f;
			lock (items) {
				foreach (var pickupable in items)
					if (pickupable != null) {
						int cell = pickupable.cachedCell;
						if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == worldId &&
								!pickupable.KPrefabID.HasTag(GameTags.StoredPrivate))
							total += pickupable.TotalAmount;
					}
			}
			return total;
		}

		/// <summary>
		/// Whether this is the first update since load.
		/// </summary>
		private bool firstUpdate;

		/// <summary>
		/// Set exactly once after the first full update.
		/// </summary>
		private bool forceRefresh;

		/// <summary>
		/// The currently updating resource index.
		/// </summary>
		private int updateIndex;

		/// <summary>
		/// Whether at least one full update has completed.
		/// </summary>
		private bool validCount;

#pragma warning disable IDE0044
#pragma warning disable CS0649
		[MyCmpReq]
		private WorldContainer worldContainer;

		[MyCmpReq]
		private WorldInventory worldInventory;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		internal BackgroundWorldInventory() {
			firstUpdate = false;
			forceRefresh = false;
			updateIndex = 0;
		}

		/// <summary>
		/// Refreshes the pinned resource panel if necessary.
		/// </summary>
		internal void CheckRefresh(ref bool hasValidCount) {
			if (forceRefresh) {
				var pinned = PinnedResourcesPanel.Instance;
				hasValidCount = true;
				forceRefresh = false;
				if (pinned != null && ClusterManager.Instance.activeWorldId == worldContainer.
						id) {
					pinned.ClearExcessiveNewItems();
					pinned.Refresh();
				}
			}
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			firstUpdate = true;
			forceRefresh = false;
			updateIndex = 0;
		}

		/// <summary>
		/// Runs the update, background thread safe.
		/// </summary>
		internal void RunUpdate() {
			int worldId = -1;
			var inventory = worldInventory.Inventory;
			var accessibleAmounts = worldInventory.accessibleAmounts;
			if (worldContainer != null)
				worldId = worldContainer.id;
			if (inventory != null && accessibleAmounts != null && worldId >= 0 && worldId !=
					ClusterManager.INVALID_WORLD_IDX) {
				int index = 0, ui = updateIndex, n = inventory.Count;
				if (firstUpdate) {
					foreach (var pair in inventory) {
						accessibleAmounts[pair.Key] = SumTotal(pair.Value, worldId);
						ui = (ui + 1) % n;
					}
					if (!validCount)
						validCount = forceRefresh = true;
					updateIndex = ui;
					firstUpdate = false;
				} else
					foreach (var pair in inventory)
						if (index++ == ui) {
							accessibleAmounts[pair.Key] = SumTotal(pair.Value, worldId);
							updateIndex = (ui + 1) % n;
							break;
						}
			}
		}
	}

	/// <summary>
	/// A singleton that manages the list of active update requests.
	/// </summary>
	public sealed class BackgroundInventoryUpdater : AsyncJobManager.IWork, IDisposable,
			IWorkItemCollection {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BackgroundInventoryUpdater Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void CreateInstance() {
			Instance?.Dispose();
			Instance = new BackgroundInventoryUpdater();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		public int Count => toUpdate.Count;

		public IWorkItemCollection Jobs => this;

		/// <summary>
		/// Triggered when all worlds complete their updates.
		/// </summary>
		private readonly EventWaitHandle onComplete;

		/// <summary>
		/// The world inventory tasks to be updated.
		/// </summary>
		private readonly IList<BackgroundWorldInventory> toUpdate;

		private BackgroundInventoryUpdater() {
			onComplete = new AutoResetEvent(false);
			toUpdate = new List<BackgroundWorldInventory>(16);
		}

		public void Dispose() {
			onComplete.Dispose();
			toUpdate.Clear();
		}

		/// <summary>
		/// Ensures that all world inventories are done updating.
		/// </summary>
		internal void EndUpdateAll() {
			if (toUpdate.Count > 0 && !onComplete.WaitOne(FastTrackMod.MAX_TIMEOUT))
				PUtil.LogWarning("Inventory updates did not complete within the timeout!");
			toUpdate.Clear();
		}

		public void InternalDoWorkItem(int index) {
			int n = toUpdate.Count;
			if (index >= 0 && index < n)
				toUpdate[index].RunUpdate();
		}

		/// <summary>
		/// Starts a multithreaded update of all world inventories.
		/// </summary>
		internal void StartUpdateAll() {
			var inst = ClusterManager.Instance;
			var jm = AsyncJobManager.Instance;
			toUpdate.Clear();
			if (!SpeedControlScreen.Instance.IsPaused && FastTrackMod.GameRunning &&
					inst != null && jm != null) {
				var worlds = inst.WorldContainers;
				foreach (var container in worlds)
					if (container.TryGetComponent(out BackgroundWorldInventory updater) &&
							container.worldInventory != null)
						toUpdate.Add(updater);
				if (toUpdate.Count > 0) {
					onComplete.Reset();
					jm.Run(this);
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
	}

	/// <summary>
	/// Applied to WorldInventory to never keep track of pickupables for the Pickupable,
	/// HasChores or PedestalDisplayable tags which match every chunk on the asteroid. Also
	/// removes many wasteful GetComponent calls.
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.OnAddedFetchable))]
	public static class WorldInventory_OnAddedFetchable_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Adds a newly found reachable item to the world inventory.
		/// </summary>
		/// <param name="pickupable">The item to add.</param>
		/// <param name="inventory">The inventory where it should be added.</param>
		/// <param name="itemTag">The tag under which it will be classified.</param>
		private static void AddFetchable(Pickupable pickupable, InventoryDict inventory,
				Tag itemTag) {
			if (BackgroundWorldInventory.IsAcceptable(itemTag)) {
				if (!inventory.TryGetValue(itemTag, out var entry))
					inventory[itemTag] = entry = new HashSet<Pickupable>();
				lock (entry) {
					entry.Add(pickupable);
				}
			}
		}

		/// <summary>
		/// Adds a newly found reachable item to the world inventory.
		/// </summary>
		/// <param name="pickupable">The item to add.</param>
		/// <param name="inventory">The inventory where it should be added.</param>
		private static void AddFetchable(Pickupable pickupable, InventoryDict inventory) {
			var kpid = pickupable.KPrefabID;
			var prefabTag = kpid.PrefabTag;
			if (!inventory.ContainsKey(prefabTag)) {
				var category = DiscoveredResources.GetCategoryForEntity(kpid);
				if (category.IsValid)
					DiscoveredResources.Instance.Discover(prefabTag, category);
				else
					PUtil.LogWarning(pickupable.name +
						" was found by WorldInventory, but has no category! Add it to the element definition.");
			}
			foreach (var itemTag in kpid.Tags)
				AddFetchable(pickupable, inventory, itemTag);
			// Prefab tag is no longer in the Tags list
			AddFetchable(pickupable, inventory, prefabTag);
		}

		/// <summary>
		/// Applied before OnAddedFetchable runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(WorldInventory __instance, object data) {
			int cell, id = __instance.worldId;
			if (data is GameObject gameObject && gameObject.TryGetComponent(
					out Pickupable pickupable) && Grid.IsValidCell(cell = Grid.PosToCell(
					gameObject.transform.position)) && id >= 0 && Grid.WorldIdx[cell] == id &&
					!gameObject.TryGetComponent(out Navigator _))
				AddFetchable(pickupable, __instance.Inventory);
			return false;
		}
	}

	/// <summary>
	/// Applied to WorldInventory to add a copy of BackgroundWorldInventory when it is created.
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.OnPrefabInit))]
	public static class WorldInventory_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(WorldInventory __instance) {
			if (__instance != null)
				__instance.gameObject.AddOrGet<BackgroundWorldInventory>();
		}
	}

	/// <summary>
	/// Applied to WorldInventory to synchronize accesses to removing items.
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.OnRemovedFetchable))]
	public static class WorldInventory_OnRemovedFetchable_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Applied after OnRemovedFetchable runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(WorldInventory __instance, object data) {
			if (data is GameObject obj && obj != null && obj.TryGetComponent(
					out Pickupable pickupable)) {
				var inventory = __instance.Inventory;
				var kpid = pickupable.KPrefabID;
				if (inventory.TryGetValue(kpid.PrefabTag, out HashSet<Pickupable> items))
					lock (items) {
						items.Remove(pickupable);
					}
				foreach (var tag in kpid.Tags)
					if (inventory.TryGetValue(tag, out items))
						lock (items) {
							items.Remove(pickupable);
						}
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to WorldInventory to remove the calculations, as we run them in the background.
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.Update))]
	public static class WorldInventory_UpdateReplace_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(WorldInventory __instance) {
			// Only need to update the pinned resources panel
			if (__instance.TryGetComponent(out BackgroundWorldInventory bwi))
				bwi.CheckRefresh(ref __instance.hasValidCount);
			else
				bwi = null;
			return bwi == null;
		}
	}
}
