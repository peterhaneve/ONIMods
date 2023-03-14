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

using KMod;
using PeterHan.PLib.Core;
using System;
using System.Reflection;

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Checks Steam to see if mods are out of date.
	/// </summary>
	public sealed class SteamVersionChecker : IModVersionChecker {
		/// <summary>
		/// A reference to the PublishedFileId_t type, or null if running on the EGS/WeGame
		/// version.
		/// </summary>
		private static readonly Type PUBLISHED_FILE_ID = PPatchTools.GetTypeSafe(
			"Steamworks.PublishedFileId_t");

		/// <summary>
		/// A reference to the SteamUGC type, or null if running on the EGS/WeGame version.
		/// </summary>
		private static readonly Type STEAM_UGC = PPatchTools.GetTypeSafe(
			"Steamworks.SteamUGC");

		/// <summary>
		/// A reference to the game's version of SteamUGCService, or null if running on the
		/// EGS/WeGame version.
		/// </summary>
		private static readonly Type STEAM_UGC_SERVICE = PPatchTools.GetTypeSafe(
			nameof(SteamUGCService), "Assembly-CSharp");

		/// <summary>
		/// Detours requires knowing the types at compile time, which might not be available,
		/// and these methods are only called once at startup.
		/// </summary>
		private static readonly MethodInfo FIND_MOD = STEAM_UGC_SERVICE?.GetMethodSafe(
			nameof(SteamUGCService.FindMod), false, PUBLISHED_FILE_ID);

		private static readonly MethodInfo GET_ITEM_INSTALL_INFO = STEAM_UGC?.GetMethodSafe(
			"GetItemInstallInfo", true, PUBLISHED_FILE_ID, typeof(ulong).
			MakeByRefType(), typeof(string).MakeByRefType(), typeof(uint), typeof(uint).
			MakeByRefType());

		private static readonly ConstructorInfo NEW_PUBLISHED_FILE_ID = PUBLISHED_FILE_ID?.
			GetConstructor(PPatchTools.BASE_FLAGS | BindingFlags.Instance, null,
			new Type[] { typeof(ulong) }, null);

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
		/// Checks to see if Steam is initialized yet.
		/// </summary>
		/// <param name="id">The mod's ID.</param>
		/// <param name="boxedID">The ID converted to a boxed PublishedFileId_t and stored in
		/// a single parameter array (for passing into the reflected function).</param>
		/// <param name="mod">The mod to check.</param>
		/// <returns>The results of the version check, or null if Steam has not populated it yet.</returns>
		private static ModVersionCheckResults CheckSteamInit(ulong id, object[] boxedID,
				Mod mod) {
			var inst = SteamUGCService.Instance;
			ModVersionCheckResults results = null;
			// Mod takes time to be populated in the list
			if (inst != null && FIND_MOD.Invoke(inst, boxedID) is
				SteamUGCService.Mod steamMod) {
				ulong ticks = steamMod.lastUpdateTime;
				var steamUpdate = (ticks == 0U) ? System.DateTime.MinValue :
					UnixEpochToDateTime(ticks);
				bool updated = steamUpdate <= GetLocalLastModified(id).AddMinutes(
					UPDATE_JITTER);
				results = new ModVersionCheckResults(mod.staticID,
					updated, updated ? null : steamUpdate.ToString("f"));
			}
			return results;
		}

		/// <summary>
		/// Gets the last modified date of a mod's local files. The time is returned in UTC.
		/// </summary>
		/// <param name="id">The mod ID to check.</param>
		/// <returns>The date and time of its last modification.</returns>
		private static System.DateTime GetLocalLastModified(ulong id) {
			var result = System.DateTime.UtcNow;
			// Create a published file object, leave it boxed
			if (GET_ITEM_INSTALL_INFO != null) {
				// 260 = MAX_PATH
				var methodArgs = new object[] {
					NEW_PUBLISHED_FILE_ID.Invoke(new object[] { id }), 0UL, "", 260U, 0U
				};
				if (GET_ITEM_INSTALL_INFO.Invoke(null, methodArgs) is bool success &&
						success && methodArgs.Length == 5 && methodArgs[4] is uint timestamp &&
						timestamp > 0U)
					result = UnixEpochToDateTime(timestamp);
				else
					PUtil.LogDebug("Unable to determine last modified date for: " + id);
			}
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
			return FIND_MOD != null && NEW_PUBLISHED_FILE_ID != null && DoCheckVersion(mod);
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
				// Jump into a coroutine and wait for it to be initialized
				Global.Instance.StartCoroutine(WaitForSteamInit(id, mod));
				check = true;
			} else
				PUtil.LogWarning("SteamVersionChecker cannot check version for non-Steam mod {0}".
					F(mod.staticID));
			return check;
		}

		/// <summary>
		/// To avoid blowing the stack, waits for Steam to initialize in a coroutine.
		/// </summary>
		/// <param name="id">The Steam file ID of the mod.</param>
		/// <param name="mod">The mod to check for updates.</param>
		private System.Collections.IEnumerator WaitForSteamInit(ulong id, Mod mod) {
			var boxedID = new[] { NEW_PUBLISHED_FILE_ID.Invoke(new object[] { id }) };
			ModVersionCheckResults results;
			int timeout = 0;
			do {
				yield return null;
				results = CheckSteamInit(id, boxedID, mod);
				// 2 seconds at 60 FPS
			} while (results == null && ++timeout < 120);
			if (results == null)
				PUtil.LogWarning("Unable to check version for mod {0} (SteamUGCService timeout)".
					F(mod.label.title));
			OnVersionCheckCompleted?.Invoke(results);
		}
	}
}
