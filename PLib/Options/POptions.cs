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

using OptionsTable = System.Collections.Generic.IDictionary<string, System.Type>;

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
		/// The default size of the Mod Settings dialog.
		/// </summary>
		internal static readonly Vector2 SETTINGS_DIALOG_SIZE = new Vector2(320.0f, 200.0f);

		/// <summary>
		/// The tooltip on the CANCEL button.
		/// </summary>
		public static LocString TOOLTIP_CANCEL = "Discard changes.";

		/// <summary>
		/// The tooltip for cycling to the next item.
		/// </summary>
		public static LocString TOOLTIP_NEXT = "Next";

		/// <summary>
		/// The tooltip on the OK button.
		/// </summary>
		public static LocString TOOLTIP_OK = "Save these options. A restart may be required " +
			"for some mods.";

		/// <summary>
		/// The tooltip for cycling to the previous item.
		/// </summary>
		public static LocString TOOLTIP_PREVIOUS = "Previous";

		/// <summary>
		/// The mod options table.
		/// </summary>
		private static OptionsTable modOptions = null;

		/// <summary>
		/// Adds the Options button to the Mods screen.
		/// </summary>
		/// <param name="modEntry">The mod entry where the button should be added.</param>
		private static void AddModOptions(Traverse modEntry) {
			int index = modEntry.GetField<int>("mod_index");
			var mods = Global.Instance.modManager.mods;
			if (index >= 0 && index < mods.Count) {
				var modSpec = mods[index];
				var transform = modEntry.GetField<RectTransform>("rect_transform");
				string modID = modSpec.label.id;
				if (modSpec.enabled && !string.IsNullOrEmpty(modID) && modOptions.TryGetValue(
						modID, out Type optionsType) && transform != null) {
					// Create delegate to spawn actions dialog
					var action = new OptionsDialog(optionsType, modSpec);
					new PButton("ModSettingsButton") {
						FlexSize = new Vector2f(0.0f, 1.0f), OnClick = action.OnModOptions,
						DynamicSize = true, ToolTip = DIALOG_TITLE.text.F(modSpec.title),
						Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(BUTTON_OPTIONS.
							text.ToLower())
						// Move before the subscription and enable button
					}.SetKleiPinkStyle().AddTo(transform.gameObject, 3);
				}
			}
		}

		/// <summary>
		/// Retrieves the mod directory for the specified assembly.
		/// </summary>
		/// <param name="modDLL">The assembly used for a mod.</param>
		/// <returns>The directory where that mod's configuration file should be found.</returns>
		public static string GetModDir(Assembly modDLL) {
			if (modDLL == null)
				throw new ArgumentNullException("modDLL");
			string dir = null;
			try {
				dir = Directory.GetParent(modDLL.Location)?.FullName;
			} catch (NotSupportedException e) {
				// Guess from the Klei strings
				PUtil.LogExcWarn(e);
			} catch (System.Security.SecurityException e) {
				// Guess from the Klei strings
				PUtil.LogExcWarn(e);
			} catch (IOException e) {
				// Guess from the Klei strings
				PUtil.LogExcWarn(e);
			}
			if (dir == null)
				dir = KMod.Manager.GetDirectory();
			return dir;
		}

		/// <summary>
		/// Initializes and stores the options table for quicker lookups later.
		/// </summary>
		internal static void Init() {
			lock (PSharedData.GetLock(PRegistry.KEY_OPTIONS_LOCK)) {
				modOptions = PSharedData.GetData<OptionsTable>(PRegistry.KEY_OPTIONS_TABLE);
			}
		}

		/// <summary>
		/// Registers a class as a mod options class.
		/// </summary>
		/// <param name="optionsType">The class which will represent the options for this mod.</param>
		public static void RegisterOptions(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			var assembly = optionsType.Assembly;
			var id = Path.GetFileName(GetModDir(assembly));
			// Prevent concurrent modification (should be impossible anyways)
			lock (PSharedData.GetLock(PRegistry.KEY_OPTIONS_LOCK)) {
				// Get options table
				var options = PSharedData.GetData<OptionsTable>(PRegistry.KEY_OPTIONS_TABLE);
				if (options == null)
					PSharedData.PutData(PRegistry.KEY_OPTIONS_TABLE, options =
						new Dictionary<string, Type>(8));
				if (options.ContainsKey(id))
					PUtil.LogWarning("Duplicate mod ID: " + id);
				else {
					// Add as options for this mod
					options.Add(id, optionsType);
					PUtil.LogDebug("Registered mod options class {0} for {1}".F(
						optionsType.Name, assembly.GetName()?.Name));
				}
			}
		}

		/// <summary>
		/// Applied to ModsScreen if mod options are registered, after BuildDisplay runs.
		/// </summary>
		internal static void BuildDisplay(object displayedMods) {
			if (modOptions != null)
				// Must cast the type because ModsScreen.DisplayedMod is private
				foreach (var displayedMod in (System.Collections.IEnumerable)displayedMods)
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
			} catch (FileNotFoundException) {
				PUtil.LogDebug("{0} was not found; using default settings".F(CONFIG_FILE));
			} catch (IOException e) {
				// Options will be set to defaults
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			}
			return options;
		}

		/// <summary>
		/// Writes mod settings to its configuration file.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="settings">The settings to write</param>
		public static void WriteSettings<T>(T settings) where T : class {
			// Calculate path from calling assembly
			string path = Path.Combine(GetModDir(Assembly.GetCallingAssembly()), CONFIG_FILE);
			try {
				using (var jw = new JsonTextWriter(File.CreateText(path))) {
					var serializer = new JsonSerializer { MaxDepth = 8 };
					// Serialize from stream avoids creating file text in memory
					serializer.Serialize(jw, settings);
				}
			} catch (IOException e) {
				// Options will be set to defaults
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			}
		}
	}
}
