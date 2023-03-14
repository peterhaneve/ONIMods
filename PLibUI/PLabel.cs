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

		public PLabel() : this(null) { }

		public PLabel(string name) : base(name ?? "Label") {
			BackColor = PUITuning.Colors.Transparent;
		}

		/// <summary>
		/// Adds a handler when this label is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This label for call chaining.</returns>
		public PLabel AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public override GameObject Build() {
			var label = PUIElements.CreateUI(null, Name);
			GameObject sprite = null, text = null;
			// Background
			if (BackColor.a > 0)
				label.AddComponent<Image>().color = BackColor;
			// Add foreground image
			if (Sprite != null)
				sprite = ImageChildHelper(label, this).gameObject;
			// Add text
			if (!string.IsNullOrEmpty(Text))
				text = TextChildHelper(label, TextStyle ?? PUITuning.Fonts.UILightStyle,
					Text).gameObject;
			// Add tooltip
			PUIElements.SetToolTip(label, ToolTip).SetActive(true);
			// Arrange the icon and text
			var layout = label.AddComponent<RelativeLayoutGroup>();
			layout.Margin = Margin;
			ArrangeComponent(layout, WrapTextAndSprite(text, sprite), TextAlignment);
			if (!DynamicSize) layout.LockLayout();
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			DestroyLayoutIfPossible(label);
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
