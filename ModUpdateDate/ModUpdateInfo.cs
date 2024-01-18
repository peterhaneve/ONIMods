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
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PeterHan.PLib.AVC;
using Steamworks;
using System;
using System.IO;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// The options class for Mod Updater.
	/// </summary>
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	[ModInfo(GITHUB_BASE)]
	[ConfigFile(SharedConfigLocation: true)]
	[RestartRequired]
	public sealed class ModUpdateInfo {
		/// <summary>
		/// The base URL for the mod's GitHub page.
		/// </summary>
		public const string GITHUB_BASE = "https://github.com/peterhaneve/ONIMods";

		/// <summary>
		/// The URL for the local install readme page.
		/// </summary>
		public const string GITHUB_README = GITHUB_BASE + "/blob/main/README.md";

		/// <summary>
		/// The mod settings for this mod.
		/// </summary>
		internal static ModUpdateInfo Settings { get; private set; }

		/// <summary>
		/// Stores information that is not serialized such as version mismatches.
		/// </summary>
		private static readonly ConcurrentDictionary<string, ModTransientInfo> VERSION_INFO = 
			new ConcurrentDictionary<string, ModTransientInfo>(2, 128);

		/// <summary>
		/// Looks for a mod in the known updates config.
		/// </summary>
		/// <param name="id">The Steam mod ID.</param>
		/// <returns>Any extra information that this mod knows about the mod from previous
		/// updates, or null if none is available.</returns>
		internal static ModUpdateData FindModInConfig(ulong id) {
			ModUpdateData info = null;
			var existing = Settings?.ModUpdates;
			if (existing != null)
				// Previously tracked by this mod?
				foreach (var ud in existing)
					if (ud.ID == id) {
						info = ud;
						break;
					}
			return info;
		}

		/// <summary>
		/// Gets or creats the transient data for the specified mod which caches its local
		/// last modified date and version.
		/// </summary>
		/// <param name="mod">The mod to look up.</param>
		/// <returns>The cached information about that mod.</returns>
		internal static ModTransientInfo GetLocalInfo(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			return VERSION_INFO.GetOrAdd(mod.label.id, _ => new ModTransientInfo(mod));
		}

		/// <summary>
		/// Loads the settings for this mod.
		/// </summary>
		internal static void LoadSettings() {
			var s = POptions.ReadSettings<ModUpdateInfo>();
			if (s == null || string.IsNullOrEmpty(s.VersionSavedOn))
				s = new ModUpdateInfo();
			s.VersionSavedOn = ModVersion.FILE_VERSION;
			Settings = s;
		}

		/// <summary>
		/// The status of each mod that has been updated by this mod.
		/// </summary>
		[JsonProperty]
		public List<ModUpdateData> ModUpdates { get; set; }

		[JsonProperty]
		[Option("STRINGS.UI.MODUPDATER.OPTION_PASSIVE", "STRINGS.UI.TOOLTIPS.MODUPDATER.OPTION_PASSIVE")]
		public bool AutoUpdate { get; set; }

		[JsonProperty]
		[Option("STRINGS.UI.MODUPDATER.OPTION_MAINMENU", "STRINGS.UI.TOOLTIPS.MODUPDATER.OPTION_MAINMENU")]
		public bool ShowMainMenuWarning { get; set; }

		[JsonProperty]
		public string VersionSavedOn { get; set; }

		public ModUpdateInfo() {
			ModUpdates = new List<ModUpdateData>(8);
			AutoUpdate = true;
			ShowMainMenuWarning = true;
			VersionSavedOn = "";
		}

		public override string ToString() {
			return "ModUpdateInfo[Version={0}]".F(VersionSavedOn);
		}
	}

	/// <summary>
	/// The status of one mod.
	/// </summary>
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public sealed class ModUpdateData {
		/// <summary>
		/// The mod ID. All are steam mods.
		/// </summary>
		[JsonProperty]
		public ulong ID { get; set; }

		/// <summary>
		/// The timestamp when this mod was last updated in DateTime ticks. In UTC.
		/// </summary>
		[JsonProperty]
		public long LastUpdated { get; set; }

		/// <summary>
		/// The mod's update status.
		/// </summary>
		[JsonProperty]
		public ModUpdateStatus Status { get; set; }

		public ModUpdateData() {
			ID = 0U;
			Status = ModUpdateStatus.Default;
			LastUpdated = System.DateTime.UtcNow.Ticks;
		}

		public ModUpdateData(ulong id, System.DateTime lastUpdate) {
			ID = id;
			LastUpdated = lastUpdate.Ticks;
			Status = ModUpdateStatus.Default;
		}

		public override bool Equals(object obj) {
			return obj is ModUpdateData other && other.ID == ID;
		}

		public override int GetHashCode() {
			return ID.GetHashCode();
		}

		public override string ToString() {
			return "ModUpdateData[id={0:D},status={1}]".F(ID, Status);
		}
	}

	/// <summary>
	/// Transient information about the mod that should not survive serialization.
	/// </summary>
	public sealed class ModTransientInfo {
		/// <summary>
		/// The version in the file system  (Steam's cached version replaces this once the
		/// mod is loaded).
		/// </summary>
		public string FilesystemVersion { get; set; }
		
		/// <summary>
		/// The time when this mod was last modified locally.
		/// </summary>
		public System.DateTime LocalLastModified { get; set; }

		/// <summary>
		/// The mod label.
		/// </summary>
		private readonly Label label;

		public ModTransientInfo(Mod mod) {
			if (mod == null)
				throw new ArgumentNullException(nameof(mod));
			label = mod.label;
			FilesystemVersion = "";
			RefreshLastModified();
		}

		/// <summary>
		/// Refreshes the local last modified date.
		/// </summary>
		public void RefreshLastModified() {
			var lastMod = System.DateTime.UtcNow;
			bool updated = false;
			if (label.distribution_platform == Label.DistributionPlatform.Steam && ulong.
					TryParse(label.id, out ulong idLong)) {
				// 260 = MAX_PATH
				if (SteamUGC.GetItemInstallInfo(new PublishedFileId_t(idLong), out _,
						out string _, 260U, out uint timestamp) && timestamp > 0U) {
					lastMod = SteamVersionChecker.UnixEpochToDateTime(timestamp);
					updated = true;
				} else
					PUtil.LogWarning("Unable to get Steam install information for " +
						label.title);
			}
			if (!updated)
				try {
					// Get the last modified date of its install path :/
					lastMod = File.GetLastWriteTimeUtc(Path.GetFullPath(label.install_path));
				} catch (IOException) { }
			LocalLastModified = lastMod;
		}

		public override string ToString() {
			return "ModTransientInfo[fsVersion={0},lastModified={1:R}]".F(FilesystemVersion,
				LocalLastModified);
		}
	}

	/// <summary>
	/// The mod update status stored in the configuration.
	/// </summary>
	public enum ModUpdateStatus {
		Default, PendingUpdate, UpdatedByThisMod
	}
}
