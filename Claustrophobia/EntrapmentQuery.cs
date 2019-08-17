/*
 * Copyright 2019 Peter Han
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
using UnityEngine;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Counts the total number of pathable cells that a Duplicant can reach, and checks to
	/// see if their bed / mess table / toilet is pathable.
	/// </summary>
	sealed class EntrapmentQuery : PathFinderQuery {
		/// <summary>
		/// Whether the bed is reachable.
		/// </summary>
		public bool CanReachBed {
			get {
				return bedCell == 0 || bedReachable;
			}
		}

		/// <summary>
		/// Whether the mess table is reachable.
		/// </summary>
		public bool CanReachMess {
			get {
				return messCell == 0 || messReachable;
			}
		}

		/// <summary>
		/// Whether the toilet is reachable.
		/// </summary>
		public bool CanReachToilet {
			get {
				return toiletCells.Length == 0 || toiletReachable;
			}
		}

		/// <summary>
		/// The number of reachable cells.
		/// </summary>
		public int ReachableCells { get; private set; }

		/// <summary>
		/// The location of this Duplicant's bed.
		/// </summary>
		private readonly int bedCell;

		/// <summary>
		/// Whether the bed is reachable.
		/// </summary>
		private bool bedReachable;

		/// <summary>
		/// The location of this Duplicant's mess table.
		/// </summary>
		private readonly int messCell;

		/// <summary>
		/// Whether the mess table is reachable.
		/// </summary>
		private bool messReachable;

		/// <summary>
		/// The location of this Duplicant's toilet.
		/// </summary>
		private readonly int[] toiletCells;

		/// <summary>
		/// Whether any toilet is reachable.
		/// </summary>
		private bool toiletReachable;

		public EntrapmentQuery(int bedCell, int messCell, int[] toiletCells) {
			ReachableCells = 0;
			if (toiletCells == null)
				throw new ArgumentNullException("toiletCells");
			int len = toiletCells.Length;
			this.bedCell = bedCell;
			this.messCell = messCell;
			this.toiletCells = new int[len];
			bedReachable = false;
			messReachable = false;
			toiletReachable = false;
			Array.Copy(toiletCells, this.toiletCells, len);
			Array.Sort(this.toiletCells);
		}

		public override bool IsMatch(int cell, int parent_cell, int cost) {
			if (parent_cell != Grid.InvalidCell)
				ReachableCells++;
			if (cell == bedCell)
				bedReachable = true;
			if (cell == messCell)
				messReachable = true;
			int len = toiletCells.Length, tc;
			// Any reachable toilet
			for (int i = 0; i < len && !toiletReachable && (tc = toiletCells[i]) <= cell; i++)
				if (tc == cell)
					toiletReachable = true;
			return false;
		}

		public override string ToString() {
			return "EntrapmentQuery: {0:D} cells".F(ReachableCells);
		}
	}
}
