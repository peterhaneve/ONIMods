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
using UnityEngine.UI;

using TableRowList = ListPool<TableRow, TableScreen>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches used for the table screen, which is the parent of things like Vitals
	/// and Consumables.
	/// </summary>
	public static class TableScreenPatches {
		/// <summary>
		/// Freezes the layouts after they are rendered.
		/// </summary>
		/// <param name="allRows">The list of all table rows.</param>
		private static System.Collections.IEnumerator FreezeLayouts(IList<TableRow> allRows) {
			yield return null;
			int n = allRows.Count;
			GameObject go;
			for (int i = 0; i < n; i++) {
				var row = allRows[i];
				if (row != null && (go = row.gameObject) != null && go.TryGetComponent(
						out LayoutGroup realLayout)) {
					var fixedLayout = go.AddOrGet<LayoutElement>();
					fixedLayout.layoutPriority = 100;
					fixedLayout.CopyFrom(realLayout);
					fixedLayout.enabled = true;
					realLayout.enabled = false;
				}
			}
		}

		/// <summary>
		/// Groups Duplicants by their current world.
		/// </summary>
		/// <param name="rows">The current table rows, one per Duplicant.</param>
		/// <param name="dupesByWorld">The location where the groups will be stored.</param>
		private static void GroupDupesByWorld(IList<TableRow> rows, IDictionary<int,
				TableRowList.PooledList> dupesByWorld) {
			int n = rows.Count;
			GameObject go;
			for (int i = 0; i < n; i++) {
				var row = rows[i];
				var owner = row.GetIdentity().GetSoleOwner();
				if (owner != null && owner.TryGetComponent(out MinionAssignablesProxy proxy) &&
						(go = proxy.GetTargetGameObject()) != null) {
					int world = go.GetMyWorldId();
					if (!dupesByWorld.TryGetValue(world, out TableRowList.PooledList dupes))
						dupesByWorld.Add(world, dupes = TableRowList.Allocate());
					dupes.Add(row);
				}
			}
		}

		/// <summary>
		/// Moves the rows and row dividers into their final positions.
		/// </summary>
		/// <param name="instance">The table screen to update.</param>
		/// <param name="rows">The current row list with sorting.</param>
		/// <param name="dividerIndices">The locations of each world divider.</param>
		private static void MoveRows(TableScreen instance, IList<TableRow> rows,
				IDictionary<int, int> dividerIndices) {
			int n = rows.Count;
			var dividers = instance.worldDividers;
			// Sort the rows in the UI
			for (int i = 0; i < n; i++) {
				var row = rows[i];
				row.transform.SetSiblingIndex(i);
				ThawLayout(row.gameObject);
			}
			foreach (var pair in dividerIndices)
				if (dividers.TryGetValue(pair.Key, out GameObject divider))
					// Will not be present in vanilla
					divider.transform.SetSiblingIndex(pair.Value);
			// Move the default row to the beginning
			if (instance.has_default_duplicant_row)
				instance.default_row.transform.SetAsFirstSibling();
		}

		/// <summary>
		/// Sorts the table's rows.
		/// </summary>
		/// <param name="instance">The table screen to sort.</param>
		private static void SortRows(TableScreen instance) {
			var rows = instance.all_sortable_rows;
			bool reversed = instance.sort_is_reversed;
			var comparison = new TableSortComparison(instance.active_sort_method, reversed);
			var dividerIndices = DictionaryPool<int, int, TableScreen>.Allocate();
			var dupesByWorld = DictionaryPool<int, TableRowList.PooledList, TableScreen>.
				Allocate();
			int index = 0;
			var entryList = instance.scroll_content_transform;
			UpdateHeaders(instance, reversed);
			GroupDupesByWorld(rows, dupesByWorld);
			rows.Clear();
			foreach (var pair in dupesByWorld) {
				var list = pair.Value;
				// 1 offset for the item added plus the number of items in this list
				dividerIndices.Add(pair.Key, index);
				index++;
				if (comparison.IsSortable)
					list.Sort(comparison);
				index += list.Count;
				rows.AddRange(list);
				list.Recycle();
			}
			dupesByWorld.Recycle();
			MoveRows(instance, rows, dividerIndices);
			dividerIndices.Recycle();
			// Schedule a freeze
			if (entryList != null && instance.isActiveAndEnabled)
				instance.StartCoroutine(FreezeLayouts(rows));
		}

		/// <summary>
		/// Thaws a reused row's layout.
		/// </summary>
		/// <param name="row">The game object to thaw.</param>
		private static void ThawLayout(GameObject row) {
			if (row != null) {
				if (row.TryGetComponent(out LayoutElement fixedLayout))
					fixedLayout.enabled = false;
				if (row.TryGetComponent(out LayoutGroup realLayout))
					realLayout.enabled = true;
			}
		}

		/// <summary>
		/// Updates the header states to match the sorting state.
		/// </summary>
		/// <param name="instance">The table screen to update.</param>
		/// <param name="reverse">Whether the sorting order is reversed.</param>
		private static void UpdateHeaders(TableScreen instance, bool reverse) {
			var sortColumn = instance.active_sort_column;
			MultiToggle sortToggle;
			foreach (var pair in instance.columns) {
				var tableColumn = pair.Value;
				if (tableColumn != null && (sortToggle = tableColumn.column_sort_toggle) !=
						null) {
					int state = sortToggle.CurrentState;
					if (tableColumn == sortColumn) {
						if (reverse) {
							if (state != 2)
								sortToggle.ChangeState(2);
						} else if (state != 1)
							sortToggle.ChangeState(1);
					} else if (state != 0)
						sortToggle.ChangeState(0);
				}
			}
		}

		/// <summary>
		/// Applied to TableScreen to make sorting rows much more efficient and freeze them
		/// after laying them out to make scrolling better. Virtual scroll lagged a lot for
		/// some reason.
		/// </summary>
		[HarmonyPatch(typeof(TableScreen), nameof(TableScreen.SortRows))]
		internal static class SortRows_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

			/// <summary>
			/// Applied before SortRows runs.
			/// </summary>
			internal static bool Prefix(TableScreen __instance) {
				SortRows(__instance);
				return false;
			}
		}

		/// <summary>
		/// Listens for scroll events 
		/// </summary>
		private sealed class ScrollListener {
			/// <summary>
			/// The screen to update.
			/// </summary>
			private readonly TableScreen screen;

			/// <summary>
			/// The scroll pane to check for the position.
			/// </summary>
			private readonly ScrollRect scrollRect;

			public ScrollListener(TableScreen screen, ScrollRect scrollRect) {
				this.screen = screen;
				this.scrollRect = scrollRect;
			}

			public void OnScroll(Vector2 _) {
				if (!screen.CheckScrollersDirty())
					screen.SetScrollersDirty(scrollRect.horizontalNormalizedPosition);
			}
		}

		/// <summary>
		/// A reversible comparator used to sort table rows.
		/// </summary>
		private sealed class TableSortComparison : IComparer<TableRow> {
			/// <summary>
			/// Checks to see if a sort order is set.
			/// </summary>
			/// <returns>true if the items should be sorted, or false otherwise.</returns>
			public bool IsSortable => comparator != null;

			/// <summary>
			/// The sorter currently in use.
			/// </summary>
			private readonly Comparison<IAssignableIdentity> comparator;

			/// <summary>
			/// Whether the sort order is reversed.
			/// </summary>
			private readonly bool reverse;

			public TableSortComparison(Comparison<IAssignableIdentity> comparator,
					bool reverse) {
				this.comparator = comparator;
				this.reverse = reverse;
			}

			public int Compare(TableRow x, TableRow y) {
				int result = comparator.Invoke(x.GetIdentity(), y.GetIdentity());
				if (reverse)
					result = -result;
				return result;
			}
		}
	}
}
