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
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Patches for slow code in the BuildTool.
	/// </summary>
	public static class BuildToolPatches {
		/// <summary>
		/// The last cell the mouse was over when the build tool was used.
		/// </summary>
		private static int lastCell;

		/// <summary>
		/// Initializes tracking of the build tool.
		/// </summary>
		internal static void Init() {
			lastCell = Grid.InvalidCell;
		}

		/// <summary>
		/// Updates the build tool visualizer only if the mouse actually moved.
		/// </summary>
		/// <param name="tool">The build tool to update.</param>
		/// <param name="pos">The mouse position.</param>
		private static void ShouldUpdateVis(BuildTool tool, Vector3 pos) {
			int cell = Grid.PosToCell(pos);
			if (cell != lastCell) {
				if (tool.def != null)
					tool.UpdateVis(pos);
				lastCell = cell;
			}
		}

		/// <summary>
		/// Applied to BuildTool to fix wasteful updates when moving the mouse by small amounts.
		/// </summary>
		[HarmonyPatch(typeof(BuildTool), nameof(BuildTool.OnMouseMove))]
		internal static class OnMouseMove_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Transpiles OnMouseMove to call through a filter method.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				return PPatchTools.ReplaceMethodCall(instructions, typeof(BuildTool).
					GetMethodSafe(nameof(BuildTool.UpdateVis), false, typeof(Vector3)), typeof(
					BuildToolPatches).GetMethodSafe(nameof(BuildToolPatches.ShouldUpdateVis),
					true, typeof(BuildTool), typeof(Vector3)));
			}
		}
	}

	/// <summary>
	/// Patches for slow code in InterfaceTool.
	/// </summary>
	public static class InterfaceToolPatches {
		/// <summary>
		/// Avoids allocating a new Comparison every frame.
		/// </summary>
		private static readonly Comparison<KSelectable> COMPARE_SELECTABLES =
			CompareSelectables;

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
		private static void FindSelectables(int cell, Vector3 coords, List<KSelectable> hits,
				ISet<Component> compareSet) {
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
					if (!collider.TryGetComponent(out KSelectable selectable))
						selectable = collider.GetComponentInParent<KSelectable>();
					if (selectable != null && selectable.IsSelectable) {
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
			// Sort the hits; compares fewer objects at the expense of comparing a slightly
			// different behaviour (the original compares the collider)
			hits.Sort(COMPARE_SELECTABLES);
		}

		/// <summary>
		/// Retrieves a list of KSelectable objects currently under the mouse cursor.
		/// </summary>
		/// <param name="cell">The cell that the cursor occupies.</param>
		/// <param name="coords">The raw mouse coordinates.</param>
		/// <param name="previousItems">The previously selected items.</param>
		/// <param name="hits">The location where the hits will be stored.</param>
		/// <returns>true to reset the cycle count, or false to leave it as is.</returns>
		private static bool GetAllSelectables(int cell, Vector3 coords,
				HashSet<Component> previousItems, List<KSelectable> hits) {
			var compareSet = HashSetPool<Component, InterfaceTool>.Allocate();
			bool reset = false;
			if (Grid.IsVisible(cell))
				FindSelectables(cell, coords, hits, compareSet);
			if (compareSet.Count < 1)
				previousItems.Clear();
			else if (!previousItems.SetEquals(compareSet)) {
				reset = true;
				previousItems.Clear();
				// Copy for next time
				previousItems.UnionWith(compareSet);
			}
			compareSet.Recycle();
			return reset;
		}

		/// <summary>
		/// Retrieves the first object under the cursor. Faster than GetIndexedSelectable with
		/// an argument of 0.
		/// </summary>
		/// <param name="hits">The list of sorted hits.</param>
		/// <param name="layerMask">The mask of objects that should not be selected.</param>
		/// <returns>The object that would be selected.</returns>
		private static KSelectable GetFirstSelectable(List<KSelectable> hits, int layerMask) {
			KSelectable result = null;
			// hits is already sorted and non-null
			foreach (var item in hits)
				if (((1 << item.gameObject.layer) & layerMask) != 0) {
					result = item;
					break;
				}
			return result;
		}

		/// <summary>
		/// Retrieves the component at the specified index when paging through info cards.
		/// </summary>
		/// <param name="hits">The list of sorted hits.</param>
		/// <param name="index">The index to look up.</param>
		/// <param name="layerMask">The mask of objects that should not be selected.</param>
		/// <param name="lastSelected">The object last selected by this method.</param>
		/// <returns>The object that would be selected.</returns>
		private static KSelectable GetIndexedSelectable(List<KSelectable> hits, ref int index,
				int layerMask, KSelectable lastSelected) {
			var filteredHits = ListPool<KSelectable, InterfaceTool>.Allocate();
			KSelectable result = null;
			// hits is already sorted and non-null
			foreach (var item in hits)
				if (((1 << item.gameObject.layer) & layerMask) != 0)
					filteredHits.Add(item);
			int n = filteredHits.Count;
			if (n > 0) {
				// Since index has to be modulo by number, have to make the full list even if
				// the item to select could be determined partway through
				int newIndex = index % n;
				if (lastSelected == null || filteredHits[newIndex] != lastSelected) {
					index = 0;
					newIndex = 0;
				} else {
					// index is allowed to keep increasing, even if n changes
					index++;
					newIndex = (newIndex + 1) % n;
				}
				result = filteredHits[newIndex];
			}
			filteredHits.Recycle();
			return result;
		}

		/// <summary>
		/// Applied to InterfaceTool to allow Better Info Cards to work around the replacement
		/// for GetObjectUnderCursor.
		/// </summary>
		[HarmonyPatch]
		internal static class BetterInfoCardsHack_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Target the KSelectable version of this generic method.
			/// </summary>
			internal static MethodBase TargetMethod() {
				return typeof(InterfaceTool).GetMethodSafe(nameof(InterfaceTool.
					GetObjectUnderCursor), false, PPatchTools.AnyArguments)?.MakeGenericMethod(
					typeof(KSelectable));
			}

			/// <summary>
			/// Applied before GetObjectUnderCursor runs.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod _) {
				yield return new CodeInstruction(OpCodes.Ldnull);
				yield return new CodeInstruction(OpCodes.Ret);
			}
		}

		/// <summary>
		/// Applied to InterfaceTool to replace the monster LateUpdate with a far more efficient
		/// alternative. Only really makes a difference on large piles, but does matter.
		/// </summary>
		[HarmonyPatch(typeof(InterfaceTool), nameof(InterfaceTool.LateUpdate))]
		internal static class LateUpdate_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

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
			/// Gets the mouse position on screen.
			/// </summary>
			/// <param name="coords">The location where the raw coordinates will be stored.</param>
			/// <returns>The mouse position as a cell, or Grid.InvalidCell if it is not valid.</returns>
			private static int GetMousePosition(out Vector3 coords) {
				coords = Camera.main.ScreenToWorldPoint(KInputManager.GetMousePos());
				return Grid.PosToCell(coords);
			}

			/// <summary>
			/// Applied before LateUpdate runs.
			/// </summary>
			internal static bool Prefix(InterfaceTool __instance) {
				int cell;
				if (!__instance.populateHitsList)
					__instance.UpdateHoverElements(null);
				else if (__instance.isAppFocused && Grid.IsValidCell(cell = GetMousePosition(
						out Vector3 coords))) {
					bool soundPlayed = false;
					// The game uses different math to arrive at the same answer in two ways...
					var hits = __instance.hits;
					var hoverOverride = __instance.hoverOverride;
					hits.Clear();
					if (hoverOverride != null)
						hits.Add(hoverOverride);
					// If the items have changed, reset cycle count
					if (GetAllSelectables(cell, coords, __instance.prevIntersectionGroup,
							hits))
						__instance.hitCycleCount = 0;
					var objectUnderCursor = GetFirstSelectable(hits, __instance.layerMask);
					__instance.UpdateHoverElements(hits);
					if (!__instance.hasFocus && hoverOverride == null)
						ClearHover(__instance);
					else if (objectUnderCursor != __instance.hover) {
						SetHover(__instance, objectUnderCursor);
						if (objectUnderCursor != null) {
							Game.Instance.Trigger((int)GameHashes.HighlightStatusItem,
								objectUnderCursor.gameObject);
							objectUnderCursor.Hover(!__instance.playedSoundThisFrame);
							// This store was dead in the base game, but the intent is
							// obviously to avoid playing more than one sound per frame...
							soundPlayed = true;
						}
					}
					__instance.playedSoundThisFrame = soundPlayed;
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

		/// <summary>
		/// Applied to SelectTool to reuse our logic for determining objects that were clicked
		/// instead of the slow vanilla GetObjectUnderCursor.
		/// </summary>
		[HarmonyPatch(typeof(SelectTool), nameof(SelectTool.OnLeftClickDown))]
		internal static class OnLeftClickDown_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Applied before OnLeftClickDown runs.
			/// </summary>
			internal static bool Prefix(Vector3 cursor_pos, SelectTool __instance) {
				int cell = Grid.PosToCell(cursor_pos);
				if (Grid.IsValidCell(cell)) {
					var hits = __instance.hits;
					int index = __instance.hitCycleCount;
					if (GetAllSelectables(cell, cursor_pos, __instance.prevIntersectionGroup,
							hits))
						index = 0;
					var target = GetIndexedSelectable(hits, ref index, __instance.layerMask,
						__instance.selected);
					__instance.hitCycleCount = index;
					// Try Aze's override
					var newTarget = __instance.GetObjectUnderCursor<KSelectable>(true);
					if (newTarget != null)
						target = newTarget;
					__instance.Select(target, false);
				}
				__instance.selectedCell = cell;
				return false;
			}
		}
	}
}
