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

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// Strings used in Airlock Door.
	/// </summary>
	public static class AirlockDoorStrings {
		public static class BUILDING {
			public static class STATUSITEMS {
				/// <summary>
				/// The charge available in an airlock door.
				/// </summary>
				public static class AIRLOCKSTOREDCHARGE {
					public static LocString NAME = "Charge Available: {0}/{1}";

					public static LocString TOOLTIP = string.Concat(
						"This Airlock has <b>{0}</b> of stored ",
						STRINGS.UI.PRE_KEYWORD,
						"Power",
						STRINGS.UI.PST_KEYWORD,
						"\n\nIt consumes up to ",
						STRINGS.UI.FormatAsNegativeRate("{2}"),
						" per use");
				}
			}
		}

		public static class BUILDINGS {
			public static class PREFABS {
				public static class PAIRLOCKDOOR {
					public static LocString NAME = STRINGS.UI.FormatAsLink("Airlock Door", AirlockDoorConfig.ID);
					public static LocString DESC = string.Concat("Sucking Duplicants that have nowhere to go into space through ",
						STRINGS.UI.FormatAsLink("Mechanized Airlocks", PressureDoorConfig.ID),
						" is poor taste. Airlock Doors now allow Duplicants safe passage without the loss of any of their mysterious fluids.");
					public static LocString EFFECT = string.Concat("Blocks ",
						STRINGS.UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"),
						" and ",
						STRINGS.UI.FormatAsLink("Gas", "ELEMENTS_GAS"),
						" flow, even while Duplicants are passing.\n\nWill not allow passage when no ",
						STRINGS.UI.FormatAsLink("Power", "POWER"),
						" is available.\n\n",
						STRINGS.UI.FormatAsLink("Critters", "CRITTERS"),
						" can never pass through this door.");

					public static LocString LOGIC_OPEN = "Unlock/Lock";
					public static LocString LOGIC_OPEN_ACTIVE = STRINGS.UI.FormatAsAutomationState(
						"Green Signal", STRINGS.UI.AutomationState.Active) + ": Unlock door";
					public static LocString LOGIC_OPEN_INACTIVE = STRINGS.UI.FormatAsAutomationState(
						"Red Signal", STRINGS.UI.AutomationState.Standby) + ": Lock door";
				}
			}
		}
	}
}
