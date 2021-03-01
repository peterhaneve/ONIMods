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
using System.Collections.Generic;
using System.Threading;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Pairs up with NavGrid objects to allow path optimization if the grid is not updated by
	/// setting up fences based on serial numbers. Mostly thread safe.
	/// </summary>
	public sealed class NavFences {
		/// <summary>
		/// One fence set is used for each nav grid type in GameNavGrids.
		/// </summary>
		internal static IDictionary<string, NavFences> AllFences { get; } =
			new Dictionary<string, NavFences>();

		/// <summary>
		/// The global serial number.
		/// </summary>
		private long globalSerial;

		/// <summary>
		/// The serial numbers for each cell.
		/// </summary>
		private long[] localSerial;

		/// <summary>
		/// The serial number for the entire grid.
		/// </summary>
		public long CurrentSerial {
			get {
				return globalSerial;
			}
		}

		internal NavFences() {
			Reset();
		}

		/// <summary>
		/// Checks to see if the specific path is still valid.
		/// </summary>
		/// <param name="oldSerial">The serial number when this path was last calculated.</param>
		/// <param name="cells">The cells on this path.</param>
		/// <returns>true if the cells on this path were not updated since then, or false if
		/// they were.</returns>
		public bool IsPathCurrent(long oldSerial, IEnumerable<int> cells) {
			long startSerial, newSerial = Interlocked.Read(ref globalSerial);
			if (cells == null)
				throw new ArgumentNullException("cells");
			bool current = true;
			do {
				// If the grid is updated mid-execution, loop until it is stable
				startSerial = newSerial;
				foreach (int cell in cells)
					if (oldSerial < localSerial[cell]) {
						current = false;
						break;
					}
			} while (current && (newSerial = Interlocked.Read(ref globalSerial)) !=
				startSerial);
			return current;
		}

		/// <summary>
		/// Resets the fences completely, used on game start.
		/// </summary>
		public void Reset() {
			localSerial = new long[Grid.CellCount];
			globalSerial = 0L;
		}

		/// <summary>
		/// Called when cells are made dirty to update paths that use those cells.
		/// </summary>
		/// <param name="updatedCells">The cells that were updated.</param>
		public void UpdateSerial(IEnumerable<int> updatedCells = null) {
			long startSerial, newSerial;
			do {
				startSerial = Interlocked.Read(ref globalSerial);
				newSerial = startSerial + 1L;
				if (updatedCells != null)
					foreach (int cell in updatedCells)
						if (cell >= 0)
							localSerial[cell] = newSerial;
				// Compare/Exchange will only increment the global serial if the operation
				// fully completed without any other thread affecting it
			} while (Interlocked.CompareExchange(ref globalSerial, newSerial, startSerial) !=
				startSerial);
		}

		/// <summary>
		/// Called when a cell is made dirty to update paths that use that cell.
		/// </summary>
		/// <param name="updatedCell">The cell that was updated.</param>
		public void UpdateSerial(int updatedCell) {
			long startSerial, newSerial;
			do {
				startSerial = Interlocked.Read(ref globalSerial);
				newSerial = startSerial + 1L;
				if (updatedCell >= 0)
					localSerial[updatedCell] = newSerial;
				// Compare/Exchange will only increment the global serial if the operation
				// fully completed without any other thread affecting it
			} while (Interlocked.CompareExchange(ref globalSerial, newSerial, startSerial) !=
				startSerial);
		}
	}
}
