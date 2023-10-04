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
		/// <param name="storage">The parent storage of this item.</param>
		/// <param name="parent">The parent object for the displayed item.</param>
		/// <param name="total">The total number of items displayed so far.</param>
		private void AddStorageItem(GameObject item, Storage storage, GameObject parent,
				ref int total) {
			if (!item.TryGetComponent(out PrimaryElement pe) || pe.Mass > 0.0f) {
				int t = total;
				var storeLabel = GetStorageLabel(parent, "storage_" + t);
				storageLabels.Add(storeLabel);
				SetItemDescription(storeLabel, item, pe);
				storeLabel.SetAllowDrop(storage.allowUIItemRemoval, storage, item);
				total = t + 1;
			}
		}

		/// <summary>
		/// Displays all items in storage.
		/// </summary>
		/// <param name="parent">The parent panel to add new labels.</param>
		private void AddAllItems(GameObject parent) {
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
							AddStorageItem(item, storage, parent, ref total);
					}
				}
			}
			if (total == 0) {
				var label = GetStorageLabel(parent, CachedStorageLabel.EMPTY_ITEM);
				label.FreezeIfMatch(1);
				storageLabels.Add(label);
			}
		}

		/// <summary>
		/// Retrieves a pooled label used for displaying stored objects.
		/// </summary>
		/// <param name="parent">The parent panel to add new labels.</param>
		/// <param name="id">The name of the label to be added or created.</param>
		/// <returns>A label which can be used to display stored items, pooled if possible.</returns>
		private CachedStorageLabel GetStorageLabel(GameObject parent, string id) {
			if (labelCache.TryGetValue(id, out CachedStorageLabel result))
				result.Reset();
			else {
				result = new CachedStorageLabel(sis, parent, id);
				labelCache[id] = result;
			}
			result.SetActive(true);
			return result;
		}

		/// <summary>
		/// Refreshes the storage objects on this object (and its children?)
		/// </summary>
		private void RefreshStorage() {
			if (storageParent != null) {
				var panel = sis.StoragePanel;
				if (storages.Count > 0) {
					setInactive.UnionWith(storageLabels);
					storageLabels.Clear();
					AddAllItems(storageParent.Content.gameObject);
					// Only turn off the things that are gone
					setInactive.ExceptWith(storageLabels);
					foreach (var inactive in setInactive)
						inactive.SetActive(false);
					setInactive.Clear();
					if (!storageActive) {
						panel.gameObject.SetActive(true);
						storageActive = true;
					}
				} else if (storageActive) {
					panel.gameObject.SetActive(false);
					storageActive = false;
				}
			}
		}

		/// <summary>
		/// Updates the text to be displayed for a single stored item.
		/// </summary>
		/// <param name="label">The label to be updated.</param>
		/// <param name="item">The item to be displayed.</param>
		/// <param name="pe">The item's primary element, or null if it has none.</param>
		private void SetItemDescription(CachedStorageLabel label, GameObject item,
				PrimaryElement pe) {
			var defaultStyle = PluginAssets.Instance.defaultTextStyleSetting;
			var text = CACHED_BUILDER;
			var rottable = item.GetSMI<Rottable.Instance>();
			var tooltip = label.tooltip;
			text.Clear();
			tooltip.ClearMultiStringTooltip();
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
				tooltip.AddMultiStringTooltip(rottable.GetToolTip(), defaultStyle);
			}
			if (pe != null && pe.DiseaseIdx != Sim.InvalidDiseaseIdx) {
				string diseased = GameUtil.GetFormattedDisease(pe.DiseaseIdx, pe.DiseaseCount);
				text.Append("\n " + Constants.BULLETSTRING).Append(diseased);
				tooltip.AddMultiStringTooltip(diseased, defaultStyle);
			}
			label.text.SetText(text);
			label.FreezeIfMatch(text.Length);
		}
	}
}
