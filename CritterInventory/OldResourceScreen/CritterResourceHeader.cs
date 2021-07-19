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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using UnityEngine;

namespace PeterHan.CritterInventory.OldResourceScreen {
	/// <summary>
	/// A marker class used to annotate additional information regarding the critter
	/// information to be displayed by a ResourceCategoryHeader object.
	/// </summary>
	public sealed class CritterResourceHeader : MonoBehaviour {
		private delegate void SetActiveColor(ResourceCategoryHeader header, bool state);
		private delegate void SetInteractable(ResourceCategoryHeader header, bool state);

		// Detours for private methods in ResourceCategoryHeader
		private static readonly SetActiveColor SET_ACTIVE_COLOR = typeof(
			ResourceCategoryHeader).Detour<SetActiveColor>();
		private static readonly SetInteractable SET_INTERACTABLE = typeof(
			ResourceCategoryHeader).Detour<SetInteractable>();

		/// <summary>
		/// Creates a new resource entry.
		/// </summary>
		/// <param name="parent">The parent category of the entry.</param>
		/// <param name="species">The critter species of this entry.</param>
		/// <param name="type">The critter type of this entry.</param>
		/// <returns>The created resource entry (already added to the resource list).</returns>
		private static ResourceEntry NewResourceEntry(ResourceCategoryHeader parent,
				Tag species, CritterType type) {
			var re = Util.KInstantiateUI(parent.Prefab_ResourceEntry, parent.
				EntryContainer.gameObject, true);
			var entry = re.GetComponent<ResourceEntry>();
			entry.SetTag(species, GameUtil.MeasureUnit.quantity);
			entry.SetName(species.ProperNameStripLink());
			// Add component to tag it as wild/tame
			re.AddComponent<CritterResourceEntry>().CritterType = type;
			return entry;
		}

		/// <summary>
		/// The critter type this ResourceCategoryHeader will show.
		/// </summary>
		public CritterType CritterType { get; set; }

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private ResourceCategoryHeader header;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		/// <summary>
		/// Highlights all critters matching this critter type on the active world.
		/// </summary>
		/// <param name="color">The color to highlight the critters.</param>
		internal void HighlightAllMatching(Color color) {
			var type = CritterType;
			int id = ClusterManager.Instance.activeWorldId;
			CritterInventoryUtils.GetCritters(id, (creature) => {
				if (creature.GetCritterType() == type)
					PGameUtils.HighlightEntity(creature, color);
			});
		}

		/// <summary>
		/// Called when a tooltip is needed for a critter category.
		/// </summary>
		/// <returns>The tool tip text for a critter type (wild or tame).</returns>
		private string OnAllTooltip() {
			CritterInventory ci;
			var categoryTracker = CritterInventoryUtils.GetTracker<AllCritterTracker>(
				ClusterManager.Instance.activeWorldId, CritterType);
			var world = ClusterManager.Instance.activeWorld;
			string result = null;
			if (world != null && (ci = world.GetComponent<CritterInventory>()) != null) {
				float trend = (categoryTracker == null) ? 0.0f : categoryTracker.GetDelta(
					CritterInventoryUtils.TREND_INTERVAL);
				result = CritterInventoryUtils.FormatTooltip(header.elements.LabelText.text,
					ci.PopulateTotals(CritterType, null), trend);
			}
			return result;
		}

		/// <summary>
		/// Updates the critter resource category header.
		/// </summary>
		/// <param name="anyDiscovered">A reference to the anyDiscovered field in header.</param>
		internal void UpdateHeader(ref bool anyDiscovered) {
			var ci = ClusterManager.Instance.activeWorld.GetComponent<CritterInventory>();
			if (ci != null) {
				var totals = DictionaryPool<Tag, CritterTotals, ResourceCategoryHeader>.
					Allocate();
				var all = ci.PopulateTotals(CritterType, totals);
				var discovered = header.ResourcesDiscovered;
				// Previously discovered but now extinct critters need an empty entry
				foreach (var pair in discovered) {
					var species = pair.Key;
					if (!totals.ContainsKey(species))
						totals.Add(species, new CritterTotals());
				}
				// Go through resource entries for each species and update them
				foreach (var pair in totals) {
					var quantity = pair.Value;
					var species = pair.Key;
					// Look up the species to see if we have found it already
					if (!discovered.TryGetValue(species, out ResourceEntry entry))
						discovered.Add(species, entry = NewResourceEntry(header, species,
							CritterType));
					var cre = entry.GetComponent<CritterResourceEntry>();
					if (cre != null)
						cre.UpdateEntry(quantity);
				}
				// Still need to set this for expand/contract to work
				anyDiscovered = discovered.Count > 0;
				// Enable display and open/close based on critter presence
				header.elements.QuantityText.SetText(all.Available.ToString());
				SET_ACTIVE_COLOR.Invoke(header, all.HasAny);
				SET_INTERACTABLE.Invoke(header, anyDiscovered);
				// Update category tooltip
				var tooltip = header.GetComponent<ToolTip>();
				if (tooltip != null)
					tooltip.OnToolTip = OnAllTooltip;
				totals.Recycle();
			}
		}

		/// <summary>
		/// Updates the line chart for this critter type.
		/// </summary>
		/// <param name="sparkChart">The chart object to update.</param>
		internal void UpdateChart(GameObject sparkChart) {
			var tracker = CritterInventoryUtils.GetTracker<AllCritterTracker>(ClusterManager.
				Instance.activeWorldId, CritterType);
			// Search for tracker that matches both world ID and wildness type
			if (tracker != null) {
				sparkChart.GetComponentInChildren<LineLayer>().RefreshLine(tracker.
					ChartableData(CritterInventoryUtils.CYCLES_TO_CHART * Constants.
					SECONDS_PER_CYCLE), "resourceAmount");
				sparkChart.GetComponentInChildren<SparkLayer>().SetColor(Constants.
					NEUTRAL_COLOR);
			}
		}
	}
}
