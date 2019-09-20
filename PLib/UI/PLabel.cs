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
		/// The label's background color. If null, no background color is applied.
		/// </summary>
		public Color BackColor { get; set; }

		public PLabel() : this(null) { }

		public PLabel(string name) : base(name ?? "Label") {
			BackColor = PUIElements.TRANSPARENT;
		}

		public override GameObject Build() {
			var label = PUIElements.CreateUI(Name);
			// Background
			if (BackColor.a > 0)
				label.AddComponent<Image>().color = BackColor;
			// Add foreground image
			if (Sprite != null) {
				var imageChild = PUIElements.CreateUI("Image");
				var img = imageChild.AddComponent<Image>();
				PUIElements.SetParent(imageChild, label);
				img.sprite = Sprite;
				img.preserveAspect = true;
				// Limit size if needed
				if (SpriteSize.x > 0.0f && SpriteSize.y > 0.0f)
					PUIElements.SetSizeImmediate(imageChild, SpriteSize);
			}
			// Add text
			if (!string.IsNullOrEmpty(Text)) {
				var textChild = PUIElements.CreateUI("Text");
				var text = PUIElements.AddLocText(textChild);
				PUIElements.SetParent(textChild, label);
				// Font needs to be set before the text
				text.alignment = TMPro.TextAlignmentOptions.Center;
				text.fontSize = (FontSize > 0.0f) ? FontSize : PUITuning.DefaultFontSize;
				text.font = PUITuning.ButtonFont;
				text.enableWordWrapping = WordWrap;
				text.text = Text;
			}
			// Icon and text are side by side
			var lg = label.AddComponent<HorizontalLayoutGroup>();
			lg.childAlignment = TextAnchor.MiddleLeft;
			lg.spacing = Math.Max(IconSpacing, 0);
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				label.AddComponent<ToolTip>().toolTip = ToolTip;
			PUIElements.AddSizeFitter(label, DynamicSize).SetFlexUISize(FlexSize).SetActive(
				true);
			InvokeRealize(label);
			return label;
		}

		/// <summary>
		/// Sets the background color to the default Klei dialog blue.
		/// </summary>
		/// <returns>This label for call chaining.</returns>
		public PLabel SetKleiBlueColor() {
			BackColor = PUITuning.ButtonStyleBlue.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the background color to the Klei dialog header pink.
		/// </summary>
		/// <returns>This label for call chaining.</returns>
		public PLabel SetKleiPinkColor() {
			BackColor = PUITuning.ButtonStylePink.inactiveColor;
			return this;
		}
	}
}
