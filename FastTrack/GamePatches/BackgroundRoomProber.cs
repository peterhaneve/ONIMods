/*
 * Copyright 2025 Peter Han
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
using PeterHan.FastTrack.CritterPatches;
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
		/// <summary>
		/// Buildings with this tag, even if they are not Deconstructable (like the base game
		/// enforces), will get added to the buildings list and sent rebuild events, for
		/// compatibility with other mods.
		/// </summary>
		public const string REGISTER_ROOM = "RegisterRoom";

		private static Database.RoomTypes roomTypes;

		private static readonly Tag REGISTER_ROOM_TAG = new Tag(REGISTER_ROOM);

		/// <summary>
		/// The singleton instance of this class.
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
			var inst = Instance;
			if (inst != null)
				inst.Dispose();
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
		/// Used in multiple locations to obtain the overcrowding monitor for a critter
		/// component quickly.
		/// </summary>
		/// <param name="critter">The critter to query. Must not be null.</param>
		/// <param name="smi">The location to store the overcrowding monitor.</param>
		/// <returns>true if the monitor was found, or false otherwise.</returns>
		internal static bool TryGetOvercrowdingMonitor(KMonoBehaviour critter,
				out OvercrowdingMonitor.Instance smi) {
			OvercrowdingMonitor.Instance result = null;
			bool found = critter.TryGetComponent(out StateMachineController smc) &&
				(result = smc.GetSMI<OvercrowdingMonitor.Instance>()) != null;
			smi = result;
			return found;
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
					prefabID.Trigger((int)GameHashes.UpdateRoom);
					if (prefabID.TryGetComponent(out Assignable assignable) && assignable.
							assignee == room)
						assignable.Unassign();
				}
			}
		}

		public int UpdateCount => updateCount;

		/// <summary>
		/// Updates the based game's room prober list of all rooms for achievements, mingling,
		/// and other references.
		/// </summary>
		private readonly IList<Room> allRooms;
		
		/// <summary>
		/// Avoid multiple destruction on the foreground thread.
		/// </summary>
		private readonly ISet<HandleVector<int>.Handle> alreadyDestroyed;

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
		private readonly ConcurrentHandleVector<CavityInfo> cavityInfos;
		
		/// <summary>
		/// Queues up requests to recalculate the room type.
		/// </summary>
		private readonly Queue<int> cellChanges;

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

		/// <summary>
		/// The maximum room size, updated when necessary so changes take effect immediately.
		/// </summary>
		private int maxRoomSize;
		
		private readonly ISet<HandleVector<int>.Handle> pendingDestroy;

		/// <summary>
		/// The solid changes that are currently pending.
		/// </summary>
		private readonly ICollection<int> pendingSolidChanges;
		
		/// <summary>
		/// Recycle room cavities if possible.
		/// </summary>
		private readonly Queue<CavityInfo> recycled;

		/// <summary>
		/// The critters which were occupying rooms that were destroyed.
		/// </summary>
		private readonly ConcurrentQueue<KPrefabID> releasedCritters;

		/// <summary>
		/// Triggered when a room needs to be updated.
		/// </summary>
		private readonly EventWaitHandle roomsChanged;
		
		/// <summary>
		/// Triggered when the room thread is ready.
		/// </summary>
		private readonly EventWaitHandle roomsReady;

		/// <summary>
		/// Queues up changes to solid tiles that cause cavity rebuilds.
		/// </summary>
		private readonly ConcurrentQueue<int> solidChanges;

		private readonly IList<KPrefabID> tempIDs;

		/// <summary>
		/// How many updates have been performed.
		/// </summary>
		private volatile int updateCount;

		/// <summary>
		/// The cells visited by the flood fill.
		/// </summary>
		private readonly ISet<int> visitedCells;

		internal BackgroundRoomProber() {
			int n = Grid.CellCount;
			allRooms = Game.Instance.roomProber.rooms;
			alreadyDestroyed = new HashSet<HandleVector<int>.Handle>();
			buildingChanges = new ConcurrentQueue<int>();
			cavityForCell = new HandleVector<int>.Handle[n];
			cavityInfos = new ConcurrentHandleVector<CavityInfo>(256);
			cellChanges = new Queue<int>();
			// ConcurrentBag is leaky and slow: https://ayende.com/blog/156097/the-high-cost-of-concurrentbag-in-net-4-0
			destroyed = new ConcurrentQueue<HandleVector<int>.Handle>();
			disposed = false;
			floodFilling = new Queue<int>();
			initialized = false;
			// Only a default value to avoid crashes while loading - the actual maximum size
			// is read from the Db
			maxRoomSize = 128;
			pendingDestroy = new HashSet<HandleVector<int>.Handle>();
			pendingSolidChanges = new HashSet<int>();
			recycled = new Queue<CavityInfo>();
			releasedCritters = new ConcurrentQueue<KPrefabID>();
			roomsChanged = new AutoResetEvent(false);
			roomsReady = new AutoResetEvent(false);
			solidChanges = new ConcurrentQueue<int>();
			tempIDs = new List<KPrefabID>(32);
			updateCount = 0;
			visitedCells = new HashSet<int>();
			for (int i = 0; i < n; i++)
				cavityForCell[i].Clear();
		}

		/// <summary>
		/// Adds the building or plant in the cell to its room.
		/// </summary>
		/// <param name="cell">The cell to query.</param>
		/// <param name="cavityID">The cavity ID for that cell.</param>
		private void AddBuildingToRoom(int cell, HandleVector<int>.Handle cavityID) {
			var cavity = cavityInfos.GetData(cavityID);
			if (cavity != null) {
				int cells = cavity.NumCells;
				if (cells > 0 && cells <= maxRoomSize) {
					var go = Grid.Objects[cell, (int)ObjectLayer.Building];
					bool scanPlants = false, scanBuildings = false, dirty = false;
					if (go != null && go.TryGetComponent(out KPrefabID prefabID)) {
						// Is this entity already in the list?
						if (go.TryGetComponent(out Deconstructable _) || prefabID.HasTag(
								REGISTER_ROOM_TAG)) {
							dirty = AddBuildingToRoom(cavity, prefabID);
							scanBuildings = true;
						} else if (prefabID.HasTag(GameTags.Plant) && !prefabID.HasTag(
								GameTags.PlantBranch)) {
							dirty = AddPlantToRoom(cavity, prefabID);
							scanPlants = true;
						}
					}
					// Because this class no longer deletes and recreates the room, need to
					// scan and purge dead buildings from the list
					if (!scanBuildings)
						dirty |= RemoveDestroyed(cavity.buildings);
					if (!scanPlants)
						dirty |= RemoveDestroyed(cavity.plants);
					if (dirty)
						cavity.dirty = true;
				}
			}
		}
		
		/// <summary>
		/// Adds a completed building to a room.
		/// </summary>
		/// <param name="cavity">The room where the building should be added.</param>
		/// <param name="prefabID">The building's prefab ID.</param>
		/// <returns>true if the room was updated, or false otherwise.</returns>
		private bool AddBuildingToRoom(CavityInfo cavity, KPrefabID prefabID) {
			bool dirty = false, found = false;
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
			return dirty;
		}

		/// <summary>
		/// Adds a plant to a room.
		/// </summary>
		/// <param name="cavity">The room where the plant should be added.</param>
		/// <param name="prefabID">The plant's prefab ID.</param>
		/// <returns>true if the room was updated, or false otherwise.</returns>
		private bool AddPlantToRoom(CavityInfo cavity, KPrefabID prefabID) {
			bool dirty = false, found = false;
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
			return dirty;
		}

		/// <summary>
		/// If the cell is not already in a valid room, creates a room with this cell as the
		/// seed.
		/// </summary>
		/// <param name="cell">The starting cell.</param>
		private void CreateCavityFrom(int cell) {
			var visited = visitedCells;
			if (!RoomProber.IsCavityBoundary(cell) && visited.Add(cell)) {
				int n = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue,
					maxY = int.MinValue;
				var cavity = (recycled.Count > 0) ? recycled.Dequeue() : new CavityInfo();
				bool filled;
				var targetCavity = cavityInfos.Allocate(cavity);
				var queue = floodFilling;
				var cells = cavity.cells;
				cavity.dirty = true;
				cavity.handle = targetCavity;
				if (cells == null)
					cavity.cells = cells = new List<int>(n);
				do {
					if (RoomProber.IsCavityBoundary(cell))
						// Walls and doors have no room
						cavityForCell[cell].Clear();
					else {
						int above = Grid.CellAbove(cell), below = Grid.CellBelow(cell),
							left = Grid.CellLeft(cell), right = Grid.CellRight(cell);
						Grid.CellToXY(cell, out int x, out int y);
						cavityForCell[cell] = targetCavity;
						cells.Add(cell);
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
				cavity.occupancy.dirty = true;
			}
		}

		/// <summary>
		/// Creates a new room and updates its type. Does not trigger events on all buildings
		/// in it!
		/// </summary>
		/// <param name="cavity">The cavity that this room represents.</param>
		/// <param name="room">The old room to use if available.</param>
		/// <returns>The room created.</returns>
		private Room CreateRoom(CavityInfo cavity, Room room = null) {
			if (room == null)
				room = new Room { cavity = cavity };
			else
				room.cavity = cavity;
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
				room.primary_buildings.Clear();
				room.current_owners.Clear();
				room.CleanUp();
				allRooms.Remove(room);
			}
		}

		public void Dispose() {
			if (!disposed) {
				cellChanges.Clear();
				roomsChanged.Set();
				roomsReady.Reset();
				disposed = true;
				Instance = null;
				recycled.Clear();
			}
		}

		/// <summary>
		/// Gets the cavity for the specified grid cell.
		/// </summary>
		/// <param name="cell">The grid cell to look up.</param>
		/// <returns>The cavity for that cell, or null if there is no matching cavity.</returns>
		public CavityInfo GetCavityForCell(int cell) {
			CavityInfo cavity = null;
			if (Grid.IsValidCell(cell)) {
				var id = cavityForCell[cell];
				if (id.IsValid()) {
					cavity = cavityInfos.GetData(id);
					if (cavity == null) {
						PUtil.LogWarning("Cavity {0:D} still present after destroy at cell {1:D}!".
							F(id._index, cell));
						cavityForCell[cell] = HandleVector<int>.Handle.InvalidHandle;
					}
				}
			}
			return cavity;
		}

		/// <summary>
		/// Synchronously waits for one room update, then force updates all critters in rooms
		/// to ensure the first Critter Sensor update uses good data.
		/// </summary>
		private void InitialUpdate() {
			var creatures = Components.Brains.Items;
			roomsReady.Reset();
			roomsChanged.Set();
			roomsReady.WaitOne(3000);
			Postprocess();
			int n = creatures.Count;
			// To work around the Critter Sensor requiring critters to be added to the room
			// on load, force update all overcrowding monitors now
			for (int i = 0; i < n; i++)
				if (TryGetOvercrowdingMonitor(creatures[i], out var smi))
					OvercrowdingMonitor.UpdateState(smi, 0.0f);
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
			while (solidChanges.TryDequeue(out _)) { }
			roomProberThread.Start();
		}

		/// <summary>
		/// Post processes the results of the background room prober thread.
		/// </summary>
		private void Postprocess() {
			while (cellChanges.Count > 0) {
				int cell = cellChanges.Dequeue();
				ref var cavityID = ref cavityForCell[cell];
				bool valid = cavityID.IsValid();
				if (valid == RoomProber.IsCavityBoundary(cell))
					// If a wall building like a mesh door was built but did not trigger a
					// solid change update, then the tile will have a valid room on a wall
					// building, set up a solid change
					solidChanges.Enqueue(cell);
				else if (valid)
					AddBuildingToRoom(cell, cavityID);
			}
			while (buildingChanges.TryDequeue(out int cell)) {
				ref var cavityID = ref cavityForCell[cell];
				if (cavityID.IsValid())
					AddBuildingToRoom(cell, cavityID);
			}
			while (destroyed.TryDequeue(out var destroyedID))
				if (destroyedID.IsValid() && alreadyDestroyed.Add(destroyedID)) {
					var cavity = cavityInfos.GetData(destroyedID);
					if (cavity != null) {
						DestroyRoom(cavity.room);
						// Clean up room state
						cavity.handle = HandleVector<int>.Handle.InvalidHandle;
						cavity.room = null;
						cavity.buildings.Clear();
						cavity.eggs.Clear();
						cavity.creatures.Clear();
						cavity.fishes.Clear();
						cavity.fish_eggs.Clear();
						cavity.cells.Clear();
						cavity.otherEntities.Clear();
						recycled.Enqueue(cavity);
					}
					cavityInfos.Free(destroyedID);
				}
			alreadyDestroyed.Clear();
			foreach (var pair in cavityInfos.BackingDictionary) {
				// No root canal found ;)
				var cavityInfo = pair.Value;
				if (cavityInfo.dirty) {
					int cells = cavityInfo.NumCells;
					if (cells > 0 && cells <= maxRoomSize)
						UpdateRoom(cavityInfo);
					cavityInfo.dirty = false;
				}
			}
			// Force a room update for each critter that was in a refreshed room
			while (releasedCritters.TryDequeue(out var critter))
				if (critter != null && TryGetOvercrowdingMonitor(critter, out var smi))
					smi.RoomRefreshUpdateCavity();
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
				roomsReady.Set();
			}
			roomsChanged.Dispose();
			roomsReady.Dispose();
		}

		/// <summary>
		/// Triggers re-evaluation of rooms that intersect the specified cell.
		/// </summary>
		/// <param name="cell">The cell that changed.</param>
		public void QueueBuildingChange(int cell) {
			cellChanges.Enqueue(cell);
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
		public void Refresh(bool allowStart = true) {
			bool init = initialized;
			maxRoomSize = TuningData<RoomProber.Tuning>.Get().maxRoomSize;
			if (allowStart || init) {
				if (init) {
					Postprocess();
					if (FastTrackMod.GameRunning && solidChanges.Count > 0)
						roomsChanged.Set();
				} else
					InitialUpdate();
			}
			Interlocked.Increment(ref updateCount);
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
			cavityInfos.Clear();
			roomsChanged.Dispose();
		}

		/// <summary>
		/// Handles all necessary room updates on the foreground thread.
		/// </summary>
		public void Sim200ms(float _) {
			DecorProviderRefreshFix.TriggerUpdates();
			Refresh(false);
		}

		/// <summary>
		/// Updates the cavities using flood fill on the list of specified cells.
		/// </summary>
		/// <param name="changedCells">The cells that changed.</param>
		private void UpdateCavities(ICollection<int> changedCells) {
			foreach (int cell in changedCells) {
				ref var cavityID = ref cavityForCell[cell];
				if (cavityID.IsValid()) {
					var cavity = cavityInfos.GetData(cavityID);
					if (cavity != null) {
						var creatures = cavity.creatures;
						lock (creatures) {
							int n = creatures.Count;
							for (int i = 0; i < n; i++)
								// These critters need a room refresh on foreground thread
								releasedCritters.Enqueue(creatures[i]);
						}
						pendingDestroy.Add(cavityID);
						cavityID.Clear();
					}
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
			int baseCell;
			// Do not attempt to create rooms on undiscovered worlds
			if (cavity != null && Grid.IsValidCell(baseCell = Grid.XYToCell(cavity.minX,
					cavity.minY))) {
				int worldIndex = Grid.WorldIdx[baseCell];
				WorldContainer world;
				// When freeing grid space (like deconstructing a rocket module), the world
				// index is still valid, but GetWorld will fail on it
				if (worldIndex != ClusterManager.INVALID_WORLD_IDX && (world = ClusterManager.
						Instance.GetWorld(worldIndex)) != null && world.IsDiscovered) {
					var room = cavity.room;
					if (room == null)
						room = CreateRoom(cavity);
					else {
						// Refresh room without destroying and rebuilding if type is the same
						RoomType roomType = room.roomType, newRoomType = roomTypes.GetRoomType(
							room);
						if (newRoomType != roomType) {
							DestroyRoom(room);
							CreateRoom(cavity, room);
						}
					}
					// Trigger events on plants and buildings
					TriggerEvents(room, room.buildings);
					TriggerEvents(room, room.plants);
				}
			}
		}
		
		/// <summary>
		/// Applied to OvercrowdingMonitor.Instance to avoid a race condition when critters
		/// are added to rooms.
		/// </summary>
		[HarmonyPatch(typeof(OvercrowdingMonitor.Instance), nameof(OvercrowdingMonitor.
			Instance.AddToCavity))]
		internal static class AddToCavity_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before AddToCavity runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(OvercrowdingMonitor.Instance __instance) {
				var inst = Instance;
				if (inst != null) {
					var cavity = __instance.cavity;
					var kpid = __instance.kpid;
					// Avoid race condition modifying the creature lists
					lock (cavity.creatures) {
						if (__instance.IsEgg) {
							cavity.eggs.Add(kpid);
							if (OvercrowdingMonitor.FetchIsFishEgg(kpid))
								cavity.fish_eggs.Add(kpid);
						} else {
							cavity.creatures.Add(kpid);
							if (__instance.IsFish)
								cavity.fishes.Add(kpid);
						}
						cavity.occupancy.dirty = true;
					}
				}
				return inst == null;
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(int cell, ref CavityInfo __result) {
				var inst = Instance;
				if (inst != null)
					__result = inst.GetCavityForCell(cell);
				return inst == null;
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(int cell) {
				var inst = Instance;
				if (inst != null)
					inst.QueueBuildingChange(cell);
				return inst == null;
			}
		}
		
		/// <summary>
		/// Applied to OvercrowdingMonitor.Instance to avoid a race condition when critters
		/// are removed from rooms.
		/// </summary>
		[HarmonyPatch(typeof(OvercrowdingMonitor.Instance), nameof(OvercrowdingMonitor.
			Instance.RemoveFromCavity))]
		internal static class RemoveFromCavity_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.BackgroundRoomRebuild;

			/// <summary>
			/// Applied before RemoveFromCavity runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(OvercrowdingMonitor.Instance __instance) {
				var inst = Instance;
				if (inst != null) {
					var cavity = __instance.cavity;
					var kpid = __instance.kpid;
					// Avoid race condition modifying the creature lists
					lock (cavity.creatures) {
						if (__instance.IsEgg) {
							cavity.RemoveFromCavity(kpid, cavity.eggs);
							if (OvercrowdingMonitor.FetchIsFishEgg(kpid))
								cavity.RemoveFromCavity(kpid, cavity.fish_eggs);
						} else {
							cavity.RemoveFromCavity(kpid, cavity.creatures);
							if (__instance.IsFish)
								cavity.RemoveFromCavity(kpid, cavity.fishes);
						}
						cavity.occupancy.dirty = true;
					}
				}
				return inst == null;
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
				var inst = Instance;
				if (inst != null)
					inst.Refresh();
				return inst == null;
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
			[HarmonyPriority(Priority.Low)]
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(int cell, bool ignoreDoors) {
				var inst = Instance;
				if (inst != null && (!ignoreDoors || !Grid.HasDoor[cell]))
					inst.QueueSolidChange(cell);
				return inst == null;
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(CavityInfo cavity) {
				var inst = Instance;
				if (inst != null)
					inst.UpdateRoom(cavity);
				return inst == null;
			}
		}
	}
}
