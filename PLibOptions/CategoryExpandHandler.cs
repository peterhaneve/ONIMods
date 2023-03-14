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
using UnityEngine;
using UnityEngine.Events;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Handles events for expanding and contracting options categories.
	/// </summary>
	internal sealed class CategoryExpandHandler {
		/// <summary>
		/// The realized panel containing the options.
		/// </summary>
		private GameObject contents;

		/// <summary>
		/// The initial state of the button.
		/// </summary>
		private readonly bool initialState;

		/// <summary>
		/// The realized toggle button.
		/// </summary>
		private GameObject toggle;

		/// <summary>
		/// Creates a new options category.
		/// </summary>
		/// <param name="initialState">true to start expanded, or false to start collapsed.</param>
		public CategoryExpandHandler(bool initialState = true) {
			this.initialState = initialState;
		}

		/// <summary>
		/// Fired when the options category is expanded or contracted.
		/// </summary>
		/// <param name="on">true if the button is on, or false if it is off.</param>
		public void OnExpandContract(GameObject _, bool on) {
			var scale = on ? Vector3.one : Vector3.zero;
			if (contents != null) {
				var rt = contents.rectTransform();
				rt.localScale = scale;
				if (rt != null)
					UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(rt);
			}
		}

		/// <summary>
		/// Fired when the header is clicked.
		/// </summary>
		private void OnHeaderClicked() {
			if (toggle != null) {
				bool state = PToggle.GetToggleState(toggle);
				PToggle.SetToggleState(toggle, !state);
			}
		}

		/// <summary>
		/// Fired when the category label is realized.
		/// </summary>
		/// <param name="header">The realized header label of the category.</param>
		public void OnRealizeHeader(GameObject header) {
			var button = header.AddComponent<UnityEngine.UI.Button>();
			button.onClick.AddListener(new UnityAction(OnHeaderClicked));
			button.interactable = true;
		}

		/// <summary>
		/// Fired when the body is realized.
		/// </summary>
		/// <param name="panel">The realized body of the category.</param>
		public void OnRealizePanel(GameObject panel) {
			contents = panel;
			OnExpandContract(null, initialState);
		}

		/// <summary>
		/// Fired when the toggle button is realized.
		/// </summary>
		/// <param name="toggle">The realized expand/contract button.</param>
		public void OnRealizeToggle(GameObject toggle) {
			this.toggle = toggle;
		}
	}
}
