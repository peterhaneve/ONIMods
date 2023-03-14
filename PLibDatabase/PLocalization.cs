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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PeterHan.PLib.Database {
	/// <summary>
	/// Handles localization for mods by automatically loading po files from the translations
	/// folder in their mod directories.
	/// </summary>
	public sealed class PLocalization : PForwardedComponent {
		/// <summary>
		/// The subfolder from which translations will be loaded.
		/// </summary>
		public const string TRANSLATIONS_DIR = "translations";

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// Localizes the specified mod assembly.
		/// </summary>
		/// <param name="modAssembly">The assembly to localize.</param>
		/// <param name="locale">The locale file name to be used.</param>
		private static void Localize(Assembly modAssembly, Localization.Locale locale) {
			string path = PUtil.GetModPath(modAssembly);
			string locCode = locale.Code;
			if (string.IsNullOrEmpty(locCode))
				locCode = Localization.GetCurrentLanguageCode();
			var poFile = Path.Combine(Path.Combine(path, TRANSLATIONS_DIR), locCode +
				PLibLocalization.TRANSLATIONS_EXT);
			try {
				Localization.OverloadStrings(Localization.LoadStringsFile(poFile, false));
				RewriteStrings(modAssembly);
			} catch (FileNotFoundException) {
				// No localization available for this locale
#if DEBUG
				PDatabaseUtils.LogDatabaseDebug("No {0} localization available for mod {1}".F(
					locCode, modAssembly.GetNameSafe() ?? "?"));
#endif
			} catch (DirectoryNotFoundException) {
			} catch (IOException e) {
				PDatabaseUtils.LogDatabaseWarning("Failed to load {0} localization for mod {1}:".
					F(locCode, modAssembly.GetNameSafe() ?? "?"));
				PUtil.LogExcWarn(e);
			}
		}

		/// <summary>
		/// Searches types in the assembly (no worries, Localization did this anyways, so they
		/// all either loaded or failed to load) for fields that already had loc string keys
		/// created, and fixes them if so.
		/// </summary>
		/// <param name="assembly">The assembly to check for strings.</param>
		internal static void RewriteStrings(Assembly assembly) {
			foreach (var type in assembly.GetTypes())
				foreach (var field in type.GetFields(PPatchTools.BASE_FLAGS | BindingFlags.
						FlattenHierarchy | BindingFlags.Static)) {
					// Only use fields of type LocString
					if (field.FieldType == typeof(LocString) && field.GetValue(null) is
							LocString ls) {
#if DEBUG
						PDatabaseUtils.LogDatabaseDebug("Rewrote string {0}: {1} to {2}".F(ls.
							key.String, Strings.Get(ls.key.String), ls.text));
#endif
						Strings.Add(ls.key.String, ls.text);
					}
				}
		}

		public override Version Version => VERSION;

		/// <summary>
		/// The assemblies to be localized.
		/// </summary>
		private readonly ICollection<Assembly> toLocalize;

		public PLocalization() {
			toLocalize = new List<Assembly>(4);
			InstanceData = toLocalize;
		}

		/// <summary>
		/// Debug dumps the translation templates for ALL registered PLib localized mods.
		/// </summary>
		internal void DumpAll() {
			var allMods = PRegistry.Instance.GetAllComponents(ID);
			if (allMods != null)
				foreach (var toDump in allMods) {
					// Reach for those assemblies
					var assemblies = toDump.GetInstanceData<ICollection<Assembly>>();
					if (assemblies != null)
						foreach (var modAssembly in assemblies)
							ModUtil.RegisterForTranslation(modAssembly.GetTypes()[0]);
				}
		}

		public override void Initialize(Harmony plibInstance) {
			// PLibLocalization will invoke Process here
		}

		public override void Process(uint operation, object _) {
			var locale = Localization.GetLocale();
			if (locale != null && operation == 0)
				foreach (var modAssembly in toLocalize)
					Localize(modAssembly, locale);
		}

		/// <summary>
		/// Registers the specified assembly for automatic PLib localization. If the argument
		/// is omitted, the calling assembly is registered.
		/// </summary>
		/// <param name="assembly">The assembly to register for PLib localization.</param>
		public void Register(Assembly assembly = null) {
			if (assembly == null)
				assembly = Assembly.GetCallingAssembly();
			var types = assembly.GetTypes();
			if (types == null || types.Length == 0)
				PDatabaseUtils.LogDatabaseWarning("Registered assembly " + assembly.
					GetNameSafe() + " that had no types for localization!");
			else {
				RegisterForForwarding();
				toLocalize.Add(assembly);
				// This call searches all types in the assembly implicitly
				Localization.RegisterForTranslation(types[0]);
#if DEBUG
				PDatabaseUtils.LogDatabaseDebug("Localizing assembly {0} using base namespace {1}".
					F(assembly.GetNameSafe(), types[0].Namespace));
#endif
			}
		}
	}
}
