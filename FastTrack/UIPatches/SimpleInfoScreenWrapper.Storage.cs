/*
 * Copyright 2024 Peter Han
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

using UnityEngine;

using DETAILTABS = STRINGS.UI.DETAILTABS;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Updates the storage sections of the default "simple" info screen.
	/// </summary>
	internal sealed partial class SimpleInfoScreenWrapper {
		/// <summary>
		/// Displays an item in storage.
		/// </summary>
		/// <param name="item">The item to be displayed.</param>
		/// <param name="total">The total number of items displayed so far.</param>
		private int AddStorageItem(GameObject item, int total) {
			if (!item.TryGetComponent(out PrimaryElement pe) || pe.Mass > 0.0f) {
				var panel = sis.StoragePanel;
				var text = CACHED_BUILDER;
				string tooltip = GetItemDescription(item, pe);
				if (item.TryGetComponent(out KSelectable selectable))
					panel.SetLabelWithButton("storage_" + total, text.ToString(), tooltip,
						() => SelectTool.Instance.Select(selectable));
				else
					panel.SetLabel("storage_" + total, text.ToString(), tooltip);
				total++;
			}
			return total;
		}

		/// <summary>
		/// Displays all items in storage.
		/// </summary>
		private void AddAllItems() {
			int n = storages.Count, total = 0;
			for (int i = 0; i < n; i++) {
				var storage = storages[i];
				// Storage could have been destroyed along the way
				if (storage != null) {
					var items = storage.GetItems();
					int nitems = items.Count;
					for (int j = 0; j < nitems; j++) {
						var item = items[j];
						if (item != null)
							total = AddStorageItem(item, total);
					}
				}
			}
			if (total == 0)
				sis.StoragePanel.SetLabel("storage_empty", DETAILTABS.DETAILS.STORAGE_EMPTY, "");
		}

		/// <summary>
		/// Updates the text to be displayed for a single stored item. The text will be stored
		/// in the cached builder.
		/// </summary>
		/// <param name="item">The item to be displayed.</param>
		/// <param name="pe">The item's primary element, or null if it has none.</param>
		/// <returns>The tooltip to display.</returns>
		private string GetItemDescription(GameObject item, PrimaryElement pe) {
			var text = CACHED_BUILDER;
			var rottable = item.GetSMI<Rottable.Instance>();
			string tooltip = "";
			text.Clear();
			if (item.TryGetComponent(out HighEnergyParticleStorage hepStorage))
				// Radbolts
				text.Append(STRINGS.ITEMS.RADIATION.HIGHENERGYPARITCLE.NAME).Append(": ").
					Append(GameUtil.GetFormattedHighEnergyParticles(hepStorage.Particles));
			else if (pe != null) {
				// Element
				var properName = item.GetProperName();
				if (item.TryGetComponent(out KPrefabID id) && Assets.IsTagCountable(id.
						PrefabTag))
					FormatStringPatches.GetUnitFormattedName(text, properName, pe.Units);
				else
					text.Append(properName);
				text.Append(": ");
				FormatStringPatches.GetFormattedMass(text, pe.Mass);
				if (optimizedStorageTemp != null) {
					text.Append(optimizedStorageTemp);
					FormatStringPatches.GetFormattedTemperature(text, pe.Temperature);
				} else {
					string mass = text.ToString();
					text.Clear().Append(DETAILTABS.DETAILS.CONTENTS_TEMPERATURE).Replace("{1}",
						GameUtil.GetFormattedTemperature(pe.Temperature)).Replace("{0}", mass);
				}
			}
			if (rottable != null) {
				string rotText = rottable.StateString();
				if (!string.IsNullOrEmpty(rotText))
					text.Append("\n " + Constants.BULLETSTRING).Append(rotText);
				tooltip = rottable.GetToolTip();
			}
			if (pe != null && !FastTrackOptions.Instance.NoDisease && pe.DiseaseIdx !=
					Sim.InvalidDiseaseIdx) {
				text.Append("\n " + Constants.BULLETSTRING).Append(GameUtil.
					GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount));
				tooltip += GameUtil.GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount, true);
			}
			return tooltip;
		}

		/// <summary>
		/// Refreshes the storage objects on this object (and its children?)
		/// </summary>
		private void RefreshStorage() {
			if (conditionParent != null) {
				var panel = sis.StoragePanel;
				if (storages.Count > 0)
					AddAllItems();
				panel.Commit();
			}
		}
	}
}
