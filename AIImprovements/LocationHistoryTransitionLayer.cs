/*
 * Copyright 2025 Peter Han
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

namespace PeterHan.AIImprovements {
	/// <summary>
	/// Tracks the last few tiles visited by a Duplicant.
	/// </summary>
	public sealed class LocationHistoryTransitionLayer : TransitionDriver.OverrideLayer {
		/// <summary>
		/// How many cells are tracked by this transition layer.
		/// </summary>
		public const int TRACK_CELLS = 5;

		/// <summary>
		/// The cells last visited.
		/// </summary>
		public int[] VisitedCells { get; }

		public LocationHistoryTransitionLayer(Navigator navigator) : base(navigator) {
			// Yes a linked list is algorithmically faster, but in reality with TRACK_CELLS
			// being so small the better cache behavior of int[] is faster
			VisitedCells = new int[TRACK_CELLS];
			Reset();
		}

		public override void BeginTransition(Navigator navigator, Navigator.
				ActiveTransition transition) {
			var inst = AllMinionsLocationHistory.Instance;
			base.BeginTransition(navigator, transition);
			int cell = Grid.PosToCell(navigator);
			if (Grid.IsValidCell(cell) && cell != VisitedCells[0]) {
				// Update global history
				if (inst != null) {
					int droppedCell = VisitedCells[TRACK_CELLS - 1];
					inst.IncrementRefCount(cell);
					if (Grid.IsValidBuildingCell(droppedCell))
						inst.DecrementRefCount(droppedCell);
				}
				for (int i = TRACK_CELLS - 1; i > 0; i--)
					VisitedCells[i] = VisitedCells[i - 1];
				VisitedCells[0] = cell;
			}
		}

		public override void Destroy() {
			var inst = AllMinionsLocationHistory.Instance;
			if (inst != null)
				for (int i = 0; i < TRACK_CELLS; i++) {
					int cell = VisitedCells[i];
					if (Grid.IsValidCell(cell))
						inst.DecrementRefCount(cell);
				}
			base.Destroy();
		}

		/// <summary>
		/// Resets the location history to all invalid cells.
		/// </summary>
		internal void Reset() {
			for (int i = 0; i < TRACK_CELLS; i++)
				VisitedCells[i] = Grid.InvalidCell;
		}
	}
}
