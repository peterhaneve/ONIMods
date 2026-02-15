/*
 * Copyright 2026 Peter Han
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
using UnityEngine;
using UnityEngine.Pool;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Makes CellChangeMonitor potentially faster by only tracking transforms that have a
	/// listener added. Crazy, right?
	/// </summary>
	public sealed class FastCellChangeMonitor {
		/// <summary>
		/// A non-locked version of the Singleton instance.
		/// </summary>
		internal static FastCellChangeMonitor FastInstance { get; private set; }

		/// <summary>
		/// Pools event entries since they turn over quite a bit.
		/// </summary>
		private static readonly ObjectPool<EventEntry> POOL = new ObjectPool<EventEntry>(
			() => new EventEntry(), null, (entry) => entry.Clear(), null, false, 10, 256);

		/// <summary>
		/// Creates the singleton instance.
		/// </summary>
		internal static void CreateInstance() {
			FastInstance = new FastCellChangeMonitor();
		}
		
		/// <summary>
		/// Stores the transforms that are currently being processed.
		/// </summary>
		private IDictionary<int, EventEntry> dirtyTransforms;

		/// <summary>
		/// Maps transformations to event handlers that can handle when they move.
		/// </summary>
		private readonly IDictionary<int, EventEntry> eventHandlers;

		/// <summary>
		/// Stores the grid width, but only after initialization, to prevent a variety of race
		/// conditions that could happen during grid initialization.
		/// </summary>
		private int gridWidth;

		/// <summary>
		/// Stores the transforms which are currently moving.
		/// </summary>
		private IDictionary<int, EventEntry> movingTransforms;

		/// <summary>
		/// The ID of the next handler to avoid a time consuming list walk on cleanup.
		/// </summary>
		private volatile uint nextHandler;

		/// <summary>
		/// Stores the transforms which were marked dirty during the last frame.
		/// </summary>
		private IDictionary<int, EventEntry> pendingDirtyTransforms;

		/// <summary>
		/// Stores the transforms which were moving last frame.
		/// </summary>
		private IDictionary<int, EventEntry> previouslyMovingTransforms;

		private FastCellChangeMonitor() {
			dirtyTransforms = new Dictionary<int, EventEntry>(256);
			eventHandlers = new Dictionary<int, EventEntry>(256);
			movingTransforms = new Dictionary<int, EventEntry>(256);
			pendingDirtyTransforms = new Dictionary<int, EventEntry>(256);
			previouslyMovingTransforms = new Dictionary<int, EventEntry>(256);
			gridWidth = 0;
			nextHandler = 0U;
		}

		/// <summary>
		/// Creates the tracking entry if necessary.
		/// </summary>
		/// <param name="id">The transform ID to create.</param>
		/// <param name="transform">The transform to track.</param>
		/// <returns>The current, or newly created entry.</returns>
		private EventEntry AddOrGet(int id, Transform transform) {
			if (!eventHandlers.TryGetValue(id, out var current)) {
				current = POOL.Get();
				current.transform = transform;
				eventHandlers.Add(id, current);
			}
			return current;
		}

		/// <summary>
		/// Arms the monitor and starts watching for changes.
		/// </summary>
		/// <param name="width">The current grid width.</param>
		internal void Arm(int width) {
			gridWidth = width;
		}

		/// <summary>
		/// Ensure nothing is leaked by clearing the event handler list on shutdown.
		/// </summary>
		public void Cleanup() {
			dirtyTransforms.Clear();
			eventHandlers.Clear();
			movingTransforms.Clear();
			pendingDirtyTransforms.Clear();
			previouslyMovingTransforms.Clear();
			Disarm();
		}

		/// <summary>
		/// Clears the last known cell of a transform.
		/// </summary>
		/// <param name="transform">The transform to reset.</param>
		public void ClearLastKnownCell(Transform transform) {
			if (transform != null && eventHandlers.TryGetValue(transform.GetInstanceID(),
					out var current))
				current.ClearLastKnownCell();
		}

		/// <summary>
		/// Cleans up the tracking entry if it has no listeners. Trees that fall in the forest
		/// with no one to hear them...
		/// </summary>
		/// <param name="id">The transform ID to clean up.</param>
		/// <param name="entry">The current entry.</param>
		private void CleanupIfEmpty(int id, EventEntry entry) {
			if (entry.moveHandlers.Count < 1 && entry.cellChangedHandlers.Count < 1) {
				eventHandlers.Remove(id);
				POOL.Release(entry);
			}
		}
		
		/// <summary>
		/// Disarms the monitor and stops watching for changes.
		/// </summary>
		internal void Disarm() {
			gridWidth = 0;
		}

		/// <summary>
		/// Checks if the transform is currently moving.
		/// 
		/// There are 2 calls to this method in ONI, both of which use an object that was
		/// just registered.
		/// </summary>
		/// <param name="transform">The transform to check.</param>
		/// <returns>true if it is moving, or false otherwise.</returns>
		public bool IsMoving(Transform transform) {
			return movingTransforms.ContainsKey(transform.GetInstanceID());
		}

		/// <summary>
		/// Marks the transform and its children as dirty.
		/// </summary>
		/// <param name="transform">The transform to mark dirty.</param>
		public void MarkDirty(Transform transform) {
			if (gridWidth > 0 && transform != null) {
				int n = transform.childCount, id = transform.GetInstanceID();
				if (eventHandlers.TryGetValue(id, out var entry))
					pendingDirtyTransforms[id] = entry;
				for (int i = 0; i < n; i++)
					MarkDirty(transform.GetChild(i));
			}
		}

		/// <summary>
		/// Registers a handler for when a transform moves to a new cell.
		/// </summary>
		/// <param name="transform">The transform to track.</param>
		/// <param name="callback">The event handler to register.</param>
		/// <returns>The instance ID of the transform thus registered.</returns>
		public ulong RegisterCellChangedHandler(Transform transform, Action<object> callback,
				object context) {
			int id = transform.GetInstanceID();
			var entry = AddOrGet(id, transform);
			uint uniqueID = nextHandler++;
			entry.cellChangedHandlers.Add(new EventEntry.CellChangeHandler(callback, context,
				uniqueID));
			return CellChangeMonitor.Join(id, uniqueID);
		}

		/// <summary>
		/// Registers a handler for when the moving state of an object changes.
		/// </summary>
		/// <param name="transform">The transform to track.</param>
		/// <param name="handler">The event handler to register.</param>
		/// <param name="context">The context to pass to the handler.</param>
		/// <returns>The ID of the new handler.</returns>
		public ulong RegisterMovementStateChanged(Transform transform,
				Action<Transform, bool, object> handler, object context) {
			int id = transform.GetInstanceID();
			var entry = AddOrGet(id, transform);
			uint uniqueID = nextHandler++;
			entry.moveHandlers.Add(new EventEntry.MoveHandler(handler, context, uniqueID));
			return CellChangeMonitor.Join(id, uniqueID);
		}

		/// <summary>
		/// Unregisters a handler from when a transform moves to a new cell.
		/// </summary>
		/// <param name="id">The ID of the transform to untrack.</param>
		/// <param name="callback">The event handler to unregister.</param>
		/// <returns>true if the handler was removed, or false if it was not found.</returns>
		public bool UnregisterCellChangedHandler(ulong id) {
			bool result = false;
			CellChangeMonitor.Split(id, out int iid, out uint uniqueID);
			if (eventHandlers.TryGetValue(iid, out var entry)) {
				result = entry.RemoveCellChangedHandler(uniqueID);
				if (result)
					CleanupIfEmpty(iid, entry);
			}
			return result;
		}

		/// <summary>
		/// Unregisters a handler from when the moving state of an object changes.
		/// </summary>
		/// <param name="id">The ID of the transform to untrack.</param>
		/// <param name="callback">The event handler to unregister.</param>
		/// <returns>true if the handler was removed, or false if it was not found.</returns>
		public bool UnregisterMovementStateChanged(ulong id) {
			bool result = false;
			CellChangeMonitor.Split(id, out int iid, out uint uniqueID);
			if (eventHandlers.TryGetValue(iid, out var entry)) {
				result = entry.RemoveMovementHandler(uniqueID);
				if (result)
					CleanupIfEmpty(iid, entry);
			}
			return result;
		}

		public void Update() {
			var si = Singleton<CellChangeMonitor>.Instance;
			// Swap the buffers
			var dirty = pendingDirtyTransforms;
			pendingDirtyTransforms = dirtyTransforms;
			dirtyTransforms = dirty;
			pendingDirtyTransforms.Clear();
			var moving = previouslyMovingTransforms;
			previouslyMovingTransforms = movingTransforms;
			movingTransforms = moving;
			moving.Clear();
			// Go through moved transforms and proc cell changed
			foreach (var pair in dirty) {
				int id = pair.Key;
				var entry = pair.Value;
				var transform = entry.transform;
				if (transform != null) {
					int oldCell = entry.lastKnownCell, newCell = si.PosToCell(transform.
						position);
					if (oldCell != newCell) {
						entry.lastKnownCell = newCell;
						entry.CallCellChangedHandlers();
					}
					moving.Add(id, entry);
					if (!previouslyMovingTransforms.ContainsKey(id))
						entry.CallMovementStateChangedHandlers(true);
				}
			}
			foreach (var pair in previouslyMovingTransforms) {
				var entry = pair.Value;
				if (entry.transform != null && !moving.ContainsKey(pair.Key))
					entry.CallMovementStateChangedHandlers(false);
			}
			dirty.Clear();
		}

		/// <summary>
		/// Stores the events for a particular transform.
		/// </summary>
		internal sealed class EventEntry {
			/// <summary>
			/// The transform to which these events are bound.
			/// </summary>
			public Transform transform;

			/// <summary>
			/// The handlers to call when this transform moves to a different cell.
			/// </summary>
			public readonly IList<CellChangeHandler> cellChangedHandlers;

			/// <summary>
			/// The cell this transform last occupied.
			/// </summary>
			public int lastKnownCell;

			/// <summary>
			/// The handlers to call when this transform starts or stops moving.
			/// </summary>
			public readonly IList<MoveHandler> moveHandlers;

			/// <summary>
			/// If handlers are being actively invoked, safely remove the ones to be destroyed.
			/// </summary>
			private volatile ICollection<uint> pendingDestroy;

			public EventEntry() {
				cellChangedHandlers = new List<CellChangeHandler>(8);
				lastKnownCell = Grid.InvalidCell;
				moveHandlers = new List<MoveHandler>(8);
				pendingDestroy = null;
				transform = null;
			}

			/// <summary>
			/// Calls the cell changed handlers for this transform.
			/// </summary>
			public void CallCellChangedHandlers() {
				// Some cell changed handlers modify the list :(
				var destroySet = ListPool<uint, EventEntry>.Allocate();
				int n = cellChangedHandlers.Count;
				pendingDestroy = destroySet;
				for (int i = 0; i < n; i++) {
					var handler = cellChangedHandlers[i];
					// Technically a hash set is faster asymptotically, but the removal case
					// is uncommon and lists are quicker due to lower memory overhead
					if (!destroySet.Contains(handler.UniqueID))
						handler.Invoke();
				}
				// Clean up removed handlers
				for (int i = n - 1; i >= 0; i--)
					if (destroySet.Contains(cellChangedHandlers[i].UniqueID))
						cellChangedHandlers.RemoveAt(i);
				pendingDestroy = null;
				destroySet.Recycle();
			}

			/// <summary>
			/// Calls the movement state changed handlers for this transform.
			/// </summary>
			/// <param name="newState">The new movement state.</param>
			public void CallMovementStateChangedHandlers(bool newState) {
				var destroySet = ListPool<uint, EventEntry>.Allocate();
				int n = moveHandlers.Count;
				pendingDestroy = destroySet;
				for (int i = 0; i < n; i++) {
					var handler = moveHandlers[i];
					if (!destroySet.Contains(handler.UniqueID))
						handler.Invoke(transform, newState);
				}
				for (int i = n - 1; i >= 0; i--)
					if (destroySet.Contains(moveHandlers[i].UniqueID))
						moveHandlers.RemoveAt(i);
				pendingDestroy = null;
				destroySet.Recycle();
			}
			
			/// <summary>
			/// Clears the state before sending the object back to the pool.
			/// </summary>
			public void Clear() {
				cellChangedHandlers.Clear();
				lastKnownCell = Grid.InvalidCell;
				moveHandlers.Clear();
				pendingDestroy = null;
				transform = null;
			}

			/// <summary>
			/// Resets the last known cell.
			/// </summary>
			public void ClearLastKnownCell() {
				lastKnownCell = Grid.InvalidCell;
			}
			
			/// <summary>
			/// Removes a cell change handler.
			/// </summary>
			/// <param name="uniqueID">The handler to remove.</param>
			/// <returns>true if a handler was removed, or false otherwise</returns>
			public bool RemoveCellChangedHandler(uint uniqueID) {
				int n = cellChangedHandlers.Count;
				bool result = false;
				var pd = pendingDestroy;
				for (int i = 0; i < n; i++) {
					// There are not too many handlers and fewer changes, lists are efficient
					var ch = cellChangedHandlers[i];
					if (ch.UniqueID == uniqueID) {
						if (pd != null)
							pd.Add(uniqueID);
						else
							cellChangedHandlers.RemoveAt(i);
						result = true;
						break;
					}
				}
				return result;
			}
			
			/// <summary>
			/// Removes a movement changed handler.
			/// </summary>
			/// <param name="uniqueID">The handler to remove.</param>
			/// <returns>true if a handler was removed, or false otherwise</returns>
			public bool RemoveMovementHandler(uint uniqueID) {
				int n = moveHandlers.Count;
				bool result = false;
				var pd = pendingDestroy;
				for (int i = 0; i < n; i++) {
					var ch = moveHandlers[i];
					if (ch.UniqueID == uniqueID) {
						if (pd != null)
							pd.Add(uniqueID);
						else
							moveHandlers.RemoveAt(i);
						result = true;
						break;
					}
				}
				return result;
			}

			public override string ToString() {
				return "Event Handlers for {0}: {1:D} on change, {2:D} on move".F(transform.
					name, cellChangedHandlers.Count, moveHandlers.Count);
			}

			/// <summary>
			/// Stores the cell change handler and the context to use when calling it.
			/// </summary>
			public sealed class CellChangeHandler : IEquatable<CellChangeHandler> {
				public readonly Action<object> Handler;

				public readonly object Context;
				
				public readonly uint UniqueID;

				public CellChangeHandler(Action<object> handler, object context,
						uint uniqueID) {
					Context = context;
					Handler = handler ?? throw new ArgumentNullException(nameof(handler));
					UniqueID = uniqueID;
 				}

				public override bool Equals(object obj) {
					return obj is CellChangeHandler ch && ch.UniqueID == UniqueID;
				}

				public bool Equals(CellChangeHandler other) {
					return UniqueID == other.UniqueID;
				}

				public override int GetHashCode() {
					return (int)UniqueID;
				}
				
				public void Invoke() {
					Handler.Invoke(Context);
				}

				public override string ToString() {
					return "Event Handler: " + Handler + " with context " + Context;
				}
			}

			/// <summary>
			/// Stores the handler and the context to use when calling it.
			/// </summary>
			public sealed class MoveHandler : IEquatable<MoveHandler> {
				public readonly Action<Transform, bool, object> Handler;

				public readonly object Context;

				public readonly uint UniqueID;

				public MoveHandler(Action<Transform, bool, object> handler, object context,
						uint uniqueID) {
					Context = context;
					Handler = handler ?? throw new ArgumentNullException(nameof(handler));
					UniqueID = uniqueID;
				}

				public override bool Equals(object obj) {
					return obj is MoveHandler ch && ch.UniqueID == UniqueID;
				}
				
				public bool Equals(MoveHandler other) {
					return UniqueID == other.UniqueID;
				}

				public override int GetHashCode() {
					return (int)UniqueID;
				}
				
				public void Invoke(Transform transform, bool moving) {
					Handler.Invoke(transform, moving, Context);
				}

				public override string ToString() {
					return "Event Handler: " + Handler + " with context " + Context;
				}
			}
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to take ClearLastKnownCell hints to clear the transform's
	/// last known location.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.ClearLastKnownCell))]
	public static class CellChangeMonitor_ClearLastKnownCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before ClearLastKnownCell runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Transform transform) {
			FastCellChangeMonitor.FastInstance.ClearLastKnownCell(transform);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace IsDirty with the fast version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.IsMoving))]
	public static class CellChangeMonitor_IsMoving_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before IsMoving runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Transform transform, ref bool __result) {
			__result = FastCellChangeMonitor.FastInstance.IsMoving(transform);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace MarkDirty with the fast version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.MarkDirty))]
	public static class CellChangeMonitor_MarkDirty_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before MarkDirty runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Transform transform) {
			FastCellChangeMonitor.FastInstance.MarkDirty(transform);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace RenderEveryTick with the fast version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.RenderEveryTick))]
	public static class CellChangeMonitor_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before RenderEveryTick runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix() {
			FastCellChangeMonitor.FastInstance.Update();
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace RegisterCellChangedHandler with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		RegisterCellChangedHandler))]
	public static class CellChangeMonitor_RegisterCellChangedHandler_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before RegisterCellChangedHandler runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Transform transform, Action<object> callback,
				object context, ref ulong __result) {
			__result = FastCellChangeMonitor.FastInstance.RegisterCellChangedHandler(transform,
				callback, context);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace RegisterMovementStateChanged with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		RegisterMovementStateChanged))]
	public static class CellChangeMonitor_RegisterMovementStateChanged_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before RegisterMovementStateChanged runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Transform transform, object context,
				Action<Transform, bool, object> handler, ref ulong __result) {
			__result = FastCellChangeMonitor.FastInstance.RegisterMovementStateChanged(
				transform, handler, context);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterCellChangedHandler with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterCellChangedHandler))]
	public static class CellChangeMonitor_UnregisterCellChangedHandler2_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterCellChangedHandler runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref ulong handlerID) {
			if (FastCellChangeMonitor.FastInstance.UnregisterCellChangedHandler(handlerID))
				handlerID = 0UL;
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterMovementStateChanged with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterMovementStateChanged))]
	public static class CellChangeMonitor_UnregisterMovementStateChanged_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterMovementStateChanged runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref ulong handlerid) {
			if (FastCellChangeMonitor.FastInstance.UnregisterMovementStateChanged(handlerid))
				handlerid = 0UL;
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to arm cell change updates after most things load.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.SetGridSize))]
	public static class CellChangeMonitor_SetGridSize_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied after SetGridSize runs.
		/// </summary>
		internal static void Postfix(CellChangeMonitor __instance) {
			FastCellChangeMonitor.FastInstance.Arm(__instance.gridWidth);
		}
	}
}
