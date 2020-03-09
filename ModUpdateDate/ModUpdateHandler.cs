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

using Harmony;
using KMod;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using Steamworks;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Adds an update button to the mod menu.
	/// </summary>
	public sealed class ModUpdateHandler {
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

		/// <summary>
		/// The number of minutes allowed before a mod is considered out of date.
		/// </summary>
		internal static double UPDATE_JITTER = 10.0;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static ModUpdateHandler Instance { get; }

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

		/// <summary>
		/// Adds the mod update date to the mods menu.
		/// </summary>
		/// <param name="modEntry">The entry in the mod menu.</param>
		internal static void AddModUpdateButton(Traverse modEntry) {
			int index = modEntry.GetField<int>("mod_index");
			var rowInstance = modEntry.GetField<RectTransform>("rect_transform")?.gameObject;
			var mods = Global.Instance.modManager?.mods;
			if (rowInstance != null && mods != null && index >= 0 && index < mods.Count) {
				var mod = mods[index];
				var tooltip = new StringBuilder(128);
				var localDate = mod.GetLocalLastModified();
				var updated = ModStatus.Disabled;
				// A nice colorful button with a warning or checkmark icon
				var addButton = new PButton("Version") {
					Margin = BUTTON_MARGIN, SpriteSize = ICON_SIZE,
					MaintainSpriteAspect = true
				};
				// Steam mods only get the steam date
				if (mod.label.distribution_platform == Label.DistributionPlatform.Steam) {
					updated = AddSteamTooltip(tooltip, mod, localDate, out System.DateTime
						globalDate);
					if (updated != ModStatus.Disabled)
						addButton.OnClick = (_) => TryUpdateMod(mod, globalDate);
				} else
					tooltip.AppendFormat(ModUpdateDateStrings.LOCAL_UPDATE, localDate);
				// Icon, color, and tooltip
				addButton.Sprite = (updated == ModStatus.UpToDate || updated == ModStatus.
					Disabled) ? PUITuning.Images.Checked : PUITuning.Images.
					GetSpriteByName("iconWarning");
				addButton.Color = (updated == ModStatus.Outdated) ? COLOR_OUTDATED :
					COLOR_UPDATED;
				addButton.ToolTip = tooltip.ToString();
				// Just before subscription button, and after the Options button
				PButton.SetButtonEnabled(addButton.AddTo(rowInstance, 3), updated != ModStatus.
					Disabled);
			}
		}

		/// <summary>
		/// Adds a tooltip to a Steam mod showing its update status.
		/// </summary>
		/// <param name="tooltip">The tooltip under construction.</param>
		/// <param name="mod">The mod to query.</param>
		/// <param name="localDate">The local last update date.</param>
		/// <returns>The status of the Steam mod.</returns>
		private static ModStatus AddSteamTooltip(StringBuilder tooltip, Mod mod,
				System.DateTime localDate, out System.DateTime globalDate) {
			var id = mod.GetSteamModID();
			var updated = ModStatus.Disabled;
			if (id.GetGlobalLastModified(out globalDate)) {
				var ours = ModUpdateInfo.FindModInConfig(id.m_PublishedFileId);
				var ourDate = System.DateTime.MinValue;
				// Do we have a better estimate?
				if (ours != null)
					ourDate = new System.DateTime(ours.LastUpdated, DateTimeKind.Utc);
				// Allow some time for download delays etc
				if (localDate.AddMinutes(UPDATE_JITTER) >= globalDate) {
					tooltip.Append(ModUpdateDateStrings.MOD_UPDATED);
					updated = ModStatus.UpToDate;
				} else if (ourDate.AddMinutes(UPDATE_JITTER) >= globalDate) {
					tooltip.Append(ModUpdateDateStrings.MOD_UPDATED_BYUS);
					localDate = ourDate;
					updated = ModStatus.UpToDateLocal;
				} else {
					tooltip.Append(ModUpdateDateStrings.MOD_OUTDATED);
					updated = ModStatus.Outdated;
				}
				tooltip.Append("\n");
				tooltip.AppendFormat(ModUpdateDateStrings.LOCAL_UPDATE, localDate);
				tooltip.Append("\n");
				tooltip.AppendFormat(ModUpdateDateStrings.STEAM_UPDATE, globalDate);
			} else {
				tooltip.AppendFormat(ModUpdateDateStrings.LOCAL_UPDATE, localDate);
				tooltip.Append("\n");
				tooltip.AppendFormat(ModUpdateDateStrings.STEAM_UPDATE_UNKNOWN);
			}
			return updated;
		}

#if false
		/// <summary>
		/// Gets the mod's current Steam state.
		/// </summary>
		/// <param name="mod">The mod to check.</param>
		/// <returns>The status that Steam thinks it is in.</returns>
		internal static EItemState GetItemState(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException("mod");
			var state = EItemState.k_EItemStateNone;
			var label = mod.label;
			if (label.distribution_platform == Label.DistributionPlatform.Steam) {
				string id = label.id;
				// This should never fail
				if (!ulong.TryParse(id, out ulong idLong))
					throw new InvalidOperationException("Steam mod with invalid ID " + id);
				state = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(idLong));
			}
			return state;
		}
#endif

		/// <summary>
		/// Does nothing.
		/// </summary>
		private static void DoNothing() { }

		/// <summary>
		/// Shows a confirmation dialog to force update the specified mod.
		/// </summary>
		/// <param name="mod">The mod to update.</param>
		/// <param name="globalDate">The update date as reported by Steam.</param>
		private static void TryUpdateMod(Mod mod, System.DateTime globalDate) {
			if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
				UpdateMod(mod, globalDate);
			else
				PUIElements.ShowConfirmDialog(null, ModUpdateDateStrings.CONFIG_WARNING,
					() => UpdateMod(mod, globalDate), DoNothing, ModUpdateDateStrings.
					UPDATE_CONTINUE, ModUpdateDateStrings.UPDATE_CANCEL);
		}

		/// <summary>
		/// Force updates the specified mod.
		/// </summary>
		/// <param name="mod">The mod to update.</param>
		/// <param name="globalDate">The update date as reported by Steam.</param>
		private static void UpdateMod(Mod mod, System.DateTime globalDate) {
			if (mod == null)
				throw new ArgumentNullException("mod");
			var label = mod.label;
			if (label.distribution_platform != Label.DistributionPlatform.Steam)
				PUtil.LogWarning("Mod to update is invalid!");
			else if (ModUpdateDetails.Details.TryGetValue(mod.GetSteamModID().m_PublishedFileId,
					out SteamUGCDetails_t details))
				Instance.StartModUpdate(mod, details, globalDate);
			else
				// Uninstalled?
				PUtil.LogWarning("Unable to find details for mod: " + label.title);
		}

		/// <summary>
		/// True if an update is already in progress.
		/// </summary>
		public bool IsUpdating {
			get {
				return mod != null;
			}
		}

		/// <summary>
		/// The CallResult for handling the Steam API call to download mod data.
		/// </summary>
		private readonly CallResult<RemoteStorageDownloadUGCResult_t> caller;

		/// <summary>
		/// The path that is being downloaded.
		/// </summary>
		private string downloadPath;

		/// <summary>
		/// The mod information that is being updated.
		/// </summary>
		private Mod mod;
		
		/// <summary>
		/// The adjusted last update date of the mod.
		/// </summary>
		private System.DateTime updateTime;

		private ModUpdateHandler() {
			caller = new CallResult<RemoteStorageDownloadUGCResult_t>(OnDownloadComplete);
			downloadPath = "";
			mod = null;
			updateTime = System.DateTime.MinValue;
		}

		/// <summary>
		/// Called when a download completes.
		/// </summary>
		/// <param name="result">The downloaded mod information.</param>
		/// <param name="failed">Whether an I/O error occurred during download.</param>
		private void OnDownloadComplete(RemoteStorageDownloadUGCResult_t result, bool failed) {
			var status = result.m_eResult;
			if (mod != null) {
				var label = mod.label;
				if (failed || (status != EResult.k_EResultAdministratorOK && status != EResult.
						k_EResultOK))
					// Failed to update
					PUIElements.ShowMessageDialog(null, string.Format(ModUpdateDateStrings.
						UPDATE_ERROR, label.title, status));
				else if (!string.IsNullOrEmpty(downloadPath)) {
					// Mod has been updated
					mod.status = Mod.Status.ReinstallPending;
					mod.reinstall_path = downloadPath;
					Global.Instance.modManager?.Save();
					// Update the config
					if (updateTime > System.DateTime.MinValue)
						ModUpdateDetails.UpdateConfigFor(mod.GetSteamModID().m_PublishedFileId,
							updateTime);
					// Tell the user
					PUIElements.ShowConfirmDialog(null, string.Format(ModUpdateDateStrings.
						UPDATE_OK, label.title), App.instance.Restart, DoNothing,
						STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.OK,
						STRINGS.UI.FRONTEND.MOD_DIALOGS.RESTART.CANCEL);
				}
				mod = null;
				downloadPath = "";
				updateTime = System.DateTime.MinValue;
			}
		}

		/// <summary>
		/// Attempts to start a mod force update.
		/// </summary>
		/// <param name="mod">The mod to update.</param>
		/// <param name="details">The mod details to force update.</param>
		/// <param name="globalDate">The update date as reported by Steam.</param>
		private void StartModUpdate(Mod mod, SteamUGCDetails_t details,
				System.DateTime globalDate) {
			if (mod == null)
				throw new ArgumentNullException("mod");
			var content = details.m_hFile;
			string error = null;
			if (IsUpdating)
				error = ModUpdateDateStrings.UPDATE_INPROGRESS;
			else if (content.Equals(UGCHandle_t.Invalid))
				error = ModUpdateDateStrings.UPDATE_NOFILE;
			else {
				ulong id = details.m_nPublishedFileId.m_PublishedFileId;
				downloadPath = ExtensionMethods.GetDownloadPath(id);
				ExtensionMethods.RemoveOldDownload(id);
				// The game should already raise an error if insufficient space / access
				// errors on the saves and mods folder
				var res = SteamRemoteStorage.UGCDownloadToLocation(content, downloadPath, 0U);
				if (res.Equals(SteamAPICall_t.Invalid))
					error = ModUpdateDateStrings.UPDATE_CANTSTART;
				else {
					caller.Set(res);
					PUtil.LogDebug("Start download of file {0:D} to {1}".F(content.m_UGCHandle,
						downloadPath));
					updateTime = globalDate;
					this.mod = mod;
				}
			}
			if (error != null)
				PUIElements.ShowMessageDialog(null, error);
		}

		/// <summary>
		/// Potential statuses in the mods menu.
		/// </summary>
		private enum ModStatus {
			Disabled, UpToDate, UpToDateLocal, Outdated
		}
	}
}
