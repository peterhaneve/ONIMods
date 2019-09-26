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
			text.textStyleSetting = PUITuning.Fonts.UILightStyle;
			obj.SetActive(active);
			return text;
		}

		/// <summary>
		/// Adds an auto-fit resizer to a UI element.
		/// 
		/// UI elements should be active before any layouts are added, especially if they are
		/// to be frozen.
		/// </summary>
		/// <param name="uiElement">The element to resize.</param>
		/// <param name="dynamic">true to use the Unity content size fitter which adjusts to
		/// content changes, or false to set the size only once.</param>
		/// <param name="modeHoriz">The sizing mode to use in the horizontal direction.</param>
		/// <param name="modeVert">The sizing mode to use in the vertical direction.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject AddSizeFitter(GameObject uiElement, bool dynamic = false,
				ContentFitMode modeHoriz = ContentFitMode.PreferredSize,
				ContentFitMode modeVert = ContentFitMode.PreferredSize) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			if (dynamic) {
				var fitter = uiElement.AddOrGet<ContentSizeFitter>();
				fitter.horizontalFit = modeHoriz;
				fitter.verticalFit = modeVert;
				fitter.enabled = true;
			} else
				FitSizeNow(uiElement, modeHoriz, modeVert);
			return uiElement;
		}

		/// <summary>
		/// Creates a UI game object.
		/// </summary>
		/// <param name="name">The object name.</param>
		/// <param name="anchor">true to anchor the object, or false otherwise.</param>
		/// <param name="renderer">true to add a canvas renderer, or false otherwise.</param>
		/// <returns>The UI object with transform and canvas initialized.</returns>
		internal static GameObject CreateUI(string name, bool anchor = true,
				bool renderer = true) {
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
			if (renderer)
				element.AddComponent<CanvasRenderer>();
			element.layer = LayerMask.NameToLayer("UI");
			return element;
		}

		/// <summary>
		/// Fits the UI element's size immediately, as if ContentSizeFitter was created on it,
		/// but does not create a component and only affects the size once.
		/// </summary>
		/// <param name="uiElement">The element to resize.</param>
		/// <param name="modeHoriz">The sizing mode to use in the horizontal direction.</param>
		/// <param name="modeVert">The sizing mode to use in the vertical direction.</param>
		/// <returns>The UI element, for call chaining.</returns>
		private static void FitSizeNow(GameObject uiElement, ContentFitMode modeHoriz,
				ContentFitMode modeVert) {
			float width = 0.0f, height = 0.0f;
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			// Follow order in https://docs.unity3d.com/Manual/UIAutoLayout.html
			var elements = uiElement.GetComponents<ILayoutElement>();
			var constraints = uiElement.AddOrGet<LayoutElement>();
			var rt = uiElement.AddOrGet<RectTransform>();
			// Calculate horizontal
			foreach (var layoutElement in elements)
				layoutElement.CalculateLayoutInputHorizontal();
			if (modeHoriz != ContentFitMode.Unconstrained) {
				// Layout horizontal
				foreach (var layoutElement in elements)
					switch (modeHoriz) {
					case ContentFitMode.MinSize:
						width = Math.Max(width, layoutElement.minWidth);
						break;
					case ContentFitMode.PreferredSize:
						width = Math.Max(width, layoutElement.preferredWidth);
						break;
					default:
						break;
					}
				width = Math.Max(width, constraints.minWidth);
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
				constraints.minWidth = width;
				constraints.flexibleWidth = 0.0f;
			}
			// Calculate vertical
			foreach (var layoutElement in elements)
				layoutElement.CalculateLayoutInputVertical();
			if (modeVert != ContentFitMode.Unconstrained) {
				// Layout vertical
				foreach (var layoutElement in elements)
					switch (modeVert) {
					case ContentFitMode.MinSize:
						height = Math.Max(height, layoutElement.minHeight);
						break;
					case ContentFitMode.PreferredSize:
						height = Math.Max(height, layoutElement.preferredHeight);
						break;
					default:
						break;
					}
				height = Math.Max(height, constraints.minHeight);
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
				constraints.minHeight = height;
				constraints.flexibleHeight = 0.0f;
			}
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
		/// Sets a UI element's size immediately.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="size">The new size.</param>
		/// <returns>The UI element, for call chaining.</returns>
		internal static GameObject SetSizeImmediate(GameObject uiElement, Vector2 size) {
			if (uiElement == null)
				throw new ArgumentNullException("uiElement");
			float width = size.x, height = size.y;
			var le = uiElement.AddOrGet<LayoutElement>();
			// Min and preferred size set
			le.minWidth = width;
			le.minHeight = height;
			le.preferredWidth = width;
			le.preferredHeight = height;
			le.flexibleHeight = 0.0f;
			le.flexibleWidth = 0.0f;
			// Apply to current size
			var rt = uiElement.rectTransform();
			if (rt != null) {
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
			}
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
			var lt = uiElement.GetComponentInChildren<TMPro.TextMeshProUGUI>();
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
		/// <param name="parent">The dialog's parent.</param>
		/// <param name="message">The message to display.</param>
		/// <returns>The dialog created.</returns>
		public static ConfirmDialogScreen ShowConfirmDialog(GameObject parent, string message) {
			if (parent == null)
				throw new ArgumentNullException("parent");
			var confirmDialog = Util.KInstantiateUI(ScreenPrefabs.Instance.ConfirmDialogScreen.
				gameObject, parent, false).GetComponent<
				ConfirmDialogScreen>();
			confirmDialog.PopupConfirmDialog(message, null, null, null, null,
				message, null, null, null, true);
			confirmDialog.gameObject.SetActive(true);
			return confirmDialog;
		}

		private PUIElements() { }
	}
}
