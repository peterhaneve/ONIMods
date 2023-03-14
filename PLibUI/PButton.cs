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
	/// A custom UI button factory class.
	/// </summary>
	public class PButton : PTextComponent {
		/// <summary>
		/// The default margins around a button.
		/// </summary>
		internal static readonly RectOffset BUTTON_MARGIN = new RectOffset(7, 7, 5, 5);

		/// <summary>
		/// Sets up the button to have the right sound and background image.
		/// </summary>
		/// <param name="button">The button to set up.</param>
		/// <param name="bgImage">The background image.</param>
		internal static void SetupButton(KButton button, KImage bgImage) {
			UIDetours.ADDITIONAL_K_IMAGES.Set(button, new KImage[0]);
			UIDetours.SOUND_PLAYER_BUTTON.Set(button, PUITuning.ButtonSounds);
			UIDetours.BG_IMAGE.Set(button, bgImage);
		}

		/// <summary>
		/// Sets up the background image to have the right sprite and slice type.
		/// </summary>
		/// <param name="bgImage">The image that forms the button background.</param>
		internal static void SetupButtonBackground(KImage bgImage) {
			UIDetours.APPLY_COLOR_STYLE.Invoke(bgImage);
			bgImage.sprite = PUITuning.Images.ButtonBorder;
			bgImage.type = Image.Type.Sliced;
		}

		/// <summary>
		/// Enables or disables a realized button.
		/// </summary>
		/// <param name="obj">The realized button object.</param>
		/// <param name="enabled">true to make it enabled, or false to make it disabled (greyed out).</param>
		public static void SetButtonEnabled(GameObject obj, bool enabled) {
			if (obj != null && obj.TryGetComponent(out KButton button))
				UIDetours.IS_INTERACTABLE.Set(button, enabled);
		}

		/// <summary>
		/// The button's background color.
		/// </summary>
		public ColorStyleSetting Color { get; set; }

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

		/// <summary>
		/// Adds a handler when this button is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This button for call chaining.</returns>
		public PButton AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public override GameObject Build() {
			var button = PUIElements.CreateUI(null, Name);
			GameObject sprite = null, text = null;
			// Background
			var bgImage = button.AddComponent<KImage>();
			var bgColorStyle = Color ?? PUITuning.Colors.ButtonPinkStyle;
			UIDetours.COLOR_STYLE_SETTING.Set(bgImage, bgColorStyle);
			SetupButtonBackground(bgImage);
			// Set on click event
			var kButton = button.AddComponent<KButton>();
			var evt = OnClick;
			if (evt != null)
				// Detouring an Event is not worth the effort
				kButton.onClick += () => evt?.Invoke(button);
			SetupButton(kButton, bgImage);
			// Add foreground image since the background already has one
			if (Sprite != null) {
				var fgImage = ImageChildHelper(button, this);
				UIDetours.FG_IMAGE.Set(kButton, fgImage);
				sprite = fgImage.gameObject;
			}
			// Add text
			if (!string.IsNullOrEmpty(Text))
				text = TextChildHelper(button, TextStyle ?? PUITuning.Fonts.UILightStyle,
					Text).gameObject;
			// Add tooltip
			PUIElements.SetToolTip(button, ToolTip).SetActive(true);
			// Arrange the icon and text
			var layout = button.AddComponent<RelativeLayoutGroup>();
			layout.Margin = Margin;
			GameObject inner;
			ArrangeComponent(layout, inner = WrapTextAndSprite(text, sprite), TextAlignment);
			if (!DynamicSize) layout.LockLayout();
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			DestroyLayoutIfPossible(button);
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
