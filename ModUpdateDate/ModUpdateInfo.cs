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

using KMod;
using Newtonsoft.Json;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using Steamworks;
using System.Collections.Generic;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// The options class for Mod Update Date.
	/// </summary>
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	[ModInfo("Mod Update Date", "https://github.com/peterhaneve/ONIMods")]
	[RestartRequired]
	public sealed class ModUpdateInfo {
		/// <summary>
		/// The mod settings for this mod.
		/// </summary>
		internal static ModUpdateInfo Settings { get; private set; }

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
		/// Loads the settings for this mod.
		/// </summary>
		internal static void LoadSettings() {
			Settings = POptions.ReadSettingsForAssembly<ModUpdateInfo>() ??
				new ModUpdateInfo();
		}

		/// <summary>
		/// The status of each mod that has been updated by this mod.
		/// </summary>
		[JsonProperty]
		public List<ModUpdateData> ModUpdates { get; set; }

		public ModUpdateInfo() {
			ModUpdates = new List<ModUpdateData>(8);
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
	/// The mod update status.
	/// </summary>
	public enum ModUpdateStatus {
		Default, PendingUpdate, UpdatedByThisMod
	}
}
