/*
 * Copyright 2022 Peter Han
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
using System;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Allows much faster fetch handling by dictionary sorting using priority value and
	/// class.
	/// </summary>
	public struct FetchInfo : IComparable<FetchInfo> {
		/// <summary>
		/// The category of the errand.
		/// </summary>
		internal readonly Storage.FetchCategory category;

		/// <summary>
		/// The chore to be executed.
		/// </summary>
		internal readonly FetchChore chore;

		/// <summary>
		/// The navigation cost to the errand.
		/// </summary>
		internal readonly int cost;

		/// <summary>
		/// The hash of the tag bits for the errand item.
		/// </summary>
		internal readonly int tagBitsHash;

		public FetchInfo(FetchChore fetchChore, int cost, Storage destination) {
			category = destination.fetchCategory;
			this.cost = cost;
			chore = fetchChore;
			tagBitsHash = fetchChore.tagBitsHash;
		}

		public int CompareTo(FetchInfo other) {
			int result = other.chore.masterPriority.CompareTo(chore.masterPriority);
			if (result == 0)
				result = cost.CompareTo(other.cost);
			return result;
		}

		public override bool Equals(object obj) {
			return obj is FetchInfo other && tagBitsHash == other.tagBitsHash &&
				category == other.category && chore.choreType == other.chore.choreType &&
				chore.tagBits.AreEqual(ref other.chore.tagBits);
		}

		public override int GetHashCode() {
			return tagBitsHash;
		}

		public override string ToString() {
			var p = chore.masterPriority;
			return "FetchInfo[category={0},cost={1:D},priority={2},{3:D}]".F(category,
				cost, p.priority_class, p.priority_value);
		}
	}
}
