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
	/// A custom UI button factory class.
	/// </summary>
	public class PButton : PTextComponent {
		/// <summary>
		/// The default margins around a button.
		/// </summary>
		internal static readonly RectOffset BUTTON_MARGIN = new RectOffset(7, 7, 5, 5);

		/// <summary>
		/// The button's background color.
		/// </summary>
		public ColorStyleSetting Color { get; set; }

		/// <summary>
		/// The margin around the component.
		/// </summary>
		public RectOffset Margin { get; set; }

		/// <summary>
		/// The action to trigger on click. It is passed the realized source object.
		/// </summary>
		public PUIDelegates.OnButtonPressed OnClick;

		public PButton() : this(null) { }

		public PButton(string name) : base(name ?? "Button") {
			Margin = BUTTON_MARGIN;
			Sprite = null;
			Text = null;
			ToolTip = "";
			WordWrap = false;
		}

		public override GameObject Build() {
			var button = PUIElements.CreateUI(Name);
			// Background
			var kImage = button.AddComponent<KImage>();
			var trueColor = Color ?? PUITuning.ButtonStylePink;
			kImage.colorStyleSetting = trueColor;
			kImage.color = trueColor.inactiveColor;
			kImage.sprite = PUITuning.ButtonImage;
			kImage.type = Image.Type.Sliced;
			// Set on click event
			var kButton = button.AddComponent<KButton>();
			var evt = OnClick;
			if (evt != null)
				kButton.onClick += () => {
					evt?.Invoke(button);
				};
			kButton.additionalKImages = new KImage[0];
			kButton.soundPlayer = PUITuning.ButtonSounds;
			kButton.bgImage = kImage;
			// Add foreground image since the background already has one
			if (Sprite != null) {
				var imageChild = PUIElements.CreateUI("Image");
				var img = imageChild.AddComponent<Image>();
				PUIElements.SetParent(imageChild, button);
				img.sprite = Sprite;
				img.preserveAspect = true;
				kButton.fgImage = img;
				// Limit size if needed
				if (SpriteSize.x > 0.0f && SpriteSize.y > 0.0f)
					PUIElements.SetSizeImmediate(imageChild, SpriteSize);
			}
			// Set colors
			kButton.colorStyleSetting = trueColor;
			// Add text
			if (!string.IsNullOrEmpty(Text)) {
				var textChild = PUIElements.CreateUI("Text");
				var text = PUIElements.AddLocText(textChild);
				PUIElements.SetParent(textChild, button);
				// Font needs to be set before the text
				text.alignment = TMPro.TextAlignmentOptions.Center;
				text.fontSize = (FontSize > 0.0f) ? FontSize : PUITuning.DefaultFontSize;
				text.font = PUITuning.ButtonFont;
				text.enableWordWrapping = WordWrap;
				text.text = Text;
			}
			// Icon and text are side by side
			var lg = button.AddComponent<HorizontalLayoutGroup>();
			lg.childAlignment = TextAnchor.MiddleLeft;
			lg.spacing = Math.Max(IconSpacing, 0);
			lg.padding = Margin;
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				button.AddComponent<ToolTip>().toolTip = ToolTip;
			PUIElements.AddSizeFitter(button, DynamicSize).SetFlexUISize(FlexSize).SetActive(
				true);
			InvokeRealize(button);
			return button;
		}

		/// <summary>
		/// Sets the default Klei pink button style as this button's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetKleiPinkStyle() {
			TextColor = PUITuning.UITextStyle;
			Color = PUITuning.ButtonStylePink;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue button style as this button's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetKleiBlueStyle() {
			TextColor = PUITuning.UITextStyle;
			Color = PUITuning.ButtonStyleBlue;
			return this;
		}
	}
}
