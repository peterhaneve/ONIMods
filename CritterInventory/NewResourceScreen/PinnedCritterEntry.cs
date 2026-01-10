/*
 * Copyright 2026 Peter Han
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
			var matching = ListPool<KPrefabID, PinnedCritterEntry>.Allocate();
			var type = CritterType;
			// Compile a list of critters matching this species
			CritterInventoryUtils.GetCritters(id, (kpid) => {
				if (kpid.GetCritterType() == type)
					matching.Add(kpid);
			}, Species);
			int n = matching.Count;
			if (selectionIndex >= n)
				selectionIndex = 0;
			else
				selectionIndex = (selectionIndex + 1) % n;
			if (n > 0)
				PGameUtils.CenterAndSelect(matching[selectionIndex]);
			matching.Recycle();
		}

		/// <summary>
		/// Unpins this critter type from the list.
		/// </summary>
		internal void OnUnpin() {
			var cm = ClusterManager.Instance;
			if (cm != null && cm.activeWorld.TryGetComponent(out CritterInventory ci)) {
				var ai = AllResourcesScreen.Instance;
				var pi = PinnedResourcesPanel.Instance;
				ci.GetPinnedSpecies(CritterType).Remove(Species);
				if (ai != null)
					ai.RefreshRows();
				if (pi != null) {
					if (pi.TryGetComponent(out PinnedCritterManager pm))
						pm.SetDirty();
					pi.Refresh();
				}
			}
		}
	}
}
