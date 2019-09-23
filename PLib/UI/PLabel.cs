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
		/// Shared routine to spawn UI image objects.
		/// </summary>
		/// <param name="parent">The parent object for the image.</param>
		/// <param name="sprite">The sprite to display.</param>
		/// <param name="imageSize">The size to which to scale the sprite.</param>
		/// <returns>The child image object.</returns>
		internal static Image ImageChildHelper(GameObject parent, Sprite sprite,
				Vector2 imageSize = default) {
			var imageChild = PUIElements.CreateUI("Image");
			var img = imageChild.AddComponent<Image>();
			PUIElements.SetParent(imageChild, parent);
			img.sprite = sprite;
			img.preserveAspect = true;
			// Limit size if needed
			if (imageSize.x > 0.0f && imageSize.y > 0.0f)
				PUIElements.SetSizeImmediate(imageChild, imageSize);
			return img;
		}

		/// <summary>
		/// Shared routine to spawn UI text objects.
		/// </summary>
		/// <param name="parent">The parent object for the text.</param>
		/// <param name="fontSize">The font size.</param>
		/// <param name="contents">The default text.</param>
		/// <param name="wordWrap">Whether to enable word wrap.</param>
		/// <returns>The child text object.</returns>
		internal static LocText TextChildHelper(GameObject parent, float fontSize,
				string contents = "", bool wordWrap = false) {
			var textChild = PUIElements.CreateUI("Text");
			var locText = PUIElements.AddLocText(textChild);
			PUIElements.SetParent(textChild, parent);
			// Font needs to be set before the text
			locText.alignment = TMPro.TextAlignmentOptions.Center;
			locText.fontSize = (fontSize > 0.0f) ? fontSize : PUITuning.DefaultFontSize;
			locText.font = PUITuning.ButtonFont;
			locText.text = contents;
			locText.enableWordWrapping = wordWrap;
			return locText;
		}

		/// <summary>
		/// The label's background color.
		/// </summary>
		public Color BackColor { get; set; }

		public PLabel() : this(null) { }

		public PLabel(string name) : base(name ?? "Label") {
			BackColor = PUITuning.Colors.Transparent;
		}

		public override GameObject Build() {
			var label = PUIElements.CreateUI(Name);
			// Background
			if (BackColor.a > 0)
				label.AddComponent<Image>().color = BackColor;
			// Add foreground image
			if (Sprite != null)
				ImageChildHelper(label, Sprite, SpriteSize);
			// Add text
			if (!string.IsNullOrEmpty(Text))
				TextChildHelper(label, FontSize, Text, WordWrap);
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
