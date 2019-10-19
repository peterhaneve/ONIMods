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
	/// </summary>
	static class BulkChangeStrings {
		// Tool tip and name in the tool list
		public static LocString TOOL_DESCRIPTION = "Enable or disable Auto-Disinfect, Auto-Repair, or buildings {Hotkey}";
		public static LocString TOOL_TITLE = "Change Settings";

		// Internal strings
		public static string PLACE_ICON_NAME = "BULKSETTINGSCHANGE.TOOL.BULKCHANGETOOL.PLACER";
		public static string TOOL_ICON_NAME = "BULKSETTINGSCHANGE.TOOL.BULKCHANGETOOL.ICON";

		// Action strings
		public static string ACTION_KEY = "BULKSETTINGSCHANGE.ACTION.CHANGESETTINGS";
		public static LocString ACTION_TITLE = "Settings Change Tool";

		// Tool strings
		public static LocString TOOL_DISABLE_BUILDINGS = "Building Disabled";
		public static LocString TOOL_DISABLE_COMPOST = "Compost Disabled";
		public static LocString TOOL_DISABLE_DISINFECT = "Disinfect Disabled";
		public static LocString TOOL_DISABLE_EMPTY = "Empty Storage Cancelled";
		public static LocString TOOL_DISABLE_REPAIR = "Auto-Repair Disabled";
		public static LocString TOOL_ENABLE_BUILDINGS = "Building Enabled";
		public static LocString TOOL_ENABLE_COMPOST = "Compost Enabled";
		public static LocString TOOL_ENABLE_DISINFECT = "Disinfect Enabled";
		public static LocString TOOL_ENABLE_EMPTY = "Storage Emptied";
		public static LocString TOOL_ENABLE_REPAIR = "Auto-Repair Enabled";
	}

	/// <summary>
	/// Store the tool mode information used in the Bulk Settings Change tool.
	/// </summary>
	static class BulkChangeTools {
		// Available tools
		public static readonly BulkToolMode DisableBuildings = new BulkToolMode(
			"DISABLE_BUILDING", STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME,
			BulkChangeStrings.TOOL_DISABLE_BUILDINGS);
		public static readonly BulkToolMode DisableCompost = new BulkToolMode(
			"DISABLE_COMPOST", STRINGS.UI.USERMENUACTIONS.COMPOST.NAME_OFF,
			BulkChangeStrings.TOOL_DISABLE_COMPOST);
		public static readonly BulkToolMode DisableDisinfect = new BulkToolMode(
			"DISABLE_DISINFECT", STRINGS.BUILDINGS.AUTODISINFECTABLE.DISABLE_AUTODISINFECT.
			NAME, BulkChangeStrings.TOOL_DISABLE_DISINFECT);
		public static readonly BulkToolMode DisableEmpty = new BulkToolMode(
			"DISABLE_EMPTY", STRINGS.UI.USERMENUACTIONS.EMPTYSTORAGE.NAME_OFF,
			BulkChangeStrings.TOOL_DISABLE_EMPTY);
		public static readonly BulkToolMode DisableRepair = new BulkToolMode(
			"DISABLE_REPAIR", STRINGS.BUILDINGS.REPAIRABLE.DISABLE_AUTOREPAIR.NAME,
			BulkChangeStrings.TOOL_DISABLE_REPAIR);
		public static readonly BulkToolMode EnableBuildings = new BulkToolMode(
			"ENABLE_BUILDING", STRINGS.UI.USERMENUACTIONS.ENABLEBUILDING.NAME_OFF,
			BulkChangeStrings.TOOL_ENABLE_BUILDINGS);
		public static readonly BulkToolMode EnableCompost = new BulkToolMode(
			"ENABLE_COMPOST", STRINGS.UI.USERMENUACTIONS.COMPOST.NAME,
			BulkChangeStrings.TOOL_ENABLE_COMPOST);
		public static readonly BulkToolMode EnableDisinfect = new BulkToolMode(
			"ENABLE_DISINFECT", STRINGS.BUILDINGS.AUTODISINFECTABLE.ENABLE_AUTODISINFECT.NAME,
			BulkChangeStrings.TOOL_ENABLE_DISINFECT);
		public static readonly BulkToolMode EnableEmpty = new BulkToolMode(
			"ENABLE_EMPTY", STRINGS.UI.USERMENUACTIONS.EMPTYSTORAGE.NAME,
			BulkChangeStrings.TOOL_ENABLE_EMPTY);
		public static readonly BulkToolMode EnableRepair = new BulkToolMode(
			"ENABLE_REPAIR", STRINGS.BUILDINGS.REPAIRABLE.ENABLE_AUTOREPAIR.NAME,
			BulkChangeStrings.TOOL_ENABLE_REPAIR);
	}
}
