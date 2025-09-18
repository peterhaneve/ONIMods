/*
 * Copyright 2025 Peter Han
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

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// Strings used in Thermal Interface Plate.
	/// </summary>
	public static class ThermalPlateStrings {
		// Thermal Interface Plate
		public static class BUILDINGS {
			public static class PREFABS {
				public static class THERMALINTERFACEPLATE {
					private static readonly string ID = ThermalPlateConfig.ID.ToUpperInvariant();

					public static LocString NAME = STRINGS.UI.FormatAsLink("Thermal Interface Plate", ID);
					public static LocString DESC = "With eyes glazed over from proposals such as \"heat pipes\" or \"vapor chambers\", one Duplicant had the bright idea of simply wedging this piece of scrap metal between buildings to transfer heat.";
					public static LocString EFFECT = string.Concat("Transfers ",
						STRINGS.UI.FormatAsLink("Heat", "HEAT"),
						" between buildings, even if they are in a complete ",
						STRINGS.UI.FormatAsLink("Vacuum", "VACUUM"),
						".\n\nPrevents ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						" and ",
						STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
						" loss in space.");

					public static class FACADES {
						public class BASIC_BLUE_COBALT {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Solid Cobalt Thermal Interface Plate", ID);

							public static LocString DESC = "It doesn't cure the blues, so much as emphasize them.";
						}

						public class BASIC_GREEN_KELLY {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Spring Green Thermal Interface Plate", ID);

							public static LocString DESC = "It's cheaper than having a garden.";
						}

						public class BASIC_GREY_CHARCOAL {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Solid Charcoal Thermal Interface Plate", ID);

							public static LocString DESC = "An elevated take on \"gray\".";
						}

						public class BASIC_ORANGE_SATSUMA {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Solid Satsuma Thermal Interface Plate", ID);

							public static LocString DESC = "Less fruit-forward, but just as fresh.";
						}

						public class BASIC_PINK_FLAMINGO {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Solid Pink Thermal Interface Plate", ID);

							public static LocString DESC = "A bold statement, for bold Duplicants.";
						}

						public class BASIC_RED_DEEP {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Chili Red Thermal Interface Plate", ID);

							public static LocString DESC = "It really spices up dull walls.";
						}

						public class BASIC_YELLOW_LEMON {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Canary Yellow Thermal Interface Plate", ID);

							public static LocString DESC = "The original coal-mine chic.";
						}

						public class PASTELBLUE {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Pastel Blue Thermal Interface Plate", ID);

							public static LocString DESC = "A soothing blue thermal interface plate.";
						}

						public class PASTELGREEN {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Pastel Green Thermal Interface Plate", ID);

							public static LocString DESC = "A soothing green thermal interface plate.";
						}

						public class PASTELPINK {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Pastel Pink Thermal Interface Plate", ID);

							public static LocString DESC = "A soothing pink thermal interface plate.";
						}

						public class PASTELPURPLE {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Pastel Purple Thermal Interface Plate", ID);

							public static LocString DESC = "A soothing purple thermal interface plate.";
						}

						public class PASTELYELLOW {
							public static LocString NAME = STRINGS.UI.FormatAsLink("Pastel Yellow Thermal Interface Plate", ID);

							public static LocString DESC = "A soothing yellow thermal interface plate.";
						}
					}
				}
			}
		}
	}
}
