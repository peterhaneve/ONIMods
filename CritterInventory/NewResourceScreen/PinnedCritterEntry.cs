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
using UnityEngine;

namespace PeterHan.CritterInventory.NewResourceScreen {
	/// <summary>
	/// Handles clicks and cycling through on a pinned critter type in the resource list.
	/// </summary>
	public sealed class PinnedCritterEntry : MonoBehaviour {
		/// <summary>
		/// The critter type for this resource row.
		/// </summary>
		public CritterType CritterType { get; set; }

		/// <summary>
		/// The critter species for this resource row.
		/// </summary>
		public Tag Species { get; set; }

		/// <summary>
		/// The current index when cycling through critters.
		/// </summary>
		private int selectionIndex;

		public PinnedCritterEntry() {
			selectionIndex = 0;
		}

		/// <summary>
		/// Cycles through critters of this type.
		/// </summary>
		internal void OnCycleThrough() {
			int id = ClusterManager.Instance.activeWorldId;
			var matching = ListPool<CreatureBrain, PinnedCritterEntry>.Allocate();
			// Compile a list of critters matching this species
			CritterInventoryUtils.GetCritters(id, (creature) => {
				if (creature.GetCritterType() == CritterType)
					matching.Add(creature);
			}, Species);
			int n = matching.Count;
			if (selectionIndex >= n)
				selectionIndex = 0;
			else
				selectionIndex = (selectionIndex + 1) % n;
			if (n > 0)
				PUtil.CenterAndSelect(matching[selectionIndex]);
			matching.Recycle();
		}

		/// <summary>
		/// Unpins this critter type from the list.
		/// </summary>
		internal void OnUnpin() {
			var ci = ClusterManager.Instance.activeWorld.GetComponent<CritterInventory>();
			if (ci != null) {
				ci.GetPinnedSpecies(CritterType).Remove(Species);
				AllResourcesScreen.Instance?.RefreshRows();
				PinnedResourcesPanel.Instance?.Refresh();
			}
		}
	}
}
