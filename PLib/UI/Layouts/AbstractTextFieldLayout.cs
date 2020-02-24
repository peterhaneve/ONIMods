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

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// Handles layout for text boxes and text areas. Not freezable.
	/// </summary>
	internal abstract class AbstractTextFieldLayout : UIBehaviour, ILayoutElement,
			ISettableFlexSize {
		/// <summary>
		/// The size of the border to apply around the text.
		/// </summary>
		protected abstract float BorderSize { get; }

		/// <summary>
		/// The flexible height of the text box.
		/// </summary>
		public float flexibleHeight { get; set; }

		/// <summary>
		/// The flexible width of the text box.
		/// </summary>
		public float flexibleWidth { get; set; }

		/// <summary>
		/// The minimum height of the text box.
		/// </summary>
		public float minHeight { get; protected set; }

		/// <summary>
		/// The minimum width of the text box.
		/// </summary>
		public float minWidth { get; set; }

		/// <summary>
		/// The preferred height of the text box.
		/// </summary>
		public float preferredHeight { get; protected set; }

		/// <summary>
		/// The preferred width of the text box.
		/// </summary>
		public float preferredWidth { get; protected set; }

		public int layoutPriority => 1;

		/// <summary>
		/// Caches elements when calculating layout to improve performance.
		/// </summary>
		protected ILayoutElement[] calcElements;

		/// <summary>
		/// Caches elements when setting layout to improve performance.
		/// </summary>
		protected ILayoutController[] setElements;

		/// <summary>
		/// The text area where the mask is displayed.
		/// </summary>
		protected GameObject textArea;

		/// <summary>
		/// The text box component used to determine the size of the overall layout.
		/// </summary>
		protected GameObject textBox;

		public virtual void CalculateLayoutInputHorizontal() {
			if (textArea != null) {
				calcElements = textArea.GetComponents<ILayoutElement>();
				// Lay out children
				foreach (var component in calcElements)
					component.CalculateLayoutInputHorizontal();
			}
			preferredWidth = minWidth;
		}

		public abstract void CalculateLayoutInputVertical();

		protected override void OnDidApplyAnimationProperties() {
			base.OnDidApplyAnimationProperties();
			SetDirty();
		}

		protected override void OnDisable() {
			base.OnEnable();
			SetDirty();
		}

		protected override void OnEnable() {
			base.OnEnable();
			UpdateComponents();
			SetDirty();
		}

		protected override void OnRectTransformDimensionsChange() {
			base.OnRectTransformDimensionsChange();
			SetDirty();
		}

		public void SetLayoutHorizontal() {
			if (textArea != null) {
				setElements = textArea.GetComponents<ILayoutController>();
				// Lay out descendents
				foreach (var component in setElements)
					component.SetLayoutHorizontal();
			}
		}

		public void SetLayoutVertical() {
			if (textArea != null && setElements != null) {
				// Lay out descendents
				foreach (var component in setElements)
					component.SetLayoutVertical();
				setElements = null;
			}
		}

		/// <summary>
		/// Sets this layout as dirty.
		/// </summary>
		private void SetDirty() {
			if (gameObject != null && IsActive())
				LayoutRebuilder.MarkLayoutForRebuild(gameObject.rectTransform());
		}

		/// <summary>
		/// Caches the child components for performance reasons at runtime.
		/// </summary>
		private void UpdateComponents() {
			var obj = gameObject;
			if (obj != null) {
				var transform = obj.transform;
				textBox = obj.GetComponentInChildren<TextMeshProUGUI>()?.gameObject;
				if (transform != null && transform.childCount > 0)
					textArea = transform.GetChild(0)?.gameObject;
				else
					textArea = null;
			} else {
				textBox = null;
				textArea = null;
			}
		}
	}
}
