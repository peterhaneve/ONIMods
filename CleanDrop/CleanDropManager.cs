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

using System;

namespace PeterHan.CleanDrop {
	/// <summary>
	/// Stores information mapping workers to the drop direction for each tile in the game.
	/// </summary>
	public sealed class CleanDropManager {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static CleanDropManager Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance.
		/// </summary>
		internal static void CreateInstance() {
			Instance = new CleanDropManager();
		}

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// Gets the direction where the worker is standing, relative to the target.
		/// </summary>
		/// <param name="workCell">The current worker location.</param>
		/// <param name="targetCell">The cell of the workable.</param>
		/// <returns>The direction of the worker relative to the workable. If the worker is
		/// below and to the left of the workable, returns DownLeft, not UpRight.</returns>
		public static LastUsedDirection GetWorkerDirection(int workCell, int targetCell) {
			LastUsedDirection direction;
			if (workCell == targetCell)
				direction = LastUsedDirection.Center;
			else {
				// Since Duplicants are 2 cells tall, anything that drops at foot or head
				// height is expelled directly in the cardinal direction
				Grid.CellToXY(workCell, out int wx, out int wy);
				Grid.CellToXY(targetCell, out int tx, out int ty);
				int dx = wx - tx, dy = wy - ty;
				if (dx < 0) {
					// To the left
					if (dy == 0 || dy == -1)
						direction = LastUsedDirection.Left;
					else if (dy < 0)
						direction = LastUsedDirection.DownLeft;
					else
						direction = LastUsedDirection.UpLeft;
				} else if (dx > 0) {
					// To the right
					if (dy == 0 || dy == -1)
						direction = LastUsedDirection.Right;
					else if (dy < 0)
						direction = LastUsedDirection.DownRight;
					else
						direction = LastUsedDirection.UpRight;
				} else
					// Directly above or below the target
					direction = (dy > 0) ? LastUsedDirection.Up : LastUsedDirection.Down;
			}
			return direction;
		}

		/// <summary>
		/// Gets or sets the last used direction for a given cell.
		/// </summary>
		/// <param name="cell">The cell to modify.</param>
		/// <returns>The last used direction for that cell</returns>
		public LastUsedDirection this[int cell] {
			get {
				return Grid.IsValidCell(cell) ? grid[cell] : LastUsedDirection.None;
			}
			set {
				if (Grid.IsValidCell(cell))
					grid[cell] = value;
			}
		}

		/// <summary>
		/// The last workable use direction for each cell in the grid. Although multiple
		/// workables may exist in each cell, since Unity is single threaded the checks for
		/// the direction will 99% of the time execute immediately after the direction is set,
		/// even if two workables finish on the same frame.
		/// 
		/// The only potential failure case here is with items that have a SimCellOccupier as
		/// their destroy method is not triggered until the Sim has replaced their element.
		/// </summary>
		private readonly LastUsedDirection[] grid;

		private CleanDropManager() {
			grid = new LastUsedDirection[Grid.CellCount];
		}
	}

	public enum LastUsedDirection {
		None, UpLeft, Up, UpRight, Left, Center, Right, DownLeft, Down, DownRight
	}
}
