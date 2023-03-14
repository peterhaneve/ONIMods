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

using PeterHan.PLib.Core;
using System;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Stores the information displayed about a mod in its options dialog.
	/// </summary>
	internal sealed class ModDialogInfo {
		/// <summary>
		/// Gets the text shown for a mod's version.
		/// </summary>
		/// <param name="optionsType">The type used for the mod settings.</param>
		/// <returns>The mod version description.</returns>
		private static string GetModVersionText(Type optionsType) {
			var asm = optionsType.Assembly;
			string version = asm.GetFileVersion();
			// Use FileVersion if available, else assembly version
			if (string.IsNullOrEmpty(version))
				version = string.Format(PLibStrings.MOD_ASSEMBLY_VERSION, asm.GetName().
					Version);
			else
				version = string.Format(PLibStrings.MOD_VERSION, version);
			return version;
		}

		/// <summary>
		/// The path to the image displayed (on the file system) for this mod.
		/// </summary>
		public string Image { get; }

		/// <summary>
		/// The mod title. The title is taken directly from the mod version information.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The URL which will be displayed. If none was provided, the Steam workshop page URL
		/// will be reported for Steam mods, and an empty string for local/dev mods.
		/// </summary>
		public string URL { get; }

		/// <summary>
		/// The mod version.
		/// </summary>
		public string Version { get; }

		internal ModDialogInfo(Type type, string url, string image) {
			var mod = POptions.GetModFromType(type);
			string title, version;
			Image = image ?? "";
			if (mod != null) {
				string modInfoVersion = mod.packagedModInfo?.version;
				title = mod.title;
				if (string.IsNullOrEmpty(url) && mod.label.distribution_platform == KMod.Label.
						DistributionPlatform.Steam)
					url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + mod.
						label.id;
				if (!string.IsNullOrEmpty(modInfoVersion))
					version = string.Format(PLibStrings.MOD_VERSION, modInfoVersion);
				else
					version = GetModVersionText(type);
			} else {
				title = type.Assembly.GetNameSafe();
				version = GetModVersionText(type);
			}
			Title = title ?? "";
			URL = url ?? "";
			Version = version;
		}

		public override string ToString() {
			return base.ToString();
		}
	}
}
