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

namespace PeterHan.PipPlantOverlay {
	/// <summary>
	/// Strings used in PipPlantOverlay.
	/// </summary>
	public static class PipPlantOverlayStrings {
		// Action command for opening the overlay, not localizable
		public const string OVERLAY_ACTION = "action_overlay_pip";
		// Sprite for the pip icon, not localizable
		public const string OVERLAY_ICON = "overlay_pip";

		public static class INPUT_BINDINGS {
			public static class ROOT {
				public static LocString PIPPLANT = "Open Pip Planting Overlay";
			}
		}

		public static class UI {
			public static class OVERLAYS {
				public static class PIPPLANTING {
					public static LocString NAME = "PIP PLANTING OVERLAY";
					public static LocString DESCRIPTION = "Displays the locations where Pips can plant seeds.\n\nPips will consider planting a seed in a tile under these conditions:\n1. The tile has no more than 2 other plants in a square 6 left, 6 down, 5 right, 5 up from the tile.\n2. It is either a \"natural tile\" of less than 150 hardness, a farm tile or a hydroponic farm.\n3. The pip can navigate to the seed and the tile, and there must be no buildings obstructing the plant's location.\n4. The atmospheric pressure is greater than 100g.\n5. The atmospheric temperature is around ±50C to ±100C of the requirements of the plant.\n";
					public static LocString BUTTON = "Pip Planting Overlay";
					public static LocString TOOLTIP = "Displays locations where Pips can plant seeds";

					public static LocString CANPLANT = "Can plant here";
					public static LocString HARDNESS = "Tile is too hard";
					public static LocString PLANTCOUNT = "Too many plants";
					public static LocString PRESSURE = "Pressure too low";
					public static LocString TEMPERATURE = "Temperature too extreme";

					public static class TOOLTIPS {
						public static LocString CANPLANT = "Seeds can be planted here, if there is room and temperature is valid";
						public static LocString HARDNESS = "Natural tile hardness is above {0:D}!";
						public static LocString PLANTCOUNT_1 = "More than one other plant is within the range of {1:D}!";
						public static LocString PLANTCOUNT = "More than {0:D} other plants are within the range of {1:D}!";
						public static LocString PRESSURE = "Atmospheric pressure is below {0} or tile is flooded!";
						public static LocString TEMPERATURE = "Temperature is below {0} or above {1}!";
					}
				}
			}
		}
	}
}
