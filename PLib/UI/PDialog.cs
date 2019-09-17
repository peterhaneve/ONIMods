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
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A dialog root for UI components.
	/// </summary>
	public sealed class PDialog : IUIComponent {
		public string Name { get; }

		/// <summary>
		/// The dialog size.
		/// </summary>
		public Vector2f Size { get; set; }

		/// <summary>
		/// The dialog's parent.
		/// </summary>
		public GameObject Parent { get; set; }

		/// <summary>
		/// The dialog's title.
		/// </summary>
		public string Title { get; set; }

		public PDialog(string name) {
			Size = new Vector2f(320.0f, 240.0f);
			Name = name ?? "Dialog";
			Parent = FrontEndManager.Instance.gameObject;
			Title = "Dialog";
		}

		public GameObject Build() {
			if (Parent == null)
				throw new InvalidOperationException("Parent for dialog may not be null");
			var dialog = PUIElements.CreateUI(Name);
			PUIElements.SetParent(dialog, Parent);
			// Background (needs to be unanchored so PPanel is not useful here)
			dialog.AddComponent<Image>().color = PUITuning.DialogBackground;
			dialog.AddComponent<Canvas>();
			// Lay out components vertically
			var lg = dialog.AddComponent<VerticalLayoutGroup>();
			lg.childForceExpandWidth = true;
			lg.padding = new RectOffset(1, 1, 1, 1);
			lg.spacing = 1;
			new PPanel("Header") {
				// Horizontal title bar
				Spacing = 3, Direction = PanelDirection.Horizontal
			}.SetKleiPinkColor().AddChild(new PLabel("Title") {
				// Title text, expand to width
				Text = Title, FlexSize = new Vector2f(1.0f, 0.0f)
			}).AddChild(new PButton("Close") {
				// Close button
				Sprite = PUITuning.CloseButtonImage, Margin = new RectOffset(3, 3, 3, 3),
				SpriteSize = new Vector2f(16.0f, 16.0f),
				OnClick = () => {
					var screen = dialog.GetComponent<KScreen>();
					if (screen != null)
						screen.Deactivate();
				}
			}.SetKleiBlueStyle()).AddTo(dialog);
			// Body, make it fill the flexible space
			new PPanel("Body") {
				FlexSize = Vector2.one
			}.SetKleiBlueColor().AddTo(dialog);
			// Set size
			var transform = dialog.rectTransform();
			transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size.x);
			transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size.y);
			dialog.AddComponent<GraphicRaycaster>();
			return dialog;
		}

		public override string ToString() {
			return "PDialog[Name={0},Title={1}]".F(Name, Title);
		}
	}
}
