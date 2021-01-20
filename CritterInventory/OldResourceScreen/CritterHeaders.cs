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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.CritterInventory.OldResourceScreen {
	/// <summary>
	/// Stores the wild and tame critter counts for each world's resource inventory.
	/// 
	/// Unfortunately behaviours cannot be parameterized, so this class needs duplicate code
	/// from NewResourceScreen.CritterCategoryRows.
	/// </summary>
	internal sealed class CritterHeaders : MonoBehaviour {
		/// <summary>
		/// Which critter type to update next.
		/// </summary>
		private int critterUpdatePacer;

		/// <summary>
		/// The headers for each critter type.
		/// </summary>
		private readonly IList<ResourceCategoryHeader> headers;

		public CritterHeaders() {
			headers = new List<ResourceCategoryHeader>(4);
			critterUpdatePacer = 0;
		}

		/// <summary>
		/// Creates a resource category header for critters.
		/// </summary>
		/// <param name="resList">The parent category screen for this header.</param>
		/// <param name="prefab">The prefab to use for creating the headers.</param>
		/// <param name="type">The critter type to create.</param>
		internal void Create(ResourceCategoryScreen resList, GameObject prefab,
				CritterType type) {
			var tag = GameTags.BagableCreature;
			// Create a heading for Critter (Type)
			PUtil.LogDebug("Creating Critter ({0}) category".F(type.GetProperName()));
			var gameObject = Util.KInstantiateUI(prefab, resList.CategoryContainer.gameObject,
				false);
			gameObject.name = "CategoryHeader_{0}_{1}".F(tag.Name, type.ToString());
			var header = gameObject.GetComponent<ResourceCategoryHeader>();
			header.SetTag(tag, GameUtil.MeasureUnit.quantity);
			// Tag it with a wild/tame tag
			header.gameObject.AddComponent<CritterResourceHeader>().CritterType = type;
			header.elements.LabelText.SetText(CritterInventoryUtils.GetTitle(tag, type));
			headers.Add(header);
		}

		/// <summary>
		/// Updates the critter headers, alternating each one to reduce CPU load.
		/// </summary>
		internal void UpdateContents() {
			if (critterUpdatePacer < headers.Count) {
				var header = headers[critterUpdatePacer];
				// Unity null check here, not ?.
				if (header != null)
					header.UpdateContents();
			}
			if (headers.Count > 0)
				critterUpdatePacer = (critterUpdatePacer + 1) % headers.Count;
		}
	}
}
