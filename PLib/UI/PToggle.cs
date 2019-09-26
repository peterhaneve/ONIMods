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
	/// A custom UI toggled button factory class.
	/// </summary>
	public sealed class PToggle : IDynamicSizable {
		/// <summary>
		/// The default margins around a toggle.
		/// </summary>
		internal static readonly RectOffset TOGGLE_MARGIN = new RectOffset(1, 1, 1, 1);

		/// <summary>
		/// The sprite to display when active.
		/// </summary>
		public Sprite ActiveSprite { get; set; }

		/// <summary>
		/// The toggle's color.
		/// </summary>
		public ColorStyleSetting Color { get; set; }

		public bool DynamicSize { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The sprite to display when inactive.
		/// </summary>
		public Sprite InactiveSprite { get; set; }

		/// <summary>
		/// The margin around the component.
		/// </summary>
		public RectOffset Margin { get; set; }

		public string Name { get; }

		/// <summary>
		/// The action to trigger when the state changes. It is passed the realized source
		/// object.
		/// </summary>
		public PUIDelegates.OnToggleButton OnStateChanged { get; set; }

		/// <summary>
		/// The size to scale the toggle images. If 0x0, it will not be scaled.
		/// </summary>
		public Vector2 Size { get; set; }

		/// <summary>
		/// The tool tip text.
		/// </summary>
		public string ToolTip { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		public PToggle() : this(null) { }

		public PToggle(string name) {
			ActiveSprite = PUITuning.Images.Contract;
			Color = PUITuning.Colors.CheckboxDarkStyle;
			Margin = TOGGLE_MARGIN;
			Name = name ?? "Toggle";
			InactiveSprite = PUITuning.Images.Expand;
			ToolTip = "";
		}

		public GameObject Build() {
			var toggle = PUIElements.CreateUI(Name);
			// Set on click event
			var kToggle = toggle.AddComponent<KToggle>();
			var evt = OnStateChanged;
			if (evt != null)
				kToggle.onValueChanged += (on) => {
					evt?.Invoke(toggle, on);
				};
			kToggle.artExtension = new KToggleArtExtensions();
			kToggle.soundPlayer = PUITuning.ToggleSounds;
			// Background image
			var fgImage = toggle.AddComponent<KImage>();
			fgImage.color = Color.activeColor;
			fgImage.sprite = InactiveSprite;
			// Toggled images
			var toggleImage = toggle.AddComponent<ImageToggleState>();
			toggleImage.TargetImage = fgImage;
			toggleImage.useSprites = true;
			toggleImage.InactiveSprite = InactiveSprite;
			toggleImage.ActiveSprite = ActiveSprite;
			toggleImage.startingState = ImageToggleState.State.Inactive;
			toggleImage.ActiveColour = Color.activeColor;
			toggleImage.DisabledActiveColour = Color.disabledActiveColor;
			toggleImage.InactiveColour = Color.inactiveColor;
			toggleImage.DisabledColour = Color.disabledColor;
			toggleImage.HoverColour = Color.hoverColor;
			toggleImage.DisabledHoverColor = Color.disabledhoverColor;
			// Set size
			if (Size.x > 0.0f && Size.y > 0.0f)
				PUIElements.SetSizeImmediate(toggle, Size);
			else
				PUIElements.AddSizeFitter(toggle, DynamicSize);
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				toggle.AddComponent<ToolTip>().toolTip = ToolTip;
			toggle.SetFlexUISize(FlexSize).SetActive(true);
			OnRealize?.Invoke(toggle);
			return toggle;
		}

		public override string ToString() {
			return "PToggle[Name={0}]".F(Name);
		}
	}
}
