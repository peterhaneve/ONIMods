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

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using AmountByTagDict = System.Collections.Generic.IDictionary<Tag, float>;
using AmountByTagDictPool = DictionaryPool<Tag, float, FetchListStatusItemUpdater>;
using FetchList2List = ListPool<FetchList2, FetchListStatusItemUpdater>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to FetchListStatusItemUpdater to avoid leaking memory and vastly speed up
	/// updates for fetchable status items tooltips and warnings.
	/// </summary>
	[HarmonyPatch(typeof(FetchListStatusItemUpdater), nameof(FetchListStatusItemUpdater.
		Render200ms))]
	public static class FetchListStatusItemUpdater_Render200ms_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastUpdatePickups;

		/// <summary>
		/// Compiles a list of pickupables already in storage.
		/// </summary>
		/// <param name="destination">The storage to scan.</param>
		/// <param name="existingItems">The location where the items will be placed.</param>
		private static void GetExistingItems(Storage destination,
				ICollection<Pickupable> existingItems) {
			var items = destination.items;
			int n = items.Count;
			existingItems.Clear();
			for (int i = 0; i < n; i++) {
				var item = items[i];
				if (item != null && item.TryGetComponent(out Pickupable component))
					existingItems.Add(component);
			}
		}

		/// <summary>
		/// Compiles a lookup of each fetch errand from its destination storage, using a list
		/// to handle multiple fetches to the same place.
		/// </summary>
		/// <param name="id">The world ID to update.</param>
		/// <param name="instance">The fetch lists to update.</param>
		/// <param name="byDestination">The location where the errands will be stored.</param>
		private static void SortByDestination(int id, FetchListStatusItemUpdater instance,
				IDictionary<int, FetchList2List.PooledList> byDestination) {
			var fetchLists = instance.fetchLists;
			int n = fetchLists.Count, curIndex = instance.currentIterationIndex[id],
				count = Math.Min(instance.maxIteratingCount, n - curIndex);
			for (int i = 0; i < count; i++) {
				var fetchList = fetchLists[i + curIndex];
				var dest = fetchList.Destination;
				if (dest != null && dest.gameObject.GetMyWorldId() == id) {
					int instanceID = dest.GetInstanceID();
					if (!byDestination.TryGetValue(instanceID, out var errands))
						byDestination.Add(instanceID, errands = FetchList2List.Allocate());
					errands.Add(fetchList);
				}
			}
			curIndex += count;
			if (curIndex >= n)
				curIndex = 0;
			instance.currentIterationIndex[id] = curIndex;
		}

		/// <summary>
		/// Applied before Render200ms runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(FetchListStatusItemUpdater __instance) {
			var existingItems = ListPool<Pickupable, FetchListStatusItemUpdater>.Allocate();
			var byDestination = DictionaryPool<int, FetchList2List.PooledList,
				FetchListStatusItemUpdater>.Allocate();
			var totalAmounts = AmountByTagDictPool.Allocate();
			var worldAmounts = AmountByTagDictPool.Allocate();
			var amounts = AmountByTagDictPool.Allocate();
			var containers = ClusterManager.Instance.WorldContainers;
			int n = containers.Count;
			for (int i = 0; i < n; i++) {
				var worldContainer = containers[i];
				var inventory = worldContainer.worldInventory;
				byDestination.Clear();
				totalAmounts.Clear();
				worldAmounts.Clear();
				SortByDestination(worldContainer.id, __instance, byDestination);
				foreach (var pair in byDestination) {
					var fetchLists = pair.Value;
					int fn = fetchLists.Count;
					if (fn > 0) {
						amounts.Clear();
						GetExistingItems(fetchLists[0].Destination, existingItems);
						for (int j = 0; j < fn; j++)
							UpdateStatus(fetchLists[j], amounts, totalAmounts, worldAmounts,
								inventory, existingItems);
					}
					fetchLists.Recycle();
				}
			}
			amounts.Recycle();
			existingItems.Recycle();
			byDestination.Recycle();
			totalAmounts.Recycle();
			worldAmounts.Recycle();
			return false;
		}
		
		/// <summary>
		/// Updates the available quantity for each item in the fetch errand.
		/// </summary>
		/// <param name="errand">The items that must be fetched.</param>
		/// <param name="amounts">The location where the currently stored amount will be stored.</param>
		/// <param name="totalAmounts">The location where the world total amount will be stored.</param>
		/// <param name="worldAmounts">The location where the world available amount will be stored.</param>
		/// <param name="inventory">The world inventory to search for available items.</param>
		/// <param name="existingItems">The items already in storage.</param>
		private static void UpdateStatus(FetchList2 errand, AmountByTagDict amounts,
				AmountByTagDict totalAmounts, AmountByTagDict worldAmounts,
				WorldInventory inventory, IList<Pickupable> existingItems) {
			var si = Db.Get().BuildingStatusItems;
			int n = existingItems.Count;
			bool noMaterials = false;
			bool needMaterials = true;
			bool resourcesLow = false;
			errand.UpdateRemaining();
			foreach (var item in errand.GetRemaining()) {
				var tag = item.Key;
				float remaining = item.Value;
				if (!amounts.TryGetValue(tag, out float inStorage)) {
					inStorage = 0.0f;
					for (int i = 0; i < n; i++) {
						var pickupable = existingItems[i];
						if (pickupable.KPrefabID.HasTag(tag))
							inStorage += pickupable.TotalAmount;
					}
					amounts.Add(tag, inStorage);
				}
				// Only rescan world inventory if not already checked for this world
				if (!totalAmounts.TryGetValue(tag, out float total))
					totalAmounts.Add(tag, total = inventory.GetTotalAmount(tag, true));
				if (!worldAmounts.TryGetValue(tag, out float available))
					worldAmounts.Add(tag, available = inventory.GetAmount(tag, true));
				float fetchable = available + Mathf.Min(remaining, total);
				float minimumAmount = errand.GetMinimumAmount(tag);
				if (inStorage + fetchable < minimumAmount)
					// No available materials
					noMaterials = true;
				if (fetchable <= remaining) {
					// Materials are stored and ready
					needMaterials = false;
					if (inStorage + fetchable > remaining)
						// Can run it with what we have + on the way, but not again
						resourcesLow = true;
				}
			}
			errand.UpdateStatusItem(si.WaitingForMaterials,
				ref errand.waitingForMaterialsHandle, needMaterials);
			errand.UpdateStatusItem(si.MaterialsUnavailable,
				ref errand.materialsUnavailableHandle, noMaterials);
			errand.UpdateStatusItem(si.MaterialsUnavailableForRefill,
				ref errand.materialsUnavailableForRefillHandle, resourcesLow);
		}
	}

	/// <summary>
	/// Applied to GameUtil to reduce memory allocations in the acoustic disturbance class.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.CollectCellsBreadthFirst))]
	public static class GameUtil_CollectCellsBreadthFirst_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// The order matters here!
		/// </summary>
		private static readonly CellOffset[] OFFSETS = {
			new CellOffset(1, 0), new CellOffset(-1, 0), new CellOffset(0, 1),
			new CellOffset(0, -1)
		};
		
		/// <summary>
		/// Applied before CollectCellsBreadthFirst runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(int start_cell, Func<int, bool> test_func, int max_depth,
				ref HashSet<int> __result) {
			var pending = QueuePool<int, AcousticDisturbance>.Allocate();
			var visited = HashSetPool<int, AcousticDisturbance>.Allocate();
			var found = new HashSet<int>();
			int n = 1, o = OFFSETS.Length;
			pending.Enqueue(start_cell);
			for (int i = max_depth; i > 0 && n > 0; i--) {
				for (int j = n; j > 0; j--) {
					int cell = pending.Dequeue();
					for (int k = 0; k < o; k++) {
						int newCell = Grid.OffsetCell(cell, OFFSETS[k]);
						if (Grid.IsValidCell(newCell) && visited.Add(newCell) && test_func(
								newCell)) {
							found.Add(newCell);
							pending.Enqueue(newCell);
						}
					}
				}
				n = pending.Count;
			}
			pending.Recycle();
			visited.Recycle();
			__result = found;
			return false;
		}
	}

	/// <summary>
	/// Applied to PathFinder to fix meaningless memory allocations and slow list lookups that
	/// happen when a game is loaded.
	/// </summary>
	[HarmonyPatch(typeof(PathFinder), nameof(PathFinder.Initialize))]
	public static class PathFinder_Initialize_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before Initialize runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			if (!Enum.TryParse(nameof(NavType.NumNavTypes), out NavType numTypes))
				numTypes = NavType.NumNavTypes;
			var navTypes = new NavType[(int)numTypes];
			for (int i = 0; i < navTypes.Length; i++)
				navTypes[i] = (NavType)i;
			PathFinder.PathGrid = new PathGrid(Grid.WidthInCells, Grid.HeightInCells, false,
				navTypes);
			var cells = HashSetPool<int, PathFinder>.Allocate();
			for (int i = 0; i < Grid.CellCount; i++)
				if (Grid.Visible[i] > 0 || Grid.Spawnable[i] > 0) {
					GameUtil.FloodFillConditional(i, PathFinder.allowPathfindingFloodFillCb,
						cells);
					Grid.AllowPathfinding[i] = true;
					cells.Clear();
				}
			cells.Recycle();
			return false;
		}
	}
}
