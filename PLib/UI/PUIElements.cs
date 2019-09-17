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

using ContentFitMode = UnityEngine.UI.ContentSizeFitter.FitMode;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Used for creating and managing UI elements.
	/// </summary>
	public sealed class PUIElements {
		/// <summary>
		/// A white color used for default backgrounds.
		/// </summary>
		public static readonly Color BG_WHITE = new Color32(255, 255, 255, 255);

		/// <summary>
		/// A completely transparent color.
		/// </summary>
		public static readonly Color TRANSPARENT = new Color32(255, 255, 255, 0);

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
		internal static LocText AddLocText(GameObject obj) {
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
		/// <param name="modeHoriz">The sizing mode to use in the horizontal direction.</param>
		/// <param name="modeVert">The sizing mode to use in the vertical direction.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject AddSizeFitter(GameObject uiElement, ContentFitMode modeHoriz =
				ContentFitMode.PreferredSize, ContentFitMode modeVert = ContentFitMode.
				PreferredSize) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var fitter = uiElement.AddOrGet<ContentSizeFitter>();
			fitter.horizontalFit = modeHoriz;
			fitter.verticalFit = modeVert;
			fitter.enabled = true;
			return uiElement;
		}

		/// <summary>
		/// Creates a UI game object.
		/// </summary>
		/// <param name="name">The object name.</param>
		/// <param name="anchor">true to anchor the object, or false otherwise.</param>
		/// <returns>The UI object with transform and canvas initialized.</returns>
		internal static GameObject CreateUI(string name, bool anchor = true) {
			var element = new GameObject(name);
			// Size and position
			var transform = element.AddOrGet<RectTransform>();
			transform.localScale = Vector3.one;
			transform.pivot = CENTER;
			if (anchor) {
				transform.anchoredPosition = CENTER;
				transform.anchorMax = UPPER_RIGHT;
				transform.anchorMin = LOWER_LEFT;
			}
			// All UI components need a canvas renderer for some reason
			element.AddComponent<CanvasRenderer>();
			element.layer = LayerMask.NameToLayer("UI");
			return element;
		}

		/// <summary>
		/// Sets a UI element's parent.
		/// </summary>
		/// <param name="child">The UI element to modify.</param>
		/// <param name="parent">The new parent object.</param>
		public static void SetParent(GameObject child, GameObject parent) {
			if (child == null)
				throw new ArgumentNullException("child");
#pragma warning disable IDE0031 // Use null propagation
			child.transform.SetParent((parent == null) ? null : parent.transform, false);
#pragma warning restore IDE0031 // Use null propagation
		}

		/// <summary>
		/// Sets a UI element's minimum size.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="minSize">The minimum size in units.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetMinSize(GameObject uiElement, Vector2f minSize) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var le = uiElement.AddOrGet<LayoutElement>();
			le.minWidth = minSize.x;
			le.minHeight = minSize.y;
			return uiElement;
		}

		/// <summary>
		/// Sets a UI element's text.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="text">The text to display on the element.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetText(GameObject uiElement, string text) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			var lt = uiElement.GetComponentInChildren<LocText>();
			if (lt != null)
				lt.SetText(text ?? string.Empty);
			return uiElement;
		}

		/// <summary>
		/// Sets a UI element's tool tip.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="tooltip">The tool tip text to display when hovered.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetToolTip(GameObject uiElement, string tooltip) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			if (!string.IsNullOrEmpty(tooltip)) {
				var tooltipComponent = uiElement.AddOrGet<ToolTip>();
				tooltipComponent.toolTip = tooltip;
			}
			return uiElement;
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
