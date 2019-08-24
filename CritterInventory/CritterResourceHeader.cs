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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Contains methods to manage resource headers for critters.
	/// </summary>
	static class CritterResourceHeader {
		/// <summary>
		/// Activates a resource category heading.
		/// </summary>
		/// <param name="header">The heading to activate.</param>
		public static void Activate(this ResourceCategoryHeader header) {
			if (header != null) {
				var go = header.gameObject;
				if (!go.activeInHierarchy)
					go.SetActive(true);
			}
		}

		/// <summary>
		/// Creates a resource category header for critters.
		/// </summary>
		/// <param name="resList">The parent category screen for this header.</param>
		/// <param name="prefab">The prefab to use for creating the headers.</param>
		/// <param name="type">The critter type to create.</param>
		/// <returns>The heading for that critter type.</returns>
		public static ResourceCategoryHeader Create(ResourceCategoryScreen resList,
				GameObject prefab, CritterType type) {
			var tag = GameTags.BagableCreature;
			string typeStr = type.GetDescription();
			// Create a heading for Critter (Type)
			PLibUtil.LogDebug("Creating Critter ({0}) category".F(typeStr));
			var gameObject = Util.KInstantiateUI(prefab, resList.CategoryContainer.gameObject,
				false);
			gameObject.name = "CategoryHeader_{0}_{1}".F(tag.Name, type.ToString());
			var header = gameObject.GetComponent<ResourceCategoryHeader>();
			header.SetTag(tag, GameUtil.MeasureUnit.quantity);
			// Tag it with a wild/tame tag
			header.gameObject.AddComponent<CritterResourceInfo>().CritterType = type;
			header.elements.LabelText.SetText("{0} ({1})".F(tag.ProperName(), typeStr));
			return header;
		}

		/// <summary>
		/// Manages compatibility with Favorites Category.
		/// 
		/// This method is quite hacky.
		/// </summary>
		/// <param name="totals">The totals calculated from Update.</param>
		public static void FavoritesCategoryCompat(IDictionary<Tag, CritterTotals> totals,
				CritterType type) {
			ResourceCategoryHeader favCategory = null;
			var favTag = TagManager.Create("Favorites", "Favorites");
			if ((ResourceCategoryScreen.Instance?.DisplayedCategories?.TryGetValue(favTag,
					out favCategory) ?? false) && favCategory != null)
				// Favorites Category is installed
				foreach (var pair in favCategory.ResourcesDiscovered) {
					var species = pair.Key;
					var entry = pair.Value;
					var intendedType = entry.gameObject.GetComponent<CritterResourceInfo>();
					if (totals.TryGetValue(species, out CritterTotals quantity) &&
							intendedType != null && intendedType.CritterType == type) {
						// A critter in Favorites Category
						UpdateEntry(entry, quantity);
						entry.SetName("{0} ({1})".F(species.ProperName(), type));
					}
				}
		}

		/// <summary>
		/// Updates a critter resource category header.
		/// </summary>
		/// <param name="header">The category header to update.</param>
		/// <param name="type">The critter type it contains (can be pulled from the CritterResourceInfo component).</param>
		public static void Update(this ResourceCategoryHeader header, CritterType type) {
			var totals = DictionaryPool<Tag, CritterTotals, ResourceCategoryHeader>.Allocate();
			var all = CritterInventoryUtils.FindCreatures(totals, type);
			var discovered = header.ResourcesDiscovered;
			var trCategory = Traverse.Create(header);
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
				if (!discovered.TryGetValue(species, out ResourceEntry entry)) {
					entry = trCategory.CallMethod<ResourceEntry>("NewResourceEntry",
						species, GameUtil.MeasureUnit.quantity);
					entry.SetName(species.ProperName());
					// Add component to tag it as wild/tame
					entry.gameObject.AddComponent<CritterResourceInfo>().CritterType = type;
					discovered.Add(species, entry);
				}
				UpdateEntry(entry, quantity);
			}
			bool anyDiscovered = discovered.Count > 0;
			// Enable display and open/close based on critter presence
			header.elements.QuantityText.SetText(all.Available.ToString());
			trCategory.CallMethod("SetActiveColor", all.HasAny);
			trCategory.CallMethod("SetInteractable", anyDiscovered);
			// Still need to set this for expand/contract to work
			trCategory.SetField("anyDiscovered", anyDiscovered);
			// Update category tooltip
			var tooltip = trCategory.GetField<ToolTip>("tooltip");
			if (tooltip != null) {
				tooltip.OnToolTip = null;
				tooltip.toolTip = CritterInventoryUtils.FormatTooltip(header.elements.
					LabelText.text, all);
			}
			// Disabled until coolazura's tags are up to date
#if false
			FavoritesCategoryCompat(totals, type);
#endif
			totals.Recycle();
		}

		/// <summary>
		/// Updates an individual resource entry with the critters found.
		/// </summary>
		/// <param name="entry">The entry to update.</param>
		/// <param name="quantity">The quantity of this critter which is present.</param>
		private static void UpdateEntry(ResourceEntry entry, CritterTotals quantity) {
			var trEntry = Traverse.Create(entry);
			// Update the tool tip text
			var tooltip = trEntry.GetField<ToolTip>("tooltip");
			if (tooltip != null) {
				tooltip.OnToolTip = null;
				tooltip.toolTip = CritterInventoryUtils.FormatTooltip(entry.NameLabel.text,
					quantity);
			}
			// Determine the color for the labels
			var color = trEntry.GetField<Color>("AvailableColor");
			if (quantity.Available <= 0)
				color = trEntry.GetField<Color>("UnavailableColor");
			LocText qLabel = entry.QuantityLabel, nLabel = entry.NameLabel;
			if (qLabel.color != color)
				qLabel.color = color;
			if (nLabel.color != color)
				nLabel.color = color;
			// Add up overall totals
			qLabel.SetText(quantity.Available.ToString());
		}
	}
}
