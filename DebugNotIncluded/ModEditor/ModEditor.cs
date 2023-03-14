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

using Ionic.Zip;
using Ionic.Zlib;
using Microsoft.Win32;
using PeterHan.PLib.Core;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.IO;

#if false
namespace PeterHan.DebugNotIncluded {
	internal sealed class ModEditor : IDisposable {
		private static readonly ConcurrentDictionary<ulong, SteamUGCDetails_t> MOD_DETAILS =
			new ConcurrentDictionary<ulong, SteamUGCDetails_t>(2, 64);

		private const string REG_PREFIX = "SOFTWARE\\Klei\\Oxygen Not Included Uploader\\MRU\\";

		private const string REMOTE_MOD_DATA = "mod_publish_data_file.zip";
		private const string REMOTE_MOD_PREVIEW = "mod_publish_preview.png";

		internal static void AddModInfo(SteamUGCDetails_t details) {
			MOD_DETAILS.AddOrUpdate(details.m_nPublishedFileId.m_PublishedFileId, details,
				(key, value) => details);
		}

		private static string PopulatePatchInfo(string path) {
			string value = null;
			foreach (var file in Directory.EnumerateFiles(path, "*.dll", SearchOption.
					AllDirectories))
				try {
					var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(file);
					value = "(" + vi.FileVersion + ") ";
					break;
				} catch (IOException) { }
			return value;
		}


		private static byte[] ZipModFolder(string modFolder) {
			byte[] data = null;
			try {
				using (var stream = new MemoryStream(4194304)) {
					using (var file = new ZipFile(System.Text.Encoding.UTF8)) {
						file.CompressionLevel = CompressionLevel.Default;
						file.CompressionMethod = CompressionMethod.Deflate;
						file.AddDirectory(modFolder);
						file.Save(stream);
					}
					data = stream.ToArray();
				}
			} catch (IOException e) {
				PUtil.LogWarning("Unable to create zipped workshop data:");
				PUtil.LogExcWarn(e);
			}
			return data;
		}

		public string DataPath { get; set; }

		public string Description { get; set; }

		public ulong ID {
			get {
				return details.m_nPublishedFileId.m_PublishedFileId;
			}
		}

		public KMod.Mod Mod { get; }

		public event System.Action OnModifyComplete;

		public event System.Action OnModifyFailed;

		public string PatchInfo { get; set; }

		public string PreviewPath { get; set; }

		public string Title { get; set; }

		public bool UpdateData { get; set; }

		public bool UpdatePreview { get; set; }

		private readonly SteamUGCDetails_t details;

		private readonly CallResult<RemoteStorageUpdatePublishedFileResult_t> updateCaller;

		private PublishedFileUpdateHandle_t updateHandle;

		private readonly CallResult<RemoteStorageFileWriteAsyncComplete_t> uploadCaller;

		internal ModEditor(KMod.Mod target) {
			if (target == null)
				throw new ArgumentNullException("target");
			if (target.label.distribution_platform != KMod.Label.DistributionPlatform.Steam)
				throw new ArgumentException("Only works on Steam mods");
			if (!ulong.TryParse(target.label.id, out ulong id))
				throw new ArgumentException("Invalid Steam mod ID!");
			if (!MOD_DETAILS.TryGetValue(id, out details)) {
				details.m_ulSteamIDOwner = ulong.MaxValue;
				details.m_nPublishedFileId = new PublishedFileId_t(id);
			}
			DataPath = Description = PreviewPath = PatchInfo = Title = null;
			Mod = target;
			updateCaller = new CallResult<RemoteStorageUpdatePublishedFileResult_t>(
				OnUpdateDone);
			uploadCaller = new CallResult<RemoteStorageFileWriteAsyncComplete_t>();
			updateHandle = PublishedFileUpdateHandle_t.Invalid;
		}

		internal bool CanBegin() {
			var user = SteamUser.GetSteamID();
			return updateHandle == PublishedFileUpdateHandle_t.Invalid && user != null &&
				details.m_ulSteamIDOwner == user.m_SteamID;
		}

		private void DeleteFromSteamStorage(string remotePath) {
			SteamRemoteStorage.FileDelete(remotePath);
		}

		public void Dispose() {
			updateCaller.Dispose();
			uploadCaller.Dispose();
			updateHandle = PublishedFileUpdateHandle_t.Invalid;
		}

		private void FinishModify() {
			PUtil.LogDebug("SUBMITTING UPDATE: " + Mod.label.title);
			var call = SteamRemoteStorage.CommitPublishedFileUpdate(updateHandle);
			if (call.Equals(SteamAPICall_t.Invalid)) {
				DeleteFromSteamStorage(REMOTE_MOD_DATA);
				DeleteFromSteamStorage(REMOTE_MOD_PREVIEW);
				OnModifyFailed?.Invoke();
			} else
				updateCaller.Set(call);
		}

		private void OnDataUploaded(RemoteStorageFileWriteAsyncComplete_t result, bool failed)
		{
			var steamStatus = result.m_eResult;
			if (!failed && steamStatus == EResult.k_EResultOK && SteamRemoteStorage.
					UpdatePublishedFileFile(updateHandle, REMOTE_MOD_DATA)) {
				PUtil.LogDebug("PUBLISH DATA: " + REMOTE_MOD_DATA);
				StartPreviewUpload();
			} else {
				PUtil.LogWarning("Unable to update mod data: " + steamStatus);
				OnModifyFailed?.Invoke();
			}
		}

		private void OnPreviewUploaded(RemoteStorageFileWriteAsyncComplete_t result,
				bool failed) {
			var steamStatus = result.m_eResult;
			if (!failed && steamStatus == EResult.k_EResultOK && SteamRemoteStorage.
					UpdatePublishedFilePreviewFile(updateHandle, REMOTE_MOD_PREVIEW)) {
				PUtil.LogDebug("PUBLISH PREVIEW: " + REMOTE_MOD_PREVIEW);
				FinishModify();
			} else {
				PUtil.LogWarning("Unable to update preview image: " + steamStatus);
				OnModifyFailed?.Invoke();
			}
		}

		private void OnUpdateDone(RemoteStorageUpdatePublishedFileResult_t result,
				bool failed) {
			var steamStatus = result.m_eResult;
			DeleteFromSteamStorage(REMOTE_MOD_DATA);
			DeleteFromSteamStorage(REMOTE_MOD_PREVIEW);
			if (!failed && !result.m_bUserNeedsToAcceptWorkshopLegalAgreement && steamStatus ==
					EResult.k_EResultOK)
				OnModifyComplete?.Invoke();
			else {
				PUtil.LogWarning("Update failed: " + steamStatus);
				OnModifyFailed?.Invoke();
			}
		}

		public void PresetFields() {
			string basePath = REG_PREFIX + ID.ToString();
			DataPath = Registry.CurrentUser.GetSubKeyValue(basePath, "DataFolder")?.
				ToString() ?? "";
			Description = details.m_rgchDescription?.Trim() ?? "";
			PreviewPath = Registry.CurrentUser.GetSubKeyValue(basePath, "PreviewImg")?.
				ToString() ?? "";
			Title = details.m_rgchTitle?.Trim() ?? "";
			string patchInfo = "(1.0.0.0) Initial release";
			if (!DataPath.IsNullOrWhiteSpace())
				patchInfo = PopulatePatchInfo(DataPath) ?? patchInfo;
			PatchInfo = patchInfo;
		}

		public void StartModify() {
			if (!updateHandle.Equals(PublishedFileUpdateHandle_t.Invalid))
				throw new InvalidOperationException("Upload already started");
			// Legacy Workshop item
			updateHandle = SteamRemoteStorage.CreatePublishedFileUpdateRequest(details.
				m_nPublishedFileId);
			if (updateHandle.Equals(PublishedFileUpdateHandle_t.Invalid))
				OnModifyFailed?.Invoke();
			else {
				PUtil.LogDebug("MODIFYING MOD: " + Mod.label.title);
				string desc = Description?.Trim(), title = Title?.Trim();
				if (!desc.IsNullOrWhiteSpace() && desc != details.m_rgchDescription) {
					PUtil.LogDebug("DESCRIPTION: " + desc);
					if (!SteamRemoteStorage.UpdatePublishedFileDescription(updateHandle, desc))
						PUtil.LogWarning("Failed to set item description!");
				}
				if (!title.IsNullOrWhiteSpace() && title != details.m_rgchTitle) {
					PUtil.LogDebug("TITLE: " + title);
					if (!SteamRemoteStorage.UpdatePublishedFileTitle(updateHandle, title))
						PUtil.LogWarning("Failed to set item title!");
				}
				if (!PatchInfo.IsNullOrWhiteSpace()) {
					PUtil.LogDebug("PATCH INFO: " + PatchInfo);
					if (!SteamRemoteStorage.UpdatePublishedFileSetChangeDescription(
							updateHandle, PatchInfo))
						PUtil.LogWarning("Failed to set change notes!");
				}
				if (UpdateData && !DataPath.IsNullOrWhiteSpace()) {
					byte[] zipData = ZipModFolder(DataPath);
					if (zipData != null) {
						PUtil.LogDebug("DATA: " + DataPath + " => " + REMOTE_MOD_DATA);
						if (!WriteToSteamStorage(zipData, REMOTE_MOD_DATA, OnDataUploaded))
							OnModifyFailed?.Invoke();
					} else
						OnModifyFailed?.Invoke();
				} else
					StartPreviewUpload();
			}
		}

		private void StartPreviewUpload() {
			if (UpdatePreview && !PreviewPath.IsNullOrWhiteSpace()) {
				bool failed = true;
				try {
					// 1 MB limit on preview images
					byte[] prevData = File.ReadAllBytes(PreviewPath);
					PUtil.LogDebug("PREVIEW: " + PreviewPath + " => " + REMOTE_MOD_PREVIEW);
					failed = !WriteToSteamStorage(prevData, REMOTE_MOD_PREVIEW,
						OnPreviewUploaded);
				} catch (IOException e) {
					PUtil.LogWarning("Unable to open preview image:");
					PUtil.LogExcWarn(e);
				}
				if (failed)
					OnModifyFailed?.Invoke();
			} else
				FinishModify();
		}

		private bool WriteToSteamStorage(byte[] localData, string remotePath, CallResult<
				RemoteStorageFileWriteAsyncComplete_t>.APIDispatchDelegate callback) {
			bool write = false;
			// Memory hog, but Steam needs the bytes in memory anyways
			try {
				var call = SteamRemoteStorage.FileWriteAsync(remotePath, localData, (uint)
					localData.Length);
				if (!call.Equals(SteamAPICall_t.Invalid)) {
					uploadCaller.Set(call, callback);
					write = true;
				} else
					PUtil.LogWarning("Unable to write " + remotePath +
						" to Steam Remote Storage!");
			} catch (IOException e) {
				PUtil.LogExcWarn(e);
			}
			return write;
		}
	}
}
#endif
