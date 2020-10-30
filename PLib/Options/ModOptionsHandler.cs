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
using PeterHan.PLib.UI;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Handles mod options directly from a KMod.Mod instance.
	/// </summary>
	internal sealed class ModOptionsHandler : IOptionsHandler {
		public string ConfigPath { get; }

		public string DefaultURL { get; }

		private readonly string modTitle;

		internal ModOptionsHandler(Mod mod) {
			// Find mod home page
			var label = mod.label;
			ConfigPath = mod.GetModBasePath();
			if (string.IsNullOrEmpty(DefaultURL) && label.distribution_platform == Label.
					DistributionPlatform.Steam)
				// Steam mods use their workshop ID as the label
				DefaultURL = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + label.id;
			else
				DefaultURL = null;
			modTitle = label.title;
		}

		public string GetTitle(string baseTitle) {
			return string.Format(PUIStrings.DIALOG_TITLE, baseTitle ?? modTitle);
		}

		public void OnCancel(object oldOptions) { }

		public void OnSaveOptions(object newOptions) { }
	}
}
