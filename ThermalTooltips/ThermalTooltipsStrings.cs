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

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Stores the strings used in Thermal Tooltips.
	/// </summary>
	static class ThermalTooltipsStrings {
		// The string used to display all temperature units
		internal const string ALL_TEMPS = "{0} / {1} / {2}";

		// <element> [<percentage>] and <secondary element> [<percentage>]
		public static LocString AND_JOIN = "[{0}] and ";

		// Building information shown in the build screen
		public static LocString BUILDING_CONDUCTIVITY = "The completed {0} will have a thermal conductivity of <b>{1}</b>\n\nFor every 1 {3} of difference between the building's " +
			STRINGS.UI.FormatAsLink("Temperature", "HEAT") + " and its surroundings, {2:##0.#} {4} will be transferred";

		public static LocString BUILDING_MELT_TEMPERATURE = "The completed {0} will melt at <b>{1}</b> into {2}";

		public static LocString BUILDING_THERMAL_MASS = "The completed {0} will have a thermal mass of <b>{1:##0.#} {2}/{3}</b>\n\nAdding or removing {1:##0.#} {2} will change the building's " +
			STRINGS.UI.FormatAsLink("Temperature", "HEAT") + " by 1 {3}";

		// State change message (fallback if icon not found)
		public static LocString CHANGES = "Changes";

		// Effect headers for the build screen
		public static LocString EFFECT_CONDUCTIVITY = STRINGS.UI.FormatAsLink(
			"Thermal Conductivity", "HEAT") + ": {0}";

		public static LocString EFFECT_MELT_TEMPERATURE = STRINGS.UI.FormatAsLink(
			"Melting Point", "HEAT") + ": {0}";

		public static LocString EFFECT_THERMAL_MASS = STRINGS.UI.FormatAsLink("Thermal Mass",
			"HEAT") + ": {0:##0.#} {1}/{2}";

		// Total heat energy in kDTU
		public static LocString HEAT_ENERGY = "Heat Energy: {0} {1}";

		// Number formats
		internal const string NUM_FORMAT_BIG = "{0}x10<sup>{1:D}</sup>";
		internal const string NUM_FORMAT_SMALL = "##0.#";

		// Format to use for temperature values (including suffix)
		internal const string TEMP_FORMAT = "{0:##0.#}{1}";

		// The sum suffix for Better Info Cards compatibility
		public static LocString SUM = " (\u03A3)";

		// Thermal mass is the amount of kDTU required to shift by 1 degree C/K/F
		public static LocString THERMAL_MASS = "Thermal Mass: {0} {1}/{2}";

		// <heat icon> to <element> (temperature)
		public static LocString TO_JOIN = " to ";
	}
}
