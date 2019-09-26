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
		public PUIDelegates.OnButtonPressed OnClick { get; set; }

		public PButton() : this(null) { }

		public PButton(string name) : base(name ?? "Button") {
			Margin = BUTTON_MARGIN;
			Sprite = null;
			Text = null;
			ToolTip = "";
		}

		public override GameObject Build() {
			var button = PUIElements.CreateUI(Name);
			// Background
			var kImage = button.AddComponent<KImage>();
			var trueColor = Color ?? PUITuning.Colors.ButtonPinkStyle;
			kImage.colorStyleSetting = trueColor;
			kImage.color = trueColor.inactiveColor;
			kImage.sprite = PUITuning.Images.ButtonBorder;
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
			if (Sprite != null)
				kButton.fgImage = ImageChildHelper(button, Sprite, SpriteTransform, SpriteSize);
			// Set colors
			kButton.colorStyleSetting = trueColor;
			// Add text
			if (!string.IsNullOrEmpty(Text))
				PLabel.TextChildHelper(button, TextStyle ?? PUITuning.Fonts.UILightStyle, Text);
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				button.AddComponent<ToolTip>().toolTip = ToolTip;
			button.SetActive(true);
			// Icon and text are side by side
			var lp = new BoxLayoutParams() {
				Spacing = IconSpacing, Direction = PanelDirection.Horizontal, Margin = Margin,
				Alignment = TextAlignment
			};
			if (DynamicSize)
				button.AddComponent<BoxLayoutGroup>().Params = lp;
			else
				BoxLayoutGroup.LayoutNow(button, lp);
			button.SetFlexUISize(FlexSize);
			InvokeRealize(button);
			return button;
		}

		/// <summary>
		/// Sets the sprite to a leftward facing arrow. Beware the size, scale the button down!
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetImageLeftArrow() {
			Sprite = PUITuning.Images.Arrow;
			SpriteTransform = ImageTransform.FlipHorizontal;
			return this;
		}

		/// <summary>
		/// Sets the sprite to a rightward facing arrow. Beware the size, scale the button
		/// down!
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetImageRightArrow() {
			Sprite = PUITuning.Images.Arrow;
			SpriteTransform = ImageTransform.None;
			return this;
		}

		/// <summary>
		/// Sets the default Klei pink button style as this button's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetKleiPinkStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			Color = PUITuning.Colors.ButtonPinkStyle;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue button style as this button's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PButton SetKleiBlueStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			Color = PUITuning.Colors.ButtonBlueStyle;
			return this;
		}
	}
}
