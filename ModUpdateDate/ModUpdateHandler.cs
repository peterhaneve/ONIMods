﻿/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.UI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using UISTRINGS = PeterHan.ModUpdateDate.ModUpdateDateStrings.UI.MODUPDATER;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Adds an update button to the mod menu.
	/// </summary>
	public sealed class ModUpdateHandler : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static ModUpdateHandler Instance { get; }

		/// <summary>
		/// The margin inside the update button around the icon.
		/// </summary>
		private static readonly RectOffset BUTTON_MARGIN = new RectOffset(6, 6, 6, 6);

		/// <summary>
		/// The background color if outdated.
		/// </summary>
		private static readonly ColorStyleSetting COLOR_OUTDATED;

		/// <summary>
		/// The background color if up to date.
		/// </summary>
		private static readonly ColorStyleSetting COLOR_UPDATED;

		/// <summary>
		/// The checked or warning icon size on the version.
		/// </summary>
		private static readonly Vector2 ICON_SIZE = new Vector2(16.0f, 16.0f);

		static ModUpdateHandler() {
			COLOR_OUTDATED = ScriptableObject.CreateInstance<ColorStyleSetting>();
			COLOR_OUTDATED.inactiveColor = new Color(0.753f, 0.0f, 0.0f);
			COLOR_OUTDATED.activeColor = new Color(1.0f, 0.0f, 0.0f);
			COLOR_OUTDATED.hoverColor = new Color(1.0f, 0.0f, 0.0f);
			// Should be unreachable
			COLOR_OUTDATED.disabledColor = COLOR_OUTDATED.disabledActiveColor =
				COLOR_OUTDATED.disabledhoverColor = new Color(0.706f, 0.549f, 0.549f);
			COLOR_UPDATED = ScriptableObject.CreateInstance<ColorStyleSetting>();
			COLOR_UPDATED.inactiveColor = new Color(0.0f, 0.753f, 0.0f);
			COLOR_UPDATED.activeColor = new Color(0.0f, 1.0f, 0.0f);
			COLOR_UPDATED.hoverColor = new Color(0.0f, 1.0f, 0.0f);
			COLOR_UPDATED.disabledColor = COLOR_UPDATED.disabledActiveColor =
				COLOR_UPDATED.disabledhoverColor = new Color(0.549f, 0.706f, 0.549f);
			Instance = new ModUpdateHandler();
		}

		private static CultureInfo cultureInfo;

		/// <summary>
		/// Adds the mod update date to the mods menu.
		/// </summary>
		/// <param name="outdated">The mods which are out of date.</param>
		/// <param name="modEntry">The entry in the mod menu.</param>
		internal static void AddModUpdateButton(ICollection<ModToUpdate> outdated,
				object modEntry) {
			int index = -1;
			var type = modEntry.GetType();
			var indexVal = type.GetFieldSafe("mod_index", false)?.GetValue(modEntry);
			if (indexVal is int intVal)
				index = intVal;
			var rowInstance = (type.GetFieldSafe("rect_transform", false)?.GetValue(
				modEntry) as RectTransform)?.gameObject;
			var mods = Global.Instance.modManager?.mods;
			if (rowInstance != null && mods != null && index >= 0 && index < mods.Count) {
				var mod = mods[index];
				var tooltip = new StringBuilder(128);
				var localDate = mod.GetLocalLastModified();
				var updated = ModStatus.Disabled;
				// A nice colorful button with a warning or checkmark icon
				var updButton = new PButton("UpdateMod") {
					Margin = BUTTON_MARGIN, SpriteSize = ICON_SIZE,
					MaintainSpriteAspect = true
				};
				// formatting DateTime
				if (cultureInfo == null) {
					var langCode = Localization.GetLocale()?.Code;
					if (string.IsNullOrEmpty(langCode))
						langCode = Localization.GetCurrentLanguageCode();
					if (string.IsNullOrEmpty(langCode))
						langCode = Localization.DEFAULT_LANGUAGE_CODE;
					cultureInfo = new CultureInfo(langCode);
				}
				if (mod.label.distribution_platform == Label.DistributionPlatform.Steam) {
					var modUpdate = new ModToUpdate(mod);
					updated = AddSteamUpdate(tooltip, modUpdate, localDate, updButton);
					if (updated == ModStatus.Outdated)
						outdated.Add(modUpdate);
				} else
					tooltip.AppendFormat(cultureInfo, UISTRINGS.LOCAL_UPDATE, localDate.ToLocalTime());
				// Icon, color, and tooltip
				updButton.Sprite = (updated == ModStatus.UpToDate || updated == ModStatus.
					Disabled) ? PUITuning.Images.Checked : PUITuning.Images.GetSpriteByName(
					"iconWarning");
				updButton.Color = (updated == ModStatus.Outdated) ? COLOR_OUTDATED :
					COLOR_UPDATED;
				updButton.ToolTip = tooltip.ToString();
				// Just before subscription button, and after the Options button
				PButton.SetButtonEnabled(updButton.AddTo(rowInstance, 4), updated != ModStatus.
					Disabled);
			}
		}

		/// <summary>
		/// Adds a tooltip to a Steam mod showing its update status.
		/// </summary>
		/// <param name="tooltip">The tooltip under construction.</param>
		/// <param name="modUpdate">The mod update executor which can update this mod.</param>
		/// <param name="localDate">The local last update date.</param>
		/// <param name="updButton">The button to be used for updating this mod.</param>
		/// <returns>The status of the Steam mod.</returns>
		private static ModStatus AddSteamUpdate(StringBuilder tooltip, ModToUpdate modUpdate,
				System.DateTime localDate, PButton updButton) {
			var steamDate = modUpdate.LastSteamUpdate;
			var updated = GetModStatus(modUpdate, ref localDate);
			// Generate tooltip for mod's current date and last Steam update
			switch (updated) {
			case ModStatus.UpToDate:
				tooltip.Append(UISTRINGS.MOD_UPDATED);
				break;
			case ModStatus.UpToDateLocal:
				tooltip.Append(UISTRINGS.MOD_UPDATED_BYUS);
				break;
			case ModStatus.Outdated:
				tooltip.Append(UISTRINGS.MOD_OUTDATED);
				break;
			default:
				break;
			}
			// AppendLine appends platform specific separator
			tooltip.Append("\n");
			tooltip.AppendFormat(cultureInfo, UISTRINGS.LOCAL_UPDATE, localDate.ToLocalTime());
			tooltip.Append("\n");
			if (updated == ModStatus.Disabled)
				tooltip.AppendFormat(UISTRINGS.STEAM_UPDATE_UNKNOWN);
			else {
				tooltip.AppendFormat(cultureInfo, UISTRINGS.STEAM_UPDATE, steamDate.ToLocalTime());
				updButton.OnClick = new ModUpdateTask(modUpdate).TryUpdateMods;
			}
			return updated;
		}

		/// <summary>
		/// Adds a button to update all outdated mods at once.
		/// </summary>
		/// <param name="parent">The parent for the button.</param>
		/// <param name="outdated">The mods that are out of date.</param>
		internal static void AddUpdateAll(GameObject parent, ICollection<ModToUpdate> outdated)
		{
			int n = outdated.Count;
			const string UPDATE_ALL = "UpdateAll";
			// Only if button is not already there
			if (parent != null && parent.transform.Find(UPDATE_ALL) == null)
				new PButton(UPDATE_ALL) {
					Margin = BUTTON_MARGIN, SpriteSize = ICON_SIZE, Text = n.ToString(),
					MaintainSpriteAspect = true, Color = COLOR_OUTDATED, IconSpacing = 4,
					ToolTip = string.Format(n == 1 ? UISTRINGS.MOD_UPDATE_1 :
					UISTRINGS.MOD_UPDATE_ALL, n),
					OnClick = new ModUpdateTask(outdated).TryUpdateMods,
					Sprite = PUITuning.Images.GetSpriteByName("iconWarning"),
				}.AddTo(parent, 0);
		}

		/// <summary>
		/// Determines how many mods may be out of date.
		/// </summary>
		/// <returns>The number of outdated mods.</returns>
		internal static int CountOutdatedMods() {
			var mods = Global.Instance?.modManager?.mods;
			int outdated = 0;
			if (mods != null && mods.Count > 0)
				foreach (var mod in mods)
					// Steam mods only, count outdated
					if (mod.label.distribution_platform == Label.DistributionPlatform.Steam) {
						System.DateTime localDate = mod.GetLocalLastModified();
						if (GetModStatus(new ModToUpdate(mod), ref localDate) == ModStatus.
								Outdated)
							outdated++;
					}
			return outdated;
		}

		/// <summary>
		/// Gets the temporary download path for a mod.
		/// </summary>
		/// <param name="id">The Steam mod ID.</param>
		/// <param name="temp">true for the temporary location, or false for the real one.</param>
		/// <returns>The path where its temporary download will be stored.</returns>
		internal static string GetDownloadPath(ulong id, bool temp = false) {
			return Path.Combine(Manager.GetDirectory(), id + (temp ? ".tmp" : ".zip"));
		}

		/// <summary>
		/// Finds the update status of a mod.
		/// </summary>
		/// <param name="modUpdate">The mod to query.</param>
		/// <param name="localDate">The date it was last updated locally, from Mod.
		/// GetLocalLastModified()</param>
		/// <returns>The status of that mod.</returns>
		private static ModStatus GetModStatus(ModToUpdate modUpdate, ref System.DateTime
				localDate) {
			ModStatus updated;
			if (modUpdate.LastSteamUpdate > System.DateTime.MinValue) {
				var ours = ModUpdateInfo.FindModInConfig(modUpdate.SteamID);
				var ourDate = System.DateTime.MinValue;
				var steamDate = modUpdate.LastSteamUpdate;
				// Do we have a better estimate?
				if (ours != null)
					ourDate = new System.DateTime(ours.LastUpdated, DateTimeKind.Utc);
				// Allow some time for download delays etc
				if (localDate.AddMinutes(SteamVersionChecker.UPDATE_JITTER) >= steamDate)
					updated = ModStatus.UpToDate;
				else if (ourDate.AddMinutes(SteamVersionChecker.UPDATE_JITTER) >= steamDate) {
					localDate = ourDate;
					updated = ModStatus.UpToDateLocal;
				} else
					updated = ModStatus.Outdated;
			} else
				updated = ModStatus.Disabled;
			return updated;
		}

		/// <summary>
		/// True if an update is already in progress.
		/// </summary>
		public bool IsUpdating {
			get {
				return task != null;
			}
		}

		/// <summary>
		/// The active mod to be updated.
		/// </summary>
		private volatile ModToUpdate active;

		/// <summary>
		/// The CallResult for handling the Steam API call to download mod data.
		/// </summary>
		private CallResult<RemoteStorageDownloadUGCResult_t> caller;

		/// <summary>
		/// The mod information that is being updated.
		/// </summary>
		private ModUpdateTask task;

		private ModUpdateHandler() {
			caller = null;
			active = null;
			task = null;
		}

		/// <summary>
		/// Attempts to back up the mod configs into the downloaded zip file.
		/// </summary>
		/// <param name="copied">The number of configuration files saved.</param>
		/// <returns>true if backup was OK, or false if it failed.</returns>
		private bool BackupConfigs(out int copied) {
			// Attempt config backup
			var backup = new ConfigBackupUtility(active.Mod, active.DownloadPath,
				GetDownloadPath(active.SteamID, true));
			bool success;
			copied = 0;
			try {
				success = backup.CreateMergedPackage(out copied);
				if (success)
					backup.CommitUpdate();
				else
					backup.RollbackUpdate();
			} catch {
				backup.RollbackUpdate();
				throw;
			}
			return success;
		}

		public void Dispose() {
			if (caller != null) {
				caller.Dispose();
				caller = null;
			}
		}

		/// <summary>
		/// Called when a download completes.
		/// </summary>
		/// <param name="result">The downloaded mod information.</param>
		/// <param name="failed">Whether an I/O error occurred during download.</param>
		private void OnDownloadComplete(RemoteStorageDownloadUGCResult_t result, bool failed) {
			var steamStatus = result.m_eResult;
			if (active != null) {
				ulong id = active.SteamID;
				ModUpdateResult status;
				var mod = active.Mod;
				if (failed || (steamStatus != EResult.k_EResultAdministratorOK &&
						steamStatus != EResult.k_EResultOK)) {
					// Clean the trash
					ExtensionMethods.RemoveOldDownload(active.DownloadPath);
					status = new ModUpdateResult(ModDownloadStatus.SteamError, mod, steamStatus);
				} else {
					// Try to copy the configs
					if (BackupConfigs(out int copied))
						status = new ModUpdateResult(ModDownloadStatus.OK, mod, steamStatus) {
							ConfigsRestored = copied
						};
					else
						status = new ModUpdateResult(ModDownloadStatus.ConfigError, mod,
							steamStatus);
					// Mod has been updated
					mod.status = Mod.Status.ReinstallPending;
					mod.reinstall_path = active.DownloadPath;
					PGameUtils.SaveMods();
					// Update the config
					var when = active.LastSteamUpdate;
					if (when > System.DateTime.MinValue)
						ModUpdateDetails.UpdateConfigFor(id, when);
				}
				task.Results.Add(status);
				active = null;
				UpdateNext();
			}
		}

		/// <summary>
		/// Starts a mod update.
		/// </summary>
		/// <param name="task">The mod(s) to be updated.</param>
		internal void StartModUpdate(ModUpdateTask task) {
			if (!IsUpdating && task.Mods.Count > 0) {
				this.task = task;
				active = null;
				UpdateNext();
			}
		}

		/// <summary>
		/// Attempts to start a mod force update.
		/// </summary>
		/// <param name="mod">The mod to update.</param>
		/// <param name="details">The mod details to force update.</param>
		/// <returns>true if the update began, or false if it failed.</returns>
		private bool StartSteamUpdate(ModToUpdate task) {
			var mod = task.Mod;
			var globalDate = task.LastSteamUpdate;
			ModUpdateResult status = null;
			UGCHandle_t content;
			if (!ModUpdateDetails.TryGetDetails(task.SteamID, out SteamUGCDetails_t details))
				status = new ModUpdateResult(ModDownloadStatus.ModUninstalled, mod,
					EResult.k_EResultFileNotFound);
			else if ((content = details.m_hFile).Equals(UGCHandle_t.Invalid))
				status = new ModUpdateResult(ModDownloadStatus.NoSteamFile, mod, EResult.
					k_EResultUnexpectedError);
			else {
				string downloadPath = task.DownloadPath;
				ExtensionMethods.RemoveOldDownload(downloadPath);
				// The game should already raise an error if insufficient space / access
				// errors on the saves and mods folder
				var res = SteamRemoteStorage.UGCDownloadToLocation(content, downloadPath, 0U);
				if (res.Equals(SteamAPICall_t.Invalid))
					status = new ModUpdateResult(ModDownloadStatus.SteamError, mod,
						EResult.k_EResultServiceUnavailable);
				else {
					active = task;
					if (caller != null)
						caller.Dispose();
					caller = new CallResult<RemoteStorageDownloadUGCResult_t>(
						OnDownloadComplete);
					caller.Set(res);
					PUtil.LogDebug("Start download of file {0:D} to {1}".F(content.m_UGCHandle,
						downloadPath));
				}
			}
			if (status != null)
				this.task.Results.Add(status);
			return status == null;
		}

		/// <summary>
		/// Updates the next mod, or invokes the completion dialog if all done.
		/// </summary>
		private void UpdateNext() {
			int n;
			while ((n = task.Mods.Count) > 0 && !StartSteamUpdate(task.Mods.Dequeue()));
			if (n <= 0 && active == null) {
				// All done
				task.OnComplete();
				task = null;
			}
		}

		/// <summary>
		/// Potential statuses in the mods menu.
		/// </summary>
		private enum ModStatus {
			Disabled, UpToDate, UpToDateLocal, Outdated
		}
	}
}
