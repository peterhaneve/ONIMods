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

using PeterHan.PLib.UI.Layouts;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A freezable layout manager that displays one of its contained objects at a time.
	/// Unlike other layout groups, even inactive children are considered for sizing.
	/// </summary>
	public sealed class CardLayoutGroup : AbstractLayoutGroup {
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
				throw new ArgumentNullException(nameof(required));
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
		/// The margin around the components as a whole.
		/// </summary>
		public RectOffset Margin {
			get {
				return margin;
			}
			set {
				margin = value;
			}
		}

		/// <summary>
		/// Results from the horizontal calculation pass.
		/// </summary>
		private CardLayoutResults horizontal;

		/// <summary>
		/// The margin around the components as a whole.
		/// </summary>
		[SerializeField]
		private RectOffset margin;

		/// <summary>
		/// Results from the vertical calculation pass.
		/// </summary>
		private CardLayoutResults vertical;

		internal CardLayoutGroup() {
			horizontal = null;
			layoutPriority = 1;
			vertical = null;
		}

		public override void CalculateLayoutInputHorizontal() {
			if (!locked) {
				var margin = Margin;
				float gap = (margin == null) ? 0.0f : margin.left + margin.right;
				horizontal = Calc(gameObject, PanelDirection.Horizontal);
				var hTotal = horizontal.total;
				minWidth = hTotal.min + gap;
				preferredWidth = hTotal.preferred + gap;
			}
		}

		public override void CalculateLayoutInputVertical() {
			if (!locked) {
				var margin = Margin;
				float gap = (margin == null) ? 0.0f : margin.top + margin.bottom;
				vertical = Calc(gameObject, PanelDirection.Vertical);
				var vTotal = vertical.total;
				minHeight = vTotal.min + gap;
				preferredHeight = vTotal.preferred + gap;
			}
		}

		protected override void OnDisable() {
			base.OnDisable();
			horizontal = null;
			vertical = null;
		}

		protected override void OnEnable() {
			base.OnEnable();
			horizontal = null;
			vertical = null;
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

		public override void SetLayoutHorizontal() {
			if (horizontal != null && !locked)
				DoLayout(Margin ?? new RectOffset(), horizontal, rectTransform.rect.width);
		}

		public override void SetLayoutVertical() {
			if (vertical != null && !locked)
				DoLayout(Margin ?? new RectOffset(), vertical, rectTransform.rect.height);
		}
	}
}
