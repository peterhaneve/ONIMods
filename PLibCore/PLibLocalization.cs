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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// Handles localization of PLib for mods by automatically loading po files stored as
	/// EmbeddedResources in PLibCore.dll and ILMerged with the mod assembly.
	/// </summary>
	public static class PLibLocalization {
		/// <summary>
		/// The file extension used for localization files.
		/// </summary>
		public const string TRANSLATIONS_EXT = ".po";

		/// <summary>
		/// The Prefix of LogicalName of EmbeddedResources that stores the content of po files.
		/// Must match the specified value in the Directory.Build.targets file.
		/// </summary>
		private const string TRANSLATIONS_RES_PATH = "PeterHan.PLib.Core.PLibStrings.";

		/// <summary>
		/// Localizes the PLib strings.
		/// </summary>
		/// <param name="locale">The locale to use.</param>
		internal static void LocalizeItself(Localization.Locale locale) {
			if (locale == null)
				throw new ArgumentNullException(nameof(locale));
			Localization.RegisterForTranslation(typeof(PLibStrings));
			var assembly = Assembly.GetExecutingAssembly();
			string locCode = locale.Code;
			if (string.IsNullOrEmpty(locCode))
				locCode = Localization.GetCurrentLanguageCode();
			try {
				using (var stream = assembly.GetManifestResourceStream(
						TRANSLATIONS_RES_PATH + locCode + TRANSLATIONS_EXT)) {
					if (stream != null) {
						// File.ReadAllLines does not work on streams unfortunately
						var lines = new List<string>(128);
						using (var reader = new StreamReader(stream, Encoding.UTF8)) {
							string line;
							while ((line = reader.ReadLine()) != null)
								lines.Add(line);
						}
						Localization.OverloadStrings(Localization.ExtractTranslatedStrings(
							lines.ToArray()));
#if DEBUG
						PUtil.LogDebug("Localizing PLib Core to locale {0}".F(locCode));
#endif
					}
				}
			} catch (Exception e) {
				PUtil.LogWarning("Failed to load {0} localization for PLib Core:".F(locCode));
				PUtil.LogExcWarn(e);
			}
		}
	}
}
