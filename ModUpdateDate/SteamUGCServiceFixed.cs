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

using PeterHan.PLib.Core;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

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
			if (allMods.TryGetValue(item, out ModInfo mod))
				ret = mod.ugcMod;
			return ret;
		}

		/// <summary>
		/// Looks for a mod in the mod list.
		/// </summary>
		/// <param name="item">The mod ID to look up.</param>
		/// <returns>The mod found, or null if no mod with that ID was found.</returns>
		internal ModInfo GetInfo(PublishedFileId_t item) {
			if (!allMods.TryGetValue(item, out ModInfo mod))
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
					if (mod.clientSeen)
						toUpdate.Add(id);
					else
						toAdd.Add(id);
					mod.clientSeen = false;
					mod.state = SteamModState.Subscribed;
					break;
				default:
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
			if (download.Idle)
				// Avoid spamming by only notifying when all outstanding mods are downloaded
				foreach (var pair in allMods) {
					var mod = pair.Value;
					var id = pair.Key;
					if (mod.state == SteamModState.Updated) {
						mod.Summon();
						if (mod.clientSeen)
							toUpdate.Add(id);
						else
							toAdd.Add(id);
						mod.clientSeen = false;
						mod.state = SteamModState.Subscribed;
					}
				}
			if (toAdd.Count > 0 || toUpdate.Count > 0 || toRemove.Count > 0 || loadPreviews.
					Count > 0) {
				firstEvent = true;
				// Event needs to be triggered
				foreach (var client in clients)
					client.UpdateMods(toAdd, toUpdate, toRemove, loadPreviews);
			}
			// Actually destroy all pending destroy mods
			foreach (var id in toRemove)
				if (allMods.TryRemove(id, out ModInfo destroyed))
					destroyed.Dispose();
			loadPreviews.Recycle();
			toAdd.Recycle();
			toUpdate.Recycle();
			toRemove.Recycle();
		}

		private void OnUGCDetailsComplete(SteamUGCQueryCompleted_t callback, bool ioError) {
			var result = callback.m_eResult;
			var handle = callback.m_handle;
			PublishedFileId_t id;
			if (!ioError && result == EResult.k_EResultOK)
				for (uint i = 0U; i < callback.m_unNumResultsReturned; i++) {
					if (SteamUGC.GetQueryUGCResult(handle, i, out SteamUGCDetails_t details) &&
							allMods.TryGetValue(id = details.m_nPublishedFileId,
							out ModInfo mod)) {
#if false
						PUtil.LogDebug("Updated mod {0:D} ({1})".F(id.m_PublishedFileId,
							details.m_rgchTitle));
#endif
						mod.Populate(details);
						// Queue up a download and a preview
						download.Queue(id);
						preview.Queue(id);
					}
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
			if (allMods.TryGetValue(id, out ModInfo mod))
				mod.Donate();
		}

		private void OnUGCUpdated(RemoteStoragePublishedFileUpdated_t callback) {
			var id = callback.m_nPublishedFileId;
			// Set needs update to true
			allMods.GetOrAdd(id, new ModInfo(id)).Pursue();
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
			internal uint updateTimestamp;

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
				if (!SteamUGC.GetItemInstallInfo(ugcMod.fileId, out _, out installPath, 260U,
						out updateTimestamp)) {
					installPath = null;
					updateTimestamp = 0U;
				}
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
}
