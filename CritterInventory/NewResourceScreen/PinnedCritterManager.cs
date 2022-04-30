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

using System;
using System.Collections.Generic;
using UnityEngine.UI;

using PinnedCrittersPerType = System.Collections.Generic.SortedList<Tag, HierarchyReferences>;

namespace PeterHan.CritterInventory.NewResourceScreen {
	/// <summary>
	/// An addon component to PinnedResourcesPanel which manages the critter-related entries.
	/// </summary>
	public sealed class PinnedCritterManager : KMonoBehaviour {
		/// <summary>
		/// The realized game objects of pinned critter entries.
		/// </summary>
		private readonly IDictionary<CritterType, PinnedCrittersPerType> pinnedObjects;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private PinnedResourcesPanel pinnedResources;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public PinnedCritterManager() {
			pinnedObjects = new SortedList<CritterType, PinnedCrittersPerType>(4);
			foreach (var type in Enum.GetValues(typeof(CritterType)))
				if (type is CritterType ct)
					pinnedObjects.Add(ct, new PinnedCrittersPerType(8, TagComparer.INSTANCE));
		}

		/// <summary>
		/// Creates a new pinned critter row.
		/// </summary>
		/// <param name="species">The species to pin.</param>
		/// <param name="type">The critter type to pin.</param>
		/// <returns>A pinned row with that critter type and species displayed.</returns>
		internal HierarchyReferences Create(Tag species, CritterType type) {
			var newRow = Util.KInstantiateUI(pinnedResources.linePrefab,
				pinnedResources.rowContainer, false);
			var refs = newRow.GetComponent<HierarchyReferences>();
			// Set the image
			var imageData = Def.GetUISprite(species, "ui", false);
			if (imageData != null) {
				var icon = refs.GetReference<Image>("Icon");
				icon.sprite = imageData.first;
				icon.color = imageData.second;
			}
			refs.GetReference<LocText>("NameLabel").SetText(CritterInventoryUtils.GetTitle(
				species, type));
			refs.GetReference("NewLabel").gameObject.SetActive(false);
			var pinRow = newRow.AddComponent<PinnedCritterEntry>();
			pinRow.CritterType = type;
			pinRow.Species = species;
			refs.GetReference<MultiToggle>("PinToggle").onClick = pinRow.OnUnpin;
			newRow.GetComponent<MultiToggle>().onClick += pinRow.OnCycleThrough;
			return refs;
		}

		/// <summary>
		/// Populates the pinned critters in the resource panel, creating new rows if needed.
		/// </summary>
		internal void PopulatePinnedRows() {
			var ci = ClusterManager.Instance.activeWorld.GetComponent<CritterInventory>();
			if (ci != null) {
				var seen = HashSetPool<Tag, PinnedCritterManager>.Allocate();
				foreach (var pair in pinnedObjects) {
					var type = pair.Key;
					var have = pair.Value;
					foreach (var species in ci.GetPinnedSpecies(type)) {
						// Check for existing pinned row
						if (!have.TryGetValue(species, out var entry))
							have.Add(species, entry = Create(species, type));
						var row = entry.gameObject;
						if (!row.activeSelf)
							row.SetActive(true);
						seen.Add(species);
					}
					// Hide entries that have been removed from pinned list
					foreach (var speciesPair in have) {
						var row = speciesPair.Value.gameObject;
						if (!seen.Contains(speciesPair.Key)) {
							if (row.activeSelf)
								row.SetActive(false);
						} else
							// These will be traversed in sorted order
							row.transform.SetAsLastSibling();
					}
					seen.Clear();
				}
				seen.Recycle();
				// Move the buttons to the end
				pinnedResources.clearNewButton.transform.SetAsLastSibling();
				pinnedResources.seeAllButton.transform.SetAsLastSibling();
			}
		}

		/// <summary>
		/// Refreshes one pinned critter entry.
		/// </summary>
		/// <param name="refs">The row to refresh.</param>
		/// <param name="available">The quantity of the critter available.</param>
		private void RefreshLine(HierarchyReferences refs, int available) {
			refs.GetReference<LocText>("ValueLabel").SetText(GameUtil.GetFormattedSimple(
				available));
		}

		/// <summary>
		/// Updates the critter counts of visible pinned rows.
		/// </summary>
		internal void UpdateContents() {
			var ci = ClusterManager.Instance.activeWorld.GetComponent<CritterInventory>();
			if (ci != null) {
				var allCounts = DictionaryPool<Tag, CritterTotals, PinnedCritterManager>.
					Allocate();
				foreach (var pair in pinnedObjects) {
					allCounts.Clear();
					ci.PopulateTotals(pair.Key, allCounts);
					foreach (var speciesPair in pair.Value) {
						// Only refresh active rows
						var entry = speciesPair.Value;
						if (entry.gameObject.activeSelf) {
							int available = 0;
							if (allCounts.TryGetValue(speciesPair.Key, out CritterTotals totals))
								available = totals.Available;
							RefreshLine(entry, available);
						}
					}
				}
				allCounts.Recycle();
			}
		}
	}
}
