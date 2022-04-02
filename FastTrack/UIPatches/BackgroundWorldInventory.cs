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
using System.Threading;

using InventoryDict = System.Collections.Generic.IDictionary<Tag, System.Collections.Generic.
	HashSet<Pickupable>>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Compiles WorldInventory updates in the background.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class BackgroundWorldInventory : KMonoBehaviour {
		/// <summary>
		/// Checks to see if the tag is valid for addition/removal. These tags are applied to
		/// every single chunk, but are never requested.
		/// </summary>
		/// <param name="tag">The category tag being used.</param>
		internal static bool IsAcceptable(Tag tag) {
			return tag != GameTags.Pickupable && tag != GameTags.PedestalDisplayable &&
				tag != GameTags.HasChores;
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
				if (pinned != null && ClusterManager.Instance.activeWorldId ==
						worldContainer.id) {
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
				int index = 0, n = inventory.Count;
				foreach (var pair in inventory) {
					if (index == updateIndex || firstUpdate) {
						float total = 0f;
						foreach (var pickupable in pair.Value) {
							int cell = pickupable.cachedCell;
							if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == worldId &&
									!pickupable.KPrefabID.HasTag(GameTags.StoredPrivate))
								total += pickupable.TotalAmount;
						}
						if (!validCount && updateIndex + 1 >= n)
							forceRefresh = validCount = true;
						accessibleAmounts[pair.Key] = total;
						updateIndex = (updateIndex + 1) % n;
						if (!firstUpdate)
							break;
					}
					index++;
				}
				firstUpdate = false;
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
				foreach (var container in worlds) {
					var updater = container.GetComponent<BackgroundWorldInventory>();
					if (updater != null && container.worldInventory != null)
						toUpdate.Add(updater);
				}
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
	/// Applied to Pathfinding to ensure that each world inventory is done calculating.
	/// </summary>
	[HarmonyPatch(typeof(Pathfinding), nameof(Pathfinding.RenderEveryTick))]
	public static class Pathfinding_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		internal static void Prefix() {
			BackgroundInventoryUpdater.Instance?.EndUpdateAll();
		}
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
		/// Applied before OnAddedFetchable runs.
		/// </summary>
		internal static bool Prefix(WorldInventory __instance, object data,
				InventoryDict ___Inventory) {
			var gameObject = (UnityEngine.GameObject)data;
			var pickupable = gameObject.GetComponent<Pickupable>();
			int cell = pickupable.cachedCell, id;
			var container = __instance.GetComponent<WorldContainer>();
			if (container != null && Grid.IsValidCell(cell) && (id = container.id) !=
					ClusterManager.INVALID_WORLD_IDX && Grid.WorldIdx[cell] == id &&
					gameObject.GetComponent<Navigator>() == null) {
				var kpid = pickupable.KPrefabID;
				var prefabTag = kpid.PrefabTag;
				if (!___Inventory.ContainsKey(prefabTag)) {
					var category = DiscoveredResources.GetCategoryForEntity(kpid);
					if (!category.IsValid)
						PUtil.LogWarning(pickupable.name +
							" was found by WorldInventory, but has no category! Add it to the element definition.");
					DiscoveredResources.Instance.Discover(prefabTag, category);
				}
				foreach (var itemTag in kpid.Tags)
					if (BackgroundWorldInventory.IsAcceptable(itemTag)) {
						if (!___Inventory.TryGetValue(itemTag, out HashSet<Pickupable> entry))
							___Inventory[itemTag] = entry = new HashSet<Pickupable>();
						entry.Add(pickupable);
					}
			}
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
	/// Applied to WorldInventory to remove the calculations, as we run them in the background.
	/// </summary>
	[HarmonyPatch(typeof(WorldInventory), nameof(WorldInventory.Update))]
	public static class WorldInventory_UpdateReplace_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ParallelInventory;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static bool Prefix(WorldInventory __instance, ref bool ___hasValidCount) {
			var bwi = __instance.GetComponent<BackgroundWorldInventory>();
			// Only need to update the pinned resources panel
			if (bwi != null)
				bwi.CheckRefresh(ref ___hasValidCount);
			return bwi == null;
		}
	}
}
