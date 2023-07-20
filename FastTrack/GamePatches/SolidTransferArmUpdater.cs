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
using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using CachedPickupable = SolidTransferArm.CachedPickupable;
using SolidTransferArmBucket = UpdateBucketWithUpdater<ISim1000ms>.Entry;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A singleton class that updates Auto-Sweepers in the background quickly.
	/// </summary>
	internal sealed class SolidTransferArmUpdater : IWorkItemCollection, IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static SolidTransferArmUpdater Instance { get; private set; }

		/// <summary>
		/// Tests an object to see if a sweeper can pick it up.
		/// </summary>
		/// <param name="pickupable">The item to pick up.</param>
		/// <param name="go">The sweeper trying to pick it up.</param>
		/// <returns>true if it can be picked up, or false otherwise.</returns>
		private static bool CanUse(Pickupable pickupable, GameObject go) {
			var prefabID = pickupable.KPrefabID;
			return pickupable.CouldBePickedUpByTransferArm(go) && Assets.
				IsTagSolidTransferArmConveyable(prefabID.PrefabTag);
		}

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void CreateInstance() {
			DestroyInstance();
			Instance = new SolidTransferArmUpdater();
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		public int Count => sweepers.Count;

		/// <summary>
		/// The cached pickupables from async fetches.
		/// </summary>
		private readonly ConcurrentQueue<CachedPickupable> cached;

		/// <summary>
		/// The current sweeper job.
		/// </summary>
		private readonly IList<SolidTransferArmInfo> sweepers;

		private SolidTransferArmUpdater() {
			cached = new ConcurrentQueue<CachedPickupable>();
			sweepers = new List<SolidTransferArmInfo>(32);
		}

		/// <summary>
		/// Updates the sweeper asynchronously.
		/// </summary>
		/// <param name="info">The sweeper to update.</param>
		private void AsyncUpdate(SolidTransferArmInfo info) {
			var sweeper = info.sweeper;
			var reachableCells = HashSetPool<int, SolidTransferArmUpdater>.Allocate();
			int range = sweeper.pickupRange, cell = info.cell;
			Grid.CellToXY(cell, out int x, out int y);
			int maxY = Math.Min(Grid.HeightInCells, y + range), maxX = Math.Min(Grid.
				WidthInCells, x + range), minY = Math.Max(0, y - range), minX = Math.Max(0,
				x - range);
			var oldReachable = sweeper.reachableCells;
			var go = info.gameObject;
			// Recalculate the visible cells
			for (int ny = minY; ny <= maxY; ny++)
				for (int nx = minX; nx <= maxX; nx++) {
					cell = Grid.XYToCell(nx, ny);
					if (Grid.IsPhysicallyAccessible(x, y, nx, ny, true))
						reachableCells.Add(cell);
				}
			if (!oldReachable.SetEquals(reachableCells)) {
				// O(n) operation worst case
				oldReachable.Clear();
				oldReachable.UnionWith(reachableCells);
				info.refreshedCells = true;
			}
			// Gather stored objects not found by the partitioner
			var pickupables = sweeper.pickupables;
			pickupables.Clear();
			foreach (var entry in cached) {
				var pickupable = entry.pickupable;
				if (pickupable != null) {
					cell = entry.storage_cell;
					Grid.CellToXY(cell, out x, out y);
					if (x >= minX && x <= maxX && y >= minY && y <= maxY && reachableCells.
							Contains(cell) && CanUse(pickupable, go))
						pickupables.Add(pickupable);
				}
			}
			var gsp = GameScenePartitioner.Instance;
			// Gather nearby pickupables with the scene partitioner, faster and more memory
			// efficient than scanning all
			if (gsp != null) {
				var found = ListPool<ScenePartitionerEntry, SolidTransferArmUpdater>.
					Allocate();
				lock (cached) {
					gsp.GatherEntries(new Extents(minX, minY, maxX - minX + 1, maxY - minY +
						1), gsp.pickupablesLayer, found);
				}
				int n = found.Count;
				for (int i = 0; i < n; i++)
					if (found[i].obj is Pickupable pickupable && pickupable != null) {
						cell = pickupable.cachedCell;
						if (reachableCells.Contains(cell) && CanUse(pickupable, go))
							pickupables.Add(pickupable);
					}
				found.Recycle();
			}
			reachableCells.Recycle();
		}

		/// <summary>
		/// Starts updating a group of Auto-Sweepers.
		/// </summary>
		/// <param name="entries">The sweepers to update.</param>
		internal void BatchUpdate(IList<SolidTransferArmBucket> entries) {
			sweepers.Clear();
			int n = entries.Count;
			for (int i = 0; i < n; i++) {
				var entry = entries[i];
				// Filter for usable auto-sweepers
				entry.lastUpdateTime = 0.0f;
				if (entry.data is SolidTransferArm autoSweeper && autoSweeper.operational.
						IsOperational)
					sweepers.Add(new SolidTransferArmInfo(autoSweeper));
			}
			n = sweepers.Count;
			if (n > 0) {
				GlobalJobManager.Run(this);
				// This has to be waited out, because it could be run more than once in
				// a frame and could race against things like SolidConsumerMonitor
				for (int i = 0; i < n; i++) {
					var info = sweepers[i];
					var sweeper = info.sweeper;
					if (info.refreshedCells)
						sweeper.IncrementSerialNo();
					sweeper.Sim();
				}
			}
			ClearCached();
		}

		/// <summary>
		/// Clears the item cache from the Async Fetch manager.
		/// </summary>
		internal void ClearCached() {
			while (cached.TryDequeue(out _)) { }
		}

		public void Dispose() {
			sweepers.Clear();
		}

		public void InternalDoWorkItem(int index) {
			if (index >= 0 && index < sweepers.Count)
				AsyncUpdate(sweepers[index]);
		}

		/// <summary>
		/// Updates the cache with items from the Async Fetch manager.
		/// </summary>
		/// <param name="items">The fetchables for a prefab ID.</param>
		internal void UpdateCache(IList<FetchManager.Fetchable> items) {
			int n = items.Count;
			for (int i = 0; i < n; i++) {
				var pickupable = items[i].pickupable;
				if (pickupable != null && pickupable.KPrefabID.HasTag(GameTags.Stored))
					cached.Enqueue(new CachedPickupable {
						pickupable = pickupable,
						storage_cell = pickupable.cachedCell
					});
			}
		}

		/// <summary>
		/// Holds job information for one Auto-Sweeper.
		/// </summary>
		private sealed class SolidTransferArmInfo {
			/// <summary>
			/// The cell of the arm.
			/// </summary>
			internal readonly int cell;

			/// <summary>
			/// .gameObject cannot be used on background tasks, so save it separately.
			/// </summary>
			internal readonly GameObject gameObject;

			/// <summary>
			/// Whether the reachable cells have been refreshed.
			/// </summary>
			internal bool refreshedCells;

			/// <summary>
			/// The sweeper to update.
			/// </summary>
			internal readonly SolidTransferArm sweeper;

			public SolidTransferArmInfo(SolidTransferArm sweeper) {
				if (sweeper == null)
					throw new ArgumentNullException(nameof(sweeper));
				var go = sweeper.gameObject;
				gameObject = go;
				cell = Grid.PosToCell(sweeper.transform.position);
				refreshedCells = false;
				this.sweeper = sweeper;
			}

			public override string ToString() {
				return "Solid Transfer Arm Updater in cell {0:D}".F(cell);
			}
		}
	}

	/// <summary>
	/// Applied to SolidTransferArm to update the Auto-Sweepers in the background.
	/// </summary>
	[HarmonyPatch(typeof(SolidTransferArm), nameof(SolidTransferArm.BatchUpdate))]
	public static class SolidTransferArm_BatchUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before BatchUpdate runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(List<SolidTransferArmBucket> solid_transfer_arms,
				float time_delta) {
			var inst = SolidTransferArmUpdater.Instance;
			bool run = true;
			if (inst != null) {
				if (time_delta > 0.0f)
					inst.BatchUpdate(solid_transfer_arms);
				run = false;
			}
			return run;
		}
	}
}
