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

using Newtonsoft.Json;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// A dialog for handling mod options events.
	/// </summary>
	internal sealed class OptionsDialog {
		/// <summary>
		/// Builds the options entries from the type.
		/// </summary>
		/// <param name="forType">The type of the options class.</param>
		/// <returns>A list of all public properties annotated for options dialogs.</returns>
		private static ICollection<OptionsEntry> BuildOptions(Type forType) {
			var entries = new List<OptionsEntry>(16);
			OptionAttribute oa;
			foreach (var prop in forType.GetProperties()) {
				// Must have the annotation
				foreach (var attr in prop.GetCustomAttributes(false))
					if ((oa = OptionsEntry.GetTitle(attr)) != null) {
						// Attempt to find a class that will represent it
						var entry = CreateOptions(prop.Name, prop.PropertyType, oa);
						if (entry != null)
							entries.Add(entry);
						break;
					}
			}
			return entries;
		}

		/// <summary>
		/// Creates an options entry wrapper for the specified property.
		/// </summary>
		/// <param name="field">The property name.</param>
		/// <param name="type">The property type.</param>
		/// <param name="oa">The option title and tool tip.</param>
		/// <returns>An options wrapper, or null if none can handle this type.</returns>
		private static OptionsEntry CreateOptions(string field, Type type, OptionAttribute oa)
		{
			OptionsEntry entry = null;
			// Enumeration type
			if (type.IsEnum)
				entry = new SelectOneOptionsEntry(field, oa.Title, oa.Tooltip, type);
			return entry;
		}

		/// <summary>
		/// The mod whose settings are being modified.
		/// </summary>
		private readonly KMod.Mod modSpec;

		/// <summary>
		/// The option entries in the dialog.
		/// </summary>
		private readonly ICollection<OptionsEntry> optionEntries;

		/// <summary>
		/// The options read from the config. It might contain hidden options so preserve its
		/// contents here.
		/// </summary>
		private object options;

		/// <summary>
		/// The type used to determine which options are visible.
		/// </summary>
		private readonly Type optionsType;

		/// <summary>
		/// The path to the options file. It may not exist.
		/// </summary>
		private readonly string path;

		internal OptionsDialog(Type optionsType, KMod.Mod modSpec) {
			this.modSpec = modSpec ?? throw new ArgumentNullException("modSpec");
			this.optionsType = optionsType ?? throw new ArgumentNullException(
				"optionsType");
			optionEntries = BuildOptions(optionsType);
			options = null;
			path = Path.Combine(modSpec.file_source.GetRoot(), POptions.CONFIG_FILE);
		}

		/// <summary>
		/// Creates an options object using the default constructor if possible.
		/// </summary>
		private void CreateOptions() {
			try {
				var cons = optionsType.GetConstructor(Type.EmptyTypes);
				if (cons != null)
					options = cons.Invoke(null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (MemberAccessException e) {
				// Other mod's error
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Triggered when the Mod Options button is clicked.
		/// </summary>
		/// <param name="ignore">The source button.</param>
		public void OnModOptions(GameObject ignore) {
			// Ensure that it is on top of other screens (which may be +100 modal)
			var dialog = new PDialog("ModOptions") {
				Title = POptions.DIALOG_TITLE.text.F(modSpec.title), Size = POptions.
				SETTINGS_DIALOG_SIZE, SortKey = 150.0f
			}.AddButton("ok", STRINGS.UI.CONFIRMDIALOG.OK, POptions.TOOLTIP_OK).
			AddButton(PDialog.DIALOG_KEY_CLOSE, STRINGS.UI.CONFIRMDIALOG.CANCEL,
				POptions.TOOLTIP_CANCEL);
			// For each option, add its UI component to panel
			foreach (var entry in optionEntries)
				dialog.Body.AddChild(entry.GetUIEntry());
			ReadOptions();
			if (options == null)
				CreateOptions();
			dialog.DialogClosed += OnOptionsSelected;
			// Manually build the dialog so the options can be updated after realization
			var obj = dialog.Build();
			UpdateOptions();
			obj.GetComponent<KScreen>().Activate();
		}

		/// <summary>
		/// Invoked when the dialog is closed.
		/// </summary>
		/// <param name="action">The action key taken.</param>
		private void OnOptionsSelected(string action) {
			if (action == "ok")
				// Save changes to mod options
				WriteOptions();
		}

		/// <summary>
		/// Reads the mod options from its config file.
		/// </summary>
		private void ReadOptions() {
			try {
				// Cannot use POptions.ReadSettings because we already have the path
				using (var jr = new JsonTextReader(File.OpenText(path))) {
					var serializer = new JsonSerializer { MaxDepth = 8 };
					// Deserialize from stream avoids reading file text into memory
					options = serializer.Deserialize(jr, optionsType);
				}
			} catch (FileNotFoundException) {
				PUtil.LogDebug("{0} was not found; using default settings".F(POptions.
					CONFIG_FILE));
			} catch (IOException e) {
				// Options will be set to defaults
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				// Again set defaults
				PUtil.LogExcWarn(e);
			}
		}

		/// <summary>
		/// Updates the dialog with the latest options from the file.
		/// </summary>
		private void UpdateOptions() {
			// Read into local options
			if (options != null)
				foreach (var option in optionEntries)
					option.ReadFrom(options);
		}

		/// <summary>
		/// Writes the mod options to its config file.
		/// </summary>
		private void WriteOptions() {
			if (options != null)
				// Update from local options
				foreach (var option in optionEntries)
					option.WriteTo(options);
			try {
				using (var jw = new JsonTextWriter(File.CreateText(path))) {
					var serializer = new JsonSerializer { MaxDepth = 8 };
					jw.CloseOutput = true;
					// Write to stream
					if (options != null)
						serializer.Serialize(jw, options);
				}
			} catch (IOException e) {
				// TODO popup a warning
				PUtil.LogExcWarn(e);
			} catch (JsonException e) {
				PUtil.LogExcWarn(e);
			}
		}
	}
}
