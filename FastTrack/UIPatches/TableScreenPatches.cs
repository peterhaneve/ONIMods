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

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches used for the table screen, which is the parent of things like Vitals
	/// and Consumables.
	/// </summary>
	public static class TableScreenPatches {
		/// <summary>
		/// Adds dividers for each planetoid in the Spaced Out DLC.
		/// </summary>
		/// <param name="instance">The table screen to update.</param>
		private static void AddDividers(TableScreen instance) {
			GameObject target;
			int id;
			var occupiedWorlds = DictionaryPool<int, WorldContainer, TableScreen>.Allocate();
			foreach (int worldId in ClusterManager.Instance.GetWorldIDsSorted())
				instance.AddWorldDivider(worldId);
			// List occupied planets
			foreach (object obj in Components.MinionAssignablesProxy) {
				var proxy = (MinionAssignablesProxy)obj;
				if (proxy != null && (target = proxy.GetTargetGameObject()) != null) {
					var world = target.GetMyWorld();
					if (world != null && !occupiedWorlds.ContainsKey(id = world.id))
						occupiedWorlds.Add(id, world);
				}
			}
			foreach (var pair in instance.worldDividers) {
				var dividerRow = pair.Value.GetComponent<HierarchyReferences>().
					GetReference("NobodyRow");
				id = pair.Key;
				if (occupiedWorlds.TryGetValue(id, out WorldContainer world)) {
					dividerRow.gameObject.SetActive(false);
					pair.Value.SetActive(world.IsDiscovered);
				} else {
					dividerRow.gameObject.SetActive(true);
					pair.Value.SetActive(ClusterManager.Instance.GetWorld(id).IsDiscovered);
				}
			}
			occupiedWorlds.Recycle();
		}

		/// <summary>
		/// Removes and pools any existing rows. Leaves the default and header row if present,
		/// otherwise creates and adds one when requested.
		/// </summary>
		/// <param name="instance">The table screen to clear.</param>
		/// <param name="toAdd">The Duplicants to be added or updated.</param>
		private static void ClearAndAddRows(TableScreen instance,
				ISet<IAssignableIdentity> toAdd) {
			var columns = instance.columns;
			var hr = instance.header_row;
			List<TableRow> rows = instance.rows, sortableRows = instance.all_sortable_rows;
			int n = rows.Count;
			TableRow defaultRow = null, headerRow = null;
			var newRows = ListPool<TableRow, TableScreen>.Allocate();
			var deadRows = HashSetPool<TableRow, TableScreen>.Allocate();
			IAssignableIdentity minion;
			for (int i = 0; i < n; i++) {
				var row = rows[i];
				var go = row.gameObject;
				// Do not destroy the default or header; pull out any rows for existing minion
				// identities
				if (row.rowType == TableRow.RowType.Default) {
					ThawLayout(go);
					defaultRow = row;
				} else if (go == hr && hr != null) {
					ThawLayout(go);
					headerRow = row;
				} else if (row.rowType != TableRow.RowType.WorldDivider && (minion = row.
						minion) != null && toAdd.Remove(minion)) {
					ThawLayout(go);
					ConfigureContent(row, minion, columns, instance);
					newRows.Add(row);
				} else
					deadRows.Add(row);
			}
			sortableRows.Clear();
			rows.Clear();
			// Restore cached default and header rows; header first
			if (headerRow != null) {
				ConfigureContent(headerRow, null, columns, instance);
				rows.Add(headerRow);
			} else
				instance.AddRow(null);
			if (defaultRow != null) {
				ConfigureContent(defaultRow, null, columns, instance);
				rows.Add(defaultRow);
			} else if (instance.has_default_duplicant_row)
				instance.AddDefaultRow();
			// Add reused rows, delete removed rows
			sortableRows.AddRange(newRows);
			rows.AddRange(newRows);
			newRows.Recycle();
			TakeOutTrash(instance, deadRows);
			deadRows.Recycle();
		}

		/// <summary>
		/// Configures the row's content, only adding widgets that are new.
		/// </summary>
		/// <param name="row">The row being configured.</param>
		/// <param name="minion">The Duplicant for this row.</param>
		/// <param name="columns">The columns to display.</param>
		/// <param name="screen">The parent table screen.</param>
		private static void ConfigureContent(TableRow row, IAssignableIdentity minion,
				IDictionary<string, TableColumn> columns, TableScreen screen) {
			bool def = row.isDefault;
			var scrollers = row.scrollers;
			var go = row.gameObject;
			row.minion = minion;
			ConfigureImage(row, minion);
			foreach (var pair in columns) {
				var column = pair.Value;
				var widgets = row.widgets;
				string id = column.scrollerID;
				// Columns cannot be deleted in vanilla
				if (!widgets.TryGetValue(column, out GameObject widget)) {
					// Create a new one
					if (minion == null) {
						if (def)
							widget = column.GetDefaultWidget(go);
						else
							widget = column.GetHeaderWidget(go);
					} else
						widget = column.GetMinionWidget(go);
					widgets.Add(column, widget);
					column.widgets_by_row.Add(row, widget);
				}
				// Update the scroller if needed
				if (!string.IsNullOrEmpty(id) && column.screen.column_scrollers.Contains(id)) {
					Transform content;
					ScrollRect sr;
					if (scrollers.TryGetValue(id, out GameObject scrollerGO)) {
						content = scrollerGO.transform;
						sr = content.parent.GetComponent<ScrollRect>();
					} else {
						var prefab = Util.KInstantiateUI(row.scrollerPrefab, go, true);
						sr = prefab.GetComponent<ScrollRect>();
						content = sr.content;
						sr.onValueChanged.AddListener(new ScrollListener(screen, sr).OnScroll);
						scrollers.Add(id, content.gameObject);
						// Is it a border?
						var border = content.parent.Find("Border");
						if (border != null)
							row.scrollerBorders.Add(id, border.gameObject);
					}
					widget.transform.SetParent(content);
					sr.horizontalNormalizedPosition = 0.0f;
				}
			}
			// Run events after the update is complete
			foreach (var pair in columns) {
				var column = pair.Value;
				if (column.widgets_by_row.TryGetValue(row, out GameObject widget)) {
					string id = column.scrollerID;
					column.on_load_action?.Invoke(minion, widget);
					// Apparently the order just... works out? <shrug>
				}
			}
			if (minion != null)
				go.name = minion.GetProperName();
			else if (def)
				go.name = "defaultRow";
			// "Click to go to Duplicant"
			if (row.selectMinionButton != null)
				row.selectMinionButton.transform.SetAsLastSibling();
			// Update the border sizes
			foreach (var pair in row.scrollerBorders) {
				var border = pair.Value;
				var rt = border.rectTransform();
				float width = rt.rect.width;
				border.transform.SetParent(go.transform);
				rt.anchorMin = rt.anchorMax = Vector2.up;
				rt.sizeDelta = new Vector2(width, 374.0f);
				var scrollRT = row.scrollers[pair.Key].transform.parent.rectTransform();
				Vector3 a = scrollRT.GetLocalPosition();
				a.x -= scrollRT.sizeDelta.x * 0.5f;
				a.y = rt.GetLocalPosition().y - rt.anchoredPosition.y;
				rt.SetLocalPosition(a);
			}
		}

		/// <summary>
		/// Configures the portrait for each row.
		/// </summary>
		/// <param name="row">The row being configured.</param>
		/// <param name="minion">The Duplicant for this row.</param>
		private static void ConfigureImage(TableRow row, IAssignableIdentity minion) {
			var img = row.GetComponentInChildren<KImage>(true);
			img.colorStyleSetting = (minion == null) ? row.style_setting_default :
				row.style_setting_minion;
			img.ColorState = KImage.ColorSelector.Inactive;
			// Dim out Duplicants in rockets
			var component = row.GetComponent<CanvasGroup>();
			if (component != null && (minion as StoredMinionIdentity) != null)
				component.alpha = 0.6f;
		}

		/// <summary>
		/// Freezes the layouts after they are rendered.
		/// </summary>
		/// <param name="allRows">The list of all table rows.</param>
		private static System.Collections.IEnumerator FreezeLayouts(IList<TableRow> allRows) {
			yield return null;
			int n = allRows.Count;
			GameObject go;
			LayoutGroup realLayout;
			for (int i = 0; i < n; i++) {
				var row = allRows[i];
				if (row != null && (go = row.gameObject) != null && (realLayout = go.
						GetComponent<LayoutGroup>()) != null) {
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
		/// Sorts the table's rows.
		/// </summary>
		/// <param name="instance">The table screen to sort.</param>
		private static void SortRows(TableScreen instance) {
			var rows = instance.all_sortable_rows;
			bool reversed = instance.sort_is_reversed;
			var comparison = new TableSortComparison(instance.active_sort_method, reversed);
			var dividerIndices = DictionaryPool<int, int, TableScreen>.Allocate();
			var dupesByWorld = DictionaryPool<int, List<TableRow>, TableScreen>.Allocate();
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
			}
			dupesByWorld.Recycle();
			MoveRows(instance, rows, dividerIndices);
			dividerIndices.Recycle();
			// Schedule a freeze
			if (entryList != null && instance.isActiveAndEnabled)
				instance.StartCoroutine(FreezeLayouts(rows));
		}

		/// <summary>
		/// Disposes and removes the specified rows from the table screen.
		/// </summary>
		/// <param name="instance">The table screen to clean up.</param>
		/// <param name="deadRows">The rows that were removed.</param>
		private static void TakeOutTrash(TableScreen instance, ICollection<TableRow> deadRows)
		{
			var ikr = instance.known_widget_rows;
			// Avoid leaking dead rows
			var deadWidgets = ListPool<GameObject, TableScreen>.Allocate();
			foreach (var pair in ikr)
				if (deadRows.Contains(pair.Value))
					deadWidgets.Add(pair.Key);
			int n = deadWidgets.Count;
			for (int i = 0; i < n; i++)
				ikr.Remove(deadWidgets[i]);
			deadWidgets.Recycle();
			foreach (var row in deadRows)
				row.Clear();
			// Dividers are fairly cheap, destroy and recreate them is easier
			var dividers = instance.worldDividers;
			foreach (var pair in dividers)
				Util.KDestroyGameObject(pair.Value);
			dividers.Clear();
		}

		/// <summary>
		/// Thaws a reused row's layout.
		/// </summary>
		/// <param name="row">The game object to thaw.</param>
		private static void ThawLayout(GameObject row) {
			if (row != null) {
				var fixedLayout = row.GetComponent<LayoutElement>();
				var realLayout = row.GetComponent<LayoutGroup>();
				if (fixedLayout != null)
					fixedLayout.enabled = false;
				if (realLayout != null)
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
		/// Applied to TableRow to configure content more efficiently.
		/// </summary>
		[HarmonyPatch(typeof(TableRow), nameof(TableRow.ConfigureContent))]
		internal static class ConfigureContent_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

			/// <summary>
			/// Applied before ConfigureContent runs.
			/// </summary>
			internal static bool Prefix(TableRow __instance, IAssignableIdentity minion,
					Dictionary<string, TableColumn> columns, TableScreen screen) {
				ConfigureContent(__instance, minion, columns, screen);
				return false;
			}
		}

		/// <summary>
		/// Applied to TableScreen to pool the rows and make refreshing them much faster.
		/// </summary>
		[HarmonyPatch(typeof(TableScreen), nameof(TableScreen.RefreshRows))]
		internal static class RefreshRows_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.VirtualScroll;

			/// <summary>
			/// Applied before RefreshRows runs.
			/// </summary>
			internal static bool Prefix(TableScreen __instance) {
				var identities = HashSetPool<IAssignableIdentity, TableScreen>.Allocate();
				var living = Components.LiveMinionIdentities.Items;
				StoredMinionIdentity smi;
				// Living Duplicants
				for (int i = 0; i < living.Count; i++) {
					var dupe = living[i];
					if (dupe != null)
						identities.Add(dupe);
				}
				// Duplicants in vanilla rockets and similar
				foreach (var minionStorage in Components.MinionStorages.Items)
					foreach (var info in minionStorage.GetStoredMinionInfo()) {
						var dupe = info.serializedMinion;
						if (dupe != null && (smi = dupe.Get<StoredMinionIdentity>()) != null)
							__instance.AddRow(smi);
					}
				ClearAndAddRows(__instance, identities);
				// Add the missing rows
				foreach (var missingMinion in identities)
					__instance.AddRow(missingMinion);
				identities.Recycle();
				if (DlcManager.FeatureClusterSpaceEnabled())
					AddDividers(__instance);
				SortRows(__instance);
				__instance.rows_dirty = false;
				return false;
			}
		}

		/// <summary>
		/// Applied to TableScreen to make sorting rows much more efficient and freeze them after
		/// laying them out to make scrolling better. Virtual scroll lagged a lot for some reason.
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
