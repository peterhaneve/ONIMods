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

using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PeterHan.PLib.Datafiles {
	/// <summary>
	/// Handles localization for mods by automatically loading po files from the translations
	/// folder in their mod directories.
	/// </summary>
	public static class PLocalization {
		/// <summary>
		/// The subfolder from which translations will be loaded.
		/// </summary>
		public const string TRANSLATIONS_DIR = "translations";

		/// <summary>
		/// The file extension used for localization files.
		/// </summary>
		public const string TRANSLATIONS_EXT = ".po";

		/// <summary>
		/// Debug dumps the translation templates for ALL registered PLib localized mods.
		/// </summary>
		internal static void DumpAll() {
			lock (PSharedData.GetLock(PRegistry.KEY_LOCALE_LOCK)) {
				// Get list holding locale information
				var list = PSharedData.GetData<IList<Assembly>>(PRegistry.KEY_LOCALE_TABLE);
				if (list != null)
					foreach (var mod in list)
						if (mod != null)
							ModUtil.RegisterForTranslation(mod.GetTypes()[0]);
			}
		}

		/// <summary>
		/// Localizes the specified mod.
		/// </summary>
		/// <param name="mod">The mod to localize.</param>
		/// <param name="path">The path to its data folder.</param>
		/// <param name="locale">The locale to use.</param>
		private static void Localize(Assembly mod, string path, Localization.Locale locale) {
			var poFile = Path.Combine(Path.Combine(path, TRANSLATIONS_DIR), locale.Code +
				TRANSLATIONS_EXT);
			try {
				Localization.OverloadStrings(Localization.LoadStringsFile(poFile, false));
			} catch (FileNotFoundException) {
				// No localization available for this locale
			} catch (DirectoryNotFoundException) {
			} catch (IOException e) {
				PUtil.LogWarning("Failed to load {0} localization for mod {1}:".F(locale.
					Code, mod.GetName()?.Name));
				PUtil.LogExcWarn(e);
			}
		}

		/// <summary>
		/// Localizes all mods which registered for it.
		/// </summary>
		/// <param name="locale">The locale to use.</param>
		internal static void LocalizeAll(Localization.Locale locale) {
			if (locale == null)
				throw new ArgumentNullException("locale");
			lock (PSharedData.GetLock(PRegistry.KEY_LOCALE_LOCK)) {
				// Get list holding locale information
				var list = PSharedData.GetData<IList<Assembly>>(PRegistry.KEY_LOCALE_TABLE);
				if (list != null) {
					PUtil.LogDebug("Localizing {0:D} mods to locale {1}".F(list.Count,
						locale.Code));
					foreach (var mod in list)
						if (mod != null)
							Localize(mod, POptions.GetModDir(mod), locale);
				}
			}
		}

		/// <summary>
		/// Registers the specified assembly for automatic PLib localization. If null is
		/// passed, the calling assembly is registered.
		/// </summary>
		/// <param name="assembly">The assembly to register for PLib localization.</param>
		public static void Register(Assembly assembly = null) {
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"PLocalization.Register!");
			}
			if (assembly == null)
				assembly = Assembly.GetCallingAssembly();
			lock (PSharedData.GetLock(PRegistry.KEY_LOCALE_LOCK)) {
				// Get list holding locale information
				var list = PSharedData.GetData<IList<Assembly>>(PRegistry.KEY_LOCALE_TABLE);
				if (list == null)
					PSharedData.PutData(PRegistry.KEY_LOCALE_TABLE, list =
						new List<Assembly>(8));
				list.Add(assembly);
			}
			var types = assembly.GetTypes();
			if (types == null || types.Length == 0)
				PUtil.LogWarning("Registered assembly " + assembly.GetNameSafe() +
					" that had no types for localization!");
			else {
				// This call searches all types in the assembly implicitly
				Localization.RegisterForTranslation(types[0]);
#if DEBUG
				PUtil.LogDebug("Localizing assembly {0} using base namespace {1}".F(assembly.
					GetNameSafe(), types[0].Namespace));
#endif
			}
		}
	}
}
