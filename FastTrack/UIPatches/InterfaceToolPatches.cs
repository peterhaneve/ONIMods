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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Patches for slow code in the BuildTool.
	/// </summary>
	public static class BuildToolPatches {
		/// <summary>
		/// The prototype matching BuildTool.UpdateVis.
		/// </summary>
		private delegate void UpdateVis(BuildTool tool, Vector3 pos);

		/// <summary>
		/// BuildTool.UpdateVis is private so compute the patch target with reflection.
		/// </summary>
		private static readonly MethodInfo UPDATE_VIS = typeof(BuildTool).GetMethodSafe(
			nameof(UpdateVis), false, typeof(Vector3));

		/// <summary>
		/// A delegate that can invoke the UpdateVis method of BuildTool.
		/// </summary>
		private static readonly UpdateVis DO_UPDATE_VIS = PDetours.Detour<UpdateVis>(
			UPDATE_VIS);

		/// <summary>
		/// Accesses the def field of BuildTool.
		/// </summary>
		private static readonly IDetouredField<BuildTool, BuildingDef> GET_DEF = PDetours.
			DetourField<BuildTool, BuildingDef>("def");

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
				if (GET_DEF == null || GET_DEF.Get(tool) != null)
					DO_UPDATE_VIS?.Invoke(tool, pos);
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
				var method = instructions;
				if (UPDATE_VIS == null)
					PUtil.LogWarning("Unable to patch BuildTool.OnMouseMove");
				else
					method = PPatchTools.ReplaceMethodCall(instructions, UPDATE_VIS, typeof(
						BuildToolPatches).GetMethodSafe(nameof(BuildToolPatches.
						ShouldUpdateVis), true, typeof(BuildTool), typeof(Vector3)));
				return method;
			}
		}
	}

	/// <summary>
	/// Patches for slow code in InterfaceTool.
	/// </summary>
	public static class InterfaceToolPatches {
		/// <summary>
		/// Calls through to InterfaceTool.UpdateHoverElements which is subclassed by each
		/// tool (like SelectToolHoverTextCard which Aze considers to be cursed).
		/// </summary>
		private delegate void UpdateHoverElements(InterfaceTool instance,
			List<KSelectable> hits);

		/// <summary>
		/// Avoids allocating a new Comparison every frame.
		/// </summary>
		private static readonly Comparison<KSelectable> COMPARE_SELECTABLES =
			CompareSelectables;

		private static readonly UpdateHoverElements UPDATE_HOVER_ELEMENTS =
			typeof(InterfaceTool).Detour<UpdateHoverElements>();

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
					var selectable = collider.GetComponent<KSelectable>();
					if (selectable == null)
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
			internal static bool Prefix(InterfaceTool __instance, List<KSelectable> ___hits,
					bool ___populateHitsList, bool ___isAppFocused, ref int ___hitCycleCount,
					KSelectable ___hoverOverride, HashSet<Component> ___prevIntersectionGroup,
					int ___layerMask, ref bool ___playedSoundThisFrame) {
				int cell;
				if (!___populateHitsList)
					UPDATE_HOVER_ELEMENTS(__instance, null);
				else if (___isAppFocused && Grid.IsValidCell(cell = GetMousePosition(
						out Vector3 coords))) {
					bool soundPlayed = false;
					// The game uses different math to arrive at the same answer in two ways...
					___hits.Clear();
					if (___hoverOverride != null)
						___hits.Add(___hoverOverride);
					// If the items have changed, reset cycle count
					if (GetAllSelectables(cell, coords, ___prevIntersectionGroup, ___hits))
						___hitCycleCount = 0;
					var objectUnderCursor = GetFirstSelectable(___hits, ___layerMask);
					UPDATE_HOVER_ELEMENTS(__instance, ___hits);
					if (!__instance.hasFocus && ___hoverOverride == null)
						ClearHover(__instance);
					else if (objectUnderCursor != __instance.hover) {
						SetHover(__instance, objectUnderCursor);
						if (objectUnderCursor != null) {
							Game.Instance.Trigger((int)GameHashes.HighlightStatusItem,
								objectUnderCursor.gameObject);
							objectUnderCursor.Hover(!___playedSoundThisFrame);
							// This store was dead in the base game, but the intent is
							// obviously to avoid playing more than one sound per frame...
							soundPlayed = true;
						}
					}
					___playedSoundThisFrame = soundPlayed;
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
			internal static bool Prefix(Vector3 cursor_pos, ref int ___selectedCell,
					SelectTool __instance, List<KSelectable> ___hits, int ___layerMask,
					HashSet<Component> ___prevIntersectionGroup, ref int ___hitCycleCount,
					KSelectable ___selected) {
				int cell = Grid.PosToCell(cursor_pos);
				if (Grid.IsValidCell(cell)) {
					int index = ___hitCycleCount;
					if (GetAllSelectables(cell, cursor_pos, ___prevIntersectionGroup, ___hits))
						index = 0;
					__instance.Select(GetIndexedSelectable(___hits, ref index, ___layerMask,
						___selected), false);
					___hitCycleCount = index;
				}
				___selectedCell = cell;
				return false;
			}
		}
	}
}
