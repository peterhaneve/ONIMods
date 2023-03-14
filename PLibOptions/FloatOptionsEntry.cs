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

using PeterHan.PLib.UI;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which represents float and displays a text field and slider.
	/// </summary>
	public class FloatOptionsEntry : SlidingBaseOptionsEntry {
		/// <summary>
		/// The format to use if none is provided.
		/// </summary>
		public const string DEFAULT_FORMAT = "F2";

		public override object Value {
			get {
				return value;
			}
			set {
				if (value is float newValue) {
					this.value = newValue;
					Update();
				}
			}
		}

		/// <summary>
		/// The realized text field.
		/// </summary>
		private GameObject textField;

		/// <summary>
		/// The value in the text field.
		/// </summary>
		private float value;

		public FloatOptionsEntry(string field, IOptionSpec spec, LimitAttribute limit = null) :
				base(field, spec, limit) {
			textField = null;
			value = 0.0f;
		}

		protected override PSliderSingle GetSlider() {
			return new PSliderSingle() {
				OnValueChanged = OnSliderChanged, ToolTip = LookInStrings(Tooltip),
				MinValue = (float)limits.Minimum, MaxValue = (float)limits.Maximum,
				InitialValue = value
			};
		}

		public override GameObject GetUIComponent() {
			textField = new PTextField() {
				OnTextChanged = OnTextChanged, ToolTip = LookInStrings(Tooltip),
				Text = value.ToString(Format ?? DEFAULT_FORMAT), MinWidth = 64, MaxLength = 16,
				Type = PTextField.FieldType.Float
			}.Build();
			Update();
			return textField;
		}

		/// <summary>
		/// Called when the slider's value is changed.
		/// </summary>
		/// <param name="newValue">The new slider value.</param>
		private void OnSliderChanged(GameObject _, float newValue) {
			// Record the value
			if (limits != null)
				value = limits.ClampToRange(newValue);
			else
				value = newValue;
			Update();
		}

		/// <summary>
		/// Called when the input field's text is changed.
		/// </summary>
		/// <param name="text">The new text.</param>
		private void OnTextChanged(GameObject _, string text) {
			if (float.TryParse(text, out float newValue)) {
				if (Format != null && Format.ToUpperInvariant().IndexOf('P') >= 0)
					newValue *= 0.01f;
				if (limits != null)
					newValue = limits.ClampToRange(newValue);
				// Record the valid value
				value = newValue;
			}
			Update();
		}

		/// <summary>
		/// Updates the displayed value.
		/// </summary>
		protected override void Update() {
			var field = textField?.GetComponentInChildren<TMP_InputField>();
			if (field != null)
				field.text = value.ToString(Format ?? DEFAULT_FORMAT);
			if (slider != null)
				PSliderSingle.SetCurrentValue(slider, value);
		}
	}
}
