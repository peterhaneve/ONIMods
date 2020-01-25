/*
 * Copyright 2020 Peter Han
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
using System.Collections.Generic;

using CellSet = System.Collections.Generic.ICollection<int>;

namespace PeterHan.FallingSand {
	/// <summary>
	/// Manages falling sand cells and tracks which ones are due to digging events, as the
	/// required functionality is spread across several classes in the game.
	/// </summary>
	sealed class FallingSandManager {
		/// <summary>
		/// This class is OK to have a single instance as Grid and many of its other
		/// dependencies are static.
		/// </summary>
		public static FallingSandManager Instance { get; } = new FallingSandManager();

		/// <summary>
		/// Cells which will be dug at the next opportunity.
		/// </summary>
		private readonly IDictionary<int, PrioritySetting> digCells;

		/// <summary>
		/// Cells which are considered falling as a result of digging.
		/// </summary>
		private readonly IDictionary<Diggable, CellSet> fallingCells;

		private FallingSandManager() {
			digCells = new Dictionary<int, PrioritySetting>(32);
			fallingCells = new Dictionary<Diggable, CellSet>(32);
		}

		/// <summary>
		/// Checks to see if the cell should be dug. Removes the cell from the list and places
		/// the dig errand if it is ound.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		public void CheckDigQueue(int cell) {
			if (digCells.TryGetValue(cell, out PrioritySetting priority)) {
				Prioritizable component;
				var xy = Grid.CellToXY(cell);
				// Assign priority to the dig
				if (Grid.IsSolidCell(cell)) {
					var obj = DigTool.PlaceDig(cell);
					PUtil.LogDebug("Placed dig in cell ({0:D},{1:D})".F(xy.X, xy.Y));
					if ((component = obj.GetComponentSafe<Prioritizable>()) != null)
						component.SetMasterPriority(priority);
				} else
					PUtil.LogDebug("Could not place dig in cell ({0:D},{1:D})".F(xy.X, xy.Y));
				digCells.Remove(cell);
			}
		}

		/// <summary>
		/// Clears all pending dig orders.
		/// </summary>
		public void ClearDig() {
			digCells.Clear();
		}

		/// <summary>
		/// Stops tracking all diggables.
		/// </summary>
		public void ClearAll() {
			PUtil.LogDebug("Stopped tracking {0:D} diggables, {1:D} queued digs".F(
				fallingCells.Count, digCells.Count));
			digCells.Clear();
			fallingCells.Clear();
		}

		/// <summary>
		/// Finds the dig errand which caused the specified cell to fall. Returns null if this
		/// cell fell due to other reasons.
		/// 
		/// If multiple dig errands caused this cell to fall, an arbitrary one is returned.
		/// </summary>
		/// <param name="cell">The cell which is falling.</param>
		/// <returns>The causing dig errand.</returns>
		public Diggable FindDigErrand(int cell) {
			var destroyed = ListPool<Diggable, FallFromDigging>.Allocate();
			Diggable cause = null;
			foreach (var pair in fallingCells) {
				var key = pair.Key;
				if (key == null)
					// Diggable was destroyed
					destroyed.Add(key);
				else if (pair.Value.Contains(cell))
					cause = key;
			}
			// Clean up destroyed dig errands, there should normally be none so this is very
			// low performance impact
			foreach (var key in destroyed)
				fallingCells.Remove(key);
			destroyed.Recycle();
			return cause;
		}

		/// <summary>
		/// Adds all cells containing falling objects or unstable items which are about to fall
		/// to the fall digging list.
		/// </summary>
		/// <param name="cell">The cell which has been dug out.</param>
		/// <param name="results">The location where falling cells will be placed.</param>
		private void GetFallingCells(int cell, CellSet results) {
			var ugm = World.Instance.GetComponent<UnstableGroundManager>();
			var fallables = ugm.GetCellsContainingFallingAbove(Grid.CellToXY(cell));
			bool moreCells;
			do {
				// Continue while cell is valid and not a foundation
				moreCells = Grid.IsValidCell(cell) && !Grid.Foundation[cell];
				if (moreCells) {
					if (Grid.Solid[cell]) {
						// Solid/unstable = add to list, solid/stable = end search, unsolid =
						// continue search without adding
						if (Grid.Element[cell].IsUnstable)
							results.Add(cell);
						else
							moreCells = false;
					} else if (fallables.Contains(cell))
						// Add falling objects also in the tiles
						results.Add(cell);
					cell = Grid.CellAbove(cell);
				}
			} while (moreCells);
		}

		/// <summary>
		/// Queues a dig on the cell if it is not already queued for digging.
		/// </summary>
		/// <param name="cell">The cell which should be dug once it solidifies.</param>
		public void QueueDig(int cell, PrioritySetting priority) {
			if (!digCells.ContainsKey(cell))
				digCells.Add(cell, priority);
		}

		/// <summary>
		/// Starts tracking falling objects caused by a processed dig errand.
		/// </summary>
		/// <param name="instance">The dig errand which just finished a cell.</param>
		public void TrackDiggable(Diggable instance) {
			int cell = Grid.PosToCell(instance);
			if (!Grid.Solid[cell] && !fallingCells.ContainsKey(instance))
			{
				var temp = ListPool<int, FallFromDigging>.Allocate();
				// If there are any falling cells, add to list
				GetFallingCells(cell, temp);
				if (temp.Count > 0) {
#if DEBUG
					var xy = Grid.CellToXY(cell);
					PUtil.LogDebug("Tracking cell ({0:D},{1:D}): {2:D} fallables".F(xy.X,
						xy.Y, temp.Count));
#endif
					fallingCells.Add(instance, new HashSet<int>(temp));
				}
				temp.Recycle();
			}
		}

		/// <summary>
		/// Stops tracking falling objects caused by a destroyed dig errand.
		/// </summary>
		/// <param name="instance">The dig errand which was just retired.</param>
		public void UntrackDiggable(Diggable instance) {
			if (fallingCells.Remove(instance)) {
				var xy = Grid.PosToXY(instance.transform.GetPosition());
#if DEBUG
				PUtil.LogDebug("Stopped tracking cell ({0:D},{1:D})".F(xy.X, xy.Y));
#endif
			}
		}
	}
}
