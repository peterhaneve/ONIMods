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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Probes for rooms on a background task.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class BackgroundRoomProber : KMonoBehaviour, ISim200ms, IDisposable {
		private static Database.RoomTypes roomTypes;

		private static readonly Tag TREE_BRANCH_TAG = new Tag(ForestTreeBranchConfig.ID);

		/// <summary>
		/// The singleton instanec of this class.
		/// </summary>
		internal static BackgroundRoomProber Instance { get; private set; }

		/// <summary>
		/// Assigns buildings to a room.
		/// </summary>
		/// <param name="room">The room that was just modified or created.</param>
		private static void Assign(Room room) {
			var roomType = room.roomType;
			if (roomType != roomTypes.Neutral) {
				var buildings = room.buildings;
				int n = buildings.Count;
				var pc = roomType.primary_constraint;
				for (int i = 0; i < n; i++) {
					var prefabID = buildings[i];
					if (prefabID != null && !prefabID.HasTag(GameTags.NotRoomAssignable) &&
							prefabID.TryGetComponent(out Assignable component) && (pc ==
							null || !pc.building_criteria(prefabID)))
						component.Assign(room);
				}
			}
		}

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
		}

		/// <summary>
		/// Sets the room types cached value on Db initialization.
		/// </summary>
		internal static void Init() {
			roomTypes = Db.Get().RoomTypes;
		}

		/// <summary>
		/// Triggers appropriate events on all items in a room.
		/// </summary>
		/// <param name="room">The room that is being updated.</param>
		/// <param name="items">The items that are in the room to receive updates.</param>
		private static void TriggerEvents(Room room, IList<KPrefabID> items) {
			int n = items.Count;
			for (int i = 0; i < n; i++) {
				var prefabID = items[i];
				if (prefabID != null)
					prefabID.Trigger((int)GameHashes.UpdateRoom, room);
			}
		}

		/// <summary>
		/// Removes buildings and plants from a room.
		/// </summary>
		/// <param name="room">The room that is being modified or destroyed.</param>
		/// <param name="items">The items to remove from the room.</param>
		private static void Unassign(Room room, IList<KPrefabID> items) {
			int n = items.Count;
			for (int i = 0; i < n; i++) {
				var prefabID = items[i];
				if (prefabID != null) {
					prefabID.Trigger((int)GameHashes.UpdateRoom, null);
					if (prefabID.TryGetComponent(out Assignable assignable) && assignable.
							assignee == room)
						assignable.Unassign();
				}
			}
		}

		/// <summary>
		/// Updates the based game's room prober list of all rooms for achievements, mingling,
		/// and other references.
		/// </summary>
		private readonly IList<Room> allRooms;

		/// <summary>
		/// Queues up the cells visited by the cavity filler for building updates on the
		/// foreground thread.
		/// </summary>
		private readonly ConcurrentQueue<int> buildingChanges;

		/// <summary>
		/// Stores the cavity used for each cell.
		/// </summary>
		private readonly HandleVector<int>.Handle[] cavityForCell;

		/// <summary>
		/// Stores all cavities with handles for quick access by index.
		/// </summary>
		private readonly KCompactedVector<CavityInfo> cavityInfos;

		/// <summary>
		/// The cavities that have been destroyed by the background thread but not yet freed
		/// on the foreground.
		/// </summary>
		private readonly ConcurrentQueue<HandleVector<int>.Handle> destroyed;

		/// <summary>
		/// Whether the prober has been destroyed.
		/// </summary>
		private volatile bool disposed;

		/// <summary>
		/// The queue of cells for the flood fill to check.
		/// </summary>
		private readonly Queue<int> floodFilling;

		/// <summary>
		/// Triggers a single first pass on every room.
		/// </summary>
		private volatile bool initialized;
		
		private readonly ISet<HandleVector<int>.Handle> pendingDestroy;

		/// <summary>
		/// The solid changes that are currently pending.
		/// </summary>
		private readonly ICollection<int> pendingSolidChanges;

		/// <summary>
		/// Triggered when a room needs to be updated.
		/// </summary>
		private readonly EventWaitHandle roomsChanged;

		/// <summary>
		/// Queues up changes to solid tiles that cause cavity rebuilds.
		/// </summary>
		private readonly ConcurrentQueue<int> solidChanges;

		private readonly IList<KPrefabID> tempIDs;

		/// <summary>
		/// The cells visited by the flood fill.
		/// </summary>
		private readonly ISet<int> visitedCells;

		internal BackgroundRoomProber() {
			int n = Grid.CellCount;
			allRooms = Game.Instance.roomProber.rooms;
			buildingChanges = new ConcurrentQueue<int>();
			cavityForCell = new HandleVector<int>.Handle[n];
			cavityInfos = new KCompactedVector<CavityInfo>(2048);
			// ConcurrentBag is leaky and slow: https://ayende.com/blog/156097/the-high-cost-of-concurrentbag-in-net-4-0
			destroyed = new ConcurrentQueue<HandleVector<int>.Handle>();
			disposed = false;
			floodFilling = new Queue<int>();
			initialized = false;
			pendingDestroy = new HashSet<HandleVector<int>.Handle>();
			pendingSolidChanges = new HashSet<int>();
			roomsChanged = new AutoResetEvent(false);
			solidChanges = new ConcurrentQueue<int>();
			tempIDs = new List<KPrefabID>(32);
			visitedCells = new HashSet<int>();
			for (int i = 0; i < n; i++)
				cavityForCell[i].Clear();
		}

		/// <summary>
		/// Adds the building or plant in the cell to its room.
		/// </summary>
		/// <param name="cell">The cell to query.</param>
		/// <param name="cavity">The cavity for that cell.</param>
		private void AddBuildingToRoom(int cell, CavityInfo cavity) {
			var go = Grid.Objects[cell, (int)ObjectLayer.Building];
			bool scanPlants = false, scanBuildings = false, dirty = false, found = false;
			if (go != null && go.TryGetComponent(out KPrefabID prefabID)) {
				// Is this entity already in the list?
				if (go.TryGetComponent(out Deconstructable _)) {
					var buildings = cavity.buildings;
					int n = buildings.Count;
					for (int i = 0; i < n; i++) {
						var building = buildings[i];
						if (building != null)
							tempIDs.Add(building);
						else
							dirty = true;
						if (building == prefabID)
							found = true;
					}
					if (dirty) {
						buildings.Clear();
						buildings.AddRange(tempIDs);
					}
					tempIDs.Clear();
					if (!found)
						cavity.AddBuilding(prefabID);
					scanBuildings = true;
				} else if (go.HasTag(GameTags.Plant) && !go.HasTag(TREE_BRANCH_TAG)) {
					var plants = cavity.plants;
					int n = plants.Count;
					for (int i = 0; i < n; i++) {
						var plant = plants[i];
						if (plant != null)
							tempIDs.Add(plant);
						else
							dirty = true;
						if (plant == prefabID)
							found = true;
					}
					if (dirty) {
						plants.Clear();
						plants.AddRange(tempIDs);
					}
					tempIDs.Clear();
					if (!found)
						cavity.AddPlants(prefabID);
					scanPlants = true;
				}
			}
			// Because this class no longer deletes and recreates the room, need to scan and
			// purge dead buildings from the list
			if (!scanBuildings)
				dirty |= RemoveDestroyed(cavity.buildings);
			if (!scanPlants)
				dirty |= RemoveDestroyed(cavity.plants);
			if (dirty)
				cavity.dirty = true;
		}

		/// <summary>
		/// If the cell is not already in a valid room, creates a room with this cell as the
		/// seed.
		/// </summary>
		/// <param name="cell">The starting cell.</param>
		private void CreateCavityFrom(int cell) {
			var visited = visitedCells;
			if (!RoomProber.CavityFloodFiller.IsWall(cell) && visited.Add(cell)) {
				int n = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue,
					maxY = int.MinValue;
				bool filled;
				HandleVector<int>.Handle targetCavity;
				var queue = floodFilling;
				var cavity = new CavityInfo();
				lock (cavityInfos) {
					targetCavity = cavityInfos.Allocate(cavity);
				}
				do {
					if (RoomProber.CavityFloodFiller.IsWall(cell))
						// Walls and doors have no room
						cavityForCell[cell].Clear();
					else {
						int above = Grid.CellAbove(cell), below = Grid.CellBelow(cell),
							left = Grid.CellLeft(cell), right = Grid.CellRight(cell);
						Grid.CellToXY(cell, out int x, out int y);
						cavityForCell[cell] = targetCavity;
						n++;
						if (x < minX)
							minX = x;
						if (x > maxX)
							maxX = x;
						if (y < minY)
							minY = y;
						if (y > maxY)
							maxY = y;
						buildingChanges.Enqueue(cell);
						if (Grid.IsValidCell(above) && visited.Add(above))
							queue.Enqueue(above);
						if (Grid.IsValidCell(below) && visited.Add(below))
							queue.Enqueue(below);
						if (Grid.IsValidCell(left) && visited.Add(left))
							queue.Enqueue(left);
						if (Grid.IsValidCell(right) && visited.Add(right))
							queue.Enqueue(right);
					}
					filled = queue.Count > 0;
					if (filled)
						cell = queue.Dequeue();
				} while (filled);
				cavity.minX = minX;
				cavity.minY = minY;
				cavity.maxX = maxX;
				cavity.maxY = maxY;
				cavity.numCells = n;
			}
		}

		/// <summary>
		/// Creates a new room and updates its type. Does not trigger events on all buildings
		/// in it!
		/// </summary>
		/// <param name="cavity">The cavity that this room represents.</param>
		/// <returns>The room created.</returns>
		private Room CreateRoom(CavityInfo cavity) {
			var room = new Room { cavity = cavity };
			cavity.room = room;
			room.roomType = roomTypes.GetRoomType(room);
			allRooms.Add(room);
			Assign(room);
			return room;
		}

		/// <summary>
		/// Cleans up all references to a room.
		/// </summary>
		/// <param name="room">The room that is being destroyed.</param>
		private void DestroyRoom(Room room) {
			if (room != null) {
				Unassign(room, room.buildings);
				Unassign(room, room.plants);
				room.CleanUp();
				allRooms.Remove(room);
			}
		}

		public void Dispose() {
			if (!disposed) {
				disposed = true;
				roomsChanged.Set();
				Instance = null;
			}
		}

		/// <summary>
		/// Gets the cavity for the specified grid cell.
		/// </summary>
		/// <param name="cell">The grid cell to look up.</param>
		/// <returns>The cavity for that cell, or null if there is no matching cavity.</returns>
		public CavityInfo GetCavityForCell(int cell) {
			CavityInfo cavity = null;
			if (Grid.IsValidCell(cell))
				lock (cavityInfos) {
					var id = cavityForCell[cell];
					if (id.IsValid())
						cavity = cavityInfos.GetData(id);
				}
			return cavity;
		}

		public override void OnCleanUp() {
			Dispose();
			base.OnCleanUp();
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			Instance = this;
		}

		public override void OnSpawn() {
			base.OnSpawn();
			var roomProberThread = new Thread(RunRoomProber) {
				IsBackground = true, Name = "Room Prober", Priority = ThreadPriority.
				BelowNormal
			};
			Util.ApplyInvariantCultureToThread(roomProberThread);
			// If any changes occurred in OnPrefabInit etc, they will be handled anyways by
			// initial init in thread
			while (solidChanges.TryDequeue(out _)) ;
			roomProberThread.Start();
		}

		/// <summary>
		/// Probes cavities in a loop.
		/// </summary>
		private void ProbeRooms() {
			var pending = pendingSolidChanges;
			while (roomsChanged.WaitOne() && !disposed) {
				if (initialized) {
					// Get list of all dirty cells
					while (solidChanges.TryDequeue(out int cell)) {
						int above = Grid.CellAbove(cell), below = Grid.CellBelow(cell),
							left = Grid.CellLeft(cell), right = Grid.CellRight(cell);
						if (Grid.IsValidCell(cell))
							pending.Add(cell);
						if (Grid.IsValidCell(above))
							pending.Add(above);
						if (Grid.IsValidCell(below))
							pending.Add(below);
						if (Grid.IsValidCell(left))
							pending.Add(left);
						if (Grid.IsValidCell(right))
							pending.Add(right);
					}
					UpdateCavities(pending);
				} else {
					int n = Grid.CellCount;
					for (int i = 0; i < n; i++)
						CreateCavityFrom(i);
					initialized = true;
				}
				visitedCells.Clear();
			}
		}

		/// <summary>
		/// Triggers re-evaluation of rooms that intersect the specified cell.
		/// </summary>
		/// <param name="cell">The cell that changed.</param>
		public void QueueBuildingChange(int cell) {
			buildingChanges.Enqueue(cell);
		}

		/// <summary>
		/// Triggers full rebuilds of rooms that intersect the specified cell.
		/// </summary>
		/// <param name="cell">The cell that changed.</param>
		public void QueueSolidChange(int cell) {
			solidChanges.Enqueue(cell);
		}

		/// <summary>
		/// Triggers a refresh of rooms. Only to be called on the foreground thread!
		/// </summary>
		public void Refresh() {
			int maxRoomSize = TuningData<RoomProber.Tuning>.Get().maxRoomSize;
			lock (cavityInfos) {
				while (buildingChanges.TryDequeue(out int cell)) {
					ref var cavityID = ref cavityForCell[cell];
					bool wall = RoomProber.CavityFloodFiller.IsWall(cell);
					bool valid = cavityID.IsValid();
					if (valid == wall)
						// If a wall building like a mesh door was built but did not trigger a
						// solid change update, then the tile will have a valid room on a wall
						// building, set up a solid change
						solidChanges.Enqueue(cell);
					else if (valid) {
						var cavity = cavityInfos.GetData(cavityID);
						int cells = cavity.numCells;
						if (cells > 0 && cells <= maxRoomSize)
							AddBuildingToRoom(cell, cavity);
					}
				}
				while (destroyed.TryDequeue(out var destroyedID))
					if (destroyedID.IsValid()) {
						var cavity = cavityInfos.GetData(destroyedID);
						if (cavity != null)
							DestroyRoom(cavity.room);
						cavityInfos.Free(destroyedID);
					}
				RefreshRooms();
			}
			if (FastTrackMod.GameRunning && (solidChanges.Count > 0 || !initialized))
				roomsChanged.Set();
		}

		/// <summary>
		/// Refreshes all rooms.
		/// </summary>
		private void RefreshRooms() {
			var cavities = cavityInfos.GetDataList();
			int maxRoomSize = TuningData<RoomProber.Tuning>.Get().maxRoomSize, n = cavities.
				Count;
			for (int i = 0; i < n; i++) {
				// No root canal found ;)
				var cavityInfo = cavities[i];
				if (cavityInfo.dirty) {
					if (cavityInfo.numCells > 0 && cavityInfo.numCells <= maxRoomSize)
						UpdateRoom(cavityInfo);
					cavityInfo.dirty = false;
				}
			}
		}

		/// <summary>
		/// Removes all destroyed items from the list of buildings or plants.
		/// </summary>
		/// <param name="items">The list to clean and purge.</param>
		private bool RemoveDestroyed(IList<KPrefabID> items) {
			int n = items.Count;
			bool dirty = false;
			for (int i = 0; i < n; i++) {
				var item = items[i];
				if (item == null)
					dirty = true;
				else
					tempIDs.Add(item);
			}
			if (dirty) {
				n = tempIDs.Count;
				items.Clear();
				for (int i = 0; i < n; i++)
					items.Add(tempIDs[i]);
			}
			tempIDs.Clear();
			return dirty;
		}

		/// <summary>
		/// Runs the room prober task in a background thread.
		/// </summary>
		private void RunRoomProber() {
#if DEBUG
			PUtil.LogDebug("Background room prober started");
#endif
			try {
				ProbeRooms();
			} catch (Exception e) {
				// Letting it go crashes to desktop with no log
				PUtil.LogException(e);
			}
			lock (cavityInfos) {
				cavityInfos.Clear();
			}
			roomsChanged.Dispose();
		}

		/// <summary>
		/// Handles all necessary room updates on the foreground thread.
		/// </summary>
		public void Sim200ms(float _) {
			DecorProviderRefreshFix.TriggerUpdates();
			Refresh();
		}

		/// <summary>
		/// Updates the cavities using flood fill on the list of specified cells.
		/// </summary>
		/// <param name="changedCells">The cells that changed.</param>
		private void UpdateCavities(ICollection<int> changedCells) {
			foreach (int cell in changedCells) {
				ref var cavityID = ref cavityForCell[cell];
				if (cavityID.IsValid()) {
					pendingDestroy.Add(cavityID);
					cavityID.Clear();
				}
			}
			foreach (int cell in changedCells)
				CreateCavityFrom(cell);
			changedCells.Clear();
			foreach (var handle in pendingDestroy)
				destroyed.Enqueue(handle);
			pendingDestroy.Clear();
		}

		/// <summary>
		/// Updates the room information for a cavity.
		/// </summary>
		/// <param name="cavity">The cavity to update.</param>
		public void UpdateRoom(CavityInfo cavity) {
			int baseCell, world;
			// Do not attempt to create rooms on undiscovered worlds
			if (cavity != null && Grid.IsValidCell(baseCell = Grid.XYToCell(cavity.minX,
					cavity.minY)) && (world = Grid.WorldIdx[baseCell]) != ClusterManager.
					INVALID_WORLD_IDX && ClusterManager.Instance.GetWorld(world).
					IsDiscovered) {
				var room = cavity.room;
				if (room == null)
					room = CreateRoom(cavity);
				else {
					// Refresh room without destroying and rebuilding if type is the same
					RoomType roomType = room.roomType, newRoomType = roomTypes.GetRoomType(
						room);
					if (newRoomType != roomType) {
						DestroyRoom(room);
						room = CreateRoom(cavity);
					}
				}
				// Trigger events on plants and buildings
				TriggerEvents(room, room.buildings);
				TriggerEvents(room, room.plants);
			}
		}

		/// <summary>
		/// Applied to RoomProber to get cavity information from the background prober.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.GetCavityForCell))]
		internal static class GetCavityForCell_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before GetCavityForCell runs.
			/// </summary>
			internal static bool Prefix(int cell, ref CavityInfo __result) {
				var inst = Instance;
				if (inst != null)
					__result = inst.GetCavityForCell(cell);
				else
					__result = null;
				return false;
			}
		}

		/// <summary>
		/// Applied to RoomProber to redirect changes in the global buildings layer to the
		/// background prober.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.OnBuildingsChanged))]
		internal static class OnBuildingsChanged_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before OnBuildingsChanged runs.
			/// </summary>
			internal static bool Prefix(int cell) {
				Instance?.QueueBuildingChange(cell);
				return false;
			}
		}

		/// <summary>
		/// Applied to RoomProber to redirect refresh requests to the background prober.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix() {
				Instance?.Refresh();
				return false;
			}
		}

		/// <summary>
		/// Applied to RoomProber to shut off its original Sim1000ms method.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.Sim1000ms))]
		internal static class Sim1000ms_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before Sim1000ms runs.
			/// </summary>
			internal static bool Prefix() {
				return false;
			}
		}

		/// <summary>
		/// Applied to RoomProber to redirect changes in solid tiles to the background prober.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.SolidChangedEvent), typeof(int),
			typeof(bool))]
		internal static class SolidChangedEvent_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before SolidChangedEvent runs.
			/// </summary>
			internal static bool Prefix(int cell, bool ignoreDoors) {
				if (!ignoreDoors || !Grid.HasDoor[cell])
					Instance?.QueueSolidChange(cell);
				return false;
			}
		}

		/// <summary>
		/// Applied to RoomProber to redirect room update requests to the background prober.
		/// </summary>
		[HarmonyPatch(typeof(RoomProber), nameof(RoomProber.UpdateRoom))]
		internal static class UpdateRoom_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before UpdateRoom runs.
			/// </summary>
			internal static bool Prefix(CavityInfo cavity) {
				Instance?.UpdateRoom(cavity);
				return false;
			}
		}
	}
}
