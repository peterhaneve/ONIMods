/*
 * Copyright 2019 Peter Han
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

using Harmony;
using Newtonsoft.Json;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Adds an "Options" screen to a mod in the Mods menu.
	/// </summary>
	public sealed class POptions {
		/// <summary>
		/// The text shown on the Done button.
		/// </summary>
		public static LocString BUTTON_OK = STRINGS.UI.FRONTEND.OPTIONS_SCREEN.BACK;

		/// <summary>
		/// The text shown on the Options button.
		/// </summary>
		public static LocString BUTTON_OPTIONS = STRINGS.UI.FRONTEND.MAINMENU.OPTIONS;

		/// <summary>
		/// The configuration file name to be used.
		/// </summary>
		public static readonly string CONFIG_FILE = "config.json";

		/// <summary>
		/// The dialog title, where {0} is substituted with the mod friendly name.
		/// </summary>
		public static LocString DIALOG_TITLE = "Options for {0}";

		/// <summary>
		/// The tooltip on the OK button.
		/// </summary>
		public static LocString TOOLTIP_OK = "Save these options. A restart may be required for some mods.";

		/// <summary>
		/// The tooltip on the CANCEL button.
		/// </summary>
		public static LocString TOOLTIP_CANCEL = "Discard changes.";

		/// <summary>
		/// The default size of the Mod Settings dialog.
		/// </summary>
		internal static readonly Vector2 SETTINGS_DIALOG_SIZE = new Vector2(320.0f, 200.0f);

		/// <summary>
		/// The location where mod option types are stored. Technically this class can manage
		/// mod options for more than one mod.
		/// </summary>
		private static readonly IDictionary<string, Type> options =
			new Dictionary<string, Type>(4);

		/// <summary>
		/// Adds the Options button to the Mods screen.
		/// </summary>
		/// <param name="modEntry">The mod entry where the button should be added.</param>
		private static void AddModOptions(Traverse modEntry) {
			var modSpec = Global.Instance.modManager.mods[modEntry.GetField<int>(
				"mod_index")];
			var transform = modEntry.GetField<RectTransform>("rect_transform");
			string modID = modSpec.label.id;
			if (modSpec.enabled && !string.IsNullOrEmpty(modID) && options.TryGetValue(modID,
					out Type optionsType) && transform != null) {
				// Create delegate to spawn actions dialog
				var action = new OptionsDialog(optionsType, modSpec);
				new PButton("ModSettingsButton") {
					FlexSize = new Vector2f(0.0f, 1.0f),
					OnClick = action.OnModOptions,
					Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(BUTTON_OPTIONS.text.
						ToLower()),
					ToolTip = DIALOG_TITLE.text.F(modSpec.title)
					// Move before the subscription and enable button
				}.SetKleiPinkStyle().AddTo(transform.gameObject, 3);
			}
		}

		/// <summary>
		/// Retrieves the mod directory for the specified assembly.
		/// </summary>
		/// <param name="modDLL">The assembly used for a mod.</param>
		/// <returns>The directory where that mod's configuration file should be found.</returns>
		private static string GetModDir(Assembly modDLL) {
			string dir = null;
			try {
				dir = Directory.GetParent(modDLL.Location)?.FullName;
			} catch (NotSupportedException e) {
				// Guess from the Klei strings
				PUtil.LogExcWarn(e);
			} catch (System.Security.SecurityException e) {
				// Guess from the Klei strings
				PUtil.LogExcWarn(e);
			}
			if (dir == null)
				dir = KMod.Manager.GetDirectory();
			return dir;
		}

		/// <summary>
		/// Registers a class as a mod options class.
		/// </summary>
		/// <param name="optionsType">The class which will represent the options for this mod.</param>
		public static void RegisterOptions(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			var assembly = optionsType.Assembly;
			bool hasPath = false;
			try {
				var id = Path.GetFileName(GetModDir(assembly));
				// Prevent concurrent modification (should be impossible anyways)
				lock (options) {
					if (options.Count < 1) {
						// Patch in the mods screen
						var instance = HarmonyInstance.Create(PRegistry.PLIB_HARMONY);
						instance.Patch(typeof(ModsScreen), "BuildDisplay", null,
							new HarmonyMethod(typeof(POptions), "BuildDisplay_Postfix"));
						PUtil.LogDebug("Patched mods options screen");
					}
					if (options.ContainsKey(id))
						PUtil.LogWarning("Duplicate mod ID: " + id);
					else {
						// Add as options for this mod
						options.Add(id, optionsType);
						PUtil.LogDebug("Registered mod options class {0} for {1}".F(
							optionsType.Name, assembly.GetName()?.Name));
					}
				}
				hasPath = true;
			} catch (IOException) { }
			if (!hasPath)
				PUtil.LogWarning("Unable to determine mod path for assembly: " + assembly.
					FullName);
		}

		/// <summary>
		/// Applied to ModsScreen if mod options are registered, after BuildDisplay runs.
		/// </summary>
		internal static void BuildDisplay_Postfix(ref object ___displayedMods) {
			// Must cast the type because ModsScreen.DisplayedMod is private
			foreach (var displayedMod in (System.Collections.IEnumerable)___displayedMods)
				AddModOptions(Traverse.Create(displayedMod));
		}

		/// <summary>
		/// Reads mod settings from its configuration file.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed)</returns>
		public static T ReadSettings<T>() where T : class {
			T options = null;
			// Calculate path from calling assembly
			string path = Path.Combine(GetModDir(Assembly.GetCallingAssembly()), CONFIG_FILE);
			try {
				using (var jr = new JsonTextReader(File.OpenText(path))) {
					var serializer = new JsonSerializer { MaxDepth = 8 };
					// Deserialize from stream avoids reading file text into memory
					options = serializer.Deserialize<T>(jr);
				}
			} catch (IOException e) {
				// Options will be set to defaults
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			}
			return options;
		}
	}
}
