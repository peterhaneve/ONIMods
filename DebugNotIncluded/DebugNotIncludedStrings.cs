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

using STRINGS;

using AutomationState = STRINGS.UI.AutomationState;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Strings used in Debug Not Included.
	/// </summary>
	public static class DebugNotIncludedStrings {
		// Mod status changes
		public static LocString MOD_ACTIVATED = UI.FormatAsAutomationState("Activated", AutomationState.Active);
		public static LocString MOD_DEACTIVATED = UI.FormatAsAutomationState("Deactivated", AutomationState.Standby);
		public static LocString MOD_NOTLOADED = UI.FormatAsAutomationState("Not loaded", AutomationState.Standby);

		// Mod management
		public static LocString TOOLTIP_TOP = "Move to top";
		public static LocString TOOLTIP_UPONE = "Move up 10 slots";
		public static LocString TOOLTIP_DOWNONE = "Move down 10 slots";
		public static LocString TOOLTIP_BOTTOM = "Move to bottom";
		public static LocString TOOLTIP_ALL = "Enable or disable all";
		public static LocString TOOLTIP_PLIB = "The currently active version of PLib\nFrom Mod: {0}";
		public static LocString BUTTON_ALL = "ALL";
		public static LocString LABEL_PLIB = "PLib Version: {0}";
		public static LocString LABEL_DESCRIPTION = "Mod ID: {0}\r\nDescription: {1}\r\n";
		public static LocString LABEL_VERSIONS_FILE = "File Version {0}, ";
		public static LocString LABEL_VERSIONS_ASSEMBLY = "{0}: {1}Assembly Version {2}\r\n";

		// Not first on the list
		public static LocString DIALOG_NOTFIRST = "Debug Not Included is not the first active mod in the load order.\r\nDebug Not Included can only debug mods loaded after it.\r\n\r\nMove Debug Not Included to the first position?";
		public static LocString NOTFIRST_TITLE = "LOAD ORDER";
		public static LocString NOTFIRST_CONFIRM = "MOVE TO TOP";
		public static LocString NOTFIRST_CANCEL = "CONTINUE";
		public static LocString NOTFIRST_IGNORE = "DISABLE THIS WARNING";

		// New restart message
		public static LocString DIALOG_LOADERROR = "An error occurred during start-up.\r\n\r\n{0}";
		public static LocString LOADERR_BLAME = "The crashing code is likely from \"{0}\"!";
		public static LocString LOADERR_UNKNOWN = "It is not clear which mod caused the error.";
		public static LocString LOADERR_DISABLEMOD = "DISABLE AND RESTART";
		public static LocString LOADERR_OPENLOG = "OPEN OUTPUT LOG";

		// Key binding to snapshot item under cursor
		public static LocString KEY_SNAPSHOT = "Log UI element under cursor";
	}
}
