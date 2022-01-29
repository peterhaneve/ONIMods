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

using System;
using System.Collections.Generic;

namespace PeterHan.SweepByType {
	/// <summary>
	/// The default comparer for Tag sorts on hash code, not alphabet. This comparer sorts
	/// tags by alphabetical <b>proper name</b> order, case <b>in</b>sensitive.
	/// </summary>
	internal sealed class TagAlphabetComparer : IComparer<Tag> {
		/// <summary>
		/// The only instance of this class.
		/// </summary>
		internal static readonly IComparer<Tag> INSTANCE = new TagAlphabetComparer();

		private TagAlphabetComparer() { }

		public int Compare(Tag x, Tag y) {
			if (x == null)
				throw new ArgumentNullException("x");
			if (y == null)
				throw new ArgumentNullException("y");
			string nx = x.ProperName(), ny = y.ProperName();
			if (string.IsNullOrEmpty(nx))
				nx = x.ToString();
			if (string.IsNullOrEmpty(ny))
				ny = y.ToString();
			int difference = string.Compare(nx, ny, true);
			if (difference == 0)
				difference = string.Compare(x.Name, y.Name, StringComparison.InvariantCulture);
			return difference;
		}
	}
}
