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
			/// The left arrow image.
			/// </summary>
			public static Sprite ArrowLeft { get; private set; }

			/// <summary>
			/// The right arrow image.
			/// </summary>
			public static Sprite ArrowRight { get; private set; }

			/// <summary>
			/// The default image used for button appearance.
			/// </summary>
			public static Sprite ButtonBorder { get; private set; }

			/// <summary>
			/// The border image around a checkbox.
			/// </summary>
			public static Sprite CheckBorder { get; private set; }

			/// <summary>
			/// The image for a check box which is checked.
			/// </summary>
			public static Sprite Checked { get; private set; }

			/// <summary>
			/// The image used for dialog close buttons.
			/// </summary>
			public static Sprite Close { get; private set; }

			/// <summary>
			/// The image for contracting a category.
			/// </summary>
			public static Sprite Contract { get; private set; }

			/// <summary>
			/// The image for expanding a category.
			/// </summary>
			public static Sprite Expand { get; private set; }

			/// <summary>
			/// The image for a check box which is neither checked nor unchecked.
			/// </summary>
			public static Sprite Partial { get; private set; }

			internal static void InitSprites() {
				ArrowLeft = PUIUtils.LoadSprite("PeterHan.PLib.Assets.ArrowLeft.dds", 64,
					64);
				ArrowRight = PUIUtils.LoadSprite("PeterHan.PLib.Assets.ArrowRight.dds", 64,
					64);
				ButtonBorder = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Button.dds", 16, 16,
					new Vector4(3.0f, 3.0f, 3.0f, 3.0f));
				CheckBorder = PUIUtils.LoadSprite("PeterHan.PLib.Assets.CheckBorder.dds", 16,
					16, new Vector4(4.0f, 4.0f, 4.0f, 4.0f));
				Checked = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Check.dds", 64, 64);
				Close = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Close.dds", 64, 64);
				Contract = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Contract.dds", 64, 64);
				Expand = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Expand.dds", 64, 64);
				Partial = PUIUtils.LoadSprite("PeterHan.PLib.Assets.Partial.dds", 64, 64);
			}
		}

		/// <summary>
		/// UI colors.
		/// </summary>
		public static class Colors {
			/// <summary>
			/// A white color used for default backgrounds.
			/// </summary>
			public static Color BackgroundLight { get; private set; }

			/// <summary>
			/// The color styles used on pink buttons.
			/// </summary>
			internal static ColorStyleSetting ButtonPinkStyle { get; private set; }

			/// <summary>
			/// The color styles used on blue buttons.
			/// </summary>
			internal static ColorStyleSetting ButtonBlueStyle { get; private set; }

			/// <summary>
			/// The default colors used on check boxes with dark backgrounds.
			/// </summary>
			internal static ColorStyleSetting CheckboxDarkStyle { get; private set; }

			/// <summary>
			/// The default colors used on check boxes with white backgrounds.
			/// </summary>
			internal static ColorStyleSetting CheckboxWhiteStyle { get; private set; }

			/// <summary>
			/// The color displayed on dialog backgrounds.
			/// </summary>
			public static Color DialogBackground { get; private set; }

			/// <summary>
			/// A completely transparent color.
			/// </summary>
			public static Color Transparent { get; private set; }

			/// <summary>
			/// Used for dark-colored UI text.
			/// </summary>
			public static Color UITextDark { get; private set; }

			/// <summary>
			/// Used for light-colored UI text.
			/// </summary>
			public static Color UITextLight { get; private set; }

			internal static void InitColors() {
				BackgroundLight = new Color32(255, 255, 255, 255);
				DialogBackground = new Color32(0, 0, 0, 255);
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
		/// The font used on all buttons.
		/// </summary>
		internal static TMPro.TMP_FontAsset ButtonFont { get; private set; }

		/// <summary>
		/// The sounds played by the button.
		/// </summary>
		internal static ButtonSoundPlayer ButtonSounds { get; private set; }

		/// <summary>
		/// The default font size.
		/// </summary>
		public static float DefaultFontSize { get; private set; }

		/// <summary>
		/// The sounds played by the toggle.
		/// </summary>
		internal static ToggleSoundPlayer ToggleSounds { get; private set; }

		/// <summary>
		/// The text styles used on all buttons by default.
		/// </summary>
		internal static TextStyleSetting UITextLightStyle { get; private set; }

		/// <summary>
		/// The text styles used on all items with a light background.
		/// </summary>
		internal static TextStyleSetting UITextDarkStyle { get; private set; }

		/// <summary>
		/// Initializes fields based on a template button.
		/// </summary>
		private static void InitTitleButton(KButton close) {
			GameObject obj;
			// Initialization: Button colors
			var transform = close.gameObject.transform;
			if (transform.childCount <= 0 || (obj = transform.GetChild(0).gameObject) == null)
				PUIUtils.LogUIWarning("Core button has wrong format!");
			else {
				// Initialization: Text style and font
				var text = obj.GetComponent<LocText>();
				if (text != null) {
					DefaultFontSize = text.fontSize;
					UITextLightStyle = text.textStyleSetting;
					UITextLightStyle.textColor = Colors.UITextLight;
					UITextDarkStyle = new TextStyleSetting() {
						enableWordWrapping = UITextLightStyle.enableWordWrapping,
						fontSize = UITextLightStyle.fontSize,
						sdfFont = UITextLightStyle.sdfFont,
						style = UITextLightStyle.style,
						textColor = Colors.UITextDark
					};
					ButtonFont = text.font;
				}
			}
		}

		static PUITuning() {
			Colors.InitColors();
			Images.InitSprites();
			// Ouch! Hacky!
			var prefab = Global.Instance.modErrorsPrefab?.GetComponent<KMod.ModErrorsScreen>();
			if (prefab == null)
				PUIUtils.LogUIWarning("Missing core prefab!");
			else {
				var trPrefab = Traverse.Create(prefab);
				// Much can be stolen from the Close button!
				var close = trPrefab.GetField<KButton>("closeButton");
				if (close == null)
					PUIUtils.LogUIWarning("Missing core button!");
				else
					InitTitleButton(close);
			}
			// Initialization: Button sounds
			ButtonSounds = new ButtonSoundPlayer() {
				Enabled = true
			};
			ToggleSounds = new ToggleSoundPlayer();
		}
	}
}
