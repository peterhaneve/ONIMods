/*
 * Copyright 2024 Peter Han
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Manages the mod update details from Steamworks.
	/// </summary>
	internal static class ModUpdateDetails {
		private const float SCRUB_COOLDOWN = 3.0f;

		/// <summary>
		/// The details of each mod by ID as fetched by the game.
		/// </summary>
		private static readonly ConcurrentDictionary<ulong, SteamUGCDetails_t> DETAILS =
			new ConcurrentDictionary<ulong, SteamUGCDetails_t>(2, 64);

		// Detours the private setter of Mod.available_content to allow it to be updated.
		// It is a property so cannot be accessed with ref ___ in a patch
		private static readonly IDetouredField<Mod, Content> MOD_AVAILABLE_CONTENT =
			PDetours.DetourField<Mod, Content>(nameof(Mod.available_content));

		/// <summary>
		/// Prevents scrubs from running more often than once every few seconds.
		/// </summary>
		private static float lastScrub;

		/// <summary>
		/// True if a config scrub is required after mod details update.
		/// </summary>
		private static bool scrubRequired;

		/// <summary>
		/// Checks one mod to see if Steam updated it.
		/// </summary>
		/// <param name="manager">The mod manager.</param>
		/// <param name="id">The ID of the Steam mod to check.</param>
		/// <returns>true if the mod was updated by Steam, or false otherwise.</returns>
		private static bool CheckMod(Manager manager, ulong id) {
			bool remove = false;
			Mod mod;
			if (DETAILS.ContainsKey(id) && (mod = manager.FindSteamMod(id)) != null) {
				var target = ModUpdateInfo.GetLocalInfo(mod);
				target.RefreshLastModified();
				// Compare date just like the button does
				if (mod.GetSteamModID().GetGlobalLastModified(out var steamTime) &&
						target.LocalLastModified.AddMinutes(SteamVersionChecker.
						UPDATE_JITTER) >= steamTime) {
					PUtil.LogDebug("Mod {0} has been updated by Steam".F(mod.label.title));
					// Steam fixed up its act
					remove = true;
				}
			} else {
				PUtil.LogDebug("Steam mod with ID {0:D} was uninstalled".F(id));
				remove = true;
			}
			return remove;
		}

		/// <summary>
		/// Scrubs the config of mods that Steam got around to actually updating.
		/// </summary>
		private static void DoScrubConfig() {
			var settings = ModUpdateInfo.Settings;
			var existing = settings?.ModUpdates;
			var manager = Global.Instance.modManager;
			if (existing != null && manager != null) {
				// Anything that is up to date gets purged
				var remove = HashSetPool<ModUpdateData, ModUpdateHandler>.Allocate();
				foreach (var info in existing) {
					ulong id = info.ID;
					if (CheckMod(manager, id))
						remove.Add(info);
					if (info.Status == ModUpdateStatus.PendingUpdate) {
						// Purge the temporary zip
						ExtensionMethods.RemoveOldDownload(ModUpdateHandler.GetDownloadPath(
							id));
						info.Status = ModUpdateStatus.UpdatedByThisMod;
					}
				}
				// Remove the obsolete entries
				foreach (var info in remove)
					existing.Remove(info);
				remove.Recycle();
				POptions.WriteSettings(settings);
			}
		}

		/// <summary>
		/// Called when the Steam service updates the installed mods.
		/// </summary>
		/// <param name="updated">The mods that Steam just updated details.</param>
		internal static void OnInstalledUpdate(ICollection<SteamUGCDetails_t> updated) {
			foreach (var modDetails in updated) {
				var id = modDetails.m_nPublishedFileId;
				// Update details of each mod
				if (!id.Equals(PublishedFileId_t.Invalid) && modDetails.m_eResult == EResult.
						k_EResultOK)
					DETAILS.AddOrUpdate(id.m_PublishedFileId, modDetails, (key, old) =>
						modDetails);
			}
			scrubRequired = true;
		}

		/// <summary>
		/// Scrubs the config of mods that Steam got around to actually updating.
		/// </summary>
		/// <returns>true if scrubbing was performed (mods may or may not have changed), or
		/// false otherwise.</returns>
		internal static bool ScrubConfig() {
			bool scrubbed = false;
			float now = Time.unscaledTime;
			lock (DETAILS) {
				if (scrubRequired && now - lastScrub > SCRUB_COOLDOWN) {
					DoScrubConfig();
					scrubbed = true;
					scrubRequired = false;
					lastScrub = now;
				}
			}
			return scrubbed;
		}

		/// <summary>
		/// If an updated mod has changed content from the broken Steam version, Klei puts up a
		/// required restart flag on it and paves it on restart with the Steam version. Fix
		/// that for mods that we have updated.
		/// </summary>
		/// <param name="changed">Whether the content was supposedly changed.</param>
		/// <param name="target">The target mod.</param>
		/// <returns>Whether the content was really changed.</returns>
		internal static bool SuppressContentChanged(bool changed, Mod target) {
			if (changed) {
				changed = !WeOwnUpdatesTo(target);
#if DEBUG
				if (!changed)
					PUtil.LogDebug("Suppressing Content Changed notification for mod " +
						target.label.title);
#endif
			}
			return changed;
		}

		/// <summary>
		/// Attempts to obtain the Steam details of a mod.
		/// </summary>
		/// <param name="id">The mod ID to look up.</param>
		/// <param name="details">The location where the details will be stored.</param>
		/// <returns>true if the details were found, or false otherwise.</returns>
		internal static bool TryGetDetails(ulong id, out SteamUGCDetails_t details) {
			return DETAILS.TryGetValue(id, out details);
		}

		/// <summary>
		/// Updates the settings for the specified mod ID.
		/// </summary>
		/// <param name="id">The Steam mod ID to update.</param>
		/// <param name="lastUpdated">The new last updated date.</param>
		internal static void UpdateConfigFor(ulong id, System.DateTime lastUpdated) {
			lock (DETAILS) {
				var settings = ModUpdateInfo.Settings;
				if (settings.ModUpdates == null)
					settings = new ModUpdateInfo();
				var info = ModUpdateInfo.FindModInConfig(id);
				// Now tracked by this mod
				if (info == null) {
					info = new ModUpdateData(id, lastUpdated);
					settings.ModUpdates.Add(info);
				} else
					info.LastUpdated = lastUpdated.Ticks;
				info.Status = ModUpdateStatus.PendingUpdate;
				POptions.WriteSettings(settings);
			}
		}

		/// <summary>
		/// If an updated mod has different available content from the broken Steam version,
		/// Klei flags it as incompatible even though it is loaded (!). Fix that for mods we
		/// have updated.
		/// </summary>
		/// <param name="current">The current mod as loaded.</param>
		/// <param name="replacement">The replacement mod coming from Steam.</param>
		internal static void UpdateContentChanged(Mod current, Mod replacement) {
			current.CopyPersistentDataTo(replacement);
			if (WeOwnUpdatesTo(current)) {
				var newContent = MOD_AVAILABLE_CONTENT.Get(current);
#if DEBUG
				var currentContent = MOD_AVAILABLE_CONTENT.Get(replacement);
				PUtil.LogDebug("Force updating content of {0}: {1} to {2}".F(current.label.
					title, currentContent, newContent));
#endif
				MOD_AVAILABLE_CONTENT.Set(replacement, newContent);
			}
			// Do the versions mismatch? Then the content must be different
			if (replacement.label.distribution_platform == Label.DistributionPlatform.Steam) {
				var activeInfo = current.packagedModInfo;
				var steamInfo = replacement.packagedModInfo;
				var localInfo = ModUpdateInfo.GetLocalInfo(replacement);
				if (activeInfo != null && steamInfo != null && activeInfo.version != steamInfo.
						version) {
					localInfo.FilesystemVersion = activeInfo.version;
					PUtil.LogWarning("Mod {0} version does not match: Active {1}, Steam {2}".
						F(current.label.title, activeInfo.version, steamInfo.version));
				} else
					localInfo.FilesystemVersion = "";
			}
		}

		/// <summary>
		/// Checks to see if we are the current updater of this mod.
		/// </summary>
		/// <param name="target">The mod to check.</param>
		/// <returns>true if it was found in the Mod Updater config and is registered as
		/// updated by this mod, or false otherwise.</returns>
		private static bool WeOwnUpdatesTo(Mod target) {
			var existing = ModUpdateInfo.Settings?.ModUpdates;
			bool found = false;
			if (existing != null && target.label.distribution_platform == Label.
					DistributionPlatform.Steam) {
				string idString = target.label.id;
				foreach (var info in existing)
					if (idString == info.ID.ToString() && (info.Status != ModUpdateStatus.
							Default)) {
						found = true;
						break;
					}
			}
			return found;
		}
	}
}
