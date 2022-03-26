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

using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Downloads mod content files from the legacy workshop.
	/// </summary>
	internal sealed class SteamDownloadQueue : SteamQueue {
		// Matches mods that are pending download or actively downloading
		private const EItemState IS_DOWNLOADING = EItemState.k_EItemStateDownloading |
			EItemState.k_EItemStateDownloadPending;

		protected override bool CanStart(PublishedFileId_t id) {
			var state = (EItemState)SteamUGC.GetItemState(id);
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			// Start if: not installed, Steam says it needs update, or local files do not match
			// the Steam date
			bool update = (state & EItemState.k_EItemStateNeedsUpdate) != EItemState.
				k_EItemStateNone;
			if (!update && mod?.installPath != null) {
				update = mod.updateTimestamp.AddMinutes(SteamVersionChecker.UPDATE_JITTER) <
					SteamVersionChecker.UnixEpochToDateTime(mod.ugcMod.lastUpdateTime);
				if (update)
					PUtil.LogDebug("Mod {0:D} is outdated, forcing correct download".F(id.
						m_PublishedFileId));
			}
			return (update || (state & EItemState.k_EItemStateInstalled) == EItemState.
				k_EItemStateNone) && (state & IS_DOWNLOADING) == EItemState.k_EItemStateNone;
		}

		protected override SteamAPICall_t ExecuteDownload(PublishedFileId_t id) {
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			var ret = SteamAPICall_t.Invalid;
			if (mod != null) {
				string tempPath = ModUpdateHandler.GetDownloadPath(id.m_PublishedFileId);
				// Purge any stale temporary zip
				ExtensionMethods.RemoveOldDownload(tempPath);
#if DEBUG
				PUtil.LogDebug("Downloading mod {0:D} to {1}".F(id.m_PublishedFileId,
					tempPath));
#endif
				if (!string.IsNullOrEmpty(mod.installPath))
					ret = SteamRemoteStorage.UGCDownloadToLocation(mod.fileId, tempPath, 0U);
				else
					ret = SteamRemoteStorage.UGCDownload(mod.fileId, 0U);
			}
			return ret;
		}

		protected override void OnComplete(PublishedFileId_t id, int size) {
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			string path = ModUpdateHandler.GetDownloadPath(id.m_PublishedFileId);
			bool ok = false;
#if DEBUG
			PUtil.LogDebug("Downloaded mod: {0:D}".F(id.m_PublishedFileId));
#endif
			// Copy zip to the install_path and destroy it
			try {
				File.Copy(path, mod.installPath, true);
				ok = true;
			} catch (IOException e) {
				PUtil.LogWarning("Unable to copy file {0} to {1}:".F(path, mod.installPath));
				PUtil.LogExcWarn(e);
			} catch (UnauthorizedAccessException) {
				PUtil.LogWarning("Access to {0} is denied!".F(mod.installPath));
			}
			ExtensionMethods.RemoveOldDownload(path);
			if (mod != null)
				mod.state = SteamUGCServiceFixed.SteamModState.Updated;
			if (ok && id.GetGlobalLastModified(out System.DateTime when))
				ModUpdateDetails.UpdateConfigFor(id.m_PublishedFileId, when);
		}

		protected override void OnError(PublishedFileId_t id, EResult result) {
			PUtil.LogWarning("Unable to download mod: {0:D}, code: {1}".F(id.
				m_PublishedFileId, result));
			// Purge the dead zip
			ExtensionMethods.RemoveOldDownload(ModUpdateHandler.GetDownloadPath(id.
				m_PublishedFileId));
		}
	}

	/// <summary>
	/// Downloads mod preview images.
	/// </summary>
	internal sealed class SteamPreviewQueue : SteamQueue {
		/// <summary>
		/// Potential file names for retrieving mod preview image names directly from their
		/// folder -- same as the ones declared in SteamUGCService
		/// </summary>
		private static readonly string[] PREVIEW_FILE_NAMES = new string[]
		{
			"preview.png",
			"Preview.png",
			"PREVIEW.png",
			".png",
			".jpg"
		};

		protected override bool CanStart(PublishedFileId_t id) {
			return true;
		}

		protected override SteamAPICall_t ExecuteDownload(PublishedFileId_t id) {
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			return (mod == null) ? SteamAPICall_t.Invalid : SteamRemoteStorage.UGCDownload(mod.
				previewId, 0U);
		}

		protected override void OnComplete(PublishedFileId_t id, int size) {
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			if (size > 0 && mod != null) {
				byte[] rawData = new byte[size];
				int read = SteamRemoteStorage.UGCRead(mod.previewId, rawData, size, 0U,
					EUGCReadAction.k_EUGCRead_ContinueReadingUntilFinished);
				if (read != size)
					// Retrieve from the mod directly
					TryLoadPreview(id, null);
				else
					TryLoadPreview(id, rawData);
			}
		}

		protected override void OnError(PublishedFileId_t id, EResult result) {
			TryLoadPreview(id, null);
		}

		/// <summary>
		/// Tries to load the preview image directly from the mod zip if unavailable.
		/// </summary>
		/// <param name="id">The mod ID to load.</param>
		/// <param name="rawData">The preview image data downloaded from Steam, or null if the
		/// Steam download failed.</param>
		private void TryLoadPreview(PublishedFileId_t id, byte[] rawData) {
			var mod = SteamUGCServiceFixed.Instance.GetInfo(id);
			if (rawData == null)
				rawData = SteamUGCService.GetBytesFromZip(id, PREVIEW_FILE_NAMES, out _);
			if (rawData != null) {
				if (mod != null) {
					var texture = new Texture2D(1, 1);
					texture.LoadImage(rawData);
					mod.Enhance(texture);
				}
			} else {
#if DEBUG
				PUtil.LogWarning("Failed to download preview: {0:D}".F(id.m_PublishedFileId));
#endif
			}
		}
	}

	/// <summary>
	/// Queues up mod IDs for processing preview images or user files.
	/// </summary>
	internal abstract class SteamQueue : IDisposable {
		/// <summary>
		/// Reports if the queue has been emptied.
		/// </summary>
		internal bool Idle {
			get {
				return pending == PublishedFileId_t.Invalid && toDo.Count < 1;
			}
		}
		
		/// <summary>
		/// Triggered when a download completes.
		/// </summary>
		private CallResult<RemoteStorageDownloadUGCResult_t> onComplete;

		/// <summary>
		/// The callback only stores the file ID, not the original mod ID, so store the active
		/// mod here.
		/// </summary>
		private PublishedFileId_t pending;

		/// <summary>
		/// The mod IDs which need to be processed.
		/// </summary>
		private readonly ConcurrentQueue<PublishedFileId_t> toDo;

		protected SteamQueue() {
			onComplete = null;
			pending = PublishedFileId_t.Invalid;
			toDo = new ConcurrentQueue<PublishedFileId_t>();
		}

		/// <summary>
		/// Checks to see if downloading can be performed on this mod.
		/// </summary>
		/// <param name="id">The mod ID to check.</param>
		/// <returns>true if that mod can be downloaded now, or false if it should not be started.</returns>
		protected abstract bool CanStart(PublishedFileId_t id);

		/// <summary>
		/// If true, the download of this mod will be temporarily skipped, and will only be
		/// rechecked
		/// </summary>
		/// <param name="id">The mod ID to check.</param>
		/// <returns>true if that mod can be downloaded now, or false if it should not be started.</returns>
		protected virtual bool Defer(PublishedFileId_t id) {
			return false;
		}

		/// <summary>
		/// Checks the mod download queue and starts a new one if necessary.
		/// </summary>
		internal void Check() {
			if (pending == PublishedFileId_t.Invalid)
				while (toDo.TryDequeue(out PublishedFileId_t id))
					if (CanStart(id) && Download(id)) break;
		}

		public void Dispose() {
			onComplete?.Dispose();
			onComplete = null;
		}

		/// <summary>
		/// Starts downloading a mod to the default Steam workshop content location.
		/// </summary>
		/// <param name="id">The mod ID to start downloading.</param>
		/// <returns>true if the download has started and callback set, or false otherwise.</returns>
		protected bool Download(PublishedFileId_t id) {
			bool ok = false;
			var apiCall = ExecuteDownload(id);
			if (apiCall != SteamAPICall_t.Invalid) {
				pending = id;
				onComplete?.Dispose();
				onComplete = new CallResult<RemoteStorageDownloadUGCResult_t>(
					OnDownloadComplete);
				onComplete.Set(apiCall);
				ok = true;
			} else {
#if DEBUG
				PUtil.LogWarning("Could not start download: {0:D}".F(id.m_PublishedFileId));
#endif
				pending = PublishedFileId_t.Invalid;
			}
			return ok;
		}

		/// <summary>
		/// Executes the Steam remote storage call to download this mod.
		/// </summary>
		/// <param name="id">The mod ID in the queue.</param>
		/// <returns>The ID of the content to be downloaded.</returns>
		protected abstract SteamAPICall_t ExecuteDownload(PublishedFileId_t id);

		/// <summary>
		/// Called when a mod successfully downloads.
		/// </summary>
		/// <param name="id">The mod ID that was just downloaded.</param>
		/// <param name="size">The size of the file downloaded in bytes.</param>
		protected abstract void OnComplete(PublishedFileId_t id, int size);

		private void OnDownloadComplete(RemoteStorageDownloadUGCResult_t callback,
				bool ioError) {
			var result = callback.m_eResult;
			if (ioError || result != EResult.k_EResultOK)
				OnError(pending, ioError ? EResult.k_EResultIOFailure : result);
			else
				OnComplete(pending, callback.m_nSizeInBytes);
			onComplete?.Dispose();
			onComplete = null;
			pending = PublishedFileId_t.Invalid;
			Check();
		}

		/// <summary>
		/// Called when an error occurs during download.
		/// </summary>
		/// <param name="id">The mod ID that failed to download.</param>
		/// <param name="result">The result of the download.</param>
		protected abstract void OnError(PublishedFileId_t id, EResult result);

		/// <summary>
		/// Queues up a mod ID for processing.
		/// </summary>
		/// <param name="mod">The mod ID to queue.</param>
		internal void Queue(PublishedFileId_t mod) {
			int count = 0;
			lock (toDo) {
				// Avoid a race between two concurrent Queue calls
				count = toDo.Count;
				toDo.Enqueue(mod);
			}
			if (count < 1)
				Check();
		}
	}
}
