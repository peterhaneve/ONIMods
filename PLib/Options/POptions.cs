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
		/// The configuration file name to be used. This field is an alias to be binary
		/// compatible with PLib <= 2.17. This file name is case sensitive.
		/// </summary>
		[Obsolete("CONFIG_FILE is obsolete. Add a ConfigFileAttribute to options classes to specify the configuration file name.")]	
		public static readonly string CONFIG_FILE = CONFIG_FILE_NAME;

		/// <summary>
		/// The configuration file name to be used by default for classes that do not specify
		/// otherwise. This file name is case sensitive.
		/// </summary>
		public const string CONFIG_FILE_NAME = "config.json";

		/// <summary>
		/// The mod_index field of the private DisplayedMod struct in ModsScreen.
		/// </summary>
		private static readonly FieldInfo FIELD_MOD_INDEX;

		/// <summary>
		/// The rect_transform field of the private DisplayedMod struct in ModsScreen.
		/// </summary>
		private static readonly FieldInfo FIELD_RECT_TRANSFORM;

		/// <summary>
		/// The maximum nested class depth which will be serialized in mod options to avoid
		/// infinite loops.
		/// </summary>
		public const int MAX_SERIALIZATION_DEPTH = 8;

		/// <summary>
		/// The mod options table.
		/// </summary>
		private static OptionsTable modOptions;

		/// <summary>
		/// Initializes the reflected FieldInfo structures. This is more efficient than making
		/// Traverse instances on each entry.
		/// </summary>
		static POptions() {
			var subStruct = typeof(ModsScreen).GetNestedType("DisplayedMod", BindingFlags.
				NonPublic | BindingFlags.Public);
			if (subStruct != null) {
				FIELD_MOD_INDEX = subStruct.GetFieldSafe("mod_index", false);
				FIELD_RECT_TRANSFORM = subStruct.GetFieldSafe("rect_transform", false);
			}
			modOptions = null;
		}

		/// <summary>
		/// Adds the Options button to the Mods screen.
		/// </summary>
		/// <param name="modEntry">The mod entry where the button should be added.</param>
		/// <param name="fallbackIndex">The index to use if it cannot be determined from the entry.</param>
		/// <param name="parent">The parent where the entries were added, used only if the
		/// fallback index is required.</param>
		private static void AddModOptions(object modEntry, int fallbackIndex, GameObject parent) {
			var mods = Global.Instance.modManager?.mods;
			var transform = (FIELD_RECT_TRANSFORM?.GetValue(modEntry)) as Transform;
			int index;
			// Try retrieving from the entry first
			if (FIELD_MOD_INDEX != null && FIELD_MOD_INDEX.GetValue(modEntry) is int realIndex)
				index = realIndex;
			else
				index = fallbackIndex;
			if (transform == null)
				transform = parent.transform.GetChild(index);
			if (mods != null && index >= 0 && index < mods.Count && transform != null) {
				var modSpec = mods[index];
				string modID = modSpec.label.id;
				if (modSpec.enabled && !string.IsNullOrEmpty(modID) && modOptions.TryGetValue(
						modID, out Type oType)) {
					// Create delegate to spawn actions dialog
					var action = new OptionsDialog(oType, new ModOptionsHandler(modSpec));
					new PButton("ModSettingsButton") {
						FlexSize = Vector2.up, OnClick = action.OnModOptions,
						ToolTip = PUIStrings.DIALOG_TITLE.text.F(modSpec.title), Text =
						CultureInfo.CurrentCulture.TextInfo.ToTitleCase(PUIStrings.
						BUTTON_OPTIONS.text.ToLower())
					}.SetKleiPinkStyle().AddTo(transform.gameObject, 3);
					// Move before the subscription and enable button
				}
			}
		}

		/// <summary>
		/// Applied to ModsScreen if mod options are registered, after BuildDisplay runs.
		/// </summary>
		internal static void BuildDisplay(System.Collections.IEnumerable displayedMods,
				GameObject entryPrefab) {
			if (modOptions != null) {
				int index = 0;
				// Harmony does not check the type at all on accessing private fields with ___
				foreach (var displayedMod in displayedMods)
					AddModOptions(displayedMod, index++, entryPrefab);
			}
		}

		/// <summary>
		/// Retrieves the base mod directory for the specified assembly. Identical to
		/// GetModDir() for most mods, but resolves to the original mod directory if an
		/// archived version is running.
		/// </summary>
		/// <param name="modDLL">The assembly used for a mod.</param>
		/// <returns>The base directory of that mod.</returns>
		public static string GetModBaseDir(Assembly modDLL) {
			if (modDLL == null)
				throw new ArgumentNullException("modDLL");
			string dir = null;
			try {
				dir = Directory.GetParent(modDLL.Location)?.FullName;
				if (dir != null) {
					var parent = Directory.GetParent(dir);
					// Unfortunately this string is hard coded in the base game
					if (parent != null && parent.Name.StartsWith("archived_version"))
						dir = Directory.GetParent(parent.FullName)?.FullName;
				}
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
				dir = Path.Combine(KMod.Manager.GetDirectory(), modDLL.GetName()?.Name ?? "");
			return dir;
		}

		/// <summary>
		/// Retrieves the configuration file attribute for a mod config.
		/// </summary>
		/// <param name="optionsType">The type potentially containing the config file attribute.</param>
		/// <returns>The ConfigFileAttribute (in this mod's assembly) applied to that type,
		/// or null if none is present.</returns>
		public static ConfigFileAttribute GetConfigFileAttribute(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			ConfigFileAttribute newAttr = null;
			foreach (var attr in optionsType.GetCustomAttributes(true))
				// Cross mod types need reflection
				if ((newAttr = ConfigFileAttribute.CreateFrom(attr)) != null) break;
			return newAttr;
		}

		/// <summary>
		/// Retrieves the configuration file path used by PLib Options for a specified type.
		/// </summary>
		/// <param name="optionsType">The options type stored in the config file.</param>
		/// <returns>The path to the configuration file that will be used by PLib for that
		/// mod's config.</returns>
		public static string GetConfigFilePath(Type optionsType) {
			return GetConfigPath(GetConfigFileAttribute(optionsType), optionsType.Assembly);
		}

		/// <summary>
		/// Retrieves the configuration file path used by PLib Options for a specified type.
		/// </summary>
		/// <param name="attr">The config file attribute for that type.</param>
		/// <param name="modAssembly">The assembly to use for determining the path.</param>
		/// <returns>The path to the configuration file that will be used by PLib for that
		/// mod's config.</returns>
		private static string GetConfigPath(ConfigFileAttribute attr, Assembly modAssembly) {
			return Path.Combine(GetModDir(modAssembly), attr?.ConfigFileName ??
				CONFIG_FILE_NAME);
		}

		/// <summary>
		/// Retrieves the information attribute a mod config.
		/// </summary>
		/// <param name="optionsType">The type potentially containing the mod info attribute.</param>
		/// <returns>The ModInfoAttribute (in this mod's assembly) applied to that type,
		/// or null if none is present.</returns>
		internal static ModInfoAttribute GetModInfoAttribute(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			ModInfoAttribute newAttr = null;
			foreach (var attr in optionsType.GetCustomAttributes(true))
				// Cross mod types need reflection
				if ((newAttr = ModInfoAttribute.CreateFrom(attr)) != null) break;
			return newAttr;
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
				dir = Path.Combine(KMod.Manager.GetDirectory(), modDLL.GetName()?.Name ?? "");
			return dir;
		}

#if !OPTIONS_ONLY
		/// <summary>
		/// Initializes and stores the options table for quicker lookups later.
		/// </summary>
		internal static void Init() {
			lock (PSharedData.GetLock(PRegistry.KEY_OPTIONS_LOCK)) {
				modOptions = PSharedData.GetData<OptionsTable>(PRegistry.KEY_OPTIONS_TABLE);
				PSharedData.PutData(PRegistry.KEY_OPTIONS_LATEST, typeof(POptions));
			}
		}
#endif

		/// <summary>
		/// Registers a class as a mod options class. The type is registered for its defining
		/// assembly, not for the calling assembly, for compatibility reasons.
		/// </summary>
		/// <param name="optionsType">The class which will represent the options for this mod.</param>
		public static void RegisterOptions(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
#if OPTIONS_ONLY
			var assembly = optionsType.Assembly;
			var id = Path.GetFileName(GetModDir(assembly));
			// Local options type
			if (modOptions == null)
				modOptions = new Dictionary<string, Type>(4);
			if (modOptions.ContainsKey(id))
				PUtil.LogWarning("Duplicate mod ID: " + id);
			else {
				// Add as options for this mod
				modOptions.Add(id, optionsType);
				PUtil.LogDebug("Registered mod options class {0} for {1}".F(optionsType.Name,
					assembly.GetName()?.Name));
			}
#else
			// In case this call is used before the library was initialized
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"RegisterOptions!");
			}
			var assembly = optionsType.Assembly;
			// Moving a mod to an archived version will technically trash the settings for
			// any users still on that version. Since Klei throws out the settings files
			// anyways upon any update this is not a huge issue however.
			var id = Path.GetFileName(GetModBaseDir(assembly));
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
					PUtil.LogDebug("Registered mod options class {0} for {1}".F(optionsType.
						Name, assembly.GetName()?.Name));
				}
			}
#endif
		}

		/// <summary>
		/// Reads a mod's settings from its configuration file. The calling assembly is used
		/// for compatibility reasons to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed).</returns>
		public static T ReadSettings<T>() where T : class {
			return ReadSettings<T>(Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Reads a mod's settings from its configuration file. The assembly defining T is used
		/// to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed).</returns>
		public static T ReadSettingsForAssembly<T>() where T : class {
			return ReadSettings<T>(typeof(T).Assembly);
		}

		/// <summary>
		/// Reads a mod's settings from its configuration file.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="assembly">The assembly used to look up the correct settings folder.</param>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed).</returns>
		internal static T ReadSettings<T>(Assembly assembly) where T : class {
			var type = typeof(T);
			return ReadSettings(GetConfigPath(GetConfigFileAttribute(type), assembly),
				type) as T;
		}

		/// <summary>
		/// Reads a mod's settings from its configuration file.
		/// </summary>
		/// <param name="path">The path to the settings file.</param>
		/// <param name="optionsType">The options type.</param>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed)</returns>
		internal static object ReadSettings(string path, Type optionsType) {
			object options = null;
			try {
				using (var jr = new JsonTextReader(File.OpenText(path))) {
					var serializer = new JsonSerializer { MaxDepth = MAX_SERIALIZATION_DEPTH };
					// Deserialize from stream avoids reading file text into memory
					options = serializer.Deserialize(jr, optionsType);
				}
			} catch (FileNotFoundException) {
#if DEBUG
				PUtil.LogDebug("{0} was not found; using default settings".F(Path.GetFileName(
					path)));
#endif
			} catch (UnauthorizedAccessException e) {
				// Options will be set to defaults
				PUtil.LogExcWarn(e);
			} catch (IOException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			}
			return options;
		}

		/// <summary>
		/// Shows a mod options dialog now, as if Options was used inside the Mods menu.
		/// </summary>
		/// <param name="optionsType">The type of the options to show.</param>
		/// <param name="title">The title to show in the dialog.</param>
		/// <param name="onClose">The method to call when the dialog is closed.</param>
		private static void ShowDialog(Type optionsType, string title, Action<object> onClose)
		{
			var handler = new RuntimeOptionsHandler(GetModDir(optionsType.Assembly), title) {
				OnClose = onClose
			};
			new OptionsDialog(optionsType, handler).OnModOptions(null);
		}

		/// <summary>
		/// Shows a mod options dialog now, as if Options was used inside the Mods menu.
		/// </summary>
		/// <param name="optionsType">The type of the options to show. The mod to configure,
		/// configuration directory, and so forth will be retrieved from the provided type.
		/// This type must be the same type configured in RegisterOptions for the mod.</param>
		/// <param name="title">The title to show in the dialog. If null, a default title
		/// will be used.</param>
		/// <param name="onClose">The method to call when the dialog is closed.</param>
		public static void ShowNow(Type optionsType, string title = null,
				Action<object> onClose = null) {
#if OPTIONS_ONLY
			ShowDialog(optionsType, title, onClose);
#else
			Type forwardType;
			if (optionsType == null)
				throw new ArgumentNullException("optionsType");
			// Find latest version if possible
			lock (PSharedData.GetLock(PRegistry.KEY_OPTIONS_LOCK)) {
				forwardType = PSharedData.GetData<Type>(PRegistry.KEY_OPTIONS_LATEST);
			}
			if (forwardType == null)
				forwardType = typeof(POptions);
			try {
				var method = forwardType.GetMethodSafe(nameof(ShowDialog), true, typeof(Type),
					typeof(string), typeof(Action<object>));
				// Forward call to that version
				if (method != null)
					method.Invoke(null, new object[] { optionsType, title, onClose });
				else {
					PUtil.LogWarning("No call to show options dialog found!");
					ShowDialog(optionsType, title, onClose);
				}
			} catch (AmbiguousMatchException e) {
				PUtil.LogException(e);
			}
#endif
		}

		/// <summary>
		/// Writes a mod's settings to its configuration file. The calling assembly is used for
		/// compatibility reasons to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="settings">The settings to write.</param>
		public static void WriteSettings<T>(T settings) where T : class {
			WriteSettings(settings, Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Writes a mod's settings to its configuration file. The assembly defining T is used
		/// to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="settings">The settings to write.</param>
		public static void WriteSettingsForAssembly<T>(T settings) where T : class {
			WriteSettings(settings, typeof(T).Assembly);
		}

		/// <summary>
		/// Writes a mod's settings to its configuration file. The calling assembly is used for
		/// compatibility reasons to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="assembly">The assembly used to look up the correct settings folder.</param>
		/// <param name="settings">The settings to write.</param>
		internal static void WriteSettings<T>(T settings, Assembly assembly) where T : class {
			var attr = GetConfigFileAttribute(typeof(T));
			WriteSettings(settings, GetConfigPath(attr, assembly), attr?.IndentOutput ??
				false);
		}

		/// <summary>
		/// Writes a mod's settings to its configuration file.
		/// </summary>
		/// <param name="settings">The settings to write.</param>
		/// <param name="path">The path to the settings file.</param>
		/// <param name="indent">true to indent the output, or false to leave it in one line.</param>
		internal static void WriteSettings(object settings, string path, bool indent = false) {
			if (settings != null)
				try {
					using (var jw = new JsonTextWriter(File.CreateText(path))) {
						var serializer = new JsonSerializer {
							MaxDepth = MAX_SERIALIZATION_DEPTH
						};
						serializer.Formatting = indent ? Formatting.Indented : Formatting.None;
						// Serialize from stream avoids creating file text in memory
						serializer.Serialize(jw, settings);
					}
				} catch (UnauthorizedAccessException e) {
					// Options cannot be set
					PUtil.LogExcWarn(e);
				} catch (IOException e) {
					// Options cannot be set
					PUtil.LogExcWarn(e);
				} catch (JsonException e) {
					// Options cannot be set
					PUtil.LogExcWarn(e);
				}
		}

		/// <summary>
		/// A class which can be used by mods to maintain a singleton of their options. This
		/// class should be the superclass of the mod options class, and &lt;T&gt; should be
		/// the type of the options class to store.
		/// </summary>
		/// <typeparam name="T">The mod options class to wrap.</typeparam>
		public abstract class SingletonOptions<T> where T : class, new() {
			/// <summary>
			/// The only instance of the singleton options.
			/// </summary>
			protected static T instance;

			/// <summary>
			/// Retrieves the program options, or lazily initializes them if not yet loaded.
			/// </summary>
			public static T Instance {
				get {
					if (instance == null)
						instance = ReadSettings<T>() ?? new T();
					return instance;
				}
				protected set {
					if (value != null)
						instance = value;
				}
			}
		}
	}
}
