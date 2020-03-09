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
	/// Strings used in Steam Mod Updater.
	/// </summary>
	public static class ModUpdateDateStrings {
		// Config erase warning
		public static readonly LocString CONFIG_WARNING = "Continuing will reinstall the latest version of\n" +
			"<b>{0}</b> from the Workshop.\n<color=#FFCC00>A best effort will be made to preserve mod options.</color>\n\n" +
			"Check again after updating and enable the mod if necessary.";

		// Local update date
		public static readonly LocString LOCAL_UPDATE = "Local Updated: {0:g}";

		// Mod update status
		public static readonly LocString MOD_UPDATED = "This mod appears to be up to date.";
		public static readonly LocString MOD_UPDATED_BYUS = "This mod was locally updated by Steam Mod Updater.";
		public static readonly LocString MOD_OUTDATED = "This mod appears to be out of date!";

		// Steam update date
		public static readonly LocString STEAM_UPDATE = "Steam Updated: {0:g}";

		// Steam update date not known
		public static readonly LocString STEAM_UPDATE_UNKNOWN = "Steam Updated: Unknown";

		// Update messages
		public static readonly LocString UPDATE_CANCEL = "CANCEL";
		public static readonly LocString UPDATE_CONTINUE = "FORCE UPDATE MOD";
		public static readonly LocString UPDATE_ERROR = "Unable to update mod <b>{0}</b>: {1}";
		public static readonly LocString UPDATE_INPROGRESS = "An update for another mod is already in progress.";
		public static readonly LocString UPDATE_NOFILE = "Mod main file reported by Steam is invalid!";
		public static readonly LocString UPDATE_CANTSTART = "Unable to start mod download.\nCheck your connection, and that the mods directory has sufficient disk space and permissions.";
		public static readonly LocString UPDATE_OK = "Downloaded mod <b>{0}</b>.\n<color=#00CC00>{1:D} custom mod option file{2} will be restored on reinstall.</color>\nIt will be updated on the next restart.";
		public static readonly LocString UPDATE_OK_NOBACKUP = "Downloaded mod <b>{0}</b>.\n<color=#FF0000>Steam Mod Updater was unable to back up mod options.\nIf they are valuable, back them up now manually!</color>\nIt will be updated on the next restart.";
	}
}
