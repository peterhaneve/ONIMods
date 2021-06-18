/*
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
using PeterHan.PLib.Core;
using Steamworks;
using System;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// Contains details about a mod that will be updated by ModUpdateExecutor.
	/// </summary>
	public sealed class ModToUpdate {
		/// <summary>
		/// The path to download this mod.
		/// </summary>
		public string DownloadPath { get; }

		/// <summary>
		/// The last update on the Steam Workshop in UTC.
		/// </summary>
		public System.DateTime LastSteamUpdate { get; }

		/// <summary>
		/// The Steam mod ID of this mod.
		/// </summary>
		public ulong SteamID { get; }

		/// <summary>
		/// The mod to update.
		/// </summary>
		public Mod Mod { get; }

		/// <summary>
		/// The title of the updated mod.
		/// </summary>
		public string Title {
			get {
				return Mod.label.title;
			}
		}

		private PublishedFileId_t steamFileID;

		public ModToUpdate(Mod mod) {
			Mod = mod ?? throw new ArgumentNullException("mod");
			if (mod.label.distribution_platform != Label.DistributionPlatform.Steam)
				throw new ArgumentException("Only Steam mods can be updated by this class");
			steamFileID = mod.GetSteamModID();
			SteamID = steamFileID.m_PublishedFileId;
			if (!steamFileID.GetGlobalLastModified(out System.DateTime steamLastUpdate))
				steamLastUpdate = System.DateTime.MinValue;
			LastSteamUpdate = steamLastUpdate;
			DownloadPath = ModUpdateHandler.GetDownloadPath(SteamID);
		}

		public override bool Equals(object obj) {
			return obj is ModToUpdate other && other.Mod.label.Match(Mod.label);
		}

		public override int GetHashCode() {
			return Mod.label.id.GetHashCode();
		}

		public override string ToString() {
			return "ModToUpdate[id={0},title={1}]".F(Mod.label.id, Mod.label.title);
		}
	}
}
