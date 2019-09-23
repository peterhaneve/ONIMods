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
using System.Collections.Generic;
using PeterHan.PLib.UI;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which represents bool and displays a check box.
	/// </summary>
	internal sealed class CheckboxOptionsEntry : OptionsEntry {
		protected override object Value {
			get {
				return check;
			}
			set {
				if (value is bool newCheck) {
					check = newCheck;
					Update();
				}
			}
		}

		/// <summary>
		/// true if it is checked, or false otherwise
		/// </summary>
		private bool check;

		/// <summary>
		/// The realized item checkbox.
		/// </summary>
		private GameObject checkbox;

		internal CheckboxOptionsEntry(string field, string title, string tooltip) :
				base(field, title, tooltip) {
			check = false;
			checkbox = null;
		}

		protected override IUIComponent GetUIComponent() {
			var cb = new PCheckBox() {
				OnChecked = (source, state) => {
					// Swap the check: checked and partial -> unchecked
					check = state == PCheckBox.STATE_UNCHECKED;
					Update();
				}, ToolTip = ToolTip
			}.SetKleiBlueStyle();
			cb.OnRealize += OnRealizeCheckBox;
			return cb;
		}

		/// <summary>
		/// Called when the checkbox is realized.
		/// </summary>
		/// <param name="obj">The actual check box.</param>
		private void OnRealizeCheckBox(GameObject realized) {
			checkbox = realized;
			Update();
		}

		private void Update() {
			var toggle = checkbox.GetComponent<MultiToggle>();
			if (toggle != null)
				toggle.ChangeState(check ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
		}
	}
}
