/*
 * Copyright 2023 Peter Han
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

#if DEBUG
using PeterHan.PLib.Core;
#endif
using System.Collections.Generic;
using UnityEngine.UI;

namespace PeterHan.CritterInventory.NewResourceScreen {
	/// <summary>
	/// A marker class used to annotate additional information regarding the critter
	/// information to be displayed by a category in the new resources screen.
	/// </summary>
	public sealed class CritterResourceRowGroup : KMonoBehaviour {
		/// <summary>
		/// The critter type for this resource group.
		/// </summary>
		public CritterType CritterType { get; set; }

		/// <summary>
		/// Whether this row group should be visible as a whole.
		/// </summary>
		public bool IsVisible { get; set; }

		/// <summary>
		/// The title displayed on screen.
		/// </summary>
		public string Title => CritterInventoryUtils.GetTitle(GameTags.BagableCreature,
			CritterType);

		/// <summary>
		/// The resources being displayed by this group.
		/// </summary>
		private readonly IDictionary<Tag, CritterResourceRow> resources;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private HierarchyReferences refs;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public CritterResourceRowGroup() {
			IsVisible = true;
			resources = new SortedList<Tag, CritterResourceRow>(32, TagComparer.INSTANCE);
		}

		/// <summary>
		/// Creates a resource category row for critters.
		/// </summary>
		/// <param name="allResources">The resources screen where the row should be added.</param>
		/// <param name="species">The critter species to create.</param>
		/// <returns>The row for that critter species.</returns>
		private CritterResourceRow Create(AllResourcesScreen allResources, Tag species) {
			var spawn = Util.KInstantiateUI(allResources.resourceLinePrefab, refs.
				GetComponent<FoldOutPanel>().container, true);
#if DEBUG
			PUtil.LogDebug("Creating resource row for {0}".F(species.ProperNameStripLink()));
#endif
			// Component which actually handles updating
			var cr = spawn.AddComponent<CritterResourceRow>();
			cr.CritterType = CritterType;
			cr.Species = species;
			if (spawn.TryGetComponent(out MultiToggle toggle))
				toggle.onClick += cr.OnPinToggle;
			if (spawn.TryGetComponent(out HierarchyReferences newRefs)) {
				var image = Def.GetUISprite(species);
				// Tint icon the correct color
				if (image != null) {
					var icon = newRefs.GetReference<Image>("Icon");
					icon.sprite = image.first;
					icon.color = image.second;
				}
				// Set up chart
				if (newRefs.GetReference<SparkLayer>("Chart").TryGetComponent(out GraphBase
						graphBase)) {
					graphBase.axis_x.min_value = 0f;
					graphBase.axis_x.max_value = 600f;
					graphBase.axis_x.guide_frequency = 120f;
					graphBase.RefreshGuides();
				}
				cr.References = newRefs;
				newRefs.GetReference<LocText>("NameLabel").SetText(cr.Title);
				// Checkmark to pin to resource list
				newRefs.GetReference<MultiToggle>("PinToggle").onClick += cr.OnPinToggle;
			}
			return cr;
		}

		/// <summary>
		/// Filters rows by the user search query.
		/// </summary>
		/// <param name="search">The search query to use.</param>
		internal void SearchFilter(string search) {
			foreach (var resource in resources) {
				var cr = resource.Value;
				cr.IsVisible = CritterInventoryUtils.PassesSearchFilter(cr.Title, search);
			}
		}

		/// <summary>
		/// Shows or hides rows depending on their visibility flags.
		/// </summary>
		internal void SetRowsActive() {
			bool visible = IsVisible && resources.Count > 0;
			foreach (var resource in resources) {
				var cr = resource.Value;
				bool showRow = cr.IsVisible;
				// If any row is visible, header must also be
				var go = cr.gameObject;
				if (go != null && showRow != go.activeSelf)
					go.SetActive(showRow);
				visible |= showRow;
			}
			// Update visibility if dirty
			if (gameObject.activeSelf != visible)
				gameObject.SetActive(visible);
		}

		/// <summary>
		/// Creates new rows if necessary for each critter species, and sorts them by name.
		/// </summary>
		/// <param name="allResources">The parent window for the rows.</param>
		internal void SpawnRows(AllResourcesScreen allResources) {
			var cm = ClusterManager.Instance;
			if (cm != null && cm.activeWorld.TryGetComponent(out CritterInventory ci)) {
				var allCritters = DictionaryPool<Tag, CritterTotals, CritterResourceRowGroup>.
					Allocate();
				ci.PopulateTotals(CritterType, allCritters);
				bool dirty = false;
				// Insert new rows where necessary
				foreach (var pair in allCritters) {
					var species = pair.Key;
					if (!resources.ContainsKey(species)) {
						resources.Add(species, Create(allResources, species));
						dirty = true;
					}
				}
				// Iterate and place in SORTED order in the UI
				if (dirty)
					foreach (var resource in resources)
						resource.Value.gameObject.transform.SetAsLastSibling();
				allCritters.Recycle();
				if (dirty)
					UpdateContents();
			}
		}

		/// <summary>
		/// Updates the graphs for the entire category.
		/// </summary>
		internal void UpdateCharts() {
			const float HISTORY = CritterInventoryUtils.CYCLES_TO_CHART *
				Constants.SECONDS_PER_CYCLE;
			float currentTime = GameClock.Instance.GetTime();
			var tracker = CritterInventoryUtils.GetTracker<AllCritterTracker>(ClusterManager.
				Instance.activeWorldId, CritterType);
			var chart = refs.GetReference<SparkLayer>("Chart");
			var chartableData = tracker.ChartableData(HISTORY);
			ref var xAxis = ref chart.graph.axis_x;
			xAxis.max_value = chartableData.Length > 0 ? chartableData[chartableData.
				Length - 1].first : 0f;
			xAxis.min_value = currentTime - HISTORY;
			chart.RefreshLine(chartableData, "resourceAmount");
			foreach (var resource in resources)
				resource.Value.UpdateChart(currentTime);
		}

		/// <summary>
		/// Updates the headings for the entire category.
		/// </summary>
		internal void UpdateContents() {
			var cm = ClusterManager.Instance;
			if (cm != null && cm.activeWorld.TryGetComponent(out CritterInventory ci)) {
				var allTotals = DictionaryPool<Tag, CritterTotals, CritterResourceRowGroup>.
					Allocate();
				var totals = ci.PopulateTotals(CritterType, allTotals);
				refs.GetReference<LocText>("AvailableLabel").SetText(GameUtil.
					GetFormattedSimple(totals.Available));
				refs.GetReference<LocText>("TotalLabel").SetText(GameUtil.
					GetFormattedSimple(totals.Total));
				refs.GetReference<LocText>("ReservedLabel").SetText(GameUtil.
					GetFormattedSimple(totals.Reserved));
				foreach (var resource in resources)
					resource.Value.UpdateContents(allTotals, ci);
				allTotals.Recycle();
			}
		}
	}
}
