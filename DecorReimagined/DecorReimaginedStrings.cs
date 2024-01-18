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

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Stores the strings used in Decor Reimagined.
	/// </summary>
	public static class DecorReimaginedStrings {
		// Decor levels of -3 and -2
		public static LocString DECORMINUS3_NAME = "Last Cycle's Decor: Terrible";
		public static LocString DECORMINUS3_TOOLTIP = "This Duplicant thought that the overall " +
			STRINGS.UI.PRE_KEYWORD + "Decor" + STRINGS.UI.PST_KEYWORD +
			" yesterday was downright depressing";

		public static LocString DECORMINUS2_NAME = "Last Cycle's Decor: Ugly";
		public static LocString DECORMINUS2_TOOLTIP = "This Duplicant thought that the overall " +
			STRINGS.UI.PRE_KEYWORD + "Decor" + STRINGS.UI.PST_KEYWORD +
			" yesterday was very poor";

		public static LocString DECORMINUS1_NAME = "Last Cycle's Decor: Drab";
		public static LocString DECORMINUS1_TOOLTIP = "This Duplicant thought that the overall " +
			STRINGS.UI.PRE_KEYWORD + "Decor" + STRINGS.UI.PST_KEYWORD +
			" yesterday could use some improvement";

		// Colony initiatives
		public static LocString FEELSLIKEHOME_NAME = "And It Feels Like Home";
		public static LocString FEELSLIKEHOME_DESC = "Have {0} unique positive decor items visible from the same location at once.";
		public static LocString FEELSLIKEHOME_PROGRESS = "Positive decor items visible at one location: {0:D}";

		public static class UI {
			public static class FRONTEND {
				public static class DECORREIMAGINED {
					public static LocString HARDMODE = "Hard Mode";
					public static LocString KEEPTILEDECOR = "Preserve Tile Decor";
					public static LocString NOCRITTERDECOR = "No Critter Decor";
				}
			}

			public static class TOOLTIPS {
				public static class DECORREIMAGINED {
					public static LocString HARDMODE = "Make your Duplicants more picky about decor, and your life much harder.";
					public static LocString KEEPTILEDECOR = "Preserve the decor values other mods have set for Tiles.";
					public static LocString NOCRITTERDECOR = "Removes decor from all critters.\r\nIncreases performance on large critter farms, but may make the game harder.";
				}
			}
		}
	}
}
