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
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Sets up common parameters for the UI in PLib based mods. Note that this class is still
	/// specific to individual mods so the values in the latest PLib will not supersede them.
	/// </summary>
	internal static class PUITuning {
		/// <summary>
		/// The default color used on blue buttons.
		/// </summary>
		internal static Color ButtonColorBlue { get; private set; }

		/// <summary>
		/// The default color used on pink buttons.
		/// </summary>
		internal static Color ButtonColorPink { get; private set; }

		/// <summary>
		/// The font used on all buttons
		/// </summary>
		internal static TMPro.TMP_FontAsset ButtonFont { get; private set; }

		/// <summary>
		/// The default image used for button appearance.
		/// </summary>
		internal static KImage ButtonImage { get; private set; }

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
		/// The text styles used on all buttons.
		/// </summary>
		internal static TextStyleSetting ButtonTextStyle { get; private set; }

		/// <summary>
		/// Initializes fields based on a template button.
		/// </summary>
		private static void InitFromTitleButton(KButton closeTitle) {
			// Initialization: Button colors
			ButtonStyleBlue = closeTitle.colorStyleSetting;
			ButtonColorBlue = ButtonStyleBlue.inactiveColor;
		}

		/// <summary>
		/// Initializes fields based on a template button.
		/// </summary>
		private static void InitFromCloseButton(KButton close) {
			GameObject obj;
			// Initialization: Button colors
			ButtonStylePink = close.colorStyleSetting;
			ButtonColorPink = ButtonStylePink.inactiveColor;
			var transform = close.gameObject.transform;
			if (transform.childCount <= 0 || (obj = transform.GetChild(0).gameObject) == null)
				LogUIWarning("Core button has wrong format!");
			else {
				// Initialization: Text style and font
				var text = obj.GetComponent<LocText>();
				ButtonTextStyle = text.textStyleSetting;
				ButtonFont = text.font;
			}
			// Initialization: Button sounds
			ButtonSounds = close.soundPlayer;
			ButtonImage = close.GetComponent<KImage>();
		}

		static PUITuning() {
			// Ouch! Hacky!
			var prefab = Global.Instance.modErrorsPrefab?.GetComponent<KMod.ModErrorsScreen>();
			if (prefab == null)
				LogUIWarning("Missing core prefab!");
			else {
				var trPrefab = Traverse.Create(prefab);
				// Much can be stolen from the Close button!
				var closeTitle = trPrefab.GetField<KButton>("closeButtonTitle");
				if (closeTitle == null)
					LogUIWarning("Missing core button!");
				else
					InitFromTitleButton(closeTitle);
				var close = trPrefab.GetField<KButton>("closeButton");
				if (close == null)
					LogUIWarning("Missing core button!");
				else
					InitFromCloseButton(close);
			}
		}

		/// <summary>
		/// Logs a debug message encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The debug message.</param>
		internal static void LogUIDebug(string message) {
			Debug.LogFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly()?.GetName()?.
				Name ?? "?", message);
		}

		/// <summary>
		/// Logs a warning encountered in PLib UI functions.
		/// </summary>
		/// <param name="message">The warning message.</param>
		internal static void LogUIWarning(string message) {
			Debug.LogWarningFormat("[PLib/UI/{0}] {1}", Assembly.GetCallingAssembly()?.
				GetName()?.Name ?? "?", message);
		}
	}
}
