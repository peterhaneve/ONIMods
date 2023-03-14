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

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A class instance that handles events for text fields.
	/// </summary>
	internal sealed class PTextFieldEvents : KScreen {
		/// <summary>
		/// The action to trigger on text change. It is passed the realized source object.
		/// </summary>
		[SerializeField]
		internal PUIDelegates.OnTextChanged OnTextChanged { get; set; }

		/// <summary>
		/// The callback to invoke when validating input.
		/// </summary>
		[SerializeField]
		internal TMP_InputField.OnValidateInput OnValidate { get; set; }

		/// <summary>
		/// The object to resize on text change.
		/// </summary>
		[SerializeField]
		internal GameObject TextObject { get; set; }

		[MyCmpReq]
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		private TMP_InputField textEntry;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// Whether editing is in progress.
		/// </summary>
		private bool editing;

		internal PTextFieldEvents() {
			activateOnSpawn = true;
			editing = false;
			TextObject = null;
		}

		/// <summary>
		/// Completes the edit process one frame after the data is entered.
		/// </summary>
		private System.Collections.IEnumerator DelayEndEdit() {
			yield return new WaitForEndOfFrame();
			StopEditing();
		}

		public override float GetSortKey() {
			return editing ? 99.0f : base.GetSortKey();
		}

		protected override void OnCleanUp() {
			textEntry.onFocus -= OnFocus;
			textEntry.onValueChanged.RemoveListener(OnValueChanged);
			textEntry.onEndEdit.RemoveListener(OnEndEdit);
			base.OnCleanUp();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			textEntry.onFocus += OnFocus;
			textEntry.onValueChanged.AddListener(OnValueChanged);
			textEntry.onEndEdit.AddListener(OnEndEdit);
			if (OnValidate != null)
				textEntry.onValidateInput = OnValidate;
		}

		/// <summary>
		/// Triggered when editing of the text ends (field loses focus).
		/// </summary>
		/// <param name="text">The text entered.</param>
		private void OnEndEdit(string text) {
			var obj = gameObject;
			if (obj != null) {
				OnTextChanged?.Invoke(obj, text);
				if (obj.activeInHierarchy)
					StartCoroutine(DelayEndEdit());
			}
		}

		/// <summary>
		/// Triggered when the text field gains focus.
		/// </summary>
		private void OnFocus() {
			editing = true;
			textEntry.Select();
			textEntry.ActivateInputField();
			KScreenManager.Instance.RefreshStack();
		}

		/// <summary>
		/// Destroys events if editing is in progress to prevent bubbling through to the
		/// game UI.
		/// </summary>
		public override void OnKeyDown(KButtonEvent e) {
			if (editing)
				e.Consumed = true;
			else
				base.OnKeyDown(e);
		}

		/// <summary>
		/// Destroys events if editing is in progress to prevent bubbling through to the
		/// game UI.
		/// </summary>
		public override void OnKeyUp(KButtonEvent e) {
			if (editing)
				e.Consumed = true;
			else
				base.OnKeyUp(e);
		}

		/// <summary>
		/// Triggered when the text box value changes.
		/// </summary>
		/// <param name="text">The text entered.</param>
		private void OnValueChanged(string text) {
			var obj = gameObject;
			if (obj != null && TextObject != null) {
				var rt = TextObject.rectTransform();
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, LayoutUtility.
					GetPreferredHeight(rt));
			}
		}

		/// <summary>
		/// Completes the edit process.
		/// </summary>
		private void StopEditing() {
			if (textEntry != null && textEntry.gameObject.activeInHierarchy)
				textEntry.DeactivateInputField();
			editing = false;
		}
	}
}
