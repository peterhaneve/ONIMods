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

#if SPACEDOUT
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory.NewResourceScreen {
	/// <summary>
	/// For the new resources screen, stores references to the custom Critter rows.
	/// </summary>
	public sealed class CritterCategoryRows : KMonoBehaviour {
		/// <summary>
		/// The headers for each critter type.
		/// </summary>
		private readonly IList<CritterResourceRowGroup> headers;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private AllResourcesScreen allResources;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public CritterCategoryRows() {
			headers = new List<CritterResourceRowGroup>(4);
		}

		/// <summary>
		/// Creates a resource category header for critters.
		/// </summary>
		/// <param name="type">The critter type to create.</param>
		/// <returns>The heading for that critter type.</returns>
		private CritterResourceRowGroup Create(CritterType type) {
			var spawn = Util.KInstantiateUI(allResources.categoryLinePrefab, allResources.
				rootListContainer, true);
			// Create a heading for Critter (Type)
			PUtil.LogDebug("Creating Critter ({0}) category".F(type.GetProperName()));
			var refs = spawn.GetComponent<HierarchyReferences>();
			// Set up chart
			var graphBase = refs.GetReference<SparkLayer>("Chart").GetComponent<GraphBase>();
			graphBase.axis_x.min_value = 0f;
			graphBase.axis_x.max_value = 600f;
			graphBase.axis_x.guide_frequency = 120f;
			graphBase.RefreshGuides();
			// Component which actually handles updating
			var rg = spawn.AddComponent<CritterResourceRowGroup>();
			rg.CritterType = type;
			refs.GetReference<LocText>("NameLabel").SetText(rg.Title);
			return rg;
		}

		/// <summary>
		/// Filters rows and categories by the user search query.
		/// </summary>
		/// <param name="search">The search query to use.</param>
		internal void SearchFilter(string search) {
			// Use current culture
			string searchUp = search.ToUpper();
			foreach (var header in headers) {
				// Runs in prefix before SetRowsActive
				header.IsVisible = CritterInventoryUtils.PassesSearchFilter(header.Title,
					searchUp);
				header.SearchFilter(searchUp);
			}
		}

		/// <summary>
		/// Shows or hides rows depending on their visibility flags.
		/// </summary>
		internal void SetRowsActive() {
			foreach (var header in headers)
				header.SetRowsActive();
		}

		/// <summary>
		/// Spawns the category headers for critters if necessary.
		/// </summary>
		internal void SpawnRows() {
			if (headers.Count < 1)
				foreach (var type in Enum.GetValues(typeof(CritterType)))
					if (type is CritterType ct)
						headers.Add(Create(ct));
			foreach (var header in headers)
				header.SpawnRows(allResources);
		}

		/// <summary>
		/// Updates the charts for all categories.
		/// </summary>
		internal void UpdateCharts() {
			foreach (var header in headers)
				header.UpdateCharts();
		}

		/// <summary>
		/// Updates the critter headers. No alternation is performed, as it does not actually
		/// index the critters in-world, and the base game expects the checked state on
		/// the pinned panel to be updated immediately.
		/// </summary>
		internal void UpdateContents() {
			foreach (var header in headers)
				header.UpdateContents();
		}
	}
}
#endif
