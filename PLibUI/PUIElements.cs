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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
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
		/// Safely adds a LocText to a game object without throwing an NRE on construction.
		/// </summary>
		/// <param name="parent">The game object to add the LocText.</param>
		/// <param name="setting">The text style.</param>
		/// <returns>The added LocText object.</returns>
		internal static LocText AddLocText(GameObject parent, TextStyleSetting setting = null)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));
			bool active = parent.activeSelf;
			parent.SetActive(false);
			var text = parent.AddComponent<LocText>();
			// This is enough to let it activate
			UIDetours.LOCTEXT_KEY.Set(text, string.Empty);
			UIDetours.LOCTEXT_STYLE.Set(text, setting ?? PUITuning.Fonts.UIDarkStyle);
			parent.SetActive(active);
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
				throw new ArgumentNullException(nameof(uiElement));
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
		/// <param name="parent">The parent of the UI object. If not set now, or added/changed
		/// later, the anchors must be redefined.</param>
		/// <param name="name">The object name.</param>
		/// <param name="canvas">true to add a canvas renderer, or false otherwise.</param>
		/// <param name="horizAnchor">How to anchor the object horizontally.</param>
		/// <param name="vertAnchor">How to anchor the object vertically.</param>
		/// <returns>The UI object with transform and canvas initialized.</returns>
		public static GameObject CreateUI(GameObject parent, string name, bool canvas = true,
				PUIAnchoring horizAnchor = PUIAnchoring.Stretch,
				PUIAnchoring vertAnchor = PUIAnchoring.Stretch) {
			var element = new GameObject(name);
			if (parent != null)
				element.SetParent(parent);
			// Size and position
			var transform = element.AddOrGet<RectTransform>();
			transform.localScale = Vector3.one;
			SetAnchors(element, horizAnchor, vertAnchor);
			// Almost all UI components need a canvas renderer for some reason
			if (canvas)
				element.AddComponent<CanvasRenderer>();
			element.layer = LayerMask.NameToLayer("UI");
			return element;
		}

		/// <summary>
		/// Does nothing, to make the buttons appear.
		/// </summary>
		private static void DoNothing() { }

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
				throw new ArgumentNullException(nameof(uiElement));
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
		/// Sets the anchor location of a UI element. The offsets will be reset, use
		/// SetAnchorOffsets to adjust the offset from the new anchor locations.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="horizAnchor">The horizontal anchor mode.</param>
		/// <param name="vertAnchor">The vertical anchor mode.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetAnchors(GameObject uiElement, PUIAnchoring horizAnchor,
				PUIAnchoring vertAnchor) {
			Vector2 aMax = new Vector2(), aMin = new Vector2(), pivot = new Vector2();
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			var transform = uiElement.rectTransform();
			// Anchor: horizontal
			switch (horizAnchor) {
			case PUIAnchoring.Center:
				aMin.x = 0.5f;
				aMax.x = 0.5f;
				pivot.x = 0.5f;
				break;
			case PUIAnchoring.End:
				aMin.x = 1.0f;
				aMax.x = 1.0f;
				pivot.x = 1.0f;
				break;
			case PUIAnchoring.Stretch:
				aMin.x = 0.0f;
				aMax.x = 1.0f;
				pivot.x = 0.5f;
				break;
			default:
				aMin.x = 0.0f;
				aMax.x = 0.0f;
				pivot.x = 0.0f;
				break;
			}
			// Anchor: vertical
			switch (vertAnchor) {
			case PUIAnchoring.Center:
				aMin.y = 0.5f;
				aMax.y = 0.5f;
				pivot.y = 0.5f;
				break;
			case PUIAnchoring.End:
				aMin.y = 1.0f;
				aMax.y = 1.0f;
				pivot.y = 1.0f;
				break;
			case PUIAnchoring.Stretch:
				aMin.y = 0.0f;
				aMax.y = 1.0f;
				pivot.y = 0.5f;
				break;
			default:
				aMin.y = 0.0f;
				aMax.y = 0.0f;
				pivot.y = 0.0f;
				break;
			}
			transform.anchorMax = aMax;
			transform.anchorMin = aMin;
			transform.pivot = pivot;
			transform.anchoredPosition = Vector2.zero;
			transform.offsetMax = Vector2.zero;
			transform.offsetMin = Vector2.zero;
			return uiElement;
		}

		/// <summary>
		/// Sets the offsets of the UI component from its anchors. Positive for each value
		/// denotes towards the component center, and negative away from the component center.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="border">The offset of each corner from the anchors.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetAnchorOffsets(GameObject uiElement, RectOffset border) {
			return SetAnchorOffsets(uiElement, border.left, border.right, border.top, border.
				bottom);
		}

		/// <summary>
		/// Sets the offsets of the UI component from its anchors. Positive for each value
		/// denotes towards the component center, and negative away from the component center.
		/// </summary>
		/// <param name="uiElement">The UI element to modify.</param>
		/// <param name="left">The left border in pixels.</param>
		/// <param name="right">The right border in pixels.</param>
		/// <param name="top">The top border in pixels.</param>
		/// <param name="bottom">The bottom border in pixels.</param>
		/// <returns>The UI element, for call chaining.</returns>
		public static GameObject SetAnchorOffsets(GameObject uiElement, float left,
				float right, float top, float bottom) {
			if (uiElement == null)
				throw new ArgumentNullException(nameof(uiElement));
			var transform = uiElement.rectTransform();
			transform.offsetMin = new Vector2(left, bottom);
			transform.offsetMax = new Vector2(-right, -top);
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
				throw new ArgumentNullException(nameof(uiElement));
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
				throw new ArgumentNullException(nameof(uiElement));
			if (!string.IsNullOrEmpty(tooltip))
				uiElement.AddOrGet<ToolTip>().toolTip = tooltip;
			return uiElement;
		}

		/// <summary>
		/// Shows a confirmation dialog.
		/// </summary>
		/// <param name="parent">The dialog's parent.</param>
		/// <param name="message">The message to display.</param>
		/// <param name="onConfirm">The action to invoke if Yes or OK is selected.</param>
		/// <param name="onCancel">The action to invoke if No or Cancel is selected.</param>
		/// <param name="confirmText">The text for the OK/Yes button.</param>
		/// <param name="cancelText">The text for the Cancel/No button.</param>
		/// <returns>The dialog created.</returns>
		public static ConfirmDialogScreen ShowConfirmDialog(GameObject parent, string message,
				System.Action onConfirm, System.Action onCancel = null,
				string confirmText = null, string cancelText = null) {
			if (parent == null)
				parent = PDialog.GetParentObject();
			var obj = Util.KInstantiateUI(ScreenPrefabs.Instance.ConfirmDialogScreen.
				gameObject, parent, false);
			if (obj.TryGetComponent(out ConfirmDialogScreen confirmDialog)) {
				UIDetours.POPUP_CONFIRM.Invoke(confirmDialog, message, onConfirm, onCancel ??
					DoNothing, null, null, null, confirmText, cancelText);
				obj.SetActive(true);
			} else
				confirmDialog = null;
			return confirmDialog;
		}

		/// <summary>
		/// Shows a message dialog.
		/// </summary>
		/// <param name="parent">The dialog's parent.</param>
		/// <param name="message">The message to display.</param>
		/// <returns>The dialog created.</returns>
		public static ConfirmDialogScreen ShowMessageDialog(GameObject parent, string message)
		{
			return ShowConfirmDialog(parent, message, DoNothing);
		}

		// This class should probably be static, but that might be binary compatibility
		// breaking
		private PUIElements() { }
	}

	/// <summary>
	/// The anchor mode to set a UI component.
	/// </summary>
	public enum PUIAnchoring {
		// Stretch to all
		Stretch,
		// Relative to beginning
		Beginning,
		// Relative to center
		Center,
		// Relative to end
		End
	}
}
