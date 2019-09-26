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
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Sets up common parameters for the UI in PLib based mods. Note that this class is still
	/// specific to individual mods so the values in the latest PLib will not supersede them.
	/// </summary>
	public static class PUITuning {
		/// <summary>
		/// UI images.
		/// </summary>
		public static class Images {
			/// <summary>
			/// The right arrow image. Rotate it in the Image to get more directions.
			/// </summary>
			public static Sprite Arrow { get; }

			/// <summary>
			/// The default image used for button appearance.
			/// </summary>
			public static Sprite ButtonBorder { get; }

			/// <summary>
			/// The border image around a checkbox.
			/// </summary>
			public static Sprite CheckBorder { get; }

			/// <summary>
			/// The image for a check box which is checked.
			/// </summary>
			public static Sprite Checked { get; }

			/// <summary>
			/// The image used for dialog close buttons.
			/// </summary>
			public static Sprite Close { get; }

			/// <summary>
			/// The image for contracting a category.
			/// </summary>
			public static Sprite Contract { get; }

			/// <summary>
			/// The image for expanding a category.
			/// </summary>
			public static Sprite Expand { get; }

			/// <summary>
			/// The image for a check box which is neither checked nor unchecked.
			/// </summary>
			public static Sprite Partial { get; }

			/// <summary>
			/// The sprite dictionary.
			/// </summary>
			private static readonly IDictionary<string, Sprite> SPRITES;

			static Images() {
				SPRITES = new Dictionary<string, Sprite>(512);
				// List out all sprites shipped with the game
				foreach (var img in Resources.FindObjectsOfTypeAll<Sprite>()) {
					string name = img?.name;
					if (!string.IsNullOrEmpty(name) && !SPRITES.ContainsKey(name))
						SPRITES.Add(name, img);
				}

				Arrow = GetSpriteByName("game_speed_play");
				ButtonBorder = GetSpriteByName("web_button");
				CheckBorder = GetSpriteByName("overview_jobs_skill_box");
				Checked = GetSpriteByName("overview_jobs_icon_checkmark");
				Close = GetSpriteByName("cancel");
				Contract = GetSpriteByName("iconDown");
				Expand = GetSpriteByName("iconRight");
				Partial = GetSpriteByName("overview_jobs_icon_mixed");
			}

			/// <summary>
			/// Retrieves a sprite by its name.
			/// </summary>
			/// <param name="name">The sprite name.</param>
			/// <returns>The matching sprite, or null if no sprite found in the resources has that name.</returns>
			public static Sprite GetSpriteByName(string name) {
				if (!SPRITES.TryGetValue(name, out Sprite sprite))
					sprite = null;
				return sprite;
			}
		}

		/// <summary>
		/// UI colors.
		/// </summary>
		public static class Colors {
			/// <summary>
			/// A white color used for default backgrounds.
			/// </summary>
			public static Color BackgroundLight { get; }

			/// <summary>
			/// The color styles used on pink buttons.
			/// </summary>
			internal static ColorStyleSetting ButtonPinkStyle { get; }

			/// <summary>
			/// The color styles used on blue buttons.
			/// </summary>
			internal static ColorStyleSetting ButtonBlueStyle { get; }

			/// <summary>
			/// The default colors used on check boxes with dark backgrounds.
			/// </summary>
			internal static ColorStyleSetting CheckboxDarkStyle { get; }

			/// <summary>
			/// The default colors used on check boxes with white backgrounds.
			/// </summary>
			internal static ColorStyleSetting CheckboxWhiteStyle { get; }

			/// <summary>
			/// The color displayed on dialog backgrounds.
			/// </summary>
			public static Color DialogBackground { get; }

			/// <summary>
			/// The background color for selections.
			/// </summary>
			public static Color SelectionBackground { get; }

			/// <summary>
			/// The foreground color for selections.
			/// </summary>
			public static Color SelectionForeground { get; }

			/// <summary>
			/// A completely transparent color.
			/// </summary>
			public static Color Transparent { get; }

			/// <summary>
			/// Used for dark-colored UI text.
			/// </summary>
			public static Color UITextDark { get; }

			/// <summary>
			/// Used for light-colored UI text.
			/// </summary>
			public static Color UITextLight { get; }

			static Colors() {
				BackgroundLight = new Color32(255, 255, 255, 255);
				DialogBackground = new Color32(0, 0, 0, 255);
				SelectionBackground = new Color32(189, 218, 255, 255);
				SelectionForeground = new Color32(0, 0, 0, 255);
				Transparent = new Color32(255, 255, 255, 0);
				UITextLight = new Color32(255, 255, 255, 255);
				UITextDark = new Color32(0, 0, 0, 255);

				// Check boxes
				Color active = new Color(0.0f, 0.0f, 0.0f), disabled = new Color(0.784f,
					0.784f, 0.784f, 1.0f);
				CheckboxWhiteStyle = ScriptableObject.CreateInstance<ColorStyleSetting>();
				CheckboxWhiteStyle.activeColor = active;
				CheckboxWhiteStyle.inactiveColor = active;
				CheckboxWhiteStyle.hoverColor = active;
				CheckboxWhiteStyle.disabledActiveColor = disabled;
				CheckboxWhiteStyle.disabledColor = disabled;
				CheckboxWhiteStyle.disabledhoverColor = disabled;

				active = new Color(1.0f, 1.0f, 1.0f);
				CheckboxDarkStyle = ScriptableObject.CreateInstance<ColorStyleSetting>();
				CheckboxDarkStyle.activeColor = active;
				CheckboxDarkStyle.inactiveColor = active;
				CheckboxDarkStyle.hoverColor = active;
				CheckboxDarkStyle.disabledActiveColor = disabled;
				CheckboxDarkStyle.disabledColor = disabled;
				CheckboxDarkStyle.disabledhoverColor = disabled;

				// Buttons: pink
				ButtonPinkStyle = ScriptableObject.CreateInstance<ColorStyleSetting>();
				ButtonPinkStyle.activeColor = new Color(0.7941176f, 0.4496107f, 0.6242238f);
				ButtonPinkStyle.inactiveColor = new Color(0.5294118f, 0.2724914f, 0.4009516f);
				ButtonPinkStyle.disabledColor = new Color(0.4156863f, 0.4117647f, 0.4f);
				ButtonPinkStyle.disabledActiveColor = Transparent;
				ButtonPinkStyle.hoverColor = new Color(0.6176471f, 0.3315311f, 0.4745891f);
				ButtonPinkStyle.disabledhoverColor = new Color(0.5f, 0.5f, 0.5f);

				// Buttons: blue
				ButtonBlueStyle = ScriptableObject.CreateInstance<ColorStyleSetting>();
				ButtonBlueStyle.activeColor = new Color(0.5033521f, 0.5444419f, 0.6985294f);
				ButtonBlueStyle.inactiveColor = new Color(0.2431373f, 0.2627451f, 0.3411765f);
				ButtonBlueStyle.disabledColor = new Color(0.4156863f, 0.4117647f, 0.4f);
				ButtonBlueStyle.disabledActiveColor = new Color(0.625f, 0.6158088f, 0.5882353f);
				ButtonBlueStyle.hoverColor = new Color(0.3461289f, 0.3739619f, 0.4852941f);
				ButtonBlueStyle.disabledhoverColor = new Color(0.5f, 0.4898898f, 0.4595588f);
			}
		}

		/// <summary>
		/// Collects references to fonts in the game.
		/// </summary>
		public static class Fonts {
			/// <summary>
			/// The text font name.
			/// </summary>
			private const string FONT_TEXT = "NotoSans-Regular";

			/// <summary>
			/// The UI font name.
			/// </summary>
			private const string FONT_UI = "GRAYSTROKE REGULAR SDF";

			/// <summary>
			/// The default font size.
			/// </summary>
			public static int DefaultSize { get; }

			/// <summary>
			/// The font used on text.
			/// </summary>
			internal static TMP_FontAsset Text { get; }

			/// <summary>
			/// The text styles used on all items with a light background.
			/// </summary>
			internal static TextStyleSetting TextDarkStyle { get; }

			/// <summary>
			/// The text styles used on all items with a dark background.
			/// </summary>
			internal static TextStyleSetting TextLightStyle { get; }

			/// <summary>
			/// The font used on UI elements.
			/// </summary>
			internal static TMP_FontAsset UI { get; }

			/// <summary>
			/// The text styles used on all UI items with a light background.
			/// </summary>
			internal static TextStyleSetting UIDarkStyle { get; }

			/// <summary>
			/// The text styles used on all UI items with a dark background.
			/// </summary>
			internal static TextStyleSetting UILightStyle { get; }

			/// <summary>
			/// The font dictionary.
			/// </summary>
			private static readonly IDictionary<string, TMP_FontAsset> FONTS;

			static Fonts() {
				FONTS = new Dictionary<string, TMP_FontAsset>(16);
				// List out all fonts shipped with the game
				foreach (var newFont in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) {
					string name = newFont?.name;
					if (!string.IsNullOrEmpty(name) && !FONTS.ContainsKey(name))
						FONTS.Add(name, newFont);
				}

				// Initialization: UI fonts
				if ((Text = GetFontByName(FONT_TEXT)) == null)
					PUtil.LogWarning("Unable to find font " + FONT_TEXT);
				if ((UI = GetFontByName(FONT_UI)) == null)
					PUtil.LogWarning("Unable to find font " + FONT_UI);

				// Initialization: Text style
				DefaultSize = 14;
				TextDarkStyle = ScriptableObject.CreateInstance<TextStyleSetting>();
				TextDarkStyle.enableWordWrapping = false;
				TextDarkStyle.fontSize = DefaultSize;
				TextDarkStyle.sdfFont = Text;
				TextDarkStyle.style = FontStyles.Normal;
				TextDarkStyle.textColor = Colors.UITextDark;
				TextLightStyle = ScriptableObject.CreateInstance<TextStyleSetting>();
				TextLightStyle.enableWordWrapping = false;
				TextLightStyle.fontSize = DefaultSize;
				TextLightStyle.sdfFont = Text;
				TextLightStyle.style = FontStyles.Normal;
				TextLightStyle.textColor = Colors.UITextLight;
				UIDarkStyle = ScriptableObject.CreateInstance<TextStyleSetting>();
				UIDarkStyle.enableWordWrapping = false;
				UIDarkStyle.fontSize = DefaultSize;
				UIDarkStyle.sdfFont = UI;
				UIDarkStyle.style = FontStyles.Normal;
				UIDarkStyle.textColor = Colors.UITextDark;
				UILightStyle = ScriptableObject.CreateInstance<TextStyleSetting>();
				UILightStyle.enableWordWrapping = false;
				UILightStyle.fontSize = DefaultSize;
				UILightStyle.sdfFont = UI;
				UILightStyle.style = FontStyles.Normal;
				UILightStyle.textColor = Colors.UITextLight;
			}

			/// <summary>
			/// Retrieves a font by its name.
			/// </summary>
			/// <param name="name">The font name.</param>
			/// <returns>The matching font, or null if no font found in the resources has that name.</returns>
			internal static TMP_FontAsset GetFontByName(string name) {
				if (!FONTS.TryGetValue(name, out TMP_FontAsset font))
					font = null;
				return font;
			}
		}

		/// <summary>
		/// The sounds played by the button.
		/// </summary>
		internal static ButtonSoundPlayer ButtonSounds { get; }

		/// <summary>
		/// The sounds played by the toggle.
		/// </summary>
		internal static ToggleSoundPlayer ToggleSounds { get; }


		static PUITuning() {
			// Initialization: Button sounds
			ButtonSounds = new ButtonSoundPlayer() {
				Enabled = true
			};
			ToggleSounds = new ToggleSoundPlayer();
		}
	}
}
