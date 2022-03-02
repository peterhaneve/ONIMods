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
using PeterHan.PLib.Detours;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Applied to InterfaceTool to replace the monster LateUpdate with a far more efficient
	/// alternative. Only really makes a difference on large piles, but does matter.
	/// </summary>
	[HarmonyPatch(typeof(InterfaceTool), nameof(InterfaceTool.LateUpdate))]
	public static class InterfaceTool_LateUpdate_Patch {
		/// <summary>
		/// Calls through to InterfaceTool.UpdateHoverElements which is subclassed by each
		/// tool (like SelectToolHoverTextCard which Aze considers to be cursed).
		/// </summary>
		private delegate void UpdateHoverElements(InterfaceTool instance,
			List<KSelectable> hits);

		private static readonly UpdateHoverElements UPDATE_HOVER_ELEMENTS =
			typeof(InterfaceTool).Detour<UpdateHoverElements>();

		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

		/// <summary>
		/// Adds the intersections of existing status items to the list by distance.
		/// </summary>
		/// <param name="seen">The location where the status items will be stored.</param>
		/// <param name="xy">The location of the mouse cursor.</param>
		private static void AddStatusIntersections(IDictionary<KSelectable, float> seen,
				Vector2 xy) {
			var intersections = ListPool<InterfaceTool.Intersection, InterfaceTool>.Allocate();
			// This overload, unlike the List<KSelectable> one, does not do the O(n^2)
			// comparisons; the original GetObjectUnderCursor call had a condition for
			// "is it selectable" but GetIntersections already checks that. This overload
			// calls GetComponent twice unfortunately, but what can we do?
			Game.Instance.statusItemRenderer.GetIntersections(xy, intersections);
			foreach (var intersection in intersections)
				// It always is a KSelectable (or is null)
				if (intersection.component is KSelectable selectable)
					// Distance is always -100
					seen[selectable] = intersection.distance;
			intersections.Recycle();
		}

		/// <summary>
		/// Clears the hovered object of an interface tool.
		/// </summary>
		private static void ClearHover(InterfaceTool instance) {
			var hover = instance.hover;
			if (hover != null) {
				instance.hover = null;
				hover.Unhover();
				Game.Instance.Trigger((int)GameHashes.HighlightObject, null);
			}
		}

		/// <summary>
		/// Compares two behaviours to ensure a consistent ordering when cycling through
		/// info cards.
		/// </summary>
		/// <param name="x">The first behaviour to compare.</param>
		/// <param name="y">The second behaviour to compare.</param>
		/// <returns>negative if the first behaviour is less than the second, positive if the
		/// first is greater than the second, or 0 otherwise.</returns>
		private static int CompareSelectables(KMonoBehaviour x, KMonoBehaviour y) {
			int result;
			bool yn = y == null;
			if (x == null)
				result = yn ? 0 : -1;
			else if (yn)
				result = 1;
			else {
				result = x.transform.GetPosition().z.CompareTo(y.transform.GetPosition().z);
				if (result == 0)
					result = x.GetHashCode().CompareTo(y.GetHashCode());
			}
			return result;
		}

		/// <summary>
		/// Finds selectable objects for creating info cards.
		/// </summary>
		/// <param name="cell">The cell to search.</param>
		/// <param name="coords">The raw mouse coordinates.</param>
		/// <param name="hits">The location where all hits will be stored.</param>
		/// <param name="compareSet">The set of objects to compare.</param>
		/// <param name="layerMask">The mask of objects that should not be selected.</param>
		private static void FindSelectables(int cell, Vector3 coords, List<KSelectable> hits,
				ISet<Component> compareSet, int layerMask) {
			var gsp = GameScenePartitioner.Instance;
			var seen = DictionaryPool<KSelectable, float, SelectTool>.Allocate();
			var xy = new Vector2(coords.x, coords.y);
			// The override might already be there
			foreach (var obj in hits)
				seen[obj] = -100.0f;
			AddStatusIntersections(seen, xy);
			var entries = ListPool<ScenePartitionerEntry, SelectTool>.Allocate();
			Grid.CellToXY(cell, out int x, out int y);
			gsp.GatherEntries(x, y, 1, 1, gsp.collisionLayer, entries);
			foreach (var entry in entries)
				if (entry.obj is KCollider2D collider && collider.Intersects(xy)) {
					var selectable = collider.GetComponent<KSelectable>();
					if (selectable == null)
						selectable = collider.GetComponentInParent<KSelectable>();
					if (selectable != null && selectable.IsSelectable && ((1 << selectable.
							gameObject.layer) & layerMask) != 0) {
						float distance = selectable.transform.GetPosition().z - coords.z;
						if (seen.TryGetValue(selectable, out float oldDistance))
							seen[selectable] = Mathf.Min(oldDistance, distance);
						else
							seen.Add(selectable, distance);
					}
				}
			entries.Recycle();
			hits.Clear();
			foreach (var pair in seen) {
				var selectable = pair.Key;
				compareSet.Add(selectable);
				hits.Add(selectable);
			}
			seen.Recycle();
			// Sort the hits; compares fewer objects at the expense of comparing a
			// different behaviour (the original compares the collider)
			hits.Sort(CompareSelectables);
		}

		/// <summary>
		/// Gets the mouse position on screen.
		/// </summary>
		/// <param name="coords">The location where the raw coordinates will be stored.</param>
		/// <returns>The mouse position as a cell, or Grid.InvalidCell if it is not valid.</returns>
		private static int GetMousePosition(out Vector3 coords) {
			coords = Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos());
			return Grid.PosToCell(coords);
		}

		// Base: 79us+36us bic on

		/// <summary>
		/// Retrieves a list of KSelectable objects currently under the mouse cursor.
		/// </summary>
		/// <param name="cell">The cell that the cursor occupies.</param>
		/// <param name="coords">The raw mouse coordinates.</param>
		/// <param name="hits">The location where the hits will be stored.</param>
		/// <param name="previousItems">The previously selected items.</param>
		/// <param name="hitCycleCount">The cycle count of which object is selected.</param>
		/// <param name="layerMask">The mask of objects that should not be selected.</param>
		/// <returns>The object that should be currently selected.</returns>
		private static KSelectable GetSelectablesUnderCursor(int cell, Vector3 coords,
				List<KSelectable> hits, HashSet<Component> previousItems, int layerMask,
				ref int hitCycleCount) {
			var compareSet = HashSetPool<Component, InterfaceTool>.Allocate();
			KSelectable result;
			if (Grid.IsVisible(cell))
				FindSelectables(cell, coords, hits, compareSet, layerMask);
			if (compareSet.Count < 1) {
				previousItems.Clear();
				result = null;
			} else {
				if (!previousItems.Equals(compareSet)) {
					previousItems.Clear();
					// Copy for next time
					foreach (var selectable in hits)
						previousItems.Add(selectable);
					hitCycleCount = 0;
				}
				// hits is already sorted, nice
				result = hits[0];
			}
			compareSet.Recycle();
			return result;
		}

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static bool Prefix(InterfaceTool __instance, ref bool ___playedSoundThisFrame,
				bool ___populateHitsList, bool ___isAppFocused, ref int ___hitCycleCount,
				KSelectable ___hoverOverride, HashSet<Component> ___prevIntersectionGroup,
				int ___layerMask, List<KSelectable> ___hits) {
			if (!___populateHitsList)
				UPDATE_HOVER_ELEMENTS(__instance, null);
			else if (___isAppFocused) {
				int cell = GetMousePosition(out Vector3 coords);
				// The game uses different math to arrive at the same answer in two ways...
				if (Grid.IsValidCell(cell)) {
					___hits.Clear();
					if (___hoverOverride != null)
						___hits.Add(___hoverOverride);
					var objectUnderCursor = GetSelectablesUnderCursor(cell, coords, ___hits,
						___prevIntersectionGroup, ___layerMask, ref ___hitCycleCount);
					UPDATE_HOVER_ELEMENTS(__instance, ___hits);
					if (!__instance.hasFocus && ___hoverOverride == null)
						ClearHover(__instance);
					else if (objectUnderCursor != __instance.hover) {
						SetHover(__instance, objectUnderCursor);
						if (objectUnderCursor != null) {
							Game.Instance.Trigger((int)GameHashes.HighlightStatusItem,
								objectUnderCursor.gameObject);
							objectUnderCursor.Hover(!___playedSoundThisFrame);
							// Dead store of ___playedSoundThisFrame to true
						}
					}
					___playedSoundThisFrame = false;
				}
			}
			// Stop the slow original method from running
			return false;
		}

		/// <summary>
		/// Sets the hovered object of an interface tool.
		/// </summary>
		private static void SetHover(InterfaceTool instance, KSelectable newHover) {
			var hover = instance.hover;
			if (hover != null) {
				hover.Unhover();
				Game.Instance.Trigger((int)GameHashes.HighlightObject, null);
			}
			instance.hover = newHover;
		}
	}
}
