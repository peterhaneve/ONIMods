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

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Strings used in Mod Updater.
	/// </summary>
	public static class ModUpdateDateStrings {
		public const int MAX_LINES = 16;
		public static readonly LocString PLURAL = "s";

		// Confirmation of update
		public static readonly LocString CONFIRM_UPDATE = "Continuing will reinstall the latest version of:\n" +
			"{0}from the Workshop.\n<color=#FFCC00>A best effort will be made to preserve mod options.</color>\n\n" +
			"Check again after updating and enable the mod if necessary.";
		public static readonly LocString CONFIRM_LINE = "<b>{0}</b>\n";
		public static readonly LocString CONFIRM_MORE = "<i>(and {0:D} more...)</i>\n";
		public static readonly LocString CONFIRM_CANCEL = "CANCEL";
		public static readonly LocString CONFIRM_OK = "FORCE UPDATE";

		// Local update date
		public static readonly LocString LOCAL_UPDATE = "<b>Local Updated:</b> {0:f}";

		// Mod update status
		public static readonly LocString MOD_UPDATED = "This mod appears to be up to date.";
		public static readonly LocString MOD_UPDATED_BYUS = "This mod was locally updated by Mod Updater.";
		public static readonly LocString MOD_OUTDATED = "This mod appears to be out of date!";
		public static readonly LocString MOD_UPDATE_ALL = "Update {0:D} mod{1} which appear to be out of date";

		// Steam update date
		public static readonly LocString STEAM_UPDATE = "<b>Steam Updated:</b> {0:f}";

		// Steam update date not known
		public static readonly LocString STEAM_UPDATE_UNKNOWN = "Steam Updated: Unknown";

		// Update results
		public static readonly LocString UPDATE_ERROR = "<b>{0}</b>: <color=#FF0000>{1}</color>\n";
		public static readonly LocString UPDATE_OK = "<b>{0}</b>: <color=#00CC00>Updated</color>\n";
		public static readonly LocString UPDATE_OK_CONFIG = "<b>{0}</b>: <color=#00CC00>{1:D} custom mod option file{2} backed up</color>\n";
		public static readonly LocString UPDATE_OK_NOCONFIG = "<b>{0}</b>: <color=#FFCC00>Unable to back up configuration files</color>\n";
		public static readonly LocString UPDATE_INPROGRESS = "An update for another mod is already in progress.";
		public static readonly LocString UPDATE_NOFILE = "No file found to download";
		public static readonly LocString UPDATE_NODETAILS = "Uninstalled or not found on workshop";
		public static readonly LocString UPDATE_CANTSTART = "Unable to start download";
		public static readonly LocString UPDATE_OFFLINE = "Cannot download in Offline Mode";
		public static readonly LocString UPDATE_HEADER = "<size=16>Update results:</size>\n\n";
		public static readonly LocString UPDATE_FOOTER_OK = "\n{0} will be updated on the next restart.";
		public static readonly LocString UPDATE_FOOTER_CONFIG = "\n<b><color=#FFCC00>Back up any valuable mod configurations now manually!</color></b>";
		public static readonly LocString UPDATE_FOOTER_ERROR = "\nCheck your connection, and that the mods directory has sufficient disk space and permissions.";
		public static readonly LocString UPDATE_ONE = "This mod";
		public static readonly LocString UPDATE_MULTIPLE = "These mods";
	}
}
