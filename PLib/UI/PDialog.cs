/*
 * Copyright 2020 Peter Han
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
		internal static readonly RectOffset BUTTON_MARGIN = new RectOffset(10, 10, 10, 10);

		/// <summary>
		/// The margin inside the dialog close button.
		/// </summary>
		private static readonly RectOffset CLOSE_ICON_MARGIN = new RectOffset(4, 4, 4, 4);

		/// <summary>
		/// The size of the dialog close button's icon.
		/// </summary>
		private static readonly Vector2 CLOSE_ICON_SIZE = new Vector2f(16.0f, 16.0f);

		/// <summary>
		/// The dialog key returned if the user closes the dialog with [ESC] or the X.
		/// </summary>
		public const string DIALOG_KEY_CLOSE = "close";

		/// <summary>
		/// Rounds the size up to the nearest even integer.
		/// </summary>
		/// <param name="size">The current size.</param>
		/// <param name="maxSize">The maximum allowed size.</param>
		/// <returns>The rounded size.</returns>
		private static float RoundUpSize(float size, float maxSize) {
			int upOne = Mathf.CeilToInt(size);
			if (upOne % 2 == 1) upOne++;
			if (upOne > maxSize && maxSize > 0.0f)
				upOne -= 2;
			return upOne;
		}

		/// <summary>
		/// The dialog body panel.
		/// </summary>
		public PPanel Body { get; }

		/// <summary>
		/// The background color of the dialog itself (including button panel).
		/// </summary>
		public Color DialogBackColor { get; set; }

		/// <summary>
		/// The dialog's maximum size. If the dialog preferred size is bigger than this size,
		/// the dialog will be decreased in size to fit. If either axis is zero, the dialog
		/// gets its preferred size in that axis, at least the value in Size.
		/// </summary>
		public Vector2 MaxSize { get; set; }

		public string Name { get; }

		/// <summary>
		/// The dialog's parent.
		/// </summary>
		public GameObject Parent { get; set; }

		/// <summary>
		/// If a dialog with an odd width/height is displayed, all offsets will end up on a
		/// half pixel offset, which may cause unusual display artifacts as Banker's Rounding
		/// will round values that are supposed to be 1.0 units apart into integer values 2
		/// units apart. If set, this flag will cause Build to round the dialog's size up to
		/// the nearest even integer. If the dialog is already at its maximum size and is still
		/// an odd integer in size, it is rounded down one instead.
		/// </summary>
		public bool RoundToNearestEven { get; set; }

		/// <summary>
		/// The dialog's minimum size. If the dialog preferred size is bigger than this size,
		/// the dialog will be increased in size to fit. If either axis is zero, the dialog
		/// gets its preferred size in that axis, up until the value in MaxSize.
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
				Alignment = TextAnchor.UpperCenter, FlexSize = Vector2.one,
				Margin = new RectOffset(6, 6, 6, 6)
			};
			DialogBackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			buttons = new List<DialogButton>(4);
			MaxSize = Vector2.zero;
			Name = name ?? "Dialog";
			Parent = FrontEndManager.Instance.gameObject;
			RoundToNearestEven = false;
			Size = Vector2.zero;
			SortKey = 0.0f;
			Title = "Dialog";
		}

		/// <summary>
		/// Adds a button to the dialog. The button will use a blue background with white text
		/// in the default UI font, except for the last button which will be pink.
		/// </summary>
		/// <param name="key">The key to report if this button is selected.</param>
		/// <param name="text">The button text.</param>
		/// <param name="tooltip">The tooltip to display on the button (optional)</param>
		/// <returns>This dialog for call chaining.</returns>
		public PDialog AddButton(string key, string text, string tooltip = null) {
			buttons.Add(new DialogButton(key, text, tooltip, null, null));
			return this;
		}

		/// <summary>
		/// Adds a button to the dialog.
		/// </summary>
		/// <param name="key">The key to report if this button is selected.</param>
		/// <param name="text">The button text.</param>
		/// <param name="tooltip">The tooltip to display on the button (optional)</param>
		/// <param name="backColor">The background color to use for the button. If null or
		/// omitted, the last button will be pink and all others will be blue.</param>
		/// <param name="foreColor">The foreground color to use for the button. If null or
		/// omitted, white text with the default game UI font will be used.</param>
		/// <returns>This dialog for call chaining.</returns>
		public PDialog AddButton(string key, string text, string tooltip = null,
				ColorStyleSetting backColor = null, TextStyleSetting foreColor = null) {
			buttons.Add(new DialogButton(key, text, tooltip, backColor, foreColor));
			return this;
		}

		public GameObject Build() {
			if (Parent == null)
				throw new InvalidOperationException("Parent for dialog may not be null");
			var dialog = PUIElements.CreateUI(Parent, Name);
			var dComponent = dialog.AddComponent<PDialogComp>();
			// Background
			dialog.AddComponent<Canvas>();
			dialog.AddComponent<GraphicRaycaster>();
			var bg = dialog.AddComponent<Image>();
			bg.color = DialogBackColor;
			bg.sprite = PUITuning.Images.BoxBorder;
			bg.type = Image.Type.Sliced;
			// Add each component
			var layout = new RelativeLayout(dialog);
			var title = LayoutTitle(layout, dComponent.DoButton);
			var body = Body.AddTo(dialog);
			var buttons = CreateUserButtons(layout, dComponent.DoButton);
			// Configure body position
			layout.SetTopEdge(body, below: title).SetMargin(body, new RectOffset(
				10, 10, 10, 10)).SetBottomEdge(body, above: buttons);
			SetDialogSize(layout.Execute(true));
			// Dialog is realized
			dComponent.dialog = this;
			dComponent.sortKey = SortKey;
			OnRealize?.Invoke(dialog);
			return dialog;
		}

		/// <summary>
		/// Creates the user buttons.
		/// </summary>
		/// <param name="layout">The location to add the buttons.</param>
		/// <param name="onPressed">The handler to call when any button is pressed.</param>
		/// <returns>The realized panel containing the user buttons.</returns>
		private GameObject CreateUserButtons(RelativeLayout layout,
				PUIDelegates.OnButtonPressed onPressed) {
			var buttonPanel = new PPanel("Buttons") {
				Alignment = TextAnchor.LowerCenter, Spacing = 7, Direction = PanelDirection.
				Horizontal, Margin = new RectOffset(5, 5, 0, 10)
			};
			int i = 0;
			// Add each user button
			foreach (var button in buttons) {
				string key = button.key;
				var bgColor = button.backColor;
				var fgColor = button.textColor ?? PUITuning.Fonts.UILightStyle;
				var db = new PButton(key) {
					Text = button.text, ToolTip = button.tooltip, Margin = BUTTON_MARGIN,
					OnClick = onPressed, Color = bgColor, TextStyle = fgColor
				};
				// Last button is special and gets a pink color
				if (bgColor == null) {
					if (++i >= buttons.Count)
						db.SetKleiPinkStyle();
					else
						db.SetKleiBlueStyle();
				}
				buttonPanel.AddChild(db);
			}
			var realized = buttonPanel.AddTo(layout.Parent);
			layout.SetBottomEdge(realized, fraction: 0.0f);
			return realized;
		}

		/// <summary>
		/// Lays out the dialog title bar and close button.
		/// </summary>
		/// <param name="layout">The layout manager for the dialog.</param>
		/// <param name="onClose">The action to invoke when close is pressed.</param>
		/// <returns>The realized title label.</returns>
		private GameObject LayoutTitle(RelativeLayout layout, PUIDelegates.OnButtonPressed
				onClose) {
			// Title text, expand to width
			var title = new PLabel("Title") {
				Margin = new RectOffset(3, 4, 0, 0), Text = Title, FlexSize = Vector2.one,
			}.SetKleiPinkColor().AddTo(layout.Parent);
			// Black border on the title bar overlaps the edge, but 0 margins so should be OK
			// Fixes the 2px bug handily!
			var titleBG = title.AddOrGet<Image>();
			titleBG.sprite = PUITuning.Images.BoxBorder;
			titleBG.type = Image.Type.Sliced;
			// Close button
			var close = new PButton(DIALOG_KEY_CLOSE) {
				Sprite = PUITuning.Images.Close, Margin = CLOSE_ICON_MARGIN, OnClick = onClose,
				SpriteSize = CLOSE_ICON_SIZE, ToolTip = STRINGS.UI.TOOLTIPS.CLOSETOOLTIP
			}.SetKleiBlueStyle().AddTo(layout.Parent);
			layout.SetTopEdge(title, fraction: 1.0f).SetTopEdge(close, fraction: 1.0f).
				SetLeftEdge(title, fraction: 0.0f).SetRightEdge(title, toLeft: close).
				SetRightEdge(close, fraction: 1.0f).SetMargin(title, new RectOffset(
				0, -2, 0, 0)).OverrideSize(title, new Vector2(0.0f, 6.0f + CLOSE_ICON_SIZE.y));
			return title;
		}

		/// <summary>
		/// Sets the final size of the dialog using its current position.
		/// </summary>
		/// <param name="dialog">The realized dialog with all components populated.</param>
		private void SetDialogSize(GameObject dialog) {
			// Calculate the final dialog size
			var dialogRT = dialog.rectTransform();
			LayoutRebuilder.ForceRebuildLayoutImmediate(dialogRT);
			float bodyWidth = Math.Max(Size.x, LayoutUtility.GetPreferredWidth(dialogRT)),
				bodyHeight = Math.Max(Size.y, LayoutUtility.GetPreferredHeight(dialogRT)),
				maxX = MaxSize.x, maxY = MaxSize.y;
			// Maximum size constraint
			if (maxX > 0.0f)
				bodyWidth = Math.Min(bodyWidth, maxX);
			if (maxY > 0.0f)
				bodyHeight = Math.Min(bodyHeight, maxY);
			if (RoundToNearestEven) {
				// Round up the size to even integers, even if currently fractional
				bodyWidth = RoundUpSize(bodyWidth, maxX);
				bodyHeight = RoundUpSize(bodyHeight, maxY);
			}
			dialogRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyWidth);
			dialogRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
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
			/// The color to use when displaying the button. If null, the default color will
			/// be used.
			/// </summary>
			public readonly ColorStyleSetting backColor;

			/// <summary>
			/// The button key used to indicate that it was selected.
			/// </summary>
			public readonly string key;

			/// <summary>
			/// The text to display for the button.
			/// </summary>
			public readonly string text;

			/// <summary>
			/// The color to use when displaying the button text. If null, the default color
			/// will be used.
			/// </summary>
			public readonly TextStyleSetting textColor;

			/// <summary>
			/// The tooltip for this button.
			/// </summary>
			public readonly string tooltip;

			internal DialogButton(string key, string text, string tooltip,
					ColorStyleSetting backColor, TextStyleSetting foreColor) {
				this.backColor = backColor;
				this.key = key;
				this.text = text;
				this.tooltip = tooltip;
				textColor = foreColor;
			}

			public override string ToString() {
				return "DialogButton[key={0:D},text={1:D}]".F(key, text);
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
