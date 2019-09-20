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
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Sets up common parameters for the UI in PLib based mods. Note that this class is still
	/// specific to individual mods so the values in the latest PLib will not supersede them.
	/// </summary>
	internal static class PUITuning {
		/// <summary>
		/// The left arrow image.
		/// </summary>
		internal static Sprite ArrowLeftImage { get; private set; }

		/// <summary>
		/// The right arrow image.
		/// </summary>
		internal static Sprite ArrowRightImage { get; private set; }

		/// <summary>
		/// The color displayed on dialog backgrounds.
		/// </summary>
		internal static Color DialogBackground { get; private set; }

		/// <summary>
		/// The font used on all buttons.
		/// </summary>
		internal static TMPro.TMP_FontAsset ButtonFont { get; private set; }

		/// <summary>
		/// The default image used for button appearance.
		/// </summary>
		internal static Sprite ButtonImage { get; private set; }

		/// <summary>
		/// The sounds played by the button.
		/// </summary>
		internal static ButtonSoundPlayer ButtonSounds { get; private set; }

		/// <summary>
		/// The color styles used on pink buttons.
		/// </summary>
		internal static ColorStyleSetting ButtonStylePink { get; private set; }

		/// <summary>
		/// The color styles used on blue buttons.
		/// </summary>
		internal static ColorStyleSetting ButtonStyleBlue { get; private set; }

		/// <summary>
		/// The image used for dialog close buttons.
		/// </summary>
		internal static Sprite CloseButtonImage { get; private set; }

		/// <summary>
		/// The default font size.
		/// </summary>
		internal static float DefaultFontSize { get; private set; }

		/// <summary>
		/// The text styles used on all buttons.
		/// </summary>
		internal static TextStyleSetting UITextStyle { get; private set; }

		/// <summary>
		/// Initializes fields based on a template button.
		/// </summary>
		private static void InitCloseButton(KButton closeTitle) {
			// Initialization: Button colors
			ButtonStyleBlue = closeTitle.colorStyleSetting;
#if BORROW_KLEI
			GameObject obj;
			var transform = closeTitle.gameObject.transform;
			if (transform.childCount <= 0 || (obj = transform.GetChild(0).gameObject) == null)
				PUIUtils.LogUIWarning("Core button has wrong format!");
			else
				// Initialization: Close button sprite
				CloseButtonImage = obj.GetComponent<Image>()?.sprite;
#else
			CloseButtonImage = PUtil.LoadSprite("PeterHan.PLib.Assets.Close.dds", 128, 128);
#endif
		}

		/// <summary>
		/// Initializes fields based on a template button.
		/// </summary>
		private static void InitTitleButton(KButton close) {
			GameObject obj;
			// Initialization: Button colors
			ButtonStylePink = close.colorStyleSetting;
			var transform = close.gameObject.transform;
			if (transform.childCount <= 0 || (obj = transform.GetChild(0).gameObject) == null)
				PUIUtils.LogUIWarning("Core button has wrong format!");
			else {
				// Initialization: Text style and font
				var text = obj.GetComponent<LocText>();
				if (text != null) {
					DefaultFontSize = text.fontSize;
					UITextStyle = text.textStyleSetting;
					ButtonFont = text.font;
				}
			}
			// Initialization: Button sounds
			ButtonSounds = close.soundPlayer;
#if BORROW_KLEI
			ButtonImage = close.GetComponent<KImage>()?.sprite;
#else
			ButtonImage = PUtil.LoadSprite("PeterHan.PLib.Assets.Button.dds", 16, 16,
				new Vector4(3.0f, 3.0f, 3.0f, 3.0f));
#endif
		}

		static PUITuning() {
			// Ouch! Hacky!
			var prefab = Global.Instance.modErrorsPrefab?.GetComponent<KMod.ModErrorsScreen>();
			if (prefab == null)
				PUIUtils.LogUIWarning("Missing core prefab!");
			else {
				var trPrefab = Traverse.Create(prefab);
				DialogBackground = new Color(0.0f, 0.0f, 0.0f);
				// Much can be stolen from the Close button!
				var closeTitle = trPrefab.GetField<KButton>("closeButtonTitle");
				if (closeTitle == null)
					PUIUtils.LogUIWarning("Missing core button!");
				else
					InitCloseButton(closeTitle);
				var close = trPrefab.GetField<KButton>("closeButton");
				if (close == null)
					PUIUtils.LogUIWarning("Missing core button!");
				else
					InitTitleButton(close);
			}
			ArrowLeftImage = PUtil.LoadSprite("PeterHan.PLib.Assets.ArrowLeft.dds", 128, 128);
			ArrowRightImage = PUtil.LoadSprite("PeterHan.PLib.Assets.ArrowRight.dds", 128, 128);
		}
	}
}
