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

using System;
using UnityEngine;

namespace PeterHan.PLib {
	/// <summary>
	/// Sets up common parameters for the UI in PLib based mods. Note that this class is still
	/// specific to individual mods so the values in the latest PLib will not supersede them.
	/// </summary>
	static class PUITuning {
		/// <summary>
		/// The default color used on buttons.
		/// </summary>
		public static Color BUTTON_COLOR { get; } = new Color(0.243137f, 0.262745f, 0.341176f);

		/// <summary>
		/// The color styles used on buttons.
		/// </summary>
		public static ColorStyleSetting BUTTON_STYLE { get; private set; }

		/// <summary>
		/// The text styles used on buttons.
		/// </summary>
		public static TextStyleSetting BUTTON_TEXT_STYLE { get; private set; }

		static PUITuning() {
			// Initialization: Button style
			BUTTON_STYLE = ScriptableObject.CreateInstance<ColorStyleSetting>();
			BUTTON_STYLE.name = "PUIButtonStyle";
			BUTTON_STYLE.activeColor = new Color(0.503352f, 0.544442f, 0.698529f);
			BUTTON_STYLE.inactiveColor = BUTTON_COLOR;
			BUTTON_STYLE.disabledColor = new Color(0.415686f, 0.411765f, 0.4f);
			BUTTON_STYLE.disabledActiveColor = new Color(0.625f, 0.615809f, 0.588235f);
			BUTTON_STYLE.hoverColor = new Color(0.346129f, 0.373962f, 0.485294f);
			BUTTON_STYLE.disabledhoverColor = new Color(0.5f, 0.48989f, 0.459559f);
			// Initialization: Button text stylke
			BUTTON_TEXT_STYLE = ScriptableObject.CreateInstance<TextStyleSetting>();
			BUTTON_TEXT_STYLE.name = "PUIButtonTextStyle";
			BUTTON_TEXT_STYLE.enableWordWrapping = true;
			BUTTON_TEXT_STYLE.textColor = new Color(1.0f, 1.0f, 1.0f);
		}
	}
}
