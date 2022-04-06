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
using UnityEngine;

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
		/// Stores the transforms which are currently moving.
		/// </summary>
		private IDictionary<int, EventEntry> movingTransforms;

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
			FastInstance = this;
		}

		/// <summary>
		/// Creates the tracking entry if necessary.
		/// </summary>
		/// <param name="id">The transform ID to create.</param>
		/// <param name="transform">The transform to track.</param>
		/// <returns>The current, or newly created entry.</returns>
		private EventEntry AddOrGet(int id, Transform transform) {
			if (!eventHandlers.TryGetValue(id, out EventEntry current)) {
				current = new EventEntry(transform);
				eventHandlers.Add(id, current);
			}
			return current;
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
		}

		/// <summary>
		/// Cleans up the tracking entry if it has no listeners. Trees that fall in the forest
		/// with no one to hear them...
		/// </summary>
		/// <param name="id">The transform ID to clean up.</param>
		/// <param name="entry">The current entry.</param>
		private void CleanupIfEmpty(int id, EventEntry entry) {
			if (entry.moveHandlers.Count < 1 && entry.cellChangedHandlers.Count < 1)
				eventHandlers.Remove(id);
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
			if (Grid.WidthInCells > 0) {
				int n = transform.childCount, id = transform.GetInstanceID();
				if (eventHandlers.TryGetValue(id, out EventEntry entry))
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
		public int RegisterCellChangedHandler(Transform transform, System.Action callback) {
			int id = transform.GetInstanceID();
			var entry = AddOrGet(id, transform);
			entry.cellChangedHandlers.Add(callback);
			return id;
		}

		/// <summary>
		/// Registers a handler for when the moving state of an object changes.
		/// </summary>
		/// <param name="transform">The transform to track.</param>
		/// <param name="handler">The event handler to register.</param>
		public void RegisterMovementStateChanged(Transform transform,
				Action<Transform, bool> handler) {
			var entry = AddOrGet(transform.GetInstanceID(), transform);
			entry.moveHandlers.Add(handler);
		}

		/// <summary>
		/// Unregisters a handler from when a transform moves to a new cell.
		/// </summary>
		/// <param name="id">The ID of the transform to untrack.</param>
		/// <param name="callback">The event handler to unregister.</param>
		public void UnregisterCellChangedHandler(int id, System.Action callback) {
			if (eventHandlers.TryGetValue(id, out EventEntry entry)) {
				entry.cellChangedHandlers.Remove(callback);
				CleanupIfEmpty(id, entry);
			}
		}

		/// <summary>
		/// Unregisters a handler from when the moving state of an object changes.
		/// </summary>
		/// <param name="id">The ID of the transform to untrack.</param>
		/// <param name="callback">The event handler to unregister.</param>
		public void UnregisterMovementStateChanged(int id,
				Action<Transform, bool> callback) {
			if (eventHandlers.TryGetValue(id, out EventEntry entry)) {
				entry.moveHandlers.Remove(callback);
				CleanupIfEmpty(id, entry);
			}
		}

		public void Update() {
			Transform transform;
			// Swap the buffers
			var swap = pendingDirtyTransforms;
			pendingDirtyTransforms = dirtyTransforms;
			dirtyTransforms = swap;
			pendingDirtyTransforms.Clear();
			swap = previouslyMovingTransforms;
			previouslyMovingTransforms = movingTransforms;
			movingTransforms = swap;
			movingTransforms.Clear();
			// Go through moved transforms and proc cell changed
			foreach (var pair in dirtyTransforms) {
				int id = pair.Key;
				var entry = pair.Value;
				if ((transform = entry.transform) != null) {
					int oldCell = entry.lastKnownCell, newCell = Grid.PosToCell(
						transform.position);
					movingTransforms.Add(id, entry);
					if (oldCell != newCell) {
						entry.CallCellChangedHandlers();
						entry.lastKnownCell = newCell;
					}
					if (!previouslyMovingTransforms.ContainsKey(id))
						entry.CallMovementStateChangedHandlers(true);
				}
			}
			foreach (var pair in previouslyMovingTransforms) {
				var entry = pair.Value;
				if ((transform = entry.transform) != null && !movingTransforms.ContainsKey(
						pair.Key))
					entry.CallMovementStateChangedHandlers(false);
			}
			dirtyTransforms.Clear();
		}

		/// <summary>
		/// Stores the events for a particular transform.
		/// </summary>
		internal sealed class EventEntry {
			/// <summary>
			/// The transform to which these events are bound.
			/// </summary>
			public readonly Transform transform;

			/// <summary>
			/// The handlers to call when this transform moves to a different cell.
			/// </summary>
			public readonly IList<System.Action> cellChangedHandlers;

			/// <summary>
			/// The cell this transform last occupied.
			/// </summary>
			public int lastKnownCell;

			/// <summary>
			/// The handlers to call when this transform starts or stops moving.
			/// </summary>
			public readonly IList<Action<Transform, bool>> moveHandlers;

			public EventEntry(Transform transform) {
				cellChangedHandlers = new List<System.Action>(8);
				lastKnownCell = Grid.InvalidCell;
				moveHandlers = new List<Action<Transform, bool>>(8);
				this.transform = transform;
			}

			/// <summary>
			/// Calls the cell changed handlers for this transform.
			/// </summary>
			public void CallCellChangedHandlers() {
				// Some cell changed handlers modify the list :(
				for (int i = 0; i < cellChangedHandlers.Count; i++)
					cellChangedHandlers[i].Invoke();
			}

			/// <summary>
			/// Calls the movement state changed handlers for this transform.
			/// </summary>
			/// <param name="newState">The new movement state.</param>
			public void CallMovementStateChangedHandlers(bool newState) {
				for (int i = 0; i < moveHandlers.Count; i++)
					moveHandlers[i].Invoke(transform, newState);
			}

			public override string ToString() {
				return "Event Handlers for {0}: {1:D} on change, {2:D} on move".F(transform.
					name, cellChangedHandlers.Count, moveHandlers.Count);
			}
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
		internal static bool Prefix(Transform transform, System.Action callback,
				ref int __result) {
			__result = FastCellChangeMonitor.FastInstance.RegisterCellChangedHandler(transform,
				callback);
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
		internal static bool Prefix(Transform transform, Action<Transform, bool> handler) {
			FastCellChangeMonitor.FastInstance.RegisterMovementStateChanged(transform,
				handler);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterCellChangedHandler with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterCellChangedHandler), typeof(int), typeof(System.Action))]
	public static class CellChangeMonitor_UnregisterCellChangedHandler_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterCellChangedHandler runs.
		/// </summary>
		internal static bool Prefix(int instance_id, System.Action callback) {
			FastCellChangeMonitor.FastInstance.UnregisterCellChangedHandler(instance_id,
				callback);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterCellChangedHandler with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterCellChangedHandler), typeof(Transform), typeof(System.Action))]
	public static class CellChangeMonitor_UnregisterCellChangedHandler2_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterCellChangedHandler runs.
		/// </summary>
		internal static bool Prefix(Transform transform, System.Action callback) {
			FastCellChangeMonitor.FastInstance.UnregisterCellChangedHandler(transform.
				GetInstanceID(), callback);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterMovementStateChanged with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterMovementStateChanged), typeof(int), typeof(Action<Transform, bool>))]
	public static class CellChangeMonitor_UnregisterMovementStateChanged_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterMovementStateChanged runs.
		/// </summary>
		internal static bool Prefix(int instance_id, Action<Transform, bool> callback) {
			FastCellChangeMonitor.FastInstance.UnregisterMovementStateChanged(instance_id,
				callback);
			return false;
		}
	}

	/// <summary>
	/// Applied to CellChangeMonitor to replace UnregisterMovementStateChanged with the fast
	/// version.
	/// </summary>
	[HarmonyPatch(typeof(CellChangeMonitor), nameof(CellChangeMonitor.
		UnregisterMovementStateChanged), typeof(Transform), typeof(Action<Transform, bool>))]
	public static class CellChangeMonitor_UnregisterMovementStateChanged2_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FastReachability;

		/// <summary>
		/// Applied before UnregisterMovementStateChanged runs.
		/// </summary>
		internal static bool Prefix(Transform transform, Action<Transform, bool> callback) {
			FastCellChangeMonitor.FastInstance.UnregisterMovementStateChanged(transform.
				GetInstanceID(), callback);
			return false;
		}
	}
}
