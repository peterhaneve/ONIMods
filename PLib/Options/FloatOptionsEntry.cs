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

using PeterHan.PLib.UI;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which represents float and displays a text field.
	/// </summary>
	internal sealed class FloatOptionsEntry : OptionsEntry {
		protected override object Value {
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
		/// The limits allowed for the entry.
		/// </summary>
		private readonly LimitAttribute limits;

		/// <summary>
		/// The realized text field.
		/// </summary>
		private GameObject textField;

		/// <summary>
		/// The value in the text field.
		/// </summary>
		private float value;

		internal FloatOptionsEntry(string title, string tooltip, PropertyInfo prop) :
				base(prop?.Name, title, tooltip) {
			LimitAttribute fieldLimits = null;
			textField = null;
			value = 0.0f;
			// Look for limits
			foreach (var attr in prop.GetCustomAttributes(false))
				if ((fieldLimits = GetLimits(attr)) != null)
					break;
			limits = fieldLimits;
		}

		protected override IUIComponent GetUIComponent() {
			var cb = new PTextField() {
				OnTextChanged = (obj, text) => {
					if (float.TryParse(text, out float newValue) && (limits == null || limits.
							InRange(newValue)))
						// Record the valid value
						value = newValue;
					Update();
				}, ToolTip = ToolTip, Text = value.ToString(), MinWidth = 64, MaxLength = 16,
				Type = PTextField.FieldType.Float
			};
			cb.OnRealize += OnRealizeTextField;
			return cb;
		}

		/// <summary>
		/// Called when the text field is realized.
		/// </summary>
		/// <param name="obj">The actual text field.</param>
		private void OnRealizeTextField(GameObject realized) {
			textField = realized;
			Update();
		}

		private void Update() {
			var field = textField.GetComponentInChildren<TMP_InputField>();
			if (field != null)
				field.text = value.ToString();
		}
	}
}
