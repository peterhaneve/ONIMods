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

using System;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI button check box factory class.
	/// </summary>
	public class PCheckBox : PTextComponent {
		/// <summary>
		/// The border size between the checkbox border and icon.
		/// </summary>
		private const float CHECKBOX_MARGIN = 2.0f;

		/// <summary>
		/// The unchecked state.
		/// </summary>
		public const int STATE_UNCHECKED = 0;

		/// <summary>
		/// The checked state.
		/// </summary>
		public const int STATE_CHECKED = 1;

		/// <summary>
		/// The partially checked state.
		/// </summary>
		public const int STATE_PARTIAL = 2;

		/// <summary>
		/// Generates the checkbox image states.
		/// </summary>
		/// <param name="imageColor">The color style for the checked icon.</param>
		/// <returns>The states for this checkbox.</returns>
		private static ToggleState[] GenerateStates(ColorStyleSetting imageColor) {
			// Structs sadly do not work well with detours
			var sps = new StatePresentationSetting() {
				color = imageColor.activeColor, use_color_on_hover = true,
				color_on_hover = imageColor.hoverColor, image_target = null,
				name = "Partial"
			};
			return new ToggleState[] {
				new ToggleState() {
					// Unchecked
					color = PUITuning.Colors.Transparent, color_on_hover = PUITuning.Colors.
					Transparent, sprite = null, use_color_on_hover = false,
					additional_display_settings = new StatePresentationSetting[] {
						new StatePresentationSetting() {
							color = imageColor.activeColor, use_color_on_hover = false,
							image_target = null, name = "Unchecked"
						}
					}
				},
				new ToggleState() {
					// Checked
					color = imageColor.activeColor, color_on_hover = imageColor.hoverColor,
					sprite = PUITuning.Images.Checked, use_color_on_hover = true,
					additional_display_settings = new StatePresentationSetting[] { sps }
				},
				new ToggleState() {
					// Partial
					color = imageColor.activeColor, color_on_hover = imageColor.hoverColor,
					sprite = PUITuning.Images.Partial, use_color_on_hover = true,
					additional_display_settings = new StatePresentationSetting[] { sps }
				}
			};
		}

		/// <summary>
		/// Gets a realized check box's state.
		/// </summary>
		/// <param name="realized">The realized check box.</param>
		/// <returns>The check box state.</returns>
		public static int GetCheckState(GameObject realized) {
			int state = STATE_UNCHECKED;
			if (realized == null)
				throw new ArgumentNullException(nameof(realized));
			var checkButton = realized.GetComponentInChildren<MultiToggle>();
			if (checkButton != null)
				state = UIDetours.CURRENT_STATE.Get(checkButton);
			return state;
		}

		/// <summary>
		/// Sets a realized check box's state.
		/// </summary>
		/// <param name="realized">The realized check box.</param>
		/// <param name="state">The new state to set.</param>
		public static void SetCheckState(GameObject realized, int state) {
			if (realized == null)
				throw new ArgumentNullException(nameof(realized));
			var checkButton = realized.GetComponentInChildren<MultiToggle>();
			if (checkButton != null && UIDetours.CURRENT_STATE.Get(checkButton) != state)
				UIDetours.CHANGE_STATE.Invoke(checkButton, state);
		}

		/// <summary>
		/// The check box color.
		/// </summary>
		public ColorStyleSetting CheckColor { get; set; }

		/// <summary>
		/// The check box's background color.
		/// 
		/// Unlike other components, this color applies only to the check box itself.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// The size to scale the check box. If 0x0, it will not be scaled.
		/// </summary>
		public Vector2 CheckSize { get; set; }

		/// <summary>
		/// The background color of everything that is not the check box.
		/// </summary>
		public Color ComponentBackColor { get; set; }

		/// <summary>
		/// The initial check box state.
		/// </summary>
		public int InitialState { get; set; }

		/// <summary>
		/// The action to trigger on click. It is passed the realized source object.
		/// </summary>
		public PUIDelegates.OnChecked OnChecked { get; set; }

		public PCheckBox() : this(null) { }

		public PCheckBox(string name) : base(name ?? "CheckBox") {
			BackColor = PUITuning.Colors.BackgroundLight;
			CheckColor = null;
			CheckSize = new Vector2(16.0f, 16.0f);
			ComponentBackColor = PUITuning.Colors.Transparent;
			IconSpacing = 3;
			InitialState = STATE_UNCHECKED;
			Sprite = null;
			Text = null;
			ToolTip = "";
		}

		/// <summary>
		/// Adds a handler when this check box is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This check box for call chaining.</returns>
		public PCheckBox AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public override GameObject Build() {
			var checkbox = PUIElements.CreateUI(null, Name);
			var actualSize = CheckSize;
			GameObject sprite = null, text = null;
			// Background
			if (ComponentBackColor.a > 0)
				checkbox.AddComponent<Image>().color = ComponentBackColor;
			var trueColor = CheckColor ?? PUITuning.Colors.ComponentLightStyle;
			// Checkbox background
			var checkBG = PUIElements.CreateUI(checkbox, "CheckBox");
			checkBG.AddComponent<Image>().color = BackColor;
			var checkImage = CreateCheckImage(checkBG, trueColor, ref actualSize);
			checkBG.SetUISize(new Vector2(actualSize.x + 2.0f * CHECKBOX_MARGIN,
				actualSize.y + 2.0f * CHECKBOX_MARGIN), true);
			// Add foreground image
			if (Sprite != null)
				sprite = ImageChildHelper(checkbox, this).gameObject;
			// Add text
			if (!string.IsNullOrEmpty(Text))
				text = TextChildHelper(checkbox, TextStyle ?? PUITuning.Fonts.UILightStyle,
					Text).gameObject;
			// Toggle
			var mToggle = checkbox.AddComponent<MultiToggle>();
			var evt = OnChecked;
			if (evt != null)
				mToggle.onClick += () => evt?.Invoke(checkbox, mToggle.CurrentState);
			UIDetours.PLAY_SOUND_CLICK.Set(mToggle, true);
			UIDetours.PLAY_SOUND_RELEASE.Set(mToggle, false);
			mToggle.states = GenerateStates(trueColor);
			mToggle.toggle_image = checkImage;
			UIDetours.CHANGE_STATE.Invoke(mToggle, InitialState);
			PUIElements.SetToolTip(checkbox, ToolTip).SetActive(true);
			// Faster than ever!
			var subLabel = WrapTextAndSprite(text, sprite);
			var layout = checkbox.AddComponent<RelativeLayoutGroup>();
			layout.Margin = Margin;
			ArrangeComponent(layout, WrapTextAndSprite(subLabel, checkBG), TextAlignment);
			if (!DynamicSize) layout.LockLayout();
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			DestroyLayoutIfPossible(checkbox);
			InvokeRealize(checkbox);
			return checkbox;
		}

		/// <summary>
		/// Creates the actual image that shows the checkbox graphically.
		/// </summary>
		/// <param name="checkbox">The parent object to add the image.</param>
		/// <param name="color">The color style for the box border.</param>
		/// <param name="actualSize">The actual check mark size, which will be updated if it
		/// is 0x0 to the default size.</param>
		/// <returns>The image reference to the checkmark image itself.</returns>
		private Image CreateCheckImage(GameObject checkbox, ColorStyleSetting color,
				ref Vector2 actualSize) {
			// Checkbox border (grr rule of only one Graphics per GO...)
			var checkBorder = PUIElements.CreateUI(checkbox, "CheckBorder");
			var borderImg = checkBorder.AddComponent<Image>();
			borderImg.sprite = PUITuning.Images.CheckBorder;
			borderImg.color = color.activeColor;
			borderImg.type = Image.Type.Sliced;
			// Checkbox foreground
			var imageChild = PUIElements.CreateUI(checkbox, "CheckMark", true, PUIAnchoring.
				Center, PUIAnchoring.Center);
			var checkImage = imageChild.AddComponent<Image>();
			checkImage.sprite = PUITuning.Images.Checked;
			checkImage.preserveAspect = true;
			// Determine the checkbox size
			if (actualSize.x <= 0.0f || actualSize.y <= 0.0f) {
				var rt = imageChild.rectTransform();
				actualSize.x = LayoutUtility.GetPreferredWidth(rt);
				actualSize.y = LayoutUtility.GetPreferredHeight(rt);
			}
			imageChild.SetUISize(CheckSize, false);
			return checkImage;
		}

		/// <summary>
		/// Sets the default Klei pink button style as this check box's color and text style.
		/// </summary>
		/// <returns>This check box for call chaining.</returns>
		public PCheckBox SetKleiPinkStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			CheckColor = PUITuning.Colors.ComponentDarkStyle;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue button style as this check box's color and text style.
		/// </summary>
		/// <returns>This check box for call chaining.</returns>
		public PCheckBox SetKleiBlueStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			CheckColor = PUITuning.Colors.ComponentDarkStyle;
			return this;
		}
	}
}
