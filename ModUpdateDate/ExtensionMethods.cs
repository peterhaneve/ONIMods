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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using Steamworks;
using System;
using System.IO;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Extension methods for Mod Updater.
	/// </summary>
	internal static class ExtensionMethods {
		/// <summary>
		/// Finds a Steam mod by its ID (published file ID).
		/// </summary>
		/// <param name="manager">The mod manager.</param>
		/// <param name="id">The steam mod ID.</param>
		/// <returns>The mod, or null if it is not found.</returns>
		internal static Mod FindSteamMod(this Manager manager, ulong id) {
			Mod result = null;
			string idString = id.ToString();
			foreach (var mod in manager.mods) {
				var label = mod.label;
				// Steam mod with the same ID
				if (label.id == idString && label.distribution_platform == Label.
						DistributionPlatform.Steam) {
					result = mod;
					break;
				}
			}
			return result;
		}

		/// <summary>
		/// Gets the last modified date of a mod on Steam.
		/// </summary>
		/// <param name="id">The Steam mod ID to check.</param>
		/// <param name="when">The location where the last updated date will be stored.</param>
		/// <returns>true if the date was determined, or false if it is invalid.</returns>
		internal static bool GetGlobalLastModified(this PublishedFileId_t id,
				out System.DateTime when) {
			bool result = false;
			var inst = SteamUGCService.Instance;
			var steamMod = (inst == null) ? null : inst.FindMod(id);
			if (steamMod != null) {
				ulong ticks = steamMod.lastUpdateTime;
				result = true;
				when = (ticks == 0U) ? System.DateTime.MinValue : SteamVersionChecker.
					UnixEpochToDateTime(ticks);
			} else
				// Mod was not found
				when = System.DateTime.UtcNow;
			return result;
		}

		/// <summary>
		/// Retrieves the Steam ID for a Steam mod. Only works on Steam mods!
		/// </summary>
		/// <param name="mod">The mod to check.</param>
		/// <returns>The Steam mod ID.</returns>
		internal static PublishedFileId_t GetSteamModID(this Mod mod) {
			string id = mod.label.id;
			// This should never fail
			if (!ulong.TryParse(id, out ulong idLong))
				throw new InvalidOperationException("Steam mod with invalid ID " + id);
			return new PublishedFileId_t(idLong);
		}

		/// <summary>
		/// Removes old downloaded copies of a mod.
		/// </summary>
		/// <param name="path">The path to the file to remove.</param>
		internal static void RemoveOldDownload(string path) {
			try {
				File.Delete(path);
			} catch (IOException) {
				PUtil.LogWarning("Unable to clean temporary download {0}".F(path));
			} catch (UnauthorizedAccessException) {
				PUtil.LogWarning("Unable to access temporary download {0}".F(path));
			}
		}
	}
}
