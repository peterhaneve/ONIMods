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

//#define DEBUG_LAYOUT
using PeterHan.PLib.UI.Layouts;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A freezable, flexible layout manager that fixes the issues I am having with
	/// HorizontalLayoutGroup and VerticalLayoutGroup. You get a content size fitter for
	/// free too!
	/// 
	/// Intended to work something like Java's BoxLayout...
	/// </summary>
	public sealed class BoxLayoutGroup : AbstractLayoutGroup {
		/// <summary>
		/// Calculates the size of the box layout container.
		/// </summary>
		/// <param name="obj">The container to lay out.</param>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="direction">The direction which is being calculated.</param>
		/// <returns>The minimum and preferred box layout size.</returns>
		private static BoxLayoutResults Calc(GameObject obj, BoxLayoutParams args,
				PanelDirection direction) {
			var transform = obj.AddOrGet<RectTransform>();
			int n = transform.childCount;
			var result = new BoxLayoutResults(direction, n);
			var components = ListPool<Component, BoxLayoutGroup>.Allocate();
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i)?.gameObject;
				if (child != null && child.activeInHierarchy) {
					// Only on active game objects
					components.Clear();
					child.GetComponents(components);
					var hc = PUIUtils.CalcSizes(child, direction, components);
					if (!hc.ignore) {
						if (args.Direction == direction)
							result.Accum(hc, args.Spacing);
						else
							result.Expand(hc);
						result.children.Add(hc);
					}
				}
			}
			components.Recycle();
			return result;
		}

		/// <summary>
		/// Lays out components in the box layout container.
		/// </summary>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="required">The calculated minimum and preferred sizes.</param>
		/// <param name="size">The total available size in this dimension.</param>
		private static void DoLayout(BoxLayoutParams args, BoxLayoutResults required,
				float size) {
			if (required == null)
				throw new ArgumentNullException(nameof(required));
			var direction = required.direction;
			var status = new BoxLayoutStatus(direction, args.Margin ?? new RectOffset(), size);
			if (args.Direction == direction)
				DoLayoutLinear(required, args, status);
			else
				DoLayoutPerp(required, args, status);
		}

		/// <summary>
		/// Lays out components in the box layout container parallel to the layout axis.
		/// </summary>
		/// <param name="required">The calculated minimum and preferred sizes.</param>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="status">The current status of layout.</param>
		private static void DoLayoutLinear(BoxLayoutResults required, BoxLayoutParams args,
				BoxLayoutStatus status) {
			var total = required.total;
			var components = ListPool<ILayoutController, BoxLayoutGroup>.Allocate();
			var direction = args.Direction;
			// Determine flex size ratio
			float size = status.size, prefRatio = 0.0f, minSize = total.min, prefSize =
				total.preferred, excess = Math.Max(0.0f, size - prefSize), flexTotal = total.
				flexible, offset = status.offset, spacing = args.Spacing;
			if (size > minSize && prefSize > minSize)
				// Do not divide by 0
				prefRatio = Math.Min(1.0f, (size - minSize) / (prefSize - minSize));
			if (excess > 0.0f && flexTotal == 0.0f)
				// If no components can be expanded, offset all
				offset += PUIUtils.GetOffset(args.Alignment, status.direction, excess);
			foreach (var child in required.children) {
				var obj = child.source;
				// Active objects only
				if (obj != null && obj.activeInHierarchy) {
					float compSize = child.min;
					if (prefRatio > 0.0f)
						compSize += (child.preferred - child.min) * prefRatio;
					if (excess > 0.0f && flexTotal > 0.0f)
						compSize += excess * child.flexible / flexTotal;
					// Place and size component
					obj.AddOrGet<RectTransform>().SetInsetAndSizeFromParentEdge(status.edge,
						offset, compSize);
					offset += compSize + ((compSize > 0.0f) ? spacing : 0.0f);
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
		/// Lays out components in the box layout container against the layout axis.
		/// </summary>
		/// <param name="required">The calculated minimum and preferred sizes.</param>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="status">The current status of layout.</param>
		private static void DoLayoutPerp(BoxLayoutResults required, BoxLayoutParams args,
				BoxLayoutStatus status) {
			var components = ListPool<ILayoutController, BoxLayoutGroup>.Allocate();
			var direction = args.Direction;
			float size = status.size;
			foreach (var child in required.children) {
				var obj = child.source;
				// Active objects only
				if (obj != null && obj.activeInHierarchy) {
					float compSize = size;
					if (child.flexible <= 0.0f)
						// Does not expand to all
						compSize = Math.Min(compSize, child.preferred);
					float offset = (size > compSize) ? PUIUtils.GetOffset(args.Alignment,
						status.direction, size - compSize) : 0.0f;
					// Place and size component
					obj.AddOrGet<RectTransform>().SetInsetAndSizeFromParentEdge(status.edge,
						offset + status.offset, compSize);
					// Invoke SetLayout on dependents
					components.Clear();
					obj.GetComponents(components);
					foreach (var component in components)
						if (direction == PanelDirection.Horizontal)
							component.SetLayoutVertical();
						else // if (direction == PanelDirection.Vertical)
							component.SetLayoutHorizontal();
				}
			}
			components.Recycle();
		}

		/// <summary>
		/// The parameters used to set up this box layout.
		/// </summary>
		public BoxLayoutParams Params {
			get {
				return parameters;
			}
			set {
				parameters = value ?? throw new ArgumentNullException(nameof(Params));
			}
		}

		/// <summary>
		/// Results from the horizontal calculation pass.
		/// </summary>
		private BoxLayoutResults horizontal;

		/// <summary>
		/// The parameters used to set up this box layout.
		/// </summary>
		[SerializeField]
		private BoxLayoutParams parameters;

		/// <summary>
		/// Results from the vertical calculation pass.
		/// </summary>
		private BoxLayoutResults vertical;

		internal BoxLayoutGroup() {
			horizontal = null;
			layoutPriority = 1;
			parameters = new BoxLayoutParams();
			vertical = null;
		}

		public override void CalculateLayoutInputHorizontal() {
			if (!locked) {
				var margin = parameters.Margin;
				float gap = (margin == null) ? 0.0f : margin.left + margin.right;
				horizontal = Calc(gameObject, parameters, PanelDirection.Horizontal);
				var hTotal = horizontal.total;
				minWidth = hTotal.min + gap;
				preferredWidth = hTotal.preferred + gap;
#if DEBUG_LAYOUT
				PUIUtils.LogUIDebug("CalculateLayoutInputHorizontal for {0} preferred {1:F2}".
					F(gameObject.name, preferredWidth));
#endif
			}
		}

		public override void CalculateLayoutInputVertical() {
			if (!locked) {
				var margin = parameters.Margin;
				float gap = (margin == null) ? 0.0f : margin.top + margin.bottom;
				vertical = Calc(gameObject, parameters, PanelDirection.Vertical);
				var vTotal = vertical.total;
				minHeight = vTotal.min + gap;
				preferredHeight = vTotal.preferred + gap;
#if DEBUG_LAYOUT
				PUIUtils.LogUIDebug("CalculateLayoutInputVertical for {0} preferred {1:F2}".F(
					gameObject.name, preferredHeight));
#endif
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

		public override void SetLayoutHorizontal() {
			if (horizontal != null && !locked) {
#if DEBUG_LAYOUT
				PUIUtils.LogUIDebug("SetLayoutHorizontal for {0} resolved width to {1:F2}".F(
					gameObject.name, rectTransform.rect.width));
#endif
				DoLayout(parameters, horizontal, rectTransform.rect.width);
			}
		}

		public override void SetLayoutVertical() {
			if (vertical != null && !locked) {
#if DEBUG_LAYOUT
				PUIUtils.LogUIDebug("SetLayoutVertical for {0} resolved height to {1:F2}".F(
					gameObject.name, rectTransform.rect.height));
#endif
				DoLayout(parameters, vertical, rectTransform.rect.height);
			}
		}
	}
}
