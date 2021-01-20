/*
 * Copyright 2021 Peter Han
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

using PeterHan.PLib;
using PeterHan.PLib.Detours;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory.OldResourceScreen {
	/// <summary>
	/// A marker class used to annotate additional information regarding the critter
	/// information to be displayed by a ResourceEntry object.
	/// </summary>
	public sealed class CritterResourceEntry : MonoBehaviour {
		/// <summary>
		/// How long to wait, in real life seconds ignoring game speed, before clearing the
		/// critter type cache.
		/// </summary>
		private const float CACHE_TIME = 10.0f;

		// Detours for private fields in ResourceEntry
		private static readonly IDetouredField<ResourceEntry, Color> AVAILABLE_COLOR =
			PDetours.DetourField<ResourceEntry, Color>("AvailableColor");
		private static readonly IDetouredField<ResourceEntry, Color> UNAVAILABLE_COLOR =
			PDetours.DetourField<ResourceEntry, Color>("UnavailableColor");

		public IList<CreatureBrain> CachedCritters { get; private set; }

		/// <summary>
		/// The critter type this ResourceEntry will show.
		/// </summary>
		public CritterType CritterType { get; set; }

		/// <summary>
		/// The unscaled time of the last time that the user cycled through critters.
		/// </summary>
		internal float lastClickTime;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private ResourceEntry entry;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public CritterResourceEntry() {
			CachedCritters = null;
			lastClickTime = Time.unscaledTime;
		}

		/// <summary>
		/// Like the vanilla ResourceEntry, clear the cached critter list only after a timeout
		/// has elapsed without futher selections.
		/// </summary>
		internal System.Collections.IEnumerator ClearCacheAfterThreshold() {
			while (CachedCritters != null && lastClickTime > 0.0f && Time.unscaledTime -
					lastClickTime < CACHE_TIME)
				yield return new WaitForSeconds(1.0f);
			CachedCritters = null;
		}

		/// <summary>
		/// Highlights all critters matching this critter type on the active world.
		/// </summary>
		/// <param name="color">The color to highlight the critters.</param>
		/// <param name="species">The species type to highlight.</param>
		internal void HighlightAllMatching(Color color) {
			var type = CritterType;
			int id = ClusterManager.Instance.activeWorldId;
			CritterInventoryUtils.GetCritters(id, (creature) => {
				if (creature.GetCritterType() == type)
					PUtil.HighlightEntity(creature, color);
			}, entry.Resource);
		}

		/// <summary>
		/// Called when a tooltip is needed for a critter species.
		/// </summary>
		/// <returns>The tool tip text for a critter type and species.</returns>
		private string OnEntryTooltip() {
			CritterInventory ci;
			var speciesTracker = CritterInventoryUtils.GetTracker<CritterTracker>(
				ClusterManager.Instance.activeWorldId, CritterType, (tracker) => tracker.Tag ==
				entry.Resource);
			var world = ClusterManager.Instance.activeWorld;
			string result = null;
			if (world != null && (ci = world.GetComponent<CritterInventory>()) != null) {
				float trend = (speciesTracker == null) ? 0.0f : speciesTracker.GetDelta(
					CritterInventoryUtils.TREND_INTERVAL);
				result = CritterInventoryUtils.FormatTooltip(entry.NameLabel.text, ci.
					GetBySpecies(CritterType, entry.Resource), trend);
			}
			return result;
		}

		/// <summary>
		/// Caches the list of critters of this type.
		/// </summary>
		/// <param name="species">The critter type to populate.</param>
		/// <returns>The list of matching critters.</returns>
		internal IList<CreatureBrain> PopulateCache() {
			int id = ClusterManager.Instance.activeWorldId;
			var type = CritterType;
			if (CachedCritters != null)
				CachedCritters.Clear();
			else
				CachedCritters = new List<CreatureBrain>(32);
			CritterInventoryUtils.GetCritters(id, (creature) => {
				if (creature.GetCritterType() == type)
					CachedCritters.Add(creature);
			}, entry.Resource);
			return CachedCritters;
		}

		/// <summary>
		/// Updates the line chart for this critter type and species.
		/// </summary>
		/// <param name="sparkChart">The chart object to update.</param>
		internal void UpdateChart(GameObject sparkChart) {
			var speciesTracker = CritterInventoryUtils.GetTracker<CritterTracker>(
				ClusterManager.Instance.activeWorldId, CritterType, (tracker) => tracker.Tag ==
				entry.Resource);
			// Search for tracker that matches both world ID and wildness type
			if (speciesTracker != null) {
				sparkChart.GetComponentInChildren<LineLayer>().RefreshLine(speciesTracker.
					ChartableData(CritterInventoryUtils.CYCLES_TO_CHART * Constants.
					SECONDS_PER_CYCLE), "resourceAmount");
				sparkChart.GetComponentInChildren<SparkLayer>().SetColor(Constants.
					NEUTRAL_COLOR);
			}
		}

		/// <summary>
		/// Updates an individual resource entry with the critters found.
		/// </summary>
		/// <param name="quantity">The quantity of this critter which is present.</param>
		internal void UpdateEntry(CritterTotals quantity) {
			// Update the tool tip text
			var tooltip = entry.GetComponent<ToolTip>();
			if (tooltip != null)
				tooltip.OnToolTip = OnEntryTooltip;
			// Determine the color for the labels (overdrawn is impossible for critters)
			var color = AVAILABLE_COLOR.Get(entry);
			if (quantity.Available <= 0)
				color = UNAVAILABLE_COLOR.Get(entry);
			LocText qLabel = entry.QuantityLabel, nLabel = entry.NameLabel;
			if (qLabel.color != color)
				qLabel.color = color;
			if (nLabel.color != color)
				nLabel.color = color;
			// Add up overall totals
			qLabel.SetText(quantity.Available.ToString());
		}

		/// <summary>
		/// Updates the last click time to be used for clearing the cache.
		/// </summary>
		internal void UpdateLastClick() {
			lastClickTime = Time.unscaledTime;
		}
	}
}
