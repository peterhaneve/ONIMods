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
		/// The color of option category titles.
		/// </summary>
		private static readonly Color CATEGORY_TITLE_COLOR = new Color32(143, 150, 175, 255);

		/// <summary>
		/// The text style applied to option category titles.
		/// </summary>
		private static readonly TextStyleSetting CATEGORY_TITLE_STYLE;

		/// <summary>
		/// The margins inside the colored boxes in each config section.
		/// </summary>
		private static readonly int CATEGORY_MARGIN = 8;

		/// <summary>
		/// The size of the mod preview image displayed.
		/// </summary>
		private static readonly Vector2 MOD_IMAGE_SIZE = new Vector2(192.0f, 192.0f);

		/// <summary>
		/// The margins between the dialog edge and the colored boxes in each config section.
		/// </summary>
		private static readonly int OUTER_MARGIN = 10;

		/// <summary>
		/// The default size of the Mod Settings dialog.
		/// </summary>
		private static readonly Vector2 SETTINGS_DIALOG_SIZE = new Vector2(320.0f, 200.0f);

		/// <summary>
		/// The maximum size of the Mod Settings dialog before it gets scroll bars.
		/// </summary>
		private static readonly Vector2 SETTINGS_DIALOG_MAX_SIZE = new Vector2(800.0f, 600.0f);

		/// <summary>
		/// The size of the toggle button on each (non-default) config section.
		/// </summary>
		private static readonly Vector2 TOGGLE_SIZE = new Vector2(12.0f, 12.0f);

		static OptionsDialog() {
			CATEGORY_TITLE_STYLE = PUITuning.Fonts.UILightStyle.DeriveStyle(newColor:
				CATEGORY_TITLE_COLOR, style: TMPro.FontStyles.Bold);
		}

		/// <summary>
		/// Gets the text shown for a mod's version.
		/// </summary>
		/// <param name="optionsType">The type used for the mod settings.</param>
		/// <returns>The mod version description.</returns>
		private static string GetModVersionText(Type optionsType) {
			var asm = optionsType.Assembly;
			string version = asm.GetFileVersion();
			// Use FileVersion if available, else assembly version
			if (string.IsNullOrEmpty(version))
				version = string.Format(PUIStrings.MOD_ASSEMBLY_VERSION, asm.GetName().
					Version);
			else
				version = string.Format(PUIStrings.MOD_VERSION, version);
			return version;
		}

		/// <summary>
		/// Saves the mod enabled settings and restarts the game.
		/// </summary>
		private static void SaveAndRestart() {
#if OPTIONS_ONLY
			POptionsPatches.SaveMods();
#else
			PUtil.SaveMods();
#endif
			App.instance.Restart();
		}

		/// <summary>
		/// The currently active dialog.
		/// </summary>
		private KScreen dialog;

		/// <summary>
		/// The mod information attribute for the options type, if present.
		/// </summary>
		private readonly ModInfoAttribute infoAttr;

		/// <summary>
		/// The sprite to display for this mod.
		/// </summary>
		private Sprite modImage;

		/// <summary>
		/// The handler for settings changes.
		/// </summary>
		private readonly IOptionsHandler handler;

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
		/// The path to the options file. It might not exist.
		/// </summary>
		private readonly string path;

		/// <summary>
		/// The config file attribute for the options type, if present.
		/// </summary>
		private readonly ConfigFileAttribute typeAttr;

		internal OptionsDialog(Type optionsType, IOptionsHandler handler) {
			string root = handler.ConfigPath;
			dialog = null;
			modImage = null;
			this.handler = handler ?? throw new ArgumentNullException("handler");
			this.optionsType = optionsType ?? throw new ArgumentNullException("optionsType");
			optionCategories = OptionsEntry.BuildOptions(optionsType);
			options = null;
			// Determine config location
			infoAttr = POptions.GetModInfoAttribute(optionsType);
			typeAttr = POptions.GetConfigFileAttribute(optionsType);
			path = (root == null) ? null : Path.Combine(root, typeAttr?.ConfigFileName ??
				POptions.CONFIG_FILE_NAME);
		}

		/// <summary>
		/// Adds a category header to the dialog.
		/// </summary>
		/// <param name="container">The parent of the header.</param>
		/// <param name="category">The header title.</param>
		/// <param name="contents">The panel containing the options in this category.</param>
		private void AddCategoryHeader(PGridPanel container, string category,
				PGridPanel contents) {
			contents.AddColumn(new GridColumnSpec(flex: 1.0f)).AddColumn(new GridColumnSpec());
			if (!string.IsNullOrEmpty(category)) {
				bool state = !(infoAttr?.ForceCollapseCategories ?? false);
				var handler = new CategoryExpandHandler(state);
				container.AddColumn(new GridColumnSpec()).AddColumn(new GridColumnSpec(
					flex: 1.0f)).AddRow(new GridRowSpec()).AddRow(new GridRowSpec(flex: 1.0f));
				// Toggle is upper left, header is upper right
				container.AddChild(new PLabel("CategoryHeader") {
					Text = OptionsEntry.LookInStrings(category), TextStyle =
					CATEGORY_TITLE_STYLE, TextAlignment = TextAnchor.LowerCenter
				}.AddOnRealize(handler.OnRealizeHeader), new GridComponentSpec(0, 1) {
					Margin = new RectOffset(OUTER_MARGIN, OUTER_MARGIN, 0, 0)
				}).AddChild(new PToggle("CategoryToggle") {
					Color = PUITuning.Colors.ComponentDarkStyle, InitialState = state,
					ToolTip = PUIStrings.TOOLTIP_TOGGLE, Size = TOGGLE_SIZE,
					OnStateChanged = handler.OnExpandContract
				}.AddOnRealize(handler.OnRealizeToggle), new GridComponentSpec(0, 0));
				if (contents != null)
					contents.OnRealize += handler.OnRealizePanel;
				container.AddChild(contents, new GridComponentSpec(1, 0) { ColumnSpan = 2 });
			} else
				// Default of unconstrained fills the whole panel
				container.AddColumn(new GridColumnSpec(flex: 1.0f)).AddRow(new GridRowSpec(
					flex: 1.0f)).AddChild(contents, new GridComponentSpec(0, 0));
		}

		/// <summary>
		/// Fills in the mod info screen, assuming that infoAttr is non-null.
		/// </summary>
		/// <param name="dialog">The dialog to populate.</param>
		private void AddModInfoScreen(PDialog dialog) {
			string image = infoAttr.Image, version = GetModVersionText(optionsType);
			var body = dialog.Body;
			// Try to load the mod image sprite if possible
			if (modImage == null && !string.IsNullOrEmpty(image)) {
				string rootDir = handler.ConfigPath;
				modImage = PUIUtils.LoadSprite(rootDir == null ? image : Path.Combine(rootDir,
					image));
			}
			var websiteButton = new PButton("ModSite") {
				Text = PUIStrings.MOD_HOMEPAGE, ToolTip = PUIStrings.TOOLTIP_HOMEPAGE,
				OnClick = VisitModHomepage, Margin = PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle();
			var versionLabel = new PLabel("ModVersion") {
				Text = version, ToolTip = PUIStrings.TOOLTIP_VERSION, TextStyle = PUITuning.
				Fonts.UILightStyle, Margin = new RectOffset(0, 0, OUTER_MARGIN, 0)
			};
			// Find mod URL
			string modURL = infoAttr?.URL;
			if (string.IsNullOrEmpty(modURL))
				modURL = handler.DefaultURL;
			if (modImage != null) {
				// 2 rows and 1 column
				if (optionCategories.Count > 0)
					body.Direction = PanelDirection.Horizontal;
				var infoPanel = new PPanel("ModInfo") {
					FlexSize = Vector2.up, Direction = PanelDirection.Vertical,
					Alignment = TextAnchor.UpperCenter
				}.AddChild(new PLabel("ModImage") {
					SpriteSize = MOD_IMAGE_SIZE, TextAlignment = TextAnchor.UpperLeft,
					Margin = new RectOffset(0, OUTER_MARGIN, 0, OUTER_MARGIN),
					Sprite = modImage
				});
				if (!string.IsNullOrEmpty(modURL))
					infoPanel.AddChild(websiteButton);
				body.AddChild(infoPanel.AddChild(versionLabel));
			} else {
				if (!string.IsNullOrEmpty(modURL))
					body.AddChild(websiteButton);
				body.AddChild(versionLabel);
			}
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
				// dialog's game object is destroyed by Deactivate()
				dialog = null;
			}
			if (modImage != null) {
				UnityEngine.Object.Destroy(modImage);
				modImage = null;
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
		/// <param name="dialog">The dialog to populate.</param>
		private void FillModOptions(PDialog dialog) {
			var body = dialog.Body;
			var margin = new RectOffset(CATEGORY_MARGIN, CATEGORY_MARGIN, CATEGORY_MARGIN,
				CATEGORY_MARGIN);
			// For each option, add its UI component to panel
			body.Margin = new RectOffset();
			var scrollBody = new PPanel("ScrollContent") {
				Spacing = OUTER_MARGIN, Direction = PanelDirection.Vertical, Alignment =
				TextAnchor.UpperCenter, FlexSize = Vector2.right
			};
			var allOptions = (options == null) ? optionCategories : OptionsEntry.
				AddCustomOptions(options, optionCategories);
			// Display all categories
			foreach (var catEntries in allOptions) {
				string category = catEntries.Key;
				if (catEntries.Value.Count > 0) {
					string name = string.IsNullOrEmpty(category) ? "Default" : category;
					int i = 0;
					// Not optimal for layout performance, but the panel is needed to have a
					// different background color for each category "box"
					var container = new PGridPanel("Category_" + name) {
						Margin = margin, BackColor = PUITuning.Colors.DialogDarkBackground,
						FlexSize = Vector2.right
					};
					// Needs to be a separate panel so that it can be collapsed
					var contents = new PGridPanel("Entries") { FlexSize = Vector2.right };
					AddCategoryHeader(container, catEntries.Key, contents);
					foreach (var entry in catEntries.Value) {
						contents.AddRow(new GridRowSpec());
						entry.CreateUIEntry(contents, ref i);
						i++;
					}
					scrollBody.AddChild(container);
				}
			}
			// Manual config button
			scrollBody.AddChild(new PButton("ManualConfig") {
				Text = PUIStrings.BUTTON_MANUAL, ToolTip = PUIStrings.TOOLTIP_MANUAL,
				OnClick = OnManualConfig, TextAlignment = TextAnchor.MiddleCenter, Margin =
				PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle());
			body.AddChild(new PScrollPane() {
				ScrollHorizontal = false, ScrollVertical = allOptions.Count > 0,
				Child = scrollBody, FlexSize = Vector2.right, TrackSize = 8,
				AlwaysShowHorizontal = false, AlwaysShowVertical = false
			});
		}

		/// <summary>
		/// Triggered when the Mod Options button is clicked.
		/// </summary>
		public void OnModOptions(GameObject _) {
			if (path != null) {
				string title = handler.GetTitle(OptionsEntry.LookInStrings(infoAttr?.Title));
				if (string.IsNullOrEmpty(title))
					title = PUIStrings.BUTTON_OPTIONS;
				// Close current dialog if open
				CloseDialog();
				// Ensure that it is on top of other screens (which may be +100 modal)
				var pDialog = new PDialog("ModOptions") {
					Title = title, Size = SETTINGS_DIALOG_SIZE, SortKey = 150.0f,
					DialogBackColor = PUITuning.Colors.OptionsBackground,
					DialogClosed = OnOptionsSelected, MaxSize = SETTINGS_DIALOG_MAX_SIZE,
					RoundToNearestEven = true
				}.AddButton("ok", STRINGS.UI.CONFIRMDIALOG.OK, PUIStrings.TOOLTIP_OK,
					PUITuning.Colors.ButtonPinkStyle).AddButton(PDialog.DIALOG_KEY_CLOSE,
					STRINGS.UI.CONFIRMDIALOG.CANCEL, PUIStrings.TOOLTIP_CANCEL,
					PUITuning.Colors.ButtonBlueStyle);
				options = POptions.ReadSettings(path, optionsType);
				if (options == null)
					CreateOptions();
				if (infoAttr != null)
					AddModInfoScreen(pDialog);
				FillModOptions(pDialog);
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
			// Only invoked once so a delegate probably will not gain anything
			if (action == "ok") {
				// Save changes to mod options
				WriteOptions();
				CheckForRestart();
			} else if (action == PDialog.DIALOG_KEY_CLOSE)
				handler.OnCancel(options);
		}

		/// <summary>
		/// Calls the user OnOptionsChanged handler if present.
		/// </summary>
		/// <param name="options">The updated options object.</param>
		private void TriggerUpdateOptions(object options) {
			// Call the user handler
			var onSave = PPatchTools.GetMethodSafe(options.GetType(), nameof(IOptions.
				OnOptionsChanged), false);
			if (onSave != null)
				try {
					onSave.Invoke(options, null);
				} catch (TargetInvocationException e) {
					PUtil.LogException(e.GetBaseException());
				}
			handler.OnSaveOptions(options);
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
		/// If configured, opens the mod's home page in the default browser.
		/// </summary>
		private void VisitModHomepage(GameObject _) {
			string modURL = infoAttr?.URL;
			if (string.IsNullOrEmpty(modURL))
				modURL = handler.DefaultURL;
			if (!string.IsNullOrWhiteSpace(modURL))
				Application.OpenURL(modURL);
		}

		/// <summary>
		/// Writes the mod options to its config file.
		/// </summary>
		private void WriteOptions() {
			if (options != null) {
				// Update from local options
				foreach (var catEntries in optionCategories)
					foreach (var option in catEntries.Value)
						option.WriteTo(options);
				POptions.WriteSettings(options, path, typeAttr?.IndentOutput ?? false);
				TriggerUpdateOptions(options);
			}
		}
	}
}
