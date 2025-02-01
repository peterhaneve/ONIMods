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

using PeterHan.PLib.Core;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// A version of SteamUGCService that fixes its problems and allows passive mod updating
	/// with many fewer bugs!
	/// </summary>
	public sealed class SteamUGCServiceFixed : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static SteamUGCServiceFixed Instance { get; } = new SteamUGCServiceFixed();

		/// <summary>
		/// Avoids a race condition when installing mods midgame where Steam only thinks the
		/// mod is actually installed between the Add and Update.
		/// </summary>
		/// <param name="id">The mod ID to check.</param>
		/// <returns>true if the mod can be reported to ONI, or false if it is not yet
		/// considered "Installed".</returns>
		private static bool CanReportMod(PublishedFileId_t id) {
			return (SteamUGC.GetItemState(id) & (uint)EItemState.k_EItemStateInstalled) != 0U;
		}

		/// <summary>
		/// Reports if downloading (auto update) is still in progress.
		/// </summary>
		public bool UpdateInProgress => !download.Idle;

		/// <summary>
		/// Stores the status of each Steam mod subscribed.
		/// </summary>
		private readonly ConcurrentDictionary<PublishedFileId_t, ModInfo> allMods;

		/// <summary>
		/// The clients which will be notified about mod list changes.
		/// </summary>
		private readonly IList<SteamUGCService.IClient> clients;

		/// <summary>
		/// Queues up mod downloads.
		/// </summary>
		private readonly SteamQueue download;

		/// <summary>
		/// Avoid duplicate events for clients added before the first Update.
		/// </summary>
		private volatile bool firstEvent;

		/// <summary>
		/// Triggered when a mod is subscribed.
		/// </summary>
		private readonly Callback<RemoteStoragePublishedFileSubscribed_t> onItemSubscribed;

		/// <summary>
		/// Triggered when a mod is unsubscribed.
		/// </summary>
		private readonly Callback<RemoteStoragePublishedFileUnsubscribed_t> onItemUnsubscribed;

		/// <summary>
		/// Triggered when a mod is updated.
		/// </summary>
		private readonly Callback<RemoteStoragePublishedFileUpdated_t> onItemUpdated;

		/// <summary>
		/// Triggered when a query for mod details completes.
		/// </summary>
		private CallResult<SteamUGCQueryCompleted_t> onQueryComplete;

		/// <summary>
		/// Queues up mod preview images.
		/// </summary>
		private readonly SteamQueue preview;

		/// <summary>
		/// Manages updating the main menu button when the idle state changes.
		/// </summary>
		private volatile bool wasIdle;

		private SteamUGCServiceFixed() {
			allMods = new ConcurrentDictionary<PublishedFileId_t, ModInfo>(2, 64);
			clients = new List<SteamUGCService.IClient>(8);
			download = new SteamDownloadQueue();
			firstEvent = false;
			onItemSubscribed = Callback<RemoteStoragePublishedFileSubscribed_t>.Create(
				OnUGCSubscribed);
			onItemUnsubscribed = Callback<RemoteStoragePublishedFileUnsubscribed_t>.Create(
				OnUGCUnsubscribed);
			onItemUpdated = Callback<RemoteStoragePublishedFileUpdated_t>.Create(OnUGCUpdated);
			onQueryComplete = null;
			preview = new SteamPreviewQueue();
			wasIdle = false;
		}

		/// <summary>
		/// Adds a client subscription for updates.
		/// </summary>
		/// <param name="client">The mod client which will receive mod status updates.</param>
		public void AddClient(SteamUGCService.IClient client) {
			if (client != null) {
				clients.Add(client);
				if (firstEvent) {
					// Update with all 
					var noMods = ListPool<PublishedFileId_t, SteamUGCServiceFixed>.Allocate();
					var noPreview = ListPool<SteamUGCService.Mod, SteamUGCServiceFixed>.
						Allocate();
					client.UpdateMods(allMods.Keys, noMods, noMods, noPreview);
					noMods.Recycle();
					noPreview.Recycle();
				}
			}
		}

		public void Dispose() {
			foreach (var mod in allMods)
				mod.Value.Dispose();
			allMods.Clear();
			clients.Clear();
			// Clear queue
			download.Dispose();
			onItemSubscribed.Dispose();
			onItemUnsubscribed.Dispose();
			onItemUpdated.Dispose();
			onQueryComplete?.Dispose();
			preview.Dispose();
		}

		/// <summary>
		/// Looks for a mod in the mod list.
		/// </summary>
		/// <param name="item">The mod ID to look up.</param>
		/// <returns>The mod found, or null if no mod with that ID was found.</returns>
		public SteamUGCService.Mod FindMod(PublishedFileId_t item) {
			SteamUGCService.Mod ret = null;
			if (allMods.TryGetValue(item, out var mod))
				ret = mod.ugcMod;
			return ret;
		}

		/// <summary>
		/// Looks for a mod in the mod list.
		/// </summary>
		/// <param name="item">The mod ID to look up.</param>
		/// <returns>The mod found, or null if no mod with that ID was found.</returns>
		internal ModInfo GetInfo(PublishedFileId_t item) {
			if (!allMods.TryGetValue(item, out var mod))
				mod = null;
			return mod;
		}

		/// <summary>
		/// Initializes the service. Must be called after SteamUGCService is initialized
		/// (postfix).
		/// </summary>
		public void Initialize() {
			uint numItems = SteamUGC.GetNumSubscribedItems();
			PUtil.LogDebug("SteamUGCServiceFixed initializing with {0:D} items".F(numItems));
			if (numItems > 0U) {
				var allItems = new PublishedFileId_t[numItems];
				SteamUGC.GetSubscribedItems(allItems, numItems);
				allMods.Clear();
				foreach (var item in allItems)
					allMods.TryAdd(item, new ModInfo(item));
			}
		}

		/// <summary>
		/// Checks to see if a mod is subscribed.
		/// </summary>
		/// <param name="item">The mod ID to look up.</param>
		/// <returns>true if the mod is still subscribed, or false otherwise.</returns>
		public bool IsSubscribed(PublishedFileId_t item) {
			return allMods.TryGetValue(item, out _);
		}

		private void OnUGCDetailsComplete(SteamUGCQueryCompleted_t callback, bool ioError) {
			var result = callback.m_eResult;
			var handle = callback.m_handle;
			if (!ioError && result == EResult.k_EResultOK) {
				var allResults = ListPool<SteamUGCDetails_t, SteamUGCServiceFixed>.Allocate();
				for (uint i = 0U; i < callback.m_unNumResultsReturned; i++) {
					PublishedFileId_t id;
					if (SteamUGC.GetQueryUGCResult(handle, i, out var details) && allMods.
							TryGetValue(id = details.m_nPublishedFileId, out var mod)) {
#if false
						PUtil.LogDebug("Updated mod {0:D} ({1})".F(id.m_PublishedFileId,
							details.m_rgchTitle));
#endif
						mod.Populate(details);
						// Queue up the preview image
						download.Queue(id);
						preview.Queue(id);
						allResults.Add(details);
					}
				}
				ModUpdateDetails.OnInstalledUpdate(allResults);
				allResults.Recycle();
			}
			SteamUGC.ReleaseQueryUGCRequest(handle);
			onQueryComplete?.Dispose();
			onQueryComplete = null;
		}

		private void OnUGCSubscribed(RemoteStoragePublishedFileSubscribed_t callback) {
			var id = callback.m_nPublishedFileId;
			allMods.TryAdd(id, new ModInfo(id));
		}

		private void OnUGCUnsubscribed(RemoteStoragePublishedFileUnsubscribed_t callback) {
			var id = callback.m_nPublishedFileId;
			if (allMods.TryGetValue(id, out var mod))
				mod.Donate();
		}

		private void OnUGCUpdated(RemoteStoragePublishedFileUpdated_t callback) {
			var id = callback.m_nPublishedFileId;
			// Set needs update to true
			allMods.GetOrAdd(id, new ModInfo(id)).Pursue();
		}
		
		/// <summary>
		/// Replace the Update function of SteamUGCService with this version that actually
		/// waits for the return and uses the same API as Mod Updater's regular mode.
		/// </summary>
		public void Process() {
			var toQuery = ListPool<PublishedFileId_t, SteamUGCServiceFixed>.Allocate();
			var toRemove = ListPool<PublishedFileId_t, SteamUGCServiceFixed>.Allocate();
			var toAdd = HashSetPool<PublishedFileId_t, SteamUGCServiceFixed>.Allocate();
			var toUpdate = HashSetPool<PublishedFileId_t, SteamUGCServiceFixed>.Allocate();
			var loadPreviews = ListPool<SteamUGCService.Mod, SteamUGCServiceFixed>.Allocate();
			int n = clients.Count;
			bool idle = download.Idle;
			// Mass request the details of all mods at once
			foreach (var pair in allMods) {
				var mod = pair.Value;
				var id = pair.Key;
				switch (mod.state) {
				case SteamModState.NeedsDetails:
					toQuery.Add(id);
					break;
				case SteamModState.PendingDestroy:
					toRemove.Add(id);
					break;
				case SteamModState.DetailsDirty:
					// Details were downloaded, send out a quick update of all the mod names
					// in mass at the beginning even if some downloads are pending
					if (CanReportMod(id)) {
						if (mod.clientSeen)
							toUpdate.Add(id);
						else
							toAdd.Add(id);
						mod.state = SteamModState.Subscribed;
					}
					break;
				case SteamModState.Updated:
					// Avoid spamming by only notifying when queue is empty
					if (idle && CanReportMod(id)) {
						mod.Summon();
						if (mod.clientSeen)
							toUpdate.Add(id);
						else
							toAdd.Add(id);
						mod.state = SteamModState.Subscribed;
					}
					break;
				}
				if (mod.previewDirty) {
					loadPreviews.Add(mod.ugcMod);
					mod.previewDirty = false;
				}
			}
			if (toQuery.Count > 0 && onQueryComplete == null)
				QueryUGCDetails(toQuery.ToArray());
			toQuery.Recycle();
			if (toAdd.Count > 0 || toUpdate.Count > 0 || toRemove.Count > 0 || loadPreviews.
					Count > 0) {
				string path;
				firstEvent = true;
				// Event needs to be triggered
				for (int i = 0; i < n; i++)
					clients[i].UpdateMods(toAdd, toUpdate, toRemove, loadPreviews);
				if (n > 0)
					foreach (var added in toAdd)
						// Mods that were successfully added will be updated in the future
						// Do not mark mods which will be classified as "failed to read
						// details" as updated to mitigate a TOCTTOU in Klei code
						if (allMods.TryGetValue(added, out var info) && !string.IsNullOrEmpty(
								path = info.installPath) && System.IO.File.Exists(path))
							info.clientSeen = true;
			}
			// Actually destroy all pending destroy mods
			foreach (var id in toRemove)
				if (allMods.TryRemove(id, out var destroyed))
					destroyed.Dispose();
			loadPreviews.Recycle();
			toAdd.Recycle();
			toUpdate.Recycle();
			toRemove.Recycle();
			if (wasIdle != idle || ModUpdateDetails.ScrubConfig()) {
				// Runs on foreground thread
				wasIdle = idle;
				ModUpdateDatePatches.UpdateMainMenu();
			}
		}

		/// <summary>
		/// Queries for the UGC details of a mod.
		/// </summary>
		/// <param name="mods">The list of mod IDs to query.</param>
		private void QueryUGCDetails(PublishedFileId_t[] mods) {
			if (mods == null)
				throw new ArgumentNullException(nameof(mods));
			var handle = SteamUGC.CreateQueryUGCDetailsRequest(mods, (uint)mods.Length);
			if (handle != UGCQueryHandle_t.Invalid) {
				SteamUGC.SetReturnLongDescription(handle, true);
				var apiCall = SteamUGC.SendQueryUGCRequest(handle);
				if (apiCall != SteamAPICall_t.Invalid) {
					onQueryComplete?.Dispose();
					onQueryComplete = new CallResult<SteamUGCQueryCompleted_t>(
						OnUGCDetailsComplete);
					onQueryComplete.Set(apiCall);
				} else
					SteamUGC.ReleaseQueryUGCRequest(handle);
			}
		}

		/// <summary>
		/// Removes a client subscription from updates.
		/// </summary>
		/// <param name="client">The mod client which will no longer receive mod status updates.</param>
		public void RemoveClient(SteamUGCService.IClient client) {
			if (client != null)
				clients.Remove(client);
		}
	}
}
