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
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A dialog root for UI components.
	/// </summary>
	public sealed class PDialog : IUIComponent {
		/// <summary>
		/// The margin around dialog buttons.
		/// </summary>
		private static readonly RectOffset BUTTON_MARGIN = new RectOffset(10, 10, 10, 10);

		/// <summary>
		/// The dialog key returned if the user closes the dialog with [ESC] or the X.
		/// </summary>
		public const string DIALOG_KEY_CLOSE = "close";

		/// <summary>
		/// The dialog body panel.
		/// </summary>
		public PPanel Body { get; }

		public string Name { get; }

		/// <summary>
		/// The dialog's parent.
		/// </summary>
		public GameObject Parent { get; set; }

		/// <summary>
		/// The dialog's minimum size. If the dialog minimum size is bigger than this size,
		/// the dialog will be increased in size to fit. If zero, the dialog gets its minimum
		/// size.
		/// </summary>
		public Vector2 Size { get; set; }

		/// <summary>
		/// The dialog sort order which determines which other dialogs this one is on top of.
		/// </summary>
		public float SortKey { get; set; }

		/// <summary>
		/// The dialog's title.
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// The allowable button choices for the dialog.
		/// </summary>
		private readonly ICollection<DialogButton> buttons;

		/// <summary>
		/// The events to invoke when the dialog is closed.
		/// </summary>
		public PUIDelegates.OnDialogClosed DialogClosed { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		public PDialog(string name) {
			Body = new PPanel("Body") {
				Alignment = TextAnchor.UpperCenter, FlexSize = Vector2.one
			};
			buttons = new List<DialogButton>(4);
			Name = name ?? "Dialog";
			Parent = FrontEndManager.Instance.gameObject;
			Size = Vector2.zero;
			SortKey = 0.0f;
			Title = "Dialog";
		}

		/// <summary>
		/// Adds a button to the dialog.
		/// </summary>
		/// <param name="key">The key to report if this button is selected.</param>
		/// <param name="text">The button text.</param>
		/// <param name="tooltip">The tooltip to display on the button (optional)</param>
		/// <returns>This dialog for call chaining.</returns>
		public PDialog AddButton(string key, string text, string tooltip = null) {
			buttons.Add(new DialogButton(key, text, tooltip));
			return this;
		}

		public GameObject Build() {
			var flexW = new Vector2(1.0f, 0.0f);
			if (Parent == null)
				throw new InvalidOperationException("Parent for dialog may not be null");
			var dialog = PUIElements.CreateUI(Name);
			var dComponent = dialog.AddComponent<PDialogComp>();
			int i = 0;
			PUIElements.SetParent(dialog, Parent);
			// Background (needs to be unanchored so PPanel is not useful here)
			dialog.AddComponent<Image>().color = PUITuning.Colors.DialogBackground;
			dialog.AddComponent<Canvas>();
			new PPanel("Header") {
				// Horizontal title bar
				Spacing = 3, Direction = PanelDirection.Horizontal, FlexSize = flexW
			}.SetKleiPinkColor().AddChild(new PLabel("Title") {
				// Title text, expand to width
				Text = Title, FlexSize = flexW, DynamicSize = true
			}).AddChild(new PButton(DIALOG_KEY_CLOSE) {
				// Close button
				Sprite = PUITuning.Images.Close, Margin = new RectOffset(3, 3, 3, 3),
				SpriteSize = new Vector2f(16.0f, 16.0f), OnClick = dComponent.DoButton
			}.SetKleiBlueStyle()).AddTo(dialog);
			// Buttons
			var buttonPanel = new PPanel("Buttons") {
				Alignment = TextAnchor.LowerCenter, Spacing = 5, Direction = PanelDirection.
				Horizontal, Margin = new RectOffset(5, 5, 0, 5), DynamicSize = false
			};
			// Add each user button
			foreach (var button in buttons) {
				string key = button.key;
				var db = new PButton(key) {
					Text = button.text, ToolTip = button.tooltip, Margin = BUTTON_MARGIN,
					OnClick = dComponent.DoButton
				};
				// Last button is special and gets a pink color
				if (++i >= buttons.Count)
					db.SetKleiPinkStyle();
				else
					db.SetKleiBlueStyle();
				buttonPanel.AddChild(db);
			}
			// Body, make it fill the flexible space
			new PPanel("BodyAndButtons") {
				Alignment = TextAnchor.MiddleCenter, Spacing = 0, Direction = PanelDirection.
				Vertical, Margin = new RectOffset(5, 5, 5, 5), DynamicSize = true,
				FlexSize = Vector2.one
			}.SetKleiBlueColor().AddChild(Body).AddChild(buttonPanel).AddTo(dialog);
			// Lay out components vertically
			BoxLayoutGroup.LayoutNow(dialog, new BoxLayoutParams() {
				Alignment = TextAnchor.UpperCenter, Margin = new RectOffset(1, 1, 1, 1),
				Spacing = 1.0f, Direction = PanelDirection.Vertical
			}, Size);
			dialog.AddComponent<GraphicRaycaster>();
			dComponent.dialog = this;
			dComponent.sortKey = SortKey;
			OnRealize?.Invoke(dialog);
			return dialog;
		}

		/// <summary>
		/// Builds and shows this dialog.
		/// </summary>
		public void Show() {
			Build().GetComponent<KScreen>()?.Activate();
		}

		public override string ToString() {
			return "PDialog[Name={0},Title={1}]".F(Name, Title);
		}

		/// <summary>
		/// Stores information about a dialog button in this dialog.
		/// </summary>
		private sealed class DialogButton {
			/// <summary>
			/// The button key used to indicate that it was selected.
			/// </summary>
			public readonly string key;

			/// <summary>
			/// The text to display for the button.
			/// </summary>
			public readonly string text;

			/// <summary>
			/// The tooltip for this button.
			/// </summary>
			public readonly string tooltip;

			internal DialogButton(string key, string text, string tooltip) {
				this.key = key;
				this.text = text;
				this.tooltip = tooltip;
			}
		}

		/// <summary>
		/// The Klei component which backs the dialog.
		/// </summary>
		private sealed class PDialogComp : KScreen {
			/// <summary>
			/// The events to invoke when the dialog is closed.
			/// </summary>
			internal PDialog dialog;

			/// <summary>
			/// The key selected by the user.
			/// </summary>
			internal string key;

			/// <summary>
			/// The sort order of this dialog.
			/// </summary>
			internal float sortKey;

			internal PDialogComp() {
				key = DIALOG_KEY_CLOSE;
				sortKey = 0.0f;
			}

			/// <summary>
			/// A delegate which closes the dialog on prompt.
			/// </summary>
			/// <param name="source">The button source.</param>
			internal void DoButton(GameObject source) {
				key = source.name;
				Deactivate();
			}

			public override float GetSortKey() {
				return sortKey;
			}

			protected override void OnDeactivate() {
				base.OnDeactivate();
				if (dialog != null)
					// Klei destroys the dialog GameObject for us
					dialog.DialogClosed?.Invoke(key);
				dialog = null;
			}

			public override void OnKeyDown(KButtonEvent e) {
				if (e.TryConsume(Action.Escape))
					Deactivate();
				else
					base.OnKeyDown(e);
			}
		}
	}
}
