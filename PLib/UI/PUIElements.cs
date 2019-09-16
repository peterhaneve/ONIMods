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
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib {
	/// <summary>
	/// Used for creating and managing UI elements.
	/// </summary>
	public sealed class PUIElements {
		/// <summary>
		/// A white color used for default backgrounds.
		/// </summary>
		public static readonly Color BG_WHITE = new Color32(255, 255, 255, 255);

		/// <summary>
		/// Represents an anchor in the center.
		/// </summary>
		private static readonly Vector2f CENTER = new Vector2f(0.5f, 0.5f);

		/// <summary>
		/// Represents an anchor in the lower left corner.
		/// </summary>
		private static readonly Vector2f LOWER_LEFT = new Vector2f(1.0f, 0.0f);

		/// <summary>
		/// Represents an anchor in the upper right corner.
		/// </summary>
		private static readonly Vector2f UPPER_RIGHT = new Vector2f(0.0f, 1.0f);

		/// <summary>
		/// Safely adds a LocText to a game object without throwing an NRE on construction.
		/// </summary>
		/// <param name="obj">The game object to add the LocText.</param>
		/// <returns>The added LocText object.</returns>
		private static LocText AddLocText(GameObject obj) {
			bool active = obj.activeSelf;
			obj.SetActive(false);
			var text = obj.AddComponent<LocText>();
			// This is enough to let it activate
			text.key = string.Empty;
			text.textStyleSetting = PUITuning.UITextStyle;
			obj.SetActive(active);
			return text;
		}

		/// <summary>
		/// Adds an auto-fit resizer to a UI element.
		/// </summary>
		/// <param name="uiElement">The element to resize.</param>
		/// <param name="mode">The sizing mode to use.</param>
		public static void AddSizeFitter(GameObject uiElement, ContentSizeFitter.FitMode mode =
				ContentSizeFitter.FitMode.MinSize) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var fitter = uiElement.AddOrGet<ContentSizeFitter>();
			fitter.horizontalFit = mode;
			fitter.verticalFit = mode;
			fitter.enabled = true;
			fitter.SetLayoutHorizontal();
			fitter.SetLayoutVertical();
		}

		/// <summary>
		/// Creates a button.
		/// </summary>
		/// <param name="parent">The parent which will contain the button.</param>
		/// <param name="name">The button name.</param>
		/// <param name="onClick">The action to execute on click (optional).</param>
		/// <returns>The matching button.</returns>
		public static GameObject CreateButton(GameObject parent, string name = null,
				System.Action onClick = null) {
			if (parent == null)
				throw new ArgumentNullException("parent");
			var button = CreateUI(parent, name ?? "Button");
			// Background
			var kImage = button.AddComponent<KImage>();
			kImage.colorStyleSetting = PUITuning.ButtonStylePink;
			kImage.color = PUITuning.ButtonColorPink;
			kImage.sprite = PUITuning.ButtonImage.sprite;
			kImage.type = Image.Type.Sliced;
			// Set on click event
			var kButton = button.AddComponent<KButton>();
			if (onClick != null)
				kButton.onClick += onClick;
			kButton.additionalKImages = new KImage[0];
			kButton.soundPlayer = PUITuning.ButtonSounds;
			kButton.bgImage = kImage;
			// Set colors
			kButton.colorStyleSetting = kImage.colorStyleSetting;
			button.AddComponent<LayoutElement>().flexibleWidth = 0;
			button.AddComponent<ToolTip>();
			// Add text to the button
			var textChild = CreateUI(button, "Text");
			var text = AddLocText(textChild);
			text.alignment = TMPro.TextAlignmentOptions.Center;
			text.font = PUITuning.ButtonFont;
			button.SetActive(true);
			return button;
		}

		/// <summary>
		/// Creates a dialog.
		/// </summary>
		/// <param name="name">The dialog name.</param>
		/// <param name="title">The dialog title.</param>
		/// <param name="size">The dialog size.</param>
		/// <returns>The dialog.</returns>
		public static GameObject CreateDialog(string name, string title = "Dialog",
				Vector2f size = default) {
			var dialog = CreateUI(FrontEndManager.Instance.gameObject, name ?? "Dialog");
			// Background
			dialog.AddComponent<Image>().color = PUITuning.DialogBackground;
			dialog.AddComponent<Canvas>();
			dialog.AddComponent<LayoutElement>();
			// Layout vertically
			var lg = dialog.AddComponent<VerticalLayoutGroup>();
			lg.childForceExpandWidth = true;
			lg.padding = new RectOffset(1, 1, 1, 1);
			lg.spacing = 1.0f;
			// Header
			var header = CreateUI(dialog, "Header");
			header.AddComponent<Image>().color = PUITuning.ButtonColorPink;
			header.AddComponent<LayoutElement>().minHeight = 24.0f;
			// Title bar
			var titleBar = CreateUI(header, "Title");
			var text = AddLocText(titleBar);
			text.text = title;
			text.alignment = TMPro.TextAlignmentOptions.Center;
			text.font = PUITuning.ButtonFont;
			// Body
			var panel = CreateUI(dialog, "Panel");
			panel.AddComponent<Image>().color = PUITuning.ButtonColorBlue;
			panel.AddComponent<LayoutElement>();
			// Resize it
			if (size.x > 0.0f && size.y > 0.0f)
				SetSize(panel, size);
			dialog.AddComponent<KScreen>();
			dialog.AddComponent<GraphicRaycaster>();
			AddSizeFitter(dialog, ContentSizeFitter.FitMode.PreferredSize);
			return dialog;
		}

		/// <summary>
		/// Creates a UI game object.
		/// </summary>
		/// <param name="parent">The object's parent.</param>
		/// <param name="name">The object name.</param>
		/// <param name="margins">The margins inside the parent object. Leave null to disable anchoring to parent.</param>
		/// <returns>The UI object with transform and canvas initialized.</returns>
		private static GameObject CreateUI(GameObject parent, string name, RectOffset margins =
				null) {
			var element = Util.NewGameObject(parent, name);
			// Size and position
			var transform = element.AddOrGet<RectTransform>();
			transform.localScale = Vector3.one;
			transform.pivot = CENTER;
			transform.anchoredPosition = CENTER;
			transform.anchorMax = UPPER_RIGHT;
			transform.anchorMin = LOWER_LEFT;
			// Margins from the parent component
			if (margins != null) {
				transform.offsetMax = new Vector2f(margins.right, margins.top);
				transform.offsetMin = new Vector2f(margins.left, margins.bottom);
			}
			// All UI components need a canvas renderer for some reason
			element.AddComponent<CanvasRenderer>();
			element.layer = LayerMask.NameToLayer("UI");
			return element;
		}

		/// <summary>
		/// Sets a UI element's minimum size.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="minSize">The minimum size in units.</param>
		public static void SetSize(GameObject uiElement, Vector2f minSize) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var le = uiElement.GetComponent<LayoutElement>();
			if (le != null) {
				le.minWidth = minSize.x;
				le.minHeight = minSize.y;
			}
		}

		/// <summary>
		/// Sets a UI element's text.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="text">The text to display on the element.</param>
		public static void SetText(GameObject uiElement, string text) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var title = uiElement.GetComponentInChildren<LocText>();
			if (title != null)
				title.SetText(text ?? string.Empty);
		}

		/// <summary>
		/// Sets a UI element's tool tip.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="tooltip">The tool tip text to display when hovered.</param>
		public static void SetToolTip(GameObject uiElement, string tooltip) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			if (!string.IsNullOrEmpty(tooltip)) {
				var tooltipComponent = uiElement.AddOrGet<ToolTip>();
				tooltipComponent.toolTip = tooltip;
			}
		}

		/// <summary>
		/// Shows a confirmation or message dialog based on a prefab.
		/// </summary>
		/// <param name="prefab">The dialog to show.</param>
		/// <param name="parent">The dialog's parent.</param>
		/// <param name="message">The message to display.</param>
		/// <returns>The dialog created.</returns>
		public static ConfirmDialogScreen ShowConfirmDialog(GameObject prefab,
				GameObject parent, string message) {
			if (prefab == null)
				throw new ArgumentNullException("prefab");
			if (parent == null)
				throw new ArgumentNullException("parent");
			var confirmDialog = Util.KInstantiateUI(prefab, parent, false).GetComponent<
				ConfirmDialogScreen>();
			confirmDialog.PopupConfirmDialog(message, null, null, null, null,
				null, null, null, null, true);
			confirmDialog.gameObject.SetActive(true);
			return confirmDialog;
		}

		private PUIElements() { }
	}
}
