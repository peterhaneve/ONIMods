/*
 * Copyright 2020 Peter Han
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

using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory.NewResourceScreen {
	/// <summary>
	/// A marker class used to annotate additional information regarding the critter
	/// information to be displayed by a row in the new resources screen.
	/// </summary>
	public sealed class CritterResourceRow : MonoBehaviour {
		/// <summary>
		/// The critter type for this resource row.
		/// </summary>
		public CritterType CritterType { get; set; }

		/// <summary>
		/// Whether this row group should be visible as a whole.
		/// </summary>
		public bool IsVisible { get; set; }

		// [MyCmpReq] populates it too late, since this component can start inactive
		internal HierarchyReferences References { get; set; }

		/// <summary>
		/// The critter species for this resource row.
		/// </summary>
		public Tag Species { get; set; }

		/// <summary>
		/// The title displayed on screen.
		/// </summary>
		public string Title {
			get {
				return CritterInventoryUtils.GetTitle(Species, CritterType);
			}
		}

		public CritterResourceRow() {
			IsVisible = true;
		}

		/// <summary>
		/// Called when the resource is pinned/unpinned.
		/// </summary>
		internal void OnPinToggle() {
			var ci = ClusterManager.Instance.activeWorld.GetComponent<CritterInventory>();
			ISet<Tag> pinned;
			if (ci != null && (pinned = ci.GetPinnedSpecies(CritterType)) != null) {
				// Toggle membership in pinned set
				if (pinned.Contains(Species))
					pinned.Remove(Species);
				else
					pinned.Add(Species);
				// Toggle visual checkbox
				UpdatePinnedState(ci);
			}
			// TODO Notify checkbox isn't implemented yet in stock game?
		}

		/// <summary>
		/// Updates the graph for this critter species.
		/// </summary>
		/// <param name="currentTime">The current time from GameClock.</param>
		internal void UpdateChart(float currentTime) {
			const float HISTORY = CritterInventoryUtils.CYCLES_TO_CHART *
				Constants.SECONDS_PER_CYCLE;
			var tracker = CritterInventoryUtils.GetTracker<CritterTracker>(ClusterManager.
				Instance.activeWorldId, CritterType, (t) => t.Tag == Species);
			if (tracker != null) {
				var chart = References.GetReference<SparkLayer>("Chart");
				var chartableData = tracker.ChartableData(HISTORY);
				var xAxis = chart.graph.axis_x;
				if (chartableData.Length > 0)
					xAxis.max_value = chartableData[chartableData.Length - 1].first;
				else
					xAxis.max_value = 0f;
				xAxis.min_value = currentTime - HISTORY;
				chart.RefreshLine(chartableData, "resourceAmount");
			}
		}

		/// <summary>
		/// Updates the headings for this critter species.
		/// </summary>
		/// <param name="allTotals">The total critter counts for all species.</param>
		/// <param name="ci">The currently active critter inventory.</param>
		internal void UpdateContents(IDictionary<Tag, CritterTotals> allTotals,
				CritterInventory ci) {
			if (!allTotals.TryGetValue(Species, out var totals))
				totals = new CritterTotals();
			References.GetReference<LocText>("AvailableLabel").SetText(GameUtil.
				GetFormattedSimple(totals.Available));
			References.GetReference<LocText>("TotalLabel").SetText(GameUtil.
				GetFormattedSimple(totals.Total));
			References.GetReference<LocText>("ReservedLabel").SetText(GameUtil.
				GetFormattedSimple(totals.Reserved));
			UpdatePinnedState(ci);
		}

		/// <summary>
		/// Updates the pin checkbox to match the actual pinned state.
		/// </summary>
		/// <param name="ci">The currently active critter inventory</param>
		private void UpdatePinnedState(CritterInventory ci) {
			References.GetReference<MultiToggle>("PinToggle").ChangeState(ci.
				GetPinnedSpecies(CritterType).Contains(Species) ? 1 : 0);
		}
	}
}
