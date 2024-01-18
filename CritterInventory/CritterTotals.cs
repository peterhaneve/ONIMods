/*
 * Copyright 2024 Peter Han
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

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Stores the total quantity of critters available and the quantity reserved for errands.
	/// 
	/// While this could be a struct, it would get copied a lot.
	/// </summary>
	internal sealed class CritterTotals {
		/// <summary>
		/// The number of critters available to be used (total minus reserved).
		/// </summary>
		public int Available => Total - Reserved;

		/// <summary>
		/// Returns true if any critters were found.
		/// </summary>
		public bool HasAny => Total > 0;

		/// <summary>
		/// The number of critters of this type "reserved" for Wrangle or Attack errands.
		/// </summary>
		public int Reserved { get; set; }

		/// <summary>
		/// The total number of critters of this type.
		/// </summary>
		public int Total { get; set; }

		public CritterTotals() {
			Reserved = 0;
			Total = 0;
		}

		public override string ToString() {
			return "Total: {0:D} Reserved: {1:D}".F(Total, Reserved);
		}
	}
}
