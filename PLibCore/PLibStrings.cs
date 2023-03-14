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

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Strings used in PLib.
	/// </summary>
	public static class PLibStrings {
		/// <summary>
		/// The button used to manually edit the mod configuration.
		/// </summary>
		public static LocString BUTTON_MANUAL = "MANUAL CONFIG";

		/// <summary>
		/// The button used to reset the configuration to its default value.
		/// </summary>
		public static LocString BUTTON_RESET = "RESET TO DEFAULT";

		/// <summary>
		/// The text shown on the Done button.
		/// </summary>
		public static LocString BUTTON_OK = STRINGS.UI.FRONTEND.OPTIONS_SCREEN.BACK;

		/// <summary>
		/// The text shown on the Options button.
		/// </summary>
		public static LocString BUTTON_OPTIONS = STRINGS.UI.FRONTEND.MAINMENU.OPTIONS;
		
		/// <summary>
		/// The dialog title used for options, where {0} is substituted with the mod friendly name.
		/// </summary>
		public static LocString DIALOG_TITLE = "Options for {0}";

		// Utility key names
		public static LocString KEY_HOME = "Home";
		public static LocString KEY_END = "End";
		public static LocString KEY_DELETE = "Delete";
		public static LocString KEY_PAGEUP = "Page Up";
		public static LocString KEY_PAGEDOWN = "Page Down";
		public static LocString KEY_SYSRQ = "SysRq";
		public static LocString KEY_PRTSCREEN = "Print Screen";
		public static LocString KEY_PAUSE = "Pause";

		// Arrow key names
		public static LocString KEY_ARROWLEFT = "Left Arrow";
		public static LocString KEY_ARROWUP = "Up Arrow";
		public static LocString KEY_ARROWRIGHT = "Right Arrow";
		public static LocString KEY_ARROWDOWN = "Down Arrow";

		/// <summary>
		/// The title used for the PLib key bind category.
		/// </summary>
		public static LocString KEY_CATEGORY_TITLE = "Mods";
		
		/// <summary>
		/// The abbreviation text shown on the Blue field.
		/// </summary>
		public static LocString LABEL_B = "B";

		/// <summary>
		/// The abbreviation text shown on the Green field.
		/// </summary>
		public static LocString LABEL_G = "G";

		/// <summary>
		/// The abbreviation text shown on the Red field.
		/// </summary>
		public static LocString LABEL_R = "R";

		/// <summary>
		/// The mod version in Mod Options if retrieved from the default AssemblyVersion, where
		/// {0} is substituted with the version text.
		/// </summary>
		public static LocString MOD_ASSEMBLY_VERSION = "Assembly Version: {0}";

		/// <summary>
		/// The button text which goes to the mod's home page when clicked.
		/// </summary>
		public static LocString MOD_HOMEPAGE = "Mod Homepage";

		/// <summary>
		/// The mod version in Mod Options if specified via AssemblyFileVersion, where {0} is
		/// substituted with the version text.
		/// </summary>
		public static LocString MOD_VERSION = "Mod Version: {0}";

		/// <summary>
		/// The cancel button in the restart dialog.
		/// </summary>
		public static LocString RESTART_CANCEL = STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.CANCEL;

		/// <summary>
		/// The OK button in the restart dialog.
		/// </summary>
		public static LocString RESTART_OK = STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.OK;

		/// <summary>
		/// The details tooltip when AVC detects a mod to be outdated.
		/// </summary>
		public static LocString OUTDATED_TOOLTIP = "This mod is out of date!\nNew version: <b>{0}</b>\n\nUpdate local mods manually, or use <b>Mod Updater</b> to force update Steam mods";

		/// <summary>
		/// Displayed when AVC detects a mod to be outdated.
		/// </summary>
		public static LocString OUTDATED_WARNING = "<b><style=\"logic_off\">Outdated!</style></b>";

		/// <summary>
		/// The message prompting the user to restart.
		/// </summary>
		public static LocString RESTART_REQUIRED = "Oxygen Not Included must be restarted " +
			"for these options to take effect.";
		
		/// <summary>
		/// The tooltip on the BLUE field in color pickers.
		/// </summary>
		public static LocString TOOLTIP_BLUE = "Blue";

		/// <summary>
		/// The tooltip on the CANCEL button.
		/// </summary>
		public static LocString TOOLTIP_CANCEL = "Discard changes.";
		
		/// <summary>
		/// The tooltip on the GREEN field in color pickers.
		/// </summary>
		public static LocString TOOLTIP_GREEN = "Green";
		
		/// <summary>
		/// The tooltip on the Mod Homepage button.
		/// </summary>
		public static LocString TOOLTIP_HOMEPAGE = "Visit the mod's website.";

		/// <summary>
		/// The tooltip on the Hue slider in color pickers.
		/// </summary>
		public static LocString TOOLTIP_HUE = "Hue";

		/// <summary>
		/// The tooltip on the MANUAL CONFIG button.
		/// </summary>
		public static LocString TOOLTIP_MANUAL = "Opens the folder containing the full mod configuration.";

		/// <summary>
		/// The tooltip for cycling to the next item.
		/// </summary>
		public static LocString TOOLTIP_NEXT = "Next";

		/// <summary>
		/// The tooltip on the OK button.
		/// </summary>
		public static LocString TOOLTIP_OK = "Save these options. Some mods may require " +
			"a restart for the options to take effect.";

		/// <summary>
		/// The tooltip for cycling to the previous item.
		/// </summary>
		public static LocString TOOLTIP_PREVIOUS = "Previous";
		
		/// <summary>
		/// The tooltip on the RED field in color pickers.
		/// </summary>
		public static LocString TOOLTIP_RED = "Red";

		/// <summary>
		/// The tooltip on the RESET TO DEFAULT button.
		/// </summary>
		public static LocString TOOLTIP_RESET = "Resets the mod configuration to default values.";

		/// <summary>
		/// The tooltip on the Saturation slider in color pickers.
		/// </summary>
		public static LocString TOOLTIP_SATURATION = "Saturation";

		/// <summary>
		/// The tooltip for each category visibility toggle.
		/// </summary>
		public static LocString TOOLTIP_TOGGLE = "Show or hide this options category";

		/// <summary>
		/// The tooltip on the Value slider in color pickers.
		/// </summary>
		public static LocString TOOLTIP_VALUE = "Value";

		/// <summary>
		/// The tooltip for the mod version.
		/// </summary>
		public static LocString TOOLTIP_VERSION = "The currently installed version of this mod.\n\nCompare this version with the mod's Release Notes to see if it is outdated.";
	}
}
