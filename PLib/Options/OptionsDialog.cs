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

using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

using OptionsList = System.Collections.Generic.ICollection<PeterHan.PLib.Options.OptionsEntry>;

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
		private static IDictionary<string, OptionsList> BuildOptions(Type forType) {
			var entries = new SortedList<string, OptionsList>(8);
			OptionAttribute oa;
			foreach (var prop in forType.GetProperties())
				// Must have the annotation
				foreach (var attr in prop.GetCustomAttributes(false))
					if ((oa = OptionAttribute.CreateFrom(attr)) != null) {
						// Attempt to find a class that will represent it
						var entry = CreateOptions(prop, oa);
						if (entry != null) {
							string category = entry.Category ?? "";
							// Add this category if it does not exist
							if (!entries.TryGetValue(category, out OptionsList inCat)) {
								inCat = new List<OptionsEntry>(16);
								entries.Add(category, inCat);
							}
							inCat.Add(entry);
						}
						break;
					}
			return entries;
		}

		/// <summary>
		/// First looks to see if the string exists in the string database; if it does, returns
		/// the localized value, otherwise returns the string unmodified.
		/// 
		/// This method is somewhat slow. Cache the result if possible.
		/// </summary>
		/// <param name="keyOrValue">The string key to check.</param>
		/// <returns>The string value with that key, or the key if there is no such localized
		/// string value.</returns>
		internal static string LookInStrings(string keyOrValue) {
			string result = keyOrValue;
			if (Strings.TryGet(keyOrValue, out StringEntry entry))
				result = entry.String;
			return result;
		}

		/// <summary>
		/// Creates an options entry wrapper for the specified property.
		/// </summary>
		/// <param name="info">The property to wrap.</param>
		/// <param name="oa">The option title and tool tip.</param>
		/// <returns>An options wrapper, or null if none can handle this type.</returns>
		private static OptionsEntry CreateOptions(PropertyInfo info, OptionAttribute oa) {
			OptionsEntry entry = null;
			Type type = info.PropertyType;
			string field = info.Name;
			// Enumeration type
			if (type.IsEnum)
				entry = new SelectOneOptionsEntry(field, oa, type);
			else if (type == typeof(bool))
				entry = new CheckboxOptionsEntry(field, oa);
			else if (type == typeof(int))
				entry = new IntOptionsEntry(oa, info);
			else if (type == typeof(float))
				entry = new FloatOptionsEntry(oa, info);
			else if (type == typeof(string))
				entry = new StringOptionsEntry(oa, info);
			return entry;
		}

		/// <summary>
		/// Saves the mod enabled settings and restarts the game.
		/// </summary>
		private static void SaveAndRestart() {
			Global.Instance?.modManager?.Save();
			App.instance.Restart();
		}

		/// <summary>
		/// The currently active dialog.
		/// </summary>
		private KScreen dialog;

		/// <summary>
		/// The mod whose settings are being modified.
		/// </summary>
		private readonly KMod.Mod modSpec;

		/// <summary>
		/// The option entries in the dialog.
		/// </summary>
		private readonly IDictionary<string, OptionsList> optionCategories;

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

		/// <summary>
		/// The config file attribute for the options type, if present.
		/// </summary>
		private readonly ConfigFileAttribute typeAttr;

		internal OptionsDialog(Type optionsType, KMod.Mod modSpec) {
			dialog = null;
			this.modSpec = modSpec ?? throw new ArgumentNullException("modSpec");
			this.optionsType = optionsType ?? throw new ArgumentNullException("optionsType");
			optionCategories = BuildOptions(optionsType);
			options = null;
			// Determine config location
			typeAttr = POptions.GetConfigFileAttribute(optionsType);
			var src = modSpec.file_source;
			if (src == null)
				path = null;
			else
				path = Path.Combine(src.GetRoot(), typeAttr?.ConfigFileName ?? POptions.
					CONFIG_FILE_NAME);
		}

		/// <summary>
		/// Adds a category header to the dialog.
		/// </summary>
		/// <param name="body">The parent of the header.</param>
		/// <param name="category">The header title.</param>
		/// <param name="contents">The panel containing the options in this category.</param>
		private void AddCategoryHeader(PPanel body, string category, PPanel contents) {
			if (!string.IsNullOrEmpty(category)) {
				var handler = new CategoryExpandHandler();
				body.AddChild(new PPanel("CategorySelect") {
					FlexSize = Vector2.right, Alignment = TextAnchor.MiddleLeft, Spacing = 5,
					Direction = PanelDirection.Horizontal, Margin = new RectOffset(0, 0, 0, 2)
				}.AddChild(new PToggle("CategoryToggle") {
					Color = PUITuning.Colors.ComponentDarkStyle, DynamicSize = false,
					ToolTip = PUIStrings.TOOLTIP_TOGGLE, Size = new Vector2(12.0f, 12.0f),
					InitialState = true, OnStateChanged = handler.OnExpandContract
				}).AddChild(new PLabel("CategoryHeader") {
					Text = LookInStrings(category), TextStyle = POptions.TITLE_STYLE,
					TextAlignment = TextAnchor.LowerCenter, DynamicSize = true,
					FlexSize = Vector2.right,
				}));
				if (contents != null)
					contents.OnRealize += handler.OnRealizePanel;
			}
			body.AddChild(contents);
		}

		/// <summary>
		/// Checks the mod config class for the [RestartRequired] attribute, and brings up a
		/// restart dialog if necessary.
		/// </summary>
		private void CheckForRestart() {
			if (options != null) {
				string rr = typeof(RestartRequiredAttribute).FullName;
				bool restartRequired = false;
				// Check for [RestartRequired]
				foreach (var attr in options.GetType().GetCustomAttributes(true))
					if (attr.GetType().FullName == rr) {
						restartRequired = true;
						break;
					}
				if (restartRequired)
					// Prompt user to restart
					PUIElements.ShowConfirmDialog(null, PUIStrings.RESTART_REQUIRED,
						SaveAndRestart, null, PUIStrings.RESTART_OK, PUIStrings.
						RESTART_CANCEL);
			}
		}

		/// <summary>
		/// Closes the current dialog.
		/// </summary>
		private void CloseDialog() {
			if (dialog != null) {
				dialog.Deactivate();
				dialog = null;
			}
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
				PUtil.LogExcWarn(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (MemberAccessException e) {
				// Other mod's error
				PUtil.LogException(e);
			}
		}

		/// <summary>
		/// Fills in the actual mod option fields.
		/// </summary>
		/// <param name="pDialog">The dialog to populate.</param>
		private void FillModOptions(PDialog pDialog) {
			PPanel body = pDialog.Body, current, contents;
			var margin = body.Margin;
			// For each option, add its UI component to panel
			body.Margin = new RectOffset();
			var scrollBody = new PPanel("ScrollContent") {
				Spacing = 10, Direction = PanelDirection.Vertical, Alignment = TextAnchor.
				UpperCenter, FlexSize = Vector2.right
			};
			// Display all categories
			foreach (var catEntries in optionCategories) {
				string category = catEntries.Key;
				if (catEntries.Value.Count > 0) {
					current = new PPanel("Category_" + category) {
						Alignment = TextAnchor.UpperCenter, Spacing = 5, Margin = margin,
						BackColor = PUITuning.Colors.DialogDarkBackground,
						FlexSize = Vector2.right
					};
					contents = new PPanel("Entries") {
						Alignment = TextAnchor.UpperCenter, Spacing = 5, FlexSize =
						Vector2.right
					};
					AddCategoryHeader(current, catEntries.Key, contents);
					foreach (var entry in catEntries.Value)
						contents.AddChild(entry.GetUIEntry());
					scrollBody.AddChild(current);
				}
			}
			// Manual config button
			scrollBody.AddChild(new PButton("ManualConfig") {
				Text = PUIStrings.BUTTON_MANUAL, ToolTip = PUIStrings.TOOLTIP_MANUAL,
				OnClick = OnManualConfig, TextAlignment = TextAnchor.MiddleCenter, Margin =
				PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle());
			body.AddChild(new PScrollPane() {
				ScrollHorizontal = false, ScrollVertical = true, Child = scrollBody,
				FlexSize = Vector2.right, TrackSize = 8, AlwaysShowHorizontal = false,
				AlwaysShowVertical = false
			});
		}

		/// <summary>
		/// Triggered when the Mod Options button is clicked.
		/// </summary>
		public void OnModOptions(GameObject _) {
			if (path != null) {
				// Close current dialog if open
				CloseDialog();
				// Ensure that it is on top of other screens (which may be +100 modal)
				var pDialog = new PDialog("ModOptions") {
					Title = PUIStrings.DIALOG_TITLE.text.F(modSpec.title), Size = POptions.
					SETTINGS_DIALOG_SIZE, SortKey = 150.0f, DialogBackColor = PUITuning.Colors.
					OptionsBackground, DialogClosed = OnOptionsSelected, MaxSize = POptions.
					SETTINGS_DIALOG_MAX_SIZE, RoundToNearestEven = true
				}.AddButton("ok", STRINGS.UI.CONFIRMDIALOG.OK, PUIStrings.TOOLTIP_OK,
					PUITuning.Colors.ButtonPinkStyle).AddButton(PDialog.DIALOG_KEY_CLOSE,
					STRINGS.UI.CONFIRMDIALOG.CANCEL, PUIStrings.TOOLTIP_CANCEL,
					PUITuning.Colors.ButtonBlueStyle);
				FillModOptions(pDialog);
				options = POptions.ReadSettings(path, optionsType);
				if (options == null)
					CreateOptions();
				// Manually build the dialog so the options can be updated after realization
				var obj = pDialog.Build();
				UpdateOptions();
				dialog = obj.GetComponent<KScreen>();
				dialog.Activate();
			}
		}

		/// <summary>
		/// Invoked when the manual config button is pressed.
		/// </summary>
		private void OnManualConfig(GameObject _) {
			string uri = null;
			try {
				uri = new Uri(Path.GetDirectoryName(path)).AbsoluteUri;
			} catch (UriFormatException e) {
				PUtil.LogWarning("Unable to convert parent of " + path + " to a URI:");
				PUtil.LogExcWarn(e);
			}
			if (!string.IsNullOrEmpty(uri)) {
				// Open the config folder, opening the file itself might start an unknown
				// editor which could execute the json somehow...
				WriteOptions();
				CloseDialog();
				PUtil.LogDebug("Opening config folder: " + uri);
				Application.OpenURL(uri);
				CheckForRestart();
			}
		}

		/// <summary>
		/// Invoked when the dialog is closed.
		/// </summary>
		/// <param name="action">The action key taken.</param>
		private void OnOptionsSelected(string action) {
			if (action == "ok") {
				// Save changes to mod options
				WriteOptions();
				CheckForRestart();
			}
		}

		/// <summary>
		/// Updates the dialog with the latest options from the file.
		/// </summary>
		private void UpdateOptions() {
			// Read into local options
			if (options != null)
				foreach (var catEntries in optionCategories)
					foreach (var option in catEntries.Value)
						option.ReadFrom(options);
		}

		/// <summary>
		/// Writes the mod options to its config file.
		/// </summary>
		private void WriteOptions() {
			if (options != null)
				// Update from local options
				foreach (var catEntries in optionCategories)
					foreach (var option in catEntries.Value)
						option.WriteTo(options);
			POptions.WriteSettings(options, path, typeAttr?.IndentOutput ?? false);
		}

		/// <summary>
		/// Handles events for expanding and contracting buttons.
		/// </summary>
		private sealed class CategoryExpandHandler {
			/// <summary>
			/// The realized panel containing the options.
			/// </summary>
			private GameObject contents;

			/// <summary>
			/// Fired when the button is expanded or contracted.
			/// </summary>
			/// <param name="on">true if the button is on, or false if it is off.</param>
			public void OnExpandContract(GameObject _, bool on) {
				// Set scale to 0% or 100% depending on "on"
				var scale = on ? Vector3.one : Vector3.zero;
				if (contents != null)
					contents.transform.localScale = scale;
			}

			/// <summary>
			/// Fired when the body is realized.
			/// </summary>
			/// <param name="panel">The realized body of the category.</param>
			public void OnRealizePanel(GameObject panel) {
				contents = panel;
			}
		}
	}
}
