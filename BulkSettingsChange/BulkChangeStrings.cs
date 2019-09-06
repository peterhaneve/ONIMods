/*
 * Copyright 2019 Peter Han
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

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// Stores the strings used in the Bulk Settings Change tool.
	/// 
	/// TODO Translation support
	/// </summary>
	static class BulkChangeStrings {
		// Tool tip and name in the tool list
		public const string ToolDescription = "Enable or disable Auto-Disinfect, Auto-Repair, or buildings {Hotkey}";
		public const string ToolTitle = "Change Settings";

		// Internal strings, no translation needed
		public const string PlaceIconName = "BULKSETTINGSCHANGE.TOOL.BULKCHANGETOOL.PLACER";
		public const string ToolIconName = "BULKSETTINGSCHANGE.TOOL.BULKCHANGETOOL.ICON";

		// Action strings
		public const string ActionKey = "BULKCHANGESETTINGS.ACTION.CHANGESETTINGS";
		public const string ActionTitle = "Settings Change Tool";
	}

	/// <summary>
	/// Store the tool mode information used in the Bulk Settings Change tool.
	/// </summary>
	static class BulkChangeTools {
		// Available tools
		public static readonly BulkToolMode DisableBuildings = new BulkToolMode(
			"DISABLE_BUILDING", STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME,
			STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME.ToString().ToUpper(),
			"Building Disabled");
		public static readonly BulkToolMode DisableDisinfect = new BulkToolMode(
			"DISABLE_DISINFECT", STRINGS.BUILDINGS.AUTODISINFECTABLE.DISABLE_AUTODISINFECT.
			NAME, STRINGS.BUILDINGS.AUTODISINFECTABLE.DISABLE_AUTODISINFECT.NAME.ToString().
			ToUpper(), "Disinfect Disabled");
		public static readonly BulkToolMode DisableRepair = new BulkToolMode(
			"DISABLE_REPAIR", STRINGS.BUILDINGS.REPAIRABLE.DISABLE_AUTOREPAIR.NAME,
			STRINGS.BUILDINGS.REPAIRABLE.DISABLE_AUTOREPAIR.NAME.ToString().ToUpper(),
			"Auto-Repair Disabled");
		public static readonly BulkToolMode EnableBuildings = new BulkToolMode(
			"ENABLE_BUILDING", STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME_OFF,
			STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME_OFF.ToString().ToUpper(),
			"Building Enabled");
		public static readonly BulkToolMode EnableDisinfect = new BulkToolMode(
			"ENABLE_DISINFECT", STRINGS.BUILDINGS.AUTODISINFECTABLE.ENABLE_AUTODISINFECT.NAME,
			STRINGS.BUILDINGS.AUTODISINFECTABLE.ENABLE_AUTODISINFECT.NAME.ToString().ToUpper(),
			"Disinfect Enabled");
		public static readonly BulkToolMode EnableRepair = new BulkToolMode(
			"ENABLE_REPAIR", STRINGS.BUILDINGS.REPAIRABLE.ENABLE_AUTOREPAIR.NAME,
			STRINGS.BUILDINGS.REPAIRABLE.ENABLE_AUTOREPAIR.NAME.ToString().ToUpper(),
			"Auto-Repair Enabled");
	}
}
