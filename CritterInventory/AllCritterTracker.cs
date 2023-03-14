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

namespace PeterHan.CritterInventory {
	/// <summary>
	/// A resource tracker which tracks critter counts for all critters of a specific type.
	/// </summary>
	public sealed class AllCritterTracker : BaseCritterTracker {
		public AllCritterTracker(int worldID, CritterType type) : base(worldID, type) { }

		public override void UpdateData() {
			var world = ClusterManager.Instance.GetWorld(WorldID);
			if (world != null) {
				var ci = world.GetComponent<CritterInventory>();
				if (ci != null)
					// Tracker excludes reserved
					AddPoint(ci.PopulateTotals(Type, null).Available);
			}
		}
	}
}
