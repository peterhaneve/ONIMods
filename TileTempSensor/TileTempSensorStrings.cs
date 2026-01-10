/*
 * Copyright 2026 Peter Han
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

namespace PeterHan.TileTempSensor {
	/// <summary>
	/// Strings used in Thermo Sensor Tile.
	/// </summary>
	public static class TileTempSensorStrings {
		// Thermo Sensor Tile
		public static class BUILDINGS {
			public static class PREFABS {
				public static class TILETEMPSENSOR {
					public static LocString NAME = STRINGS.UI.FormatAsLink("Thermo Sensor Tile", TileTempSensorConfig.ID);
					public static LocString DESC = "Liquid drops have been sent an official eviction notice with the invention of a Thermo Sensor that can transfer heat effectively with its surrounding solid tiles.";
					public static LocString EFFECT = string.Concat("Sends a ",
						STRINGS.UI.FormatAsAutomationState("Green Signal", STRINGS.UI.AutomationState.Active),
						" or a ",
						STRINGS.UI.FormatAsAutomationState("Red Signal", STRINGS.UI.AutomationState.Standby),
						" when ambient ",
						STRINGS.UI.FormatAsLink("Temperature", "HEAT"),
						" enters the chosen range.");
				}
			}
		}
	}
}
