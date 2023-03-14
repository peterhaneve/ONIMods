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
using PeterHan.PLib.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// The abstract base of options entries that display a color picker with sliders.
	/// </summary>
	internal abstract class ColorBaseOptionsEntry : OptionsEntry {
		/// <summary>
		/// The margin between the color sliders and the rest of the dialog.
		/// </summary>
		protected static readonly RectOffset ENTRY_MARGIN = new RectOffset(10, 10, 2, 5);
		
		/// <summary>
		/// The margin around each slider.
		/// </summary>
		protected static readonly RectOffset SLIDER_MARGIN = new RectOffset(10, 0, 2, 2);

		/// <summary>
		/// The size of the sample swatch.
		/// </summary>
		protected const float SWATCH_SIZE = 32.0f;

		/// <summary>
		/// The hue displayed gradient.
		/// </summary>
		protected ColorGradient hueGradient;
		
		/// <summary>
		/// The hue slider.
		/// </summary>
		protected KSlider hueSlider;
		
		/// <summary>
		/// The realized text field for BLUE.
		/// </summary>
		protected TMP_InputField blue;

		/// <summary>
		/// The realized text field for GREEN.
		/// </summary>
		protected TMP_InputField green;

		/// <summary>
		/// The realized text field for RED.
		/// </summary>
		protected TMP_InputField red;
		
		/// <summary>
		/// The saturation displayed gradient.
		/// </summary>
		protected ColorGradient satGradient;

		/// <summary>
		/// The saturation slider.
		/// </summary>
		protected KSlider satSlider;

		/// <summary>
		/// The color sample swatch.
		/// </summary>
		protected Image swatch;

		/// <summary>
		/// The value displayed gradient.
		/// </summary>
		protected ColorGradient valGradient;

		/// <summary>
		/// The value slider.
		/// </summary>
		protected KSlider valSlider;

		/// <summary>
		/// The value as a Color.
		/// </summary>
		protected Color value;

		protected ColorBaseOptionsEntry(string field, IOptionSpec spec) : base(field, spec) {
			value = Color.white;
			blue = null;
			green = null;
			red = null;
			hueGradient = null; hueSlider = null;
			satGradient = null; satSlider = null;
			valGradient = null; valSlider = null;
			swatch = null;
		}
		
		public override void CreateUIEntry(PGridPanel parent, ref int row) {
			base.CreateUIEntry(parent, ref row);
			// Add 3 rows for the H, S, and V
			parent.AddRow(new GridRowSpec());
			var h = new PSliderSingle("Hue") {
				ToolTip = PLibStrings.TOOLTIP_HUE, MinValue = 0.0f, MaxValue = 1.0f,
				CustomTrack = true, FlexSize = Vector2.right, OnValueChanged = OnHueChanged
			}.AddOnRealize(OnHueRealized);
			var s = new PSliderSingle("Saturation") {
				ToolTip = PLibStrings.TOOLTIP_SATURATION, MinValue = 0.0f, MaxValue = 1.0f,
				CustomTrack = true, FlexSize = Vector2.right, OnValueChanged = OnSatChanged
			}.AddOnRealize(OnSatRealized);
			var v = new PSliderSingle("Value") {
				ToolTip = PLibStrings.TOOLTIP_VALUE, MinValue = 0.0f, MaxValue = 1.0f,
				CustomTrack = true, FlexSize = Vector2.right, OnValueChanged = OnValChanged
			}.AddOnRealize(OnValRealized);
			var sw = new PLabel("Swatch") {
				ToolTip = LookInStrings(Tooltip), DynamicSize = false,
				Sprite = PUITuning.Images.BoxBorder, SpriteMode = Image.Type.Sliced,
				SpriteSize = new Vector2(SWATCH_SIZE, SWATCH_SIZE)
			}.AddOnRealize(OnSwatchRealized);
			var panel = new PRelativePanel("ColorPicker") {
				FlexSize = Vector2.right, DynamicSize = false
			}.AddChild(h).AddChild(s).AddChild(v).AddChild(sw).SetRightEdge(h, fraction: 1.0f).
				SetRightEdge(s, fraction: 1.0f).SetRightEdge(v, fraction: 1.0f).
				SetLeftEdge(sw, fraction: 0.0f).SetMargin(h, SLIDER_MARGIN).
				SetMargin(s, SLIDER_MARGIN).SetMargin(v, SLIDER_MARGIN).AnchorYAxis(sw).
				SetLeftEdge(h, toRight: sw).SetLeftEdge(s, toRight: sw).
				SetLeftEdge(v, toRight: sw).SetTopEdge(h, fraction: 1.0f).
				SetBottomEdge(v, fraction: 0.0f).SetTopEdge(s, below: h).
				SetTopEdge(v, below: s);
			parent.AddChild(panel, new GridComponentSpec(++row, 0) {
				ColumnSpan = 2, Margin = ENTRY_MARGIN
			});
		}

		public override GameObject GetUIComponent() {
			Color32 rgb = value;
			var go = new PPanel("RGB") {
				DynamicSize = false, Alignment = TextAnchor.MiddleRight, Spacing = 5,
				Direction = PanelDirection.Horizontal
			}.AddChild(new PLabel("Red") {
				TextStyle = PUITuning.Fonts.TextLightStyle, Text = PLibStrings.LABEL_R
			}).AddChild(new PTextField("RedValue") {
				OnTextChanged = OnRGBChanged, ToolTip = PLibStrings.TOOLTIP_RED,
				Text = rgb.r.ToString(), MinWidth = 32, MaxLength = 3,
				Type = PTextField.FieldType.Integer
			}.AddOnRealize(OnRedRealized)).AddChild(new PLabel("Green") {
				TextStyle = PUITuning.Fonts.TextLightStyle, Text = PLibStrings.LABEL_G
			}).AddChild(new PTextField("GreenValue") {
				OnTextChanged = OnRGBChanged, ToolTip = PLibStrings.TOOLTIP_GREEN,
				Text = rgb.g.ToString(), MinWidth = 32, MaxLength = 3,
				Type = PTextField.FieldType.Integer
			}.AddOnRealize(OnGreenRealized)).AddChild(new PLabel("Blue") {
				TextStyle = PUITuning.Fonts.TextLightStyle, Text = PLibStrings.LABEL_B
			}).AddChild(new PTextField("BlueValue") {
				OnTextChanged = OnRGBChanged, ToolTip = PLibStrings.TOOLTIP_BLUE,
				Text = rgb.b.ToString(), MinWidth = 32, MaxLength = 3,
				Type = PTextField.FieldType.Integer
			}.AddOnRealize(OnBlueRealized)).Build();
			UpdateAll();
			return go;
		}
		
		private void OnBlueRealized(GameObject realized) {
			blue = realized.GetComponentInChildren<TMP_InputField>();
		}

		private void OnGreenRealized(GameObject realized) {
			green = realized.GetComponentInChildren<TMP_InputField>();
		}

		private void OnHueChanged(GameObject _, float newHue) {
			if (hueGradient != null && hueSlider != null) {
				float oldAlpha = value.a;
				hueGradient.Position = hueSlider.value;
				value = hueGradient.SelectedColor;
				value.a = oldAlpha;
				UpdateRGB();
				UpdateSat(false);
				UpdateVal(false);
			}
		}

		private void OnHueRealized(GameObject realized) {
			hueGradient = realized.AddOrGet<ColorGradient>();
			realized.TryGetComponent(out hueSlider);
		}

		private void OnRedRealized(GameObject realized) {
			red = realized.GetComponentInChildren<TMP_InputField>();
		}
		
		/// <summary>
		/// Called when the red, green, or blue field's text is changed.
		/// </summary>
		/// <param name="text">The new color value.</param>
		protected void OnRGBChanged(GameObject _, string text) {
			Color32 rgb = value;
			if (byte.TryParse(red.text, out byte r))
				rgb.r = r;
			if (byte.TryParse(green.text, out byte g))
				rgb.g = g;
			if (byte.TryParse(blue.text, out byte b))
				rgb.b = b;
			value = rgb;
			UpdateAll();
		}

		private void OnSatChanged(GameObject _, float newSat) {
			if (satGradient != null && satSlider != null) {
				float oldAlpha = value.a;
				satGradient.Position = satSlider.value;
				value = satGradient.SelectedColor;
				value.a = oldAlpha;
				UpdateRGB();
				UpdateHue(false);
				UpdateVal(false);
			}
		}

		private void OnSatRealized(GameObject realized) {
			satGradient = realized.AddOrGet<ColorGradient>();
			realized.TryGetComponent(out satSlider);
		}
		
		private void OnSwatchRealized(GameObject realized) {
			swatch = realized.GetComponentInChildren<Image>();
		}

		private void OnValChanged(GameObject _, float newValue) {
			if (valGradient != null && valSlider != null) {
				float oldAlpha = value.a;
				valGradient.Position = valSlider.value;
				value = valGradient.SelectedColor;
				value.a = oldAlpha;
				UpdateRGB();
				UpdateHue(false);
				UpdateSat(false);
			}
		}

		private void OnValRealized(GameObject realized) {
			valGradient = realized.AddOrGet<ColorGradient>();
			realized.TryGetComponent(out valSlider);
		}

		/// <summary>
		/// If the color is changed externally, updates all sliders.
		/// </summary>
		protected void UpdateAll() {
			UpdateRGB();
			UpdateHue(true);
			UpdateSat(true);
			UpdateVal(true);
		}

		/// <summary>
		/// Updates the position of the hue slider with the currently selected color.
		/// </summary>
		/// <param name="moveSlider">true to move the slider handle if necessary, or false to
		/// leave it where it is.</param>
		protected void UpdateHue(bool moveSlider) {
			if (hueGradient != null && hueSlider != null) {
				Color.RGBToHSV(value, out _, out float s, out float v);
				hueGradient.SetRange(0.0f, 1.0f, s, s, v, v);
				hueGradient.SelectedColor = value;
				if (moveSlider)
					hueSlider.value = hueGradient.Position;
			}
		}

		/// <summary>
		/// Updates the displayed value.
		/// </summary>
		protected void UpdateRGB() {
			Color32 rgb = value;
			if (red != null)
				red.text = rgb.r.ToString();
			if (green != null)
				green.text = rgb.g.ToString();
			if (blue != null)
				blue.text = rgb.b.ToString();
			if (swatch != null)
				swatch.color = value;
		}

		/// <summary>
		/// Updates the position of the saturation slider with the currently selected color.
		/// </summary>
		/// <param name="moveSlider">true to move the slider handle if necessary, or false to
		/// leave it where it is.</param>
		protected void UpdateSat(bool moveSlider) {
			if (satGradient != null && satSlider != null) {
				Color.RGBToHSV(value, out float h, out _, out float v);
				satGradient.SetRange(h, h, 0.0f, 1.0f, v, v);
				satGradient.SelectedColor = value;
				if (moveSlider)
					satSlider.value = satGradient.Position;
			}
		}

		/// <summary>
		/// Updates the position of the value slider with the currently selected color.
		/// </summary>
		/// <param name="moveSlider">true to move the slider handle if necessary, or false to
		/// leave it where it is.</param>
		protected void UpdateVal(bool moveSlider) {
			if (valGradient != null && valSlider != null) {
				Color.RGBToHSV(value, out float h, out float s, out _);
				valGradient.SetRange(h, h, s, s, 0.0f, 1.0f);
				valGradient.SelectedColor = value;
				if (moveSlider)
					valSlider.value = valGradient.Position;
			}
		}
	}
}
