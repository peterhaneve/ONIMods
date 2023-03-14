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

using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

using OptionsList = System.Collections.Generic.ICollection<PeterHan.PLib.Options.IOptionsEntry>;

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
		/// Creates an options object using the default constructor if possible.
		/// </summary>
		/// <param name="type">The type of the object to create.</param>
		internal static object CreateOptions(Type type) {
			object result = null;
			try {
				var cons = type.GetConstructor(Type.EmptyTypes);
				if (cons != null)
					result = cons.Invoke(null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogExcWarn(e.GetBaseException());
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (MemberAccessException e) {
				// Other mod's error
				PUtil.LogException(e);
			}
			return result;
		}

		/// <summary>
		/// Saves the mod enabled settings and restarts the game.
		/// </summary>
		private static void SaveAndRestart() {
#if OPTIONS_ONLY
			POptionsPatches.SaveMods();
#else
			PGameUtils.SaveMods();
#endif
			App.instance.Restart();
		}

		/// <summary>
		/// If true, all categories begin collapsed.
		/// </summary>
		private readonly bool collapseCategories;

		/// <summary>
		/// The config file attribute for the options type, if present.
		/// </summary>
		private readonly ConfigFileAttribute configAttr;

		/// <summary>
		/// The currently active dialog.
		/// </summary>
		private KScreen dialog;

		/// <summary>
		/// The sprite to display for this mod.
		/// </summary>
		private Sprite modImage;

		/// <summary>
		/// Collects information from the ModInfoAttribute and KMod.Mod objects for display.
		/// </summary>
		private readonly ModDialogInfo displayInfo;

		/// <summary>
		/// The event to invoke when the dialog is closed.
		/// </summary>
		public Action<object> OnClose { get; set; }

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

		internal OptionsDialog(Type optionsType) {
			OnClose = null;
			dialog = null;
			modImage = null;
			this.optionsType = optionsType ?? throw new ArgumentNullException(nameof(
				optionsType));
			optionCategories = OptionsEntry.BuildOptions(optionsType);
			options = null;
			// Determine config location
			var infoAttr = optionsType.GetCustomAttribute<ModInfoAttribute>();
			collapseCategories = infoAttr != null && infoAttr.ForceCollapseCategories;
			configAttr = optionsType.GetCustomAttribute<ConfigFileAttribute>();
			displayInfo = new ModDialogInfo(optionsType, infoAttr?.URL, infoAttr?.Image);
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
				bool state = !collapseCategories;
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
					ToolTip = PLibStrings.TOOLTIP_TOGGLE, Size = TOGGLE_SIZE,
					OnStateChanged = handler.OnExpandContract
				}.AddOnRealize(handler.OnRealizeToggle), new GridComponentSpec(0, 0));
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
		/// <param name="optionsDialog">The dialog to populate.</param>
		private void AddModInfoScreen(PDialog optionsDialog) {
			string image = displayInfo.Image;
			var body = optionsDialog.Body;
			// Try to load the mod image sprite if possible
			if (modImage == null && !string.IsNullOrEmpty(image)) {
				string rootDir = PUtil.GetModPath(optionsType.Assembly);
				modImage = PUIUtils.LoadSpriteFile(rootDir == null ? image : Path.Combine(
					rootDir, image));
			}
			var websiteButton = new PButton("ModSite") {
				Text = PLibStrings.MOD_HOMEPAGE, ToolTip = PLibStrings.TOOLTIP_HOMEPAGE,
				OnClick = VisitModHomepage, Margin = PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle();
			var versionLabel = new PLabel("ModVersion") {
				Text = displayInfo.Version, ToolTip = PLibStrings.TOOLTIP_VERSION,
				TextStyle = PUITuning.Fonts.UILightStyle, Margin = new RectOffset(0, 0,
				OUTER_MARGIN, 0)
			};
			// Find mod URL
			string modURL = displayInfo.URL;
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
			if (options != null && options.GetType().GetCustomAttribute(typeof(
					RestartRequiredAttribute)) != null)
				// Prompt user to restart
				PUIElements.ShowConfirmDialog(null, PLibStrings.RESTART_REQUIRED,
					SaveAndRestart, null, PLibStrings.RESTART_OK, PLibStrings.RESTART_CANCEL);
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
		/// Fills in the actual mod option fields.
		/// </summary>
		/// <param name="optionsDialog">The dialog to populate.</param>
		private void FillModOptions(PDialog optionsDialog) {
			IEnumerable<IOptionsEntry> dynamicOptions;
			var body = optionsDialog.Body;
			var margin = new RectOffset(CATEGORY_MARGIN, CATEGORY_MARGIN, CATEGORY_MARGIN,
				CATEGORY_MARGIN);
			// For each option, add its UI component to panel
			body.Margin = new RectOffset();
			var scrollBody = new PPanel("ScrollContent") {
				Spacing = OUTER_MARGIN, Direction = PanelDirection.Vertical, Alignment =
				TextAnchor.UpperCenter, FlexSize = Vector2.right
			};
			var allOptions = optionCategories;
			// Add options from the user's class
			if (options is IOptions dynOptions && (dynamicOptions = dynOptions.
					CreateOptions()) != null) {
				allOptions = new Dictionary<string, OptionsList>(optionCategories);
				foreach (var dynamicOption in dynamicOptions)
					OptionsEntry.AddToCategory(allOptions, dynamicOption);
			}
			// Display all categories
			foreach (var catEntries in allOptions) {
				string category = catEntries.Key;
				var optionsList = catEntries.Value;
				if (optionsList.Count > 0) {
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
					foreach (var entry in optionsList) {
						contents.AddRow(new GridRowSpec());
						entry.CreateUIEntry(contents, ref i);
						i++;
					}
					scrollBody.AddChild(container);
				}
			}
			// Manual config and reset button
			scrollBody.AddChild(new PPanel("ConfigButtons") {
				Spacing = 10, Direction = PanelDirection.Horizontal, Alignment =
				TextAnchor.MiddleCenter, FlexSize = Vector2.right
			}.AddChild(new PButton("ManualConfig") {
				Text = PLibStrings.BUTTON_MANUAL, ToolTip = PLibStrings.TOOLTIP_MANUAL,
				OnClick = OnManualConfig, TextAlignment = TextAnchor.MiddleCenter, Margin =
				PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle()).AddChild(new PButton("ResetConfig") {
				Text = PLibStrings.BUTTON_RESET, ToolTip = PLibStrings.TOOLTIP_RESET,
				OnClick = OnResetConfig, TextAlignment = TextAnchor.MiddleCenter, Margin =
				PDialog.BUTTON_MARGIN
			}.SetKleiBlueStyle()));
			body.AddChild(new PScrollPane() {
				ScrollHorizontal = false, ScrollVertical = allOptions.Count > 0,
				Child = scrollBody, FlexSize = Vector2.right, TrackSize = 8,
				AlwaysShowHorizontal = false, AlwaysShowVertical = false
			});
		}

		/// <summary>
		/// Invoked when the manual config button is pressed.
		/// </summary>
		private void OnManualConfig(GameObject _) {
			string uri = null, path = POptions.GetConfigFilePath(optionsType);
			try {
				uri = new Uri(Path.GetDirectoryName(path) ?? path).AbsoluteUri;
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
			switch (action) {
			case "ok":
				// Save changes to mod options
				WriteOptions();
				CheckForRestart();
				break;
			case PDialog.DIALOG_KEY_CLOSE:
				OnClose?.Invoke(options);
				break;
			}
		}

		/// <summary>
		/// Invoked when the reset to default button is pressed.
		/// </summary>
		private void OnResetConfig(GameObject _) {
			options = CreateOptions(optionsType);
			UpdateOptions();
		}

		/// <summary>
		/// Triggered when the Mod Options button is clicked.
		/// </summary>
		public void ShowDialog() {
			string title;
			if (string.IsNullOrEmpty(displayInfo.Title))
				title = PLibStrings.BUTTON_OPTIONS;
			else
				title = string.Format(PLibStrings.DIALOG_TITLE, OptionsEntry.LookInStrings(
					displayInfo.Title));
			// Close current dialog if open
			CloseDialog();
			// Ensure that it is on top of other screens (which may be +100 modal)
			var pDialog = new PDialog("ModOptions") {
				Title = title, Size = SETTINGS_DIALOG_SIZE, SortKey = 150.0f,
				DialogBackColor = PUITuning.Colors.OptionsBackground,
				DialogClosed = OnOptionsSelected, MaxSize = SETTINGS_DIALOG_MAX_SIZE,
				RoundToNearestEven = true
			}.AddButton("ok", STRINGS.UI.CONFIRMDIALOG.OK, PLibStrings.TOOLTIP_OK,
				PUITuning.Colors.ButtonPinkStyle).AddButton(PDialog.DIALOG_KEY_CLOSE,
				STRINGS.UI.CONFIRMDIALOG.CANCEL, PLibStrings.TOOLTIP_CANCEL,
				PUITuning.Colors.ButtonBlueStyle);
			options = POptions.ReadSettings(POptions.GetConfigFilePath(optionsType),
				optionsType) ?? CreateOptions(optionsType);
			AddModInfoScreen(pDialog);
			FillModOptions(pDialog);
			// Manually build the dialog so the options can be updated after realization
			var obj = pDialog.Build();
			UpdateOptions();
			if (obj.TryGetComponent(out dialog))
				dialog.Activate();
		}

		/// <summary>
		/// Calls the user OnOptionsChanged handler if present.
		/// </summary>
		/// <param name="newOptions">The updated options object.</param>
		private void TriggerUpdateOptions(object newOptions) {
			// Call the user handler
			if (newOptions is IOptions onChanged)
				onChanged.OnOptionsChanged();
			OnClose?.Invoke(newOptions);
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
			if (!string.IsNullOrWhiteSpace(displayInfo.URL))
				Application.OpenURL(displayInfo.URL);
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
				POptions.WriteSettings(options, POptions.GetConfigFilePath(optionsType),
					configAttr?.IndentOutput ?? false);
				TriggerUpdateOptions(options);
			}
		}
	}
}
