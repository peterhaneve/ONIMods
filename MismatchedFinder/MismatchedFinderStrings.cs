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

using System;

namespace PeterHan.MismatchedFinder {
	/// <summary>
	/// Strings used in Mismatched Wire Finder.
	/// </summary>
	public static class MismatchedFinderStrings {
		public static class UI {
			public static class TOOLTIPS {
				public static LocString FIND_PIPE = "Locate connected " +
					STRINGS.UI.FormatAsLink("Pipes", "LIQUIDPIPING") +
					" made of a different material";
				public static LocString FIND_WIRE = "Locate connected " +
					STRINGS.UI.FormatAsLink("Wires", "WIRE") + " with a different maximum " +
					STRINGS.UI.FormatAsLink("Wattage", "POWER");
			}

			public static class USERMENUOPTIONS {
				public static LocString FIND_PIPE = "Find Mismatched";
				public static LocString FIND_WIRE = "Find Mismatched";
			}
		}
	}
}
