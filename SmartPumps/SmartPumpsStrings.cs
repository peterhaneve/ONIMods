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

namespace PeterHan.SmartPumps {
	/// <summary>
	/// Strings used in Smart Pumps.
	/// </summary>
	public static class SmartPumpsStrings {
		public static class BUILDINGS {
			public static class PREFABS {
				public static class FILTEREDGASPUMP {
					public static LocString NAME = STRINGS.UI.FormatAsLink("Filtered Gas Pump", FilteredGasPumpConfig.ID);
					public static LocString DESC = "Rumors hold that hidden behind the parts from discarded Carbon Skimmers used in this pump is a demon with a pair of chopsticks, picking gas molecules from the air forever and ever.";
					public static LocString EFFECT = string.Concat("Draws in only the specified ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						" and runs it through ",
						STRINGS.UI.FormatAsLink("Pipes", "GASPIPING"),
						".\n\nMust be immersed in ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						".");
				}

				public static class FILTEREDLIQUIDPUMP {
					public static LocString NAME = STRINGS.UI.FormatAsLink("Filtered Liquid Pump", FilteredLiquidPumpConfig.ID);
					public static LocString DESC = "Scarred by nightmares of mixed liquid pools, an obsessive colony AI commissioned this pump with an attached Element Sensor in an attempt to purge this heresy forever.";
					public static LocString EFFECT = string.Concat("Draws in only the specified ",
						STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
						" and runs it through ",
						STRINGS.UI.FormatAsLink("Pipes", "LIQUIDPIPING"),
						".\n\nMust be submerged in ",
						STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
						".");
				}

				public static class VACUUMPUMP {
					public static LocString NAME = STRINGS.UI.FormatAsLink("Vacuum Pump", VacuumPumpConfig.ID);
					public static LocString DESC = "After watching a Gas Pump work all night to lower the pressure in a room from 5 mg to 4 mg, Liam decided to invent this Vacuum Pump instead of wasting his time on the Manual Generator all day.";
					public static LocString EFFECT = string.Concat("Draws in low-pressure ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						" and runs it through ",
						STRINGS.UI.FormatAsLink("Pipes", "GASPIPING"),
						".\n\nMust be immersed in ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						".");
				}
			}
		}

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
