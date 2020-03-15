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
using PeterHan.PLib;
using Steamworks;
using System;

namespace PeterHan.ModUpdateDate {
	/// <summary>
	/// The results of updating one mod.
	/// </summary>
	public sealed class ModUpdateResult : IComparable<ModUpdateResult> {
		/// <summary>
		/// The number of configuration files that were saved during config backup.
		/// </summary>
		public int ConfigsRestored { get; set; }

		/// <summary>
		/// The mod thus updated.
		/// </summary>
		public Mod Mod { get; }

		/// <summary>
		/// The error code (if any) returned by Steam.
		/// </summary>
		public EResult Result { get; }

		/// <summary>
		/// The status of the overall download and reinstall.
		/// </summary>
		public ModDownloadStatus Status { get; }

		/// <summary>
		/// The title of the updated mod.
		/// </summary>
		public string Title { get; }

		public ModUpdateResult(ModDownloadStatus status, Mod mod, EResult result) {
			Status = status;
			Mod = mod ?? throw new ArgumentNullException("mod");
			Result = result;
			Title = mod.label.title;
		}

		public int CompareTo(ModUpdateResult other) {
			int result = other.Status.CompareTo(Status);
			if (result == 0)
				result = StringComparer.CurrentCultureIgnoreCase.Compare(Title, other.Title);
			if (result == 0)
				result = StringComparer.InvariantCulture.Compare(Mod.label.id, other.Mod.
					label.id);
			return result;
		}

		public override bool Equals(object obj) {
			return obj is ModUpdateResult other && Status == other.Status && Mod.label.Match(
				other.Mod.label);
		}

		public override int GetHashCode() {
			return Title.GetHashCode();
		}

		public override string ToString() {
			return "ModUpdateResult[mod={0},title={1}]".F(Mod.label.id, Title);
		}
	}

	/// <summary>
	/// The status of the Steam I/O and config backup.
	/// 
	/// Note that the order here is the reverse order that the mods will be sorted by in
	/// the results dialog! (the first one will be at the bottom and so forth)
	/// </summary>
	public enum ModDownloadStatus {
		OK, ConfigError, ModUninstalled, NoSteamFile, SteamError
	}
}
