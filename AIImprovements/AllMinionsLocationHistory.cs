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

using System;

namespace PeterHan.AIImprovements {
	/// <summary>
	/// Stores the last few locations travelled by all Duplicants.
	/// </summary>
	internal sealed class AllMinionsLocationHistory {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static AllMinionsLocationHistory Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		public static void InitInstance() {
			Instance = new AllMinionsLocationHistory();
		}

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		public static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// The locations where Duplicants have been.
		/// </summary>
		private readonly int[] history;

		private AllMinionsLocationHistory() {
			history = new int[Grid.CellCount];
		}

		/// <summary>
		/// Subtracts one from the reference count of a cell when it leaves a Duplicant's
		/// location history.
		/// </summary>
		/// <param name="cell">The cell to modify.</param>
		internal void DecrementRefCount(int cell) {
			int value = history[cell];
			if (value > 0)
				history[cell] = value - 1;
		}

		/// <summary>
		/// Adds one to the reference count of a cell when a Duplicant enters it.
		/// </summary>
		/// <param name="cell">The cell to modify.</param>
		internal void IncrementRefCount(int cell) {
			history[cell]++;
		}

		/// <summary>
		/// Returns true if the cell is (or was recently) occupied by a Duplicant, or false
		/// otherwise.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>true if it was recently occupied, or false otherwise.</returns>
		public bool WasRecentlyOccupied(int cell) {
			return Grid.IsValidCell(cell) && history[cell] > 0;
		}
	}
}
