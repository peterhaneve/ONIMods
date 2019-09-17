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
			float width = SpriteSize.x, height = SpriteSize.y;
			// Background
			if (BackColor.a > 0)
				label.AddComponent<Image>().color = BackColor;
			// Add foreground image since the background already has one
			if (Sprite != null) {
				var img = label.AddOrGet<Image>();
				img.sprite = Sprite;
				img.preserveAspect = true;
			}
			// Add text
			if (!string.IsNullOrEmpty(Text)) {
				var text = PUIElements.AddLocText(label);
				// Font needs to be set before the text
				text.alignment = TextAlignment;
				text.fontSize = (FontSize > 0.0f) ? FontSize : PUITuning.DefaultFontSize;
				text.font = PUITuning.ButtonFont;
				text.enableWordWrapping = WordWrap;
				text.text = Text;
			}
			PUIElements.AddSizeFitter(label);
			// Set flex size
			var le = label.AddComponent<LayoutElement>();
			le.flexibleWidth = FlexSize.x;
			le.flexibleHeight = FlexSize.y;
			if (Sprite != null) {
				if (width > 0.0f)
					le.preferredWidth = width;
				if (height > 0.0f)
					le.preferredHeight = height;
			}
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				label.AddComponent<ToolTip>().toolTip = ToolTip;
			label.SetActive(true);
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
