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

namespace PeterHan.SmartPumps {
	/// <summary>
	/// Strings used in Smart Pumps.
	/// </summary>
	static class SmartPumpsStrings {
		// Filtered Gas Pump
		public static LocString GASPUMP_NAME = "Filtered Gas Pump";
		public static LocString GASPUMP_DESCRIPTION = "Rumors hold that hidden behind the parts from discarded Carbon Skimmers used in this pump is a demon with a pair of chopsticks, picking gas molecules from the air forever and ever.";
		public static LocString GASPUMP_EFFECT = string.Concat("Draws in only the specified ",
			STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
			" and runs it through ",
			STRINGS.UI.FormatAsLink("Pipes", "GASPIPING"),
			".\n\nMust be immersed in ",
			STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
			".");

		// Filtered Liquid Pump
		public static LocString LIQUIDPUMP_NAME = "Filtered Liquid Pump";
		public static LocString LIQUIDPUMP_DESCRIPTION = "Scarred by nightmares of mixed liquid pools, an obsessive colony AI commissioned this pump with an attached Element Sensor in an attempt to purge this heresy forever.";
		public static LocString LIQUIDPUMP_EFFECT = string.Concat("Draws in only the specified ",
			STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
			" and runs it through ",
			STRINGS.UI.FormatAsLink("Pipes", "LIQUIDPIPING"),
			".\n\nMust be submerged in ",
			STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
			".");

		// Vacuum Pump
		public static LocString VACUUMPUMP_NAME = "Vacuum Pump";
		public static LocString VACUUMPUMP_DESCRIPTION = "After watching a Gas Pump work all night to lower the pressure in a room from 5 mg to 4 mg, Liam decided to invent this Vacuum Pump instead of wasting his time on the Manual Generator all day.";
		public static LocString VACUUMPUMP_EFFECT = string.Concat("Draws in low-pressure ",
			STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
			" and runs it through ",
			STRINGS.UI.FormatAsLink("Pipes", "GASPIPING"),
			".\n\nMust be immersed in ",
			STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
			".");

		// "No Matching Gas to Pump"
		public static LocString NOGASTOPUMP_NAME = "No Configured Gas to Pump";
		public static LocString NOGASTOPUMP_DESC = string.Concat(
			"This pump must be immersed in the proper ",
			STRINGS.UI.PRE_KEYWORD,
			"Gas",
			STRINGS.UI.PST_KEYWORD,
			" to work");

		// "No Matching Liquid to Pump"
		public static LocString NOLIQUIDTOPUMP_NAME = "No Configured Liquid to Pump";
		public static LocString NOLIQUIDTOPUMP_DESC = string.Concat(
			"This pump must be immersed in the proper ",
			STRINGS.UI.PRE_KEYWORD,
			"Liquid",
			STRINGS.UI.PST_KEYWORD,
			" to work");
	}
}
