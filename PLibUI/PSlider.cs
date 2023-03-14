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

using PeterHan.PLib.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI slider factory class with one handle. Does not include a text field to set
	/// the value.
	/// </summary>
	public class PSliderSingle : IUIComponent {
		/// <summary>
		/// Sets the current value of a realized slider.
		/// </summary>
		/// <param name="realized">The realized slider.</param>
		/// <param name="value">The value to set.</param>
		public static void SetCurrentValue(GameObject realized, float value) {
			if (realized != null && realized.TryGetComponent(out KSlider slider) && !value.
					IsNaNOrInfinity())
				slider.value = value.InRange(slider.minValue, slider.maxValue);
		}

		/// <summary>
		/// If true, the default Klei track and fill will be skipped; only the handle will be
		/// shown.
		/// </summary>
		public bool CustomTrack { get; set; }

		/// <summary>
		/// The direction of the slider. The slider goes from minimum to maximum value in the
		/// direction indicated, i.e. LeftToRight is minimum left, maximum right.
		/// </summary>
		public Slider.Direction Direction { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The slider's handle color.
		/// </summary>
		public ColorStyleSetting HandleColor { get; set; }

		/// <summary>
		/// The size of the slider handle.
		/// </summary>
		public float HandleSize { get; set; }

		/// <summary>
		/// The initial slider value.
		/// </summary>
		public float InitialValue { get; set; }

		/// <summary>
		/// true to make the slider snap to integers, or false to allow any representable
		/// floating point number in the range.
		/// </summary>
		public bool IntegersOnly { get; set; }

		/// <summary>
		/// The maximum value that can be set by this slider. The slider is a linear scale, but
		/// can be post-scaled by the user to nonlinear if necessary.
		/// </summary>
		public float MaxValue { get; set; }

		/// <summary>
		/// The minimum value that can be set by this slider.
		/// </summary>
		public float MinValue { get; set; }

		public string Name { get; }

		/// <summary>
		/// The action to trigger during slider dragging.
		/// </summary>
		public PUIDelegates.OnSliderDrag OnDrag { get; set; }

		/// <summary>
		/// The preferred length of the scrollbar. If vertical, this is the height, otherwise
		/// it is the width.
		/// </summary>
		public float PreferredLength { get; set; }

		/// <summary>
		/// The action to trigger after the slider is changed. It is passed the realized source
		/// object and new value.
		/// </summary>
		public PUIDelegates.OnSliderChanged OnValueChanged { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		/// <summary>
		/// The tool tip text. If {0} is present, it will be formatted with the slider's
		/// current value.
		/// </summary>
		public string ToolTip { get; set; }

		/// <summary>
		/// The size of the slider track.
		/// </summary>
		public float TrackSize { get; set; }

		public PSliderSingle() : this("SliderSingle") { }

		public PSliderSingle(string name) {
			CustomTrack = false;
			Direction = Slider.Direction.LeftToRight;
			HandleColor = PUITuning.Colors.ButtonPinkStyle;
			HandleSize = 16.0f;
			InitialValue = 0.5f;
			IntegersOnly = false;
			MaxValue = 1.0f;
			MinValue = 0.0f;
			Name = name;
			PreferredLength = 100.0f;
			TrackSize = 12.0f;
		}

		/// <summary>
		/// Adds a handler when this slider is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This slider for call chaining.</returns>
		public PSliderSingle AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public GameObject Build() {
			// Bounds must be valid
			if (MaxValue.IsNaNOrInfinity())
				throw new ArgumentException(nameof(MaxValue));
			if (MinValue.IsNaNOrInfinity())
				throw new ArgumentException(nameof(MinValue));
			// max > min
			if (MaxValue <= MinValue)
				throw new ArgumentOutOfRangeException(nameof(MaxValue));
			// Initial value must be in range
			var slider = PUIElements.CreateUI(null, Name);
			bool isVertical = Direction == Slider.Direction.BottomToTop || Direction ==
				Slider.Direction.TopToBottom;
			var trueColor = HandleColor ?? PUITuning.Colors.ButtonBlueStyle;
			slider.SetActive(false);
			// Track (visual)
			if (!CustomTrack) {
				var trackImg = slider.AddComponent<Image>();
				trackImg.sprite = isVertical ? PUITuning.Images.ScrollBorderVertical :
					PUITuning.Images.ScrollBorderHorizontal;
				trackImg.type = Image.Type.Sliced;
			}
			// Fill
			var fill = PUIElements.CreateUI(slider, "Fill", true);
			if (!CustomTrack) {
				var fillImg = fill.AddComponent<Image>();
				fillImg.sprite = isVertical ? PUITuning.Images.ScrollHandleVertical :
					PUITuning.Images.ScrollHandleHorizontal;
				fillImg.color = trueColor.inactiveColor;
				fillImg.type = Image.Type.Sliced;
			}
			PUIElements.SetAnchorOffsets(fill, 1.0f, 1.0f, 1.0f, 1.0f);
			// Slider component itself
			var ks = slider.AddComponent<KSlider>();
			ks.maxValue = MaxValue;
			ks.minValue = MinValue;
			ks.value = InitialValue.IsNaNOrInfinity() ? MinValue : InitialValue.InRange(
				MinValue, MaxValue);
			ks.wholeNumbers = IntegersOnly;
			ks.handleRect = CreateHandle(slider).rectTransform();
			ks.fillRect = fill.rectTransform();
			ks.SetDirection(Direction, true);
			if (OnValueChanged != null)
				ks.onValueChanged.AddListener((value) => OnValueChanged(slider, value));
			if (OnDrag != null)
				ks.onDrag += () => OnDrag(slider, ks.value);
			// Manually add tooltip with slider link
			string tt = ToolTip;
			if (!string.IsNullOrEmpty(tt)) {
				var toolTip = slider.AddComponent<ToolTip>();
				toolTip.OnToolTip = () => string.Format(tt, ks.value);
				// Tooltip can be dynamically updated
				toolTip.refreshWhileHovering = true;
			}
			slider.SetActive(true);
			// Static layout!
			slider.SetMinUISize(isVertical ? new Vector2(TrackSize, PreferredLength) :
				new Vector2(PreferredLength, TrackSize));
			slider.SetFlexUISize(FlexSize);
			OnRealize?.Invoke(slider);
			return slider;
		}

		/// <summary>
		/// Creates the handle component.
		/// </summary>
		/// <param name="slider">The parent component.</param>
		/// <returns>The sliding handle object.</returns>
		private GameObject CreateHandle(GameObject slider) {
			// Handle
			var handle = PUIElements.CreateUI(slider, "Handle", true, PUIAnchoring.Center,
				PUIAnchoring.Center);
			var handleImg = handle.AddComponent<Image>();
			handleImg.sprite = PUITuning.Images.SliderHandle;
			handleImg.preserveAspect = true;
			handle.SetUISize(new Vector2(HandleSize, HandleSize));
			// Rotate the handle if needed (CCW)
			float rot = 0.0f;
			switch (Direction) {
			case Slider.Direction.TopToBottom:
				rot = 90.0f;
				break;
			case Slider.Direction.RightToLeft:
				rot = 180.0f;
				break;
			case Slider.Direction.BottomToTop:
				rot = 270.0f;
				break;
			default:
				break;
			}
			if (rot != 0.0f)
				handle.transform.Rotate(new Vector3(0.0f, 0.0f, rot));
			return handle;
		}

		/// <summary>
		/// Sets the default Klei pink button style as this slider's foreground color style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PSliderSingle SetKleiPinkStyle() {
			HandleColor = PUITuning.Colors.ButtonPinkStyle;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue button style as this slider's foreground color style.
		/// 
		/// Note that the default slider handle has a hard coded pink color.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PSliderSingle SetKleiBlueStyle() {
			HandleColor = PUITuning.Colors.ButtonBlueStyle;
			return this;
		}

		public override string ToString() {
			return string.Format("PSliderSingle[Name={0}]", Name);
		}
	}
}
