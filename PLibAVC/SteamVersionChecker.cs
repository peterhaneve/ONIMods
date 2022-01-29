/*
 * Copyright 2022 Peter Han
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

using KMod;
using PeterHan.PLib.Core;
using Steamworks;
using System;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Checks Steam to see if mods are out of date.
	/// </summary>
	public sealed class SteamVersionChecker : IModVersionChecker {
		/// <summary>
		/// A reference to the game's version of SteamUGCService, or null if running on the
		/// EGS version.
		/// </summary>
		private static readonly Type STEAM_UGC_SERVICE = PPatchTools.GetTypeSafe(
			nameof(SteamUGCService), "Assembly-CSharp");

		/// <summary>
		/// The number of minutes allowed before a mod is considered out of date.
		/// </summary>
		public const double UPDATE_JITTER = 10.0;

		/// <summary>
		/// The epoch time for Steam time stamps.
		/// </summary>
		private static readonly System.DateTime UNIX_EPOCH = new System.DateTime(1970, 1, 1,
			0, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// Gets the last modified date of a mod's local files. The time is returned in UTC.
		/// </summary>
		/// <param name="id">The mod ID to check.</param>
		/// <returns>The date and time of its last modification.</returns>
		private static System.DateTime GetLocalLastModified(ulong id) {
			var result = System.DateTime.UtcNow;
			// 260 = MAX_PATH
			if (SteamUGC.GetItemInstallInfo(new PublishedFileId_t(id), out _,
					out string _, 260U, out uint timestamp) && timestamp > 0U)
				result = UnixEpochToDateTime(timestamp);
			return result;
		}

		/// <summary>
		/// Converts a time from Steam (seconds since Unix epoch) to a C# DateTime.
		/// </summary>
		/// <param name="timeSeconds">The timestamp since the epoch.</param>
		/// <returns>The UTC date and time that it represents.</returns>
		public static System.DateTime UnixEpochToDateTime(ulong timeSeconds) {
			return UNIX_EPOCH.AddSeconds(timeSeconds);
		}

		public event PVersionCheck.OnVersionCheckComplete OnVersionCheckCompleted;

		public bool CheckVersion(Mod mod) {
			// Epic editions of the game do not even have SteamUGCService
			return STEAM_UGC_SERVICE != null && DoCheckVersion(mod);
		}

		/// <summary>
		/// Checks the mod on Steam and reports if it is out of date. This helper method
		/// avoids a type load error if a non-Steam version of the game is used to load this
		/// mod.
		/// </summary>
		/// <param name="mod">The mod whose version is being checked.</param>
		/// <returns>true if the version check has started, or false if it could not be
		/// started.</returns>
		private bool DoCheckVersion(Mod mod) {
			bool check = false;
			if (mod.label.distribution_platform == Label.DistributionPlatform.Steam && ulong.
					TryParse(mod.label.id, out ulong id)) {
				var steamMod = SteamUGCService.Instance?.FindMod(new PublishedFileId_t(id));
				if (steamMod != null) {
					ulong ticks = steamMod.lastUpdateTime;
					var steamUpdate = (ticks == 0U) ? System.DateTime.MinValue :
						UnixEpochToDateTime(ticks);
					bool updated = steamUpdate <= GetLocalLastModified(id).AddMinutes(
						UPDATE_JITTER);
					check = true;
					OnVersionCheckCompleted?.Invoke(new ModVersionCheckResults(mod.staticID,
						updated, updated ? null : steamUpdate.ToString("f")));
				}
			} else
				PUtil.LogWarning("SteamVersionChecker cannot check version for non-Steam mod {0}".
					F(mod.staticID));
			return check;
		}
	}
}
