/*
 * Copyright 2019 Peter Han
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

using Harmony;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// A resource header for "Critter (Wild)" or "Critter (Tame)"
	/// </summary>
	sealed class CritterResourceHeader {
		/// <summary>
		/// Creates tool tip text for the critter screen.
		/// </summary>
		/// <param name="heading">The heading to display on the tool tip.</param>
		/// <param name="totals">The total quantity available and reserved for errands.</param>
		/// <returns>The tool tip text formatted for those values.</returns>
		private static string FormatTooltip(string heading, CritterTotals totals) {
			int total = totals.Total, reserved = totals.Reserved;
			return heading + "\n" + string.Format(STRINGS.UI.RESOURCESCREEN.AVAILABLE_TOOLTIP,
				total - reserved, reserved, total);
		}

		/// <summary>
		/// The category for "Critter (Type)".
		/// </summary>
		public ResourceCategoryHeader Header { get; }
		/// <summary>
		/// Cached Traverse instance for this header.
		/// </summary>
		private readonly Traverse trCategory;
		/// <summary>
		/// The critter type included in this header.
		/// </summary>
		public CritterType Type { get; set; }

		/// <summary>
		/// Creates a new critter resource header.
		/// </summary>
		/// <param name="allCategories">The parent of this object.</param>
		/// <param name="type">The critter type for this resource header.</param>
		public CritterResourceHeader(ResourceCategoryScreen allCategories, CritterType type) {
			var trInstance = Traverse.Create(allCategories);
			var tag = GameTags.BagableCreature;
			string typeStr = type.GetDescription();
			// Create a heading for Critter
			PLibUtil.LogDebug("Creating Critter ({0}) category".F(typeStr));
			var gameObject = Util.KInstantiateUI(trInstance.GetField<GameObject>(
				"Prefab_CategoryBar"), allCategories.CategoryContainer.gameObject, false);
			gameObject.name = "CategoryHeader_{0}_{1}".F(tag.Name, type.ToString());
			Header = gameObject.GetComponent<ResourceCategoryHeader>();
			Header.SetTag(tag, GameUtil.MeasureUnit.quantity);
			// Tag it with a wild/tame tag
			Header.gameObject.AddComponent<CritterResourceInfo>().CritterType = type;
			Header.elements.LabelText.SetText("{0} ({1})".F(tag.ProperName(), typeStr));
			trCategory = Traverse.Create(Header);
			Type = type;
		}
		/// <summary>
		/// Activates this category if it is not already active.
		/// </summary>
		public void Activate() {
			var go = Header.gameObject;
			if (!go.activeInHierarchy)
				go.SetActive(true);
		}
		/// <summary>
		/// Creates a new resource entry.
		/// </summary>
		/// <param name="species">The species for this entry.</param>
		private ResourceEntry CreateEntry(Tag species) {
			var entry = trCategory.CallMethod<ResourceEntry>("NewResourceEntry", species,
				GameUtil.MeasureUnit.quantity);
			entry.SetName(species.ProperName());
			entry.gameObject.AddComponent<CritterResourceInfo>().CritterType = Type;
			return entry;
		}
		public override string ToString() {
			return Header.name;
		}
		/// <summary>
		/// Updates the resource entries with the critters found, creating if necessary.
		/// </summary>
		/// <param name="found">The distribution of critters found.</param>
		public void Update(IDictionary<Tag, CritterTotals> found) {
			var all = new CritterTotals();
			var discovered = Header.ResourcesDiscovered;
			// Previously discovered but now extinct critters need an empty entry
			foreach (var pair in discovered) {
				var species = pair.Key;
				if (!found.ContainsKey(species))
					found.Add(species, new CritterTotals());
			}
			// Go through resource entries for each species and update them
			foreach (var pair in found) {
				var quantity = pair.Value;
				UpdateEntry(pair.Key, quantity);
				all.Add(quantity);
			}
			bool anyDiscovered = discovered.Count > 0;
			// Enable display and open/close based on critter presence
			Header.elements.QuantityText.SetText(all.Available.ToString());
			trCategory.CallMethod("SetActiveColor", all.HasAny);
			trCategory.CallMethod("SetInteractable", anyDiscovered);
			// Still need to set this for expand/contract to work
			trCategory.SetField("anyDiscovered", anyDiscovered);
			// Update category tooltip
			var tooltip = trCategory.GetField<ToolTip>("tooltip");
			if (tooltip != null) {
				tooltip.OnToolTip = null;
				tooltip.toolTip = FormatTooltip(Header.elements.LabelText.text, all);
			}
		}
		/// <summary>
		/// Updates an individual resource entry with the critters found.
		/// </summary>
		/// <param name="species">The species of critter which was found.</param>
		/// <param name="quantity">The quantity of this critter which is present.</param>
		private void UpdateEntry(Tag species, CritterTotals quantity) {
			var discovered = Header.ResourcesDiscovered;
			// Look up the species to see if we have found it already
			if (!discovered.TryGetValue(species, out ResourceEntry entry)) {
				entry = CreateEntry(species);
				discovered.Add(species, entry);
			}
			var trEntry = Traverse.Create(entry);
			// Update the tool tip text
			var tooltip = trEntry.GetField<ToolTip>("tooltip");
			if (tooltip != null) {
				tooltip.OnToolTip = null;
				tooltip.toolTip = FormatTooltip(entry.NameLabel.text, quantity);
			}
			// Determine the color for the labels
			var color = trEntry.GetField<Color>("AvailableColor");
			if (!quantity.HasAny)
				color = trEntry.GetField<Color>("UnavailableColor");
			if (entry.QuantityLabel.color != color)
				entry.QuantityLabel.color = color;
			if (entry.NameLabel.color != color)
				entry.NameLabel.color = color;
			// Add up overall totals
			entry.QuantityLabel.SetText(quantity.Available.ToString());
		}
	}
}
