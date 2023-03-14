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

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// The abstract parent of most layout groups.
	/// </summary>
	public abstract class AbstractLayoutGroup : UIBehaviour, ISettableFlexSize, ILayoutElement
	{
		/// <summary>
		/// Sets an object's layout dirty on the next frame.
		/// </summary>
		/// <param name="transform">The transform to set dirty.</param>
		/// <returns>A coroutine to set it dirty.</returns>
		internal static IEnumerator DelayedSetDirty(RectTransform transform) {
			yield return null;
			LayoutRebuilder.MarkLayoutForRebuild(transform);
		}

		/// <summary>
		/// Removes and destroys any PLib layouts on the component. They will be replaced with
		/// a static LayoutElement containing the old size of the component.
		/// </summary>
		/// <param name="component">The component to cleanse.</param>
		internal static void DestroyAndReplaceLayout(GameObject component) {
			if (component != null && component.TryGetComponent(out AbstractLayoutGroup
					layoutGroup)) {
				var replacement = component.AddOrGet<LayoutElement>();
				replacement.flexibleHeight = layoutGroup.flexibleHeight;
				replacement.flexibleWidth = layoutGroup.flexibleWidth;
				replacement.layoutPriority = layoutGroup.layoutPriority;
				replacement.minHeight = layoutGroup.minHeight;
				replacement.minWidth = layoutGroup.minWidth;
				replacement.preferredHeight = layoutGroup.preferredHeight;
				replacement.preferredWidth = layoutGroup.preferredWidth;
				DestroyImmediate(layoutGroup);
			}
		}

		public float minWidth {
			get {
				return mMinWidth;
			}
			set {
				mMinWidth = value;
			}
		}

		public float preferredWidth {
			get {
				return mPreferredWidth;
			}
			set {
				mPreferredWidth = value;
			}
		}

		/// <summary>
		/// The flexible width of the completed layout group can be set.
		/// </summary>
		public float flexibleWidth {
			get {
				return mFlexibleWidth;
			}
			set {
				mFlexibleWidth = value;
			}
		}

		public float minHeight {
			get {
				return mMinHeight;
			}
			set {
				mMinHeight = value;
			}
		}

		public float preferredHeight {
			get {
				return mPreferredHeight;
			}
			set {
				mPreferredHeight = value;
			}
		}

		/// <summary>
		/// The flexible height of the completed layout group can be set.
		/// </summary>
		public float flexibleHeight {
			get {
				return mFlexibleHeight;
			}
			set {
				mFlexibleHeight = value;
			}
		}

		/// <summary>
		/// The priority of this layout group.
		/// </summary>
		public int layoutPriority {
			get {
				return mLayoutPriority;
			}
			set {
				mLayoutPriority = value;
			}
		}

		protected RectTransform rectTransform {
			get {
				if (cachedTransform == null)
					cachedTransform = gameObject.rectTransform();
				return cachedTransform;
			}
		}

		/// <summary>
		/// Whether the layout is currently locked.
		/// </summary>
		[SerializeField]
		protected bool locked;

		// The backing fields must be annotated with the attribute to have prefabbed versions
		// successfully copy the values
		[SerializeField]
		private float mMinWidth, mMinHeight, mPreferredWidth, mPreferredHeight;
		[SerializeField]
		private float mFlexibleWidth, mFlexibleHeight;
		[SerializeField]
		private int mLayoutPriority;

		/// <summary>
		/// The cached rect transform to speed up layout.
		/// </summary>
		private RectTransform cachedTransform;

		protected AbstractLayoutGroup() {
			cachedTransform = null;
			locked = false;
			mLayoutPriority = 1;
		}

		public abstract void CalculateLayoutInputHorizontal();

		public abstract void CalculateLayoutInputVertical();

		/// <summary>
		/// Triggers a layout with the current parent, and then locks the layout size. Further
		/// attempts to automatically lay out the component, unless UnlockLayout is called,
		/// will not trigger any action.
		/// 
		/// The resulting layout has very good performance, but cannot adapt to changes in the
		/// size of its children or its own size.
		/// </summary>
		/// <returns>The computed size of this component when locked.</returns>
		public virtual Vector2 LockLayout() {
			var rt = gameObject.rectTransform();
			if (rt != null) {
				locked = false;
				CalculateLayoutInputHorizontal();
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minWidth);
				SetLayoutHorizontal();
				CalculateLayoutInputVertical();
				rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minHeight);
				SetLayoutVertical();
				locked = true;
			}
			return new Vector2(minWidth, minHeight);
		}

		protected override void OnDidApplyAnimationProperties() {
			base.OnDidApplyAnimationProperties();
			SetDirty();
		}

		protected override void OnDisable() {
			base.OnDisable();
			SetDirty();
		}

		protected override void OnEnable() {
			base.OnEnable();
			SetDirty();
		}

		protected override void OnRectTransformDimensionsChange() {
			base.OnRectTransformDimensionsChange();
			SetDirty();
		}

		/// <summary>
		/// Sets this layout as dirty.
		/// </summary>
		protected virtual void SetDirty() {
			if (gameObject != null && IsActive()) {
				if (CanvasUpdateRegistry.IsRebuildingLayout())
					LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
				else
					StartCoroutine(DelayedSetDirty(rectTransform));
			}
		}

		public abstract void SetLayoutHorizontal();

		public abstract void SetLayoutVertical();

		/// <summary>
		/// Unlocks the layout, allowing it to again dynamically resize when component sizes
		/// are changed.
		/// </summary>
		public virtual void UnlockLayout() {
			locked = false;
			LayoutRebuilder.MarkLayoutForRebuild(gameObject.rectTransform());
		}
	}
}
