/*
 * Copyright  Peter Han
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

using PeterHan.PLib.AVC;
using Steamworks;
using System;

using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Wraps the completed Steam mod (as normally returned by SteamUGCService) along with
	/// the extended status to retrieve the correct item.
	/// </summary>
	internal sealed class ModInfo : IDisposable {
		/// <summary>
		/// Set when the mod has been messaged to clients as "installed".
		/// Future changes are considered an update.
		/// </summary>
		internal bool clientSeen;

		/// <summary>
		/// Handle to the mod's main zip file on Steam.
		/// </summary>
		internal UGCHandle_t fileId;

		/// <summary>
		/// The install path for the mod archive file.
		/// </summary>
		internal string installPath;

		/// <summary>
		/// True if the preview image was updated but not yet communicated to the event
		/// listeners.
		/// </summary>
		internal bool previewDirty;

		/// <summary>
		/// Handle to the mod's preview image on Steam.
		/// </summary>
		internal UGCHandle_t previewId;

		/// <summary>
		/// The current mod state.
		/// </summary>
		internal SteamModState state;

		/// <summary>
		/// Reference to the Klei version of this mod.
		/// </summary>
		internal SteamUGCService.Mod ugcMod;

		/// <summary>
		/// The timestamp of the last update for the local mod files.
		/// </summary>
		internal System.DateTime updateTimestamp;

		internal ModInfo(PublishedFileId_t id) {
			clientSeen = false;
			fileId = UGCHandle_t.Invalid;
			installPath = null;
			previewDirty = false;
			previewId = UGCHandle_t.Invalid;
			state = SteamModState.NeedsDetails;
			// Fill in with a proxy mod by default
			ugcMod = new SteamUGCService.Mod(id);
			Summon();
		}

		public void Dispose() {
			var texture = ugcMod.previewImage;
			state = SteamModState.PendingDestroy;
			if (texture != null) {
				UnityEngine.Object.Destroy(texture);
				ugcMod.previewImage = null;
			}
		}

		/// <summary>
		/// Sets the mod pending destroy.
		/// </summary>
		internal void Donate() {
			state = SteamModState.PendingDestroy;
		}

		/// <summary>
		/// Adds a preview image to the mod.
		/// </summary>
		/// <param name="texture">The preview image to set, or null if no preview is available.</param>
		internal void Enhance(Texture2D texture) {
			ugcMod.previewImage = texture;
			previewDirty = true;
		}

		public override bool Equals(object obj) {
			return obj is ModInfo other && other.ugcMod.fileId == ugcMod.fileId;
		}

		public override int GetHashCode() {
			return ugcMod.fileId.m_PublishedFileId.GetHashCode();
		}

		/// <summary>
		/// Requests a Steam details update.
		/// </summary>
		internal void Pursue() {
			state = SteamModState.NeedsDetails;
			Summon();
		}

		/// <summary>
		/// Fills in the required mod info from Steam.
		/// </summary>
		/// <param name="details">The mod details.</param>
		internal void Populate(SteamUGCDetails_t details) {
			state = SteamModState.DetailsDirty;
			ugcMod = new SteamUGCService.Mod(details, ugcMod?.previewImage);
			fileId = details.m_hFile;
			previewId = details.m_hPreviewFile;
		}

		/// <summary>
		/// Updates the mod local update time and archive path.
		/// </summary>
		internal void Summon() {
			var id = ugcMod.fileId;
			if (SteamUGC.GetItemInstallInfo(id, out _, out installPath, 260U,
					out uint ts))
				updateTimestamp = SteamVersionChecker.UnixEpochToDateTime(ts);
			else {
				installPath = null;
				updateTimestamp = System.DateTime.MinValue;
			}
			// But reuse the local timestamp if we updated it
			var ourData = ModUpdateInfo.FindModInConfig(id.m_PublishedFileId);
			if (ourData != null)
				updateTimestamp = new System.DateTime(ourData.LastUpdated, DateTimeKind.
					Utc);
		}

		public override string ToString() {
			return "Mod #" + ugcMod.fileId.m_PublishedFileId;
		}
	}

	/// <summary>
	/// Stores the state of a Steam mod to avoid parallel lists and excess fields.
	/// </summary>
	internal enum SteamModState {
		NeedsDetails, DetailsDirty, Subscribed, Updated, PendingDestroy
	}
}
