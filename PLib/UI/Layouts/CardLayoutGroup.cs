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

using PeterHan.PLib.UI.Layouts;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A freezable layout manager that displays one of its contained objects at a time.
	/// Unlike other layout groups, even inactive children are considered for sizing.
	/// </summary>
	public sealed class CardLayoutGroup : UIBehaviour, ISettableFlexSize, ILayoutElement {
		/// <summary>
		/// Calculates the size of the card layout container.
		/// </summary>
		/// <param name="obj">The container to lay out.</param>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="direction">The direction which is being calculated.</param>
		/// <returns>The minimum and preferred box layout size.</returns>
		private static CardLayoutResults Calc(GameObject obj, PanelDirection direction) {
			var transform = obj.AddOrGet<RectTransform>();
			int n = transform.childCount;
			var result = new CardLayoutResults(direction, n);
			var components = ListPool<Component, BoxLayoutGroup>.Allocate();
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i)?.gameObject;
				if (child != null) {
					bool active = child.activeInHierarchy;
					// Not only on active game objects
					components.Clear();
					child.GetComponents(components);
					child.SetActive(true);
					var hc = PUIUtils.CalcSizes(child, direction, components);
					if (!hc.ignore) {
						result.Expand(hc);
						result.children.Add(hc);
					}
					child.SetActive(active);
				}
			}
			components.Recycle();
			return result;
		}

		/// <summary>
		/// Lays out components in the card layout container.
		/// </summary>
		/// <param name="margin">The margin to allow around the components.</param>
		/// <param name="required">The calculated minimum and preferred sizes.</param>
		/// <param name="size">The total available size in this dimension.</param>
		private static void DoLayout(RectOffset margin, CardLayoutResults required, float size)
		{
			if (required == null)
				throw new ArgumentNullException("required");
			var direction = required.direction;
			var components = ListPool<ILayoutController, BoxLayoutGroup>.Allocate();
			// Compensate for margins
			if (direction == PanelDirection.Horizontal)
				size -= margin.left + margin.right;
			else
				size -= margin.top + margin.bottom;
			foreach (var child in required.children) {
				var obj = child.source;
				if (obj != null) {
					float compSize = PUIUtils.GetProperSize(child, size);
					// Place and size component
					var transform = obj.AddOrGet<RectTransform>();
					if (direction == PanelDirection.Horizontal)
						transform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left,
							margin.left, compSize);
					else
						transform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top,
							margin.top, compSize);
					// Invoke SetLayout on dependents
					components.Clear();
					obj.GetComponents(components);
					foreach (var component in components)
						if (direction == PanelDirection.Horizontal)
							component.SetLayoutHorizontal();
						else // if (direction == PanelDirection.Vertical)
							component.SetLayoutVertical();
				}
			}
			components.Recycle();
		}

		/// <summary>
		/// Without adding a CardLayoutGroup component to the specified object, lays it out
		/// based on its current child and layout element sizes, then updates its preferred
		/// and minimum sizes based on the results. The component will be laid out at a fixed
		/// size equal to its preferred size.
		/// 
		/// Child UI elements will be activated one at a time while they are being laid out.
		/// Only children which were active initially will remain active after layout
		/// completes.
		/// </summary>
		/// <param name="obj">The object to lay out immediately.</param>
		/// <param name="margin">The margin to allow around all components.</param>
		/// <param name="size">The minimum component size.</param>
		/// <returns>obj for call chaining.</returns>
		public static GameObject LayoutNow(GameObject obj, RectOffset margin = default,
				Vector2 size = default) {
			if (obj == null)
				throw new ArgumentNullException("obj");
			if (margin == null)
				margin = new RectOffset();
			var layoutElement = obj.AddOrGet<LayoutElement>();
			var rt = obj.rectTransform();
			// Calculate H
			var horizontal = Calc(obj, PanelDirection.Horizontal);
			// Update or create fixed layout element
			float hmin = horizontal.total.preferred + margin.left + margin.right,
				hsize = Math.Max(size.x, hmin);
			layoutElement.minWidth = hsize;
			layoutElement.preferredWidth = hsize;
			layoutElement.flexibleWidth = 0.0f;
			// Size the object now
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hsize);
			DoLayout(margin, horizontal, hsize);
			// Calculate V
			var vertical = Calc(obj, PanelDirection.Vertical);
			float vmin = vertical.total.preferred + margin.top + margin.bottom,
				vsize = Math.Max(size.y, vmin);
			layoutElement.minHeight = vsize;
			layoutElement.preferredHeight = vsize;
			layoutElement.flexibleHeight = 0.0f;
			// Size the object now
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, vsize);
			DoLayout(margin, vertical, vsize);
			return obj;
		}

		public float minWidth { get; private set; }

		public float preferredWidth { get; private set; }

		/// <summary>
		/// The flexible width of the completed layout group can be set.
		/// </summary>
		public float flexibleWidth { get; set; }

		public float minHeight { get; private set; }

		public float preferredHeight { get; private set; }

		/// <summary>
		/// The flexible height of the completed layout group can be set.
		/// </summary>
		public float flexibleHeight { get; set; }

		/// <summary>
		/// The priority of this layout group.
		/// </summary>
		public int layoutPriority { get; set; }

		/// <summary>
		/// The margin to allow around the components.
		/// </summary>
		public RectOffset Margin { get; set; }

		/// <summary>
		/// Results from the horizontal calculation pass.
		/// </summary>
		private CardLayoutResults horizontal;

		/// <summary>
		/// Results from the vertical calculation pass.
		/// </summary>
		private CardLayoutResults vertical;

		internal CardLayoutGroup() {
			horizontal = null;
			layoutPriority = 1;
			vertical = null;
		}

		public void CalculateLayoutInputHorizontal() {
			var margin = Margin;
			float gap = (margin == null) ? 0.0f : margin.left + margin.right;
			horizontal = Calc(gameObject, PanelDirection.Horizontal);
			var hTotal = horizontal.total;
			minWidth = hTotal.min + gap;
			preferredWidth = hTotal.preferred + gap;
		}

		public void CalculateLayoutInputVertical() {
			var margin = Margin;
			float gap = (margin == null) ? 0.0f : margin.top + margin.bottom;
			vertical = Calc(gameObject, PanelDirection.Vertical);
			var vTotal = vertical.total;
			minHeight = vTotal.min + gap;
			preferredHeight = vTotal.preferred + gap;
		}

		protected override void OnDidApplyAnimationProperties() {
			base.OnDidApplyAnimationProperties();
			SetDirty();
		}

		protected override void OnDisable() {
			base.OnDisable();
			SetDirty();
			horizontal = null;
			vertical = null;
		}

		protected override void OnEnable() {
			base.OnEnable();
			SetDirty();
			horizontal = null;
			vertical = null;
		}

		protected override void OnRectTransformDimensionsChange() {
			base.OnRectTransformDimensionsChange();
			SetDirty();
		}

		/// <summary>
		/// Sets this layout as dirty.
		/// </summary>
		private void SetDirty() {
			if (gameObject != null && IsActive())
				LayoutRebuilder.MarkLayoutForRebuild(gameObject.rectTransform());
		}

		/// <summary>
		/// Switches the active card.
		/// </summary>
		/// <param name="card">The child to make active, or null to inactivate all children.</param>
		public void SetActiveCard(GameObject card) {
			int n = transform.childCount;
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i)?.gameObject;
				if (child != null)
					child.SetActive(child == card);
			}
		}

		/// <summary>
		/// Switches the active card.
		/// </summary>
		/// <param name="index">The child index to make active, or -1 to inactivate all children.</param>
		public void SetActiveCard(int index) {
			int n = transform.childCount;
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i)?.gameObject;
				if (child != null)
					child.SetActive(i == index);
			}
		}

		public void SetLayoutHorizontal() {
#if DEBUG
			if (horizontal == null)
				throw new InvalidOperationException("SetLayoutHorizontal before CalculateLayoutInputHorizontal");
#endif
			if (horizontal != null) {
				var rt = gameObject.rectTransform();
				DoLayout(Margin ?? new RectOffset(), horizontal, rt.rect.width);
			}
		}

		public void SetLayoutVertical() {
#if DEBUG
			if (vertical == null)
				throw new InvalidOperationException("SetLayoutVertical before CalculateLayoutInputVertical");
#endif
			if (vertical != null) {
				var rt = gameObject.rectTransform();
				DoLayout(Margin ?? new RectOffset(), vertical, rt.rect.height);
			}
		}
	}
}
