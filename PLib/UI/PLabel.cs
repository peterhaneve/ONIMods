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
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI label factory class.
	/// </summary>
	public class PLabel : PTextComponent {
		/// <summary>
		/// The label's background color.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// The margin around the component.
		/// </summary>
		public RectOffset Margin { get; set; }

		public PLabel() : this(null) { }

		public PLabel(string name) : base(name ?? "Label") {
			BackColor = PUITuning.Colors.Transparent;
			Margin = null;
		}

		public override GameObject Build() {
			var label = PUIElements.CreateUI(Name);
			// Background
			if (BackColor.a > 0)
				label.AddComponent<Image>().color = BackColor;
			// Add foreground image
			if (Sprite != null)
				ImageChildHelper(label, Sprite, SpriteTransform, SpriteSize);
			// Add text
			if (!string.IsNullOrEmpty(Text))
				TextChildHelper(label, TextStyle ?? PUITuning.Fonts.UILightStyle, Text);
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				label.AddComponent<ToolTip>().toolTip = ToolTip;
			label.SetActive(true);
			// Icon and text are side by side
			var lp = new BoxLayoutParams() {
				Spacing = IconSpacing, Direction = PanelDirection.Horizontal, Margin = Margin,
				Alignment = TextAlignment
			};
			if (DynamicSize)
				label.AddComponent<BoxLayoutGroup>().Params = lp;
			else
				BoxLayoutGroup.LayoutNow(label, lp);
			label.SetFlexUISize(FlexSize);
			InvokeRealize(label);
			return label;
		}

		/// <summary>
		/// Sets the background color to the default Klei dialog blue.
		/// </summary>
		/// <returns>This label for call chaining.</returns>
		public PLabel SetKleiBlueColor() {
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the background color to the Klei dialog header pink.
		/// </summary>
		/// <returns>This label for call chaining.</returns>
		public PLabel SetKleiPinkColor() {
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			return this;
		}
	}
}
