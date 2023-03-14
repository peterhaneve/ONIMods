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
	/// A custom UI toggled button factory class.
	/// </summary>
	public sealed class PToggle : IDynamicSizable {
		/// <summary>
		/// The default margins around a toggle.
		/// </summary>
		internal static readonly RectOffset TOGGLE_MARGIN = new RectOffset(1, 1, 1, 1);

		/// <summary>
		/// Gets a realized toggle button's state.
		/// </summary>
		/// <param name="realized">The realized toggle button.</param>
		/// <returns>The toggle button state.</returns>
		public static bool GetToggleState(GameObject realized) {
			return realized != null && realized.TryGetComponent(out KToggle toggle) &&
				UIDetours.IS_ON.Get(toggle);
		}

		/// <summary>
		/// Sets a realized toggle button's state.
		/// </summary>
		/// <param name="realized">The realized toggle button.</param>
		/// <param name="on">Whether the button should be on or off.</param>
		public static void SetToggleState(GameObject realized, bool on) {
			if (realized != null && realized.TryGetComponent(out KToggle toggle))
				UIDetours.IS_ON.Set(toggle, on);
		}

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
		/// The initial state of the toggle button.
		/// </summary>
		public bool InitialState { get; set; }

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
			Color = PUITuning.Colors.ComponentDarkStyle;
			InitialState = false;
			Margin = TOGGLE_MARGIN;
			Name = name ?? "Toggle";
			InactiveSprite = PUITuning.Images.Expand;
			ToolTip = "";
		}

		/// <summary>
		/// Adds a handler when this toggle button is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This toggle button for call chaining.</returns>
		public PToggle AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public GameObject Build() {
			var toggle = PUIElements.CreateUI(null, Name);
			// Set on click event
			var kToggle = toggle.AddComponent<KToggle>();
			var evt = OnStateChanged;
			if (evt != null)
				kToggle.onValueChanged += (on) => {
					evt?.Invoke(toggle, on);
				};
			UIDetours.ART_EXTENSION.Set(kToggle, new KToggleArtExtensions());
			UIDetours.SOUND_PLAYER_TOGGLE.Set(kToggle, PUITuning.ToggleSounds);
			// Background image
			var fgImage = toggle.AddComponent<Image>();
			fgImage.color = Color.activeColor;
			fgImage.sprite = InactiveSprite;
			toggle.SetActive(false);
			// Toggled images
			var toggleImage = toggle.AddComponent<ImageToggleState>();
			toggleImage.TargetImage = fgImage;
			toggleImage.useSprites = true;
			toggleImage.InactiveSprite = InactiveSprite;
			toggleImage.ActiveSprite = ActiveSprite;
			toggleImage.startingState = InitialState ? ImageToggleState.State.Active :
				ImageToggleState.State.Inactive;
			toggleImage.useStartingState = true;
			toggleImage.ActiveColour = Color.activeColor;
			toggleImage.DisabledActiveColour = Color.disabledActiveColor;
			toggleImage.InactiveColour = Color.inactiveColor;
			toggleImage.DisabledColour = Color.disabledColor;
			toggleImage.HoverColour = Color.hoverColor;
			toggleImage.DisabledHoverColor = Color.disabledhoverColor;
			UIDetours.IS_ON.Set(kToggle, InitialState);
			toggle.SetActive(true);
			// Set size
			if (Size.x > 0.0f && Size.y > 0.0f)
				toggle.SetUISize(Size, true);
			else
				PUIElements.AddSizeFitter(toggle, DynamicSize);
			// Add tooltip
			PUIElements.SetToolTip(toggle, ToolTip).SetFlexUISize(FlexSize).SetActive(true);
			OnRealize?.Invoke(toggle);
			return toggle;
		}

		public override string ToString() {
			return string.Format("PToggle[Name={0}]", Name);
		}
	}
}
