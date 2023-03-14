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
using System;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which represents a string and displays a text field.
	/// </summary>
	public class StringOptionsEntry : OptionsEntry {
		public override object Value {
			get {
				return value;
			}
			set {
				this.value = (value == null) ? "" : value.ToString();
				Update();
			}
		}

		/// <summary>
		/// The maximum entry length.
		/// </summary>
		private readonly int maxLength;

		/// <summary>
		/// The realized text field.
		/// </summary>
		private GameObject textField;

		/// <summary>
		/// The value in the text field.
		/// </summary>
		private string value;

		public StringOptionsEntry(string field, IOptionSpec spec,
				LimitAttribute limit = null) : base(field, spec) {
			// Use the maximum limit value for the max length, if present
			if (limit != null)
				maxLength = Math.Max(2, (int)Math.Round(limit.Maximum));
			else
				maxLength = 256;
			textField = null;
			value = "";
		}

		public override GameObject GetUIComponent() {
			textField = new PTextField() {
				OnTextChanged = OnTextChanged, ToolTip = LookInStrings(Tooltip),
				Text = value.ToString(), MinWidth = 128, Type = PTextField.FieldType.Text,
				MaxLength = maxLength
			}.Build();
			Update();
			return textField;
		}

		/// <summary>
		/// Called when the input field's text is changed.
		/// </summary>
		/// <param name="text">The new text.</param>
		private void OnTextChanged(GameObject _, string text) {
			value = text;
			Update();
		}

		/// <summary>
		/// Updates the displayed value.
		/// </summary>
		private void Update() {
			TMP_InputField field;
			if (textField != null && (field = textField.
					GetComponentInChildren<TMP_InputField>()) != null)
				field.text = value;
		}
	}
}
