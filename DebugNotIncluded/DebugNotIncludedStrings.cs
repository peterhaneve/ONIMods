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

using AutomationState = STRINGS.UI.AutomationState;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Strings used in Debug Not Included.
	/// </summary>
	public static class DebugNotIncludedStrings {
		public static class UI {
			// New restart message
			public static class LOADERRORDIALOG {
				public static LocString TEXT = "An error occurred during start-up.\n\n{0}";
				public static LocString BLAME = "The crashing code is likely from <b>{0}</b>!";
				public static LocString UNKNOWN = "It is not clear which mod caused the error.";
				public static LocString DISABLEMOD = "DISABLE AND RESTART";
				public static LocString OPENLOG = "OPEN OUTPUT LOG";
			}

			// Mod status changes
			public static class MODEVENTS {
				public static LocString ACTIVATED = STRINGS.UI.FormatAsAutomationState("Activated",
					AutomationState.Active);
				public static LocString DEACTIVATED = STRINGS.UI.FormatAsAutomationState("Deactivated",
					AutomationState.Standby);
				public static LocString NOTLOADED = STRINGS.UI.FormatAsAutomationState("Not loaded",
					AutomationState.Standby);
			}

			// Edit Mod
			public static class MODIFYDIALOG {
				public static LocString CAPTION = "Title";
				public static LocString DATA_PATH = "Mod Data";
				public static LocString DESC = "Description";
				public static LocString CANCEL = "CANCEL";
				public static LocString IMAGE_PATH = "Preview Image";
				public static LocString OK = "OK";
				public static LocString PATCHNOTES = "Patch Notes";
				public static LocString SUCCESS = "Succesfully updated mod <b>{0}</b>!";
				public static LocString TITLE = "UPDATE MOD <i>{0}</i>";
			}

			// Unable to modify mod
			public static class MODIFYFAILEDDIALOG {
				public static LocString TEXT = "Unable to modify mod <b>{0}</b>.\nMods can only be edited if you are the owner.";
			}

			// Mod management
			public static class MODSSCREEN {
				public static LocString BUTTON_ALL = "ALL";
				public static LocString BUTTON_LOCAL = "Local Folder";
				public static LocString BUTTON_MODIFY = "Edit";
				public static LocString BUTTON_SUBSCRIPTION = "Subscription";
				public static LocString BUTTON_UNSUB = "Unsubscribe";
				public static LocString LABEL_PLIB = "PLib Version: {0}";
				public static LocString LABEL_DESCRIPTION = "<b>Mod ID</b>: {0}\n";
				public static LocString LABEL_THISMOD = "Thank you for using Debug Not Included!";
				public static LocString LABEL_VERSIONS_FILE = "File Version {0}, ";
				public static LocString LABEL_VERSIONS_ASSEMBLY = "<b>{0}</b>: {1}Assembly Version {2}\n";
				public static LocString LABEL_VERSIONS_BOTH = "<b>{0}</b>: Version {1}\n";
				public static LocString LABEL_PLIB_MERGED = " <b>merged</b>";
				public static LocString LABEL_PLIB_PACKED = " <b>packed</b>";
			}

			// Not first on the list
			public static class NOTFIRSTDIALOG {
				public static LocString TEXT = "Debug Not Included is not the first active mod in the load order.\nDebug Not Included can only debug mods loaded after it.\n\nMove Debug Not Included to the first position?";
				public static LocString TITLE = "LOAD ORDER";
				public static LocString CONFIRM = "MOVE TO TOP";
				public static LocString CANCEL = "CONTINUE";
				public static LocString IGNORE = "DISABLE THIS WARNING";
			}

			public static class TOOLTIPS {
				public static LocString DNI_TOP = "Move to top";
				public static LocString DNI_UP = "Move up 10 slots";
				public static LocString DNI_DOWN = "Move down 10 slots";
				public static LocString DNI_BOTTOM = "Move to bottom";
				public static LocString DNI_ALL = "Enable or disable all";
				public static LocString DNI_UNSUB = "Unsubscribe from this mod";
				public static LocString DNI_MODIFY = "Modify this mod";
				public static LocString DNI_PLIB = "The currently active version of PLib\nFrom Mod: <b>{0}</b>";
				public static LocString DNI_INSTANT_GROW = "Set plant growth to 100%";
				public static LocString DNI_INSTANT_TAME = "Set wildness to 0%";
				public static LocString DNI_OVERJOY = "Start this Duplicant's Overjoyed reaction immediately";
				public static LocString DNI_STRESSOUT = "Start this Duplicant's Stress reaction immediately";
			}

			// Unable to unsubscribe from mod
			public static class UNSUBFAILEDDIALOG {
				public static LocString TEXT = "Unable to unsubscribe from mod <b>{0}</b>.";
			}

			public static class USERMENUOPTIONS {
				public static LocString INSTANTGROW = "Instantly Grow";
				public static LocString OVERJOY = "Start Overjoyed Reaction";
				public static LocString STRESSOUT = "Start Stress Reaction";
			}
		}

		public static class INPUT_BINDINGS {
			public static class DEBUG {
				// Key binding to snapshot item under cursor
				public static LocString SNAPSHOT = "Log UI element under cursor";
			}
		}
	}
}
