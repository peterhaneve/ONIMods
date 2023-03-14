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
	/// An options entry which represents float? and displays a text field and slider.
	/// </summary>
	public class NullableFloatOptionsEntry : SlidingBaseOptionsEntry {
		/// <summary>
		/// The text that is rendered for the current value of the entry.
		/// </summary>
		protected virtual string FieldText {
			get {
				return value?.ToString(Format ?? FloatOptionsEntry.DEFAULT_FORMAT) ?? string.
					Empty;
			}
		}

		public override object Value {
			get {
				return value;
			}
			set {
				if (value == null) {
					this.value = null;
					Update();
				} else if (value is float newValue) {
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
		private float? value;

		public NullableFloatOptionsEntry(string field, IOptionSpec spec,
				LimitAttribute limit = null) : base(field, spec, limit) {
			textField = null;
			value = null;
		}

		protected override PSliderSingle GetSlider() {
			float minLimit = (float)limits.Minimum, maxLimit = (float)limits.Maximum;
			return new PSliderSingle() {
				OnValueChanged = OnSliderChanged, ToolTip = LookInStrings(Tooltip),
				MinValue = minLimit, MaxValue = maxLimit, InitialValue = 0.5f * (minLimit +
				maxLimit)
			};
		}

		public override GameObject GetUIComponent() {
			textField = new PTextField() {
				OnTextChanged = OnTextChanged, ToolTip = LookInStrings(Tooltip),
				Text = FieldText, MinWidth = 64, MaxLength = 16,
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
			if (string.IsNullOrWhiteSpace(text))
				// Limits are assumed to allow null, because why not use non-nullable
				// otherwise?
				value = null;
			else if (float.TryParse(text, out float newValue)) {
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
				field.text = FieldText;
			if (slider != null && value != null)
				PSliderSingle.SetCurrentValue(slider, (float)value);
		}
	}
}
