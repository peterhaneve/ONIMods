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
using Newtonsoft.Json;
using PeterHan.PLib.Core;
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
	public sealed class POptions : PForwardedComponent {
		/// <summary>
		/// The configuration file name used by default for classes that do not specify
		/// otherwise. This file name is case sensitive.
		/// </summary>
		public const string CONFIG_FILE_NAME = "config.json";

		/// <summary>
		/// The maximum nested class depth which will be serialized in mod options to avoid
		/// infinite loops.
		/// </summary>
		public const int MAX_SERIALIZATION_DEPTH = 8;

		/// <summary>
		/// The margins around the Options button.
		/// </summary>
		internal static readonly RectOffset OPTION_BUTTON_MARGIN = new RectOffset(11, 11, 5, 5);

		/// <summary>
		/// The shared mod configuration folder, which works between archived versions and
		/// local/dev/Steam.
		/// </summary>
		public const string SHARED_CONFIG_FOLDER = "config";

		/// <summary>
		/// The version of this component. Uses the running PLib version.
		/// </summary>
		internal static readonly Version VERSION = new Version(PVersion.VERSION);

		/// <summary>
		/// The instantiated copy of this class.
		/// </summary>
		internal static POptions Instance { get; private set; }

		/// <summary>
		/// Applied to ModsScreen if mod options are registered, after BuildDisplay runs.
		/// </summary>
		private static void BuildDisplay_Postfix(GameObject ___entryPrefab,
				System.Collections.IEnumerable ___displayedMods) {
			if (Instance != null) {
				int index = 0;
				// Harmony does not check the type at all on accessing private fields with ___
				foreach (var displayedMod in ___displayedMods)
					Instance.AddModOptions(displayedMod, index++, ___entryPrefab);
			}
		}

		/// <summary>
		/// Retrieves the configuration file path used by PLib Options for a specified type.
		/// </summary>
		/// <param name="optionsType">The options type stored in the config file.</param>
		/// <returns>The path to the configuration file that will be used by PLib for that
		/// mod's config.</returns>
		public static string GetConfigFilePath(Type optionsType) {
			return GetConfigPath(optionsType.GetCustomAttribute<ConfigFileAttribute>(),
				optionsType.Assembly);
		}

		/// <summary>
		/// Attempts to find the mod which owns the specified type.
		/// </summary>
		/// <param name="optionsType">The type to look up.</param>
		/// <returns>The Mod that owns it, or null if no owning mod could be found, such as for
		/// types in System or Assembly-CSharp.</returns>
		internal static KMod.Mod GetModFromType(Type optionsType) {
			if (optionsType == null)
				throw new ArgumentNullException(nameof(optionsType));
			var sd = PRegistry.Instance.GetSharedData(typeof(POptions).FullName);
			// Look up mod in the shared data
			if (!(sd is IDictionary<Assembly, KMod.Mod> lookup) || !lookup.TryGetValue(
					optionsType.Assembly, out KMod.Mod result))
				result = null;
			return result;
		}

		/// <summary>
		/// Retrieves the configuration file path used by PLib Options for a specified type.
		/// </summary>
		/// <param name="attr">The config file attribute for that type.</param>
		/// <param name="modAssembly">The assembly to use for determining the path.</param>
		/// <returns>The path to the configuration file that will be used by PLib for that
		/// mod's config.</returns>
		private static string GetConfigPath(ConfigFileAttribute attr, Assembly modAssembly) {
			string path, name = modAssembly.GetNameSafe();
			path = (name != null && (attr?.UseSharedConfigLocation == true)) ?
				Path.Combine(KMod.Manager.GetDirectory(), SHARED_CONFIG_FOLDER, name) :
				PUtil.GetModPath(modAssembly);
			return Path.Combine(path, attr?.ConfigFileName ?? CONFIG_FILE_NAME);
		}

		/// <summary>
		/// Reads a mod's settings from its configuration file. The assembly defining T is used
		/// to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <returns>The settings read, or null if they could not be read (e.g. newly installed).</returns>
		public static T ReadSettings<T>() where T : class {
			var type = typeof(T);
			return ReadSettings(GetConfigPath(type.GetCustomAttribute<ConfigFileAttribute>(),
				type.Assembly), type) as T;
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
			} catch (DirectoryNotFoundException) {
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
		/// <param name="optionsType">The type of the options to show. The mod to configure,
		/// configuration directory, and so forth will be retrieved from the provided type.
		/// This type must be the same type configured in RegisterOptions for the mod.</param>
		/// <param name="onClose">The method to call when the dialog is closed.</param>
		public static void ShowDialog(Type optionsType, Action<object> onClose = null) {
			var args = new OpenDialogArgs(optionsType, onClose);
			var allOptions = PRegistry.Instance.GetAllComponents(typeof(POptions).FullName);
			if (allOptions != null)
				foreach (var mod in allOptions)
					mod?.Process(0, args);
		}

		/// <summary>
		/// Writes a mod's settings to its configuration file. The assembly defining T is used
		/// to resolve the proper settings folder.
		/// </summary>
		/// <typeparam name="T">The type of the settings object.</typeparam>
		/// <param name="settings">The settings to write.</param>
		public static void WriteSettings<T>(T settings) where T : class {
			var attr = typeof(T).GetCustomAttribute<ConfigFileAttribute>();
			WriteSettings(settings, GetConfigPath(attr, typeof(T).Assembly), attr?.
				IndentOutput ?? false);
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
					// SharedConfigLocation
					Directory.CreateDirectory(Path.GetDirectoryName(path));
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
		/// Maps mod static IDs to their options.
		/// </summary>
		private readonly IDictionary<string, Type> modOptions;

		/// <summary>
		/// Maps mod assemblies to handlers that can fire their options. Only populated in
		/// the instantiated copy of POptions.
		/// </summary>
		private readonly IDictionary<string, ModOptionsHandler> registered;

		public override Version Version => VERSION;

		public POptions() {
			modOptions = new Dictionary<string, Type>(8);
			registered = new Dictionary<string, ModOptionsHandler>(32);
			InstanceData = modOptions;
		}

		/// <summary>
		/// Adds the Options button to the Mods screen.
		/// </summary>
		/// <param name="modEntry">The mod entry where the button should be added.</param>
		/// <param name="fallbackIndex">The index to use if it cannot be determined from the entry.</param>
		/// <param name="parent">The parent where the entries were added, used only if the
		/// fallback index is required.</param>
		private void AddModOptions(object modEntry, int fallbackIndex, GameObject parent) {
			var mods = Global.Instance.modManager?.mods;
			if (!PPatchTools.TryGetFieldValue(modEntry, "mod_index", out int index))
				index = fallbackIndex;
			if (!PPatchTools.TryGetFieldValue(modEntry, "rect_transform", out Transform
					transform))
				transform = parent.transform.GetChild(index);
			if (mods != null && index >= 0 && index < mods.Count && transform != null) {
				var modSpec = mods[index];
				string label = modSpec.staticID;
				if (modSpec.IsEnabledForActiveDlc() && registered.TryGetValue(label,
						out ModOptionsHandler handler)) {
#if DEBUG
					PUtil.LogDebug("Adding options for mod: {0}".F(modSpec.staticID));
#endif
					// Create delegate to open settings dialog
					new PButton("ModSettingsButton") {
						FlexSize = Vector2.up, OnClick = handler.ShowDialog,
						ToolTip = PLibStrings.DIALOG_TITLE.text.F(modSpec.title), Text =
						CultureInfo.CurrentCulture.TextInfo.ToTitleCase(PLibStrings.
						BUTTON_OPTIONS.text.ToLower()), Margin = OPTION_BUTTON_MARGIN
						// Move before the subscription and enable button
					}.SetKleiPinkStyle().AddTo(transform.gameObject, 4);
				}
			}
		}

		/// <summary>
		/// Initializes and stores the options table for quicker lookups later.
		/// </summary>
		public override void Initialize(Harmony plibInstance) {
			Instance = this;

			registered.Clear();
			SetSharedData(PUtil.CreateAssemblyToModTable());
			foreach (var optionsProvider in PRegistry.Instance.GetAllComponents(ID)) {
				var options = optionsProvider.GetInstanceData<IDictionary<string, Type>>();
				if (options != null)
					// Map the static ID to the mod's option type and fire the correct handler
					foreach (var pair in options) {
						string label = pair.Key;
						if (registered.ContainsKey(label))
							PUtil.LogWarning("Mod {0} already has options registered - only one option type per mod".
								F(label ?? "?"));
						else
							registered.Add(label, new ModOptionsHandler(optionsProvider,
								pair.Value));
					}
			}

			plibInstance.Patch(typeof(ModsScreen), "BuildDisplay", postfix: PatchMethod(nameof(
				BuildDisplay_Postfix)));
		}

		public override void Process(uint operation, object args) {
			// POptions is no longer forwarded, show the dialog from the assembly that has the
			// options type - ignore calls that are not for our mod
			if (operation == 0 && PPatchTools.TryGetPropertyValue(args, nameof(OpenDialogArgs.
					OptionsType), out Type forType)) {
				foreach (var pair in modOptions)
					// Linear search is not phenomenal, but there is usually only one options
					// type per instance
					if (pair.Value == forType) {
						var dialog = new OptionsDialog(forType);
						if (PPatchTools.TryGetPropertyValue(args, nameof(OpenDialogArgs.
								OnClose), out Action<object> handler))
							dialog.OnClose = handler;
						dialog.ShowDialog();
						break;
					}
			}
		}

		/// <summary>
		/// Registers a class as a mod options class. The type is registered for the mod
		/// instance specified, which is easily available in OnLoad.
		/// </summary>
		/// <param name="mod">The mod for which the type will be registered.</param>
		/// <param name="optionsType">The class which will represent the options for this mod.</param>
		public void RegisterOptions(KMod.UserMod2 mod, Type optionsType) {
			var kmod = mod?.mod;
			if (optionsType == null)
				throw new ArgumentNullException(nameof(optionsType));
			if (kmod == null)
				throw new ArgumentNullException(nameof(mod));
			RegisterForForwarding();
			string id = kmod.staticID;
			if (modOptions.TryGetValue(id, out Type curType))
				PUtil.LogWarning("Mod {0} already has options type {1}".F(id, curType.
					FullName));
			else {
				modOptions.Add(id, optionsType);
				PUtil.LogDebug("Registered mod options class {0} for {1}".F(optionsType.
					FullName, id));
			}
		}

		/// <summary>
		/// Opens the mod options dialog for a specific mod assembly.
		/// </summary>
		private sealed class ModOptionsHandler {
			/// <summary>
			/// The type whose options will be shown.
			/// </summary>
			private readonly Type forType;

			/// <summary>
			/// The options instance that will handle the dialog.
			/// </summary>
			private readonly PForwardedComponent options;

			internal ModOptionsHandler(PForwardedComponent options, Type forType) {
				this.forType = forType ?? throw new ArgumentNullException(nameof(forType));
				this.options = options ?? throw new ArgumentNullException(nameof(options));
			}

			/// <summary>
			/// Shows the options dialog.
			/// </summary>
			internal void ShowDialog(GameObject _) {
				options.Process(0, new OpenDialogArgs(forType, null));
			}

			public override string ToString() {
				return "ModOptionsHandler[Type={0}]".F(forType);
			}
		}

		/// <summary>
		/// The arguments to be passed with message SHOW_DIALOG_MOD.
		/// </summary>
		private sealed class OpenDialogArgs {
			/// <summary>
			/// The handler (if not null) to be called when the dialog is closed.
			/// </summary>
			public Action<object> OnClose { get; }

			/// <summary>
			/// The mod options type to show.
			/// </summary>
			public Type OptionsType { get; }

			public OpenDialogArgs(Type optionsType, Action<object> onClose) {
				OnClose = onClose;
				OptionsType = optionsType ?? throw new ArgumentNullException(
					nameof(optionsType));
			}

			public override string ToString() {
				return "OpenDialogArgs[Type={0}]".F(OptionsType);
			}
		}
	}
}
