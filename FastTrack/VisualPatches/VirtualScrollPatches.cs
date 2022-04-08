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

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to KChildFitter to add an updater to fit it only on layout changes.
	/// </summary>
	[HarmonyPatch(typeof(KChildFitter), nameof(KChildFitter.Awake))]
	public static class KChildFitter_Awake_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after Awake runs.
		/// </summary>
		internal static void Postfix(KChildFitter __instance) {
			__instance.gameObject.AddOrGet<KChildFitterUpdater>();
		}
	}

	/// <summary>
	/// Applied to KChildFitter to turn off an expensive fitter method that runs every frame!
	/// </summary>
	[HarmonyPatch(typeof(KChildFitter), nameof(KChildFitter.LateUpdate))]
	public static class KChildFitter_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to ModsScreen to update the scroll pane whenever the list changes.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.BuildDisplay))]
	public static class ModsScreen_BuildDisplay_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after BuildDisplay runs.
		/// </summary>
		[HarmonyPriority(Priority.VeryLow)]
		internal static void Postfix(ModsScreen __instance) {
			var entryList = __instance.entryParent;
			if (entryList != null) {
				var vs = entryList.gameObject.GetComponentSafe<VirtualScroll>();
				if (vs != null)
					vs.Rebuild();
			}
		}
	}

	/// <summary>
	/// Applied to ModsScreen to set up listeners and state for virtual scroll.
	/// </summary>
	[HarmonyPatch(typeof(ModsScreen), nameof(ModsScreen.OnActivate))]
	public static class ModsScreen_OnActivate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Applied after OnActivate runs.
		/// </summary>
		internal static void Postfix(ModsScreen __instance) {
			var entryList = __instance.entryParent;
			GameObject go;
			RectTransform rt;
			if (entryList != null && (go = entryList.gameObject) != null && (rt = go.
					rectTransform()) != null) {
				var vs = go.AddOrGet<VirtualScroll>();
				vs.freezeLayout = true;
				vs.Initialize(rt);
			}
		}
	}

	/// <summary>
	/// Applied to TableScreen to update the virtual scroll when the row orders change.
	/// This method was slow enough that the whole thing was worth getting rid of.
	/// </summary>
	[HarmonyPatch(typeof(TableScreen), nameof(TableScreen.SortRows))]
	public static class TableScreen_SortRows_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

		/// <summary>
		/// Groups Duplicants by their current world.
		/// </summary>
		/// <param name="rows">The current table rows, one per Duplicant.</param>
		/// <param name="dupesByWorld">The location where the groups will be stored.</param>
		private static void GroupDupesByWorld(IList<TableRow> rows, IDictionary<int,
				List<TableRow>> dupesByWorld) {
			int n = rows.Count;
			GameObject go;
			for (int i = 0; i < n; i++) {
				var row = rows[i];
				var owner = row.GetIdentity().GetSoleOwner();
				if (owner != null && (go = owner.GetComponent<MinionAssignablesProxy>().
						GetTargetGameObject()) != null) {
					int world = go.GetMyWorldId();
					if (!dupesByWorld.TryGetValue(world, out List<TableRow> dupes))
						dupesByWorld.Add(world, dupes = new List<TableRow>());
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
			for (int i = 0; i < n; i++)
				rows[i].transform.SetSiblingIndex(i);
			// Move the dividers
			foreach (var pair in dividerIndices)
				dividers[pair.Key].transform.SetSiblingIndex(pair.Value);
			// Move the default row to the beginning
			if (instance.has_default_duplicant_row)
				instance.default_row.transform.SetAsFirstSibling();
		}

		/// <summary>
		/// Applied before SortRows runs.
		/// </summary>
		internal static bool Prefix(TableScreen __instance) {
			var rows = __instance.all_sortable_rows;
			bool reversed = __instance.sort_is_reversed;
			var comparison = new TableSortComparison(__instance.active_sort_method, reversed);
			var dividerIndices = DictionaryPool<int, int, TableScreen>.Allocate();
			var dupesByWorld = DictionaryPool<int, List<TableRow>, TableScreen>.Allocate();
			int index = 0;
			UpdateHeaders(__instance, reversed);
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
			}
			dupesByWorld.Recycle();
			MoveRows(__instance, rows, dividerIndices);
			dividerIndices.Recycle();
			return false;
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
	}

	/// <summary>
	/// A reversible comparator used to sort table rows.
	/// </summary>
	internal sealed class TableSortComparison : IComparer<TableRow> {
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

		public TableSortComparison(Comparison<IAssignableIdentity> comparator, bool reverse) {
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

	/// <summary>
	/// A layout element that triggers child fitting only if layout has actually changed.
	/// </summary>
	internal sealed class KChildFitterUpdater : KMonoBehaviour, ILayoutElement {
		public float minWidth => -1.0f;

		public float preferredWidth => -1.0f;

		public float flexibleWidth => -1.0f;

		public float minHeight => -1.0f;

		public float preferredHeight => -1.0f;

		public float flexibleHeight => -1.0f;

		public int layoutPriority => int.MinValue;

#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private KChildFitter fitter;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public void CalculateLayoutInputHorizontal() {
			fitter.FitSize();
		}

		public void CalculateLayoutInputVertical() { }
	}
}
