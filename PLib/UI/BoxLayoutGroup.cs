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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A freezable, flexible layout manager that fixes the issues I am having with
	/// HorizontalLayoutGroup and VerticalLayoutGroup. You get a content size fitter for
	/// free too!
	/// 
	/// Intended to work something like Java's BoxLayout...
	/// </summary>
	public sealed class BoxLayoutGroup : UIBehaviour, ILayoutGroup, ILayoutElement {
		/// <summary>
		/// Calculates the size of the box layout container.
		/// </summary>
		/// <param name="obj">The container to lay out.</param>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="direction">The direction which is being calculated.</param>
		/// <returns>The minimum and preferred box layout size.</returns>
		private static LayoutResults Calc(GameObject obj, BoxLayoutParams args,
				PanelDirection direction) {
			var transform = obj.AddOrGet<RectTransform>();
			int n = transform.childCount;
			var result = new LayoutResults(direction, n);
			var components = ListPool<ILayoutElement, BoxLayoutGroup>.Allocate();
			for (int i = 0; i < n; i++) {
				var child = transform.GetChild(i)?.gameObject;
				if (child != null && child.activeInHierarchy) {
					// Only on active game objects
					components.Clear();
					child.GetComponents(components);
					var hc = GetSize(child, direction, components);
					if (args.Direction == direction)
						result.Accum(hc, args.Spacing);
					else
						result.Expand(hc);
					result.children.Add(hc);
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
		private static void DoLayout(BoxLayoutParams args, LayoutResults required, float size)
		{
			if (required == null)
				throw new ArgumentNullException("required");
			var direction = required.direction;
			var status = new LayoutStatus(direction, args.Margin ?? new RectOffset(), size);
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
		private static void DoLayoutLinear(LayoutResults required, BoxLayoutParams args,
				LayoutStatus status) {
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
				offset += GetOffset(args, status.direction, excess);
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
						if (!IgnoreLayout(component)) {
							if (direction == PanelDirection.Horizontal)
								component.SetLayoutHorizontal();
							else // if (direction == PanelDirection.Vertical)
								component.SetLayoutVertical();
						}
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
		private static void DoLayoutPerp(LayoutResults required, BoxLayoutParams args,
				LayoutStatus status) {
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
					float offset = (size > compSize) ? GetOffset(args, status.direction,
						size - compSize) : 0.0f;
					// Place and size component
					obj.AddOrGet<RectTransform>().SetInsetAndSizeFromParentEdge(status.edge,
						offset + status.offset, compSize);
					// Invoke SetLayout on dependants
					components.Clear();
					obj.GetComponents(components);
					foreach (var component in components)
						if (!IgnoreLayout(component)) {
							if (direction == PanelDirection.Horizontal)
								component.SetLayoutVertical();
							else // if (direction == PanelDirection.Vertical)
								component.SetLayoutHorizontal();
						}
				}
			}
			components.Recycle();
		}

		/// <summary>
		/// Gets the offset required for a component in its box.
		/// </summary>
		/// <param name="args">The parameters to use for layout.</param>
		/// <param name="direction">The direction of layout.</param>
		/// <param name="delta">The remaining space.</param>
		/// <returns>The offset from the edge.</returns>
		private static float GetOffset(BoxLayoutParams args, PanelDirection direction,
				float delta) {
			float offset = 0.0f;
			// Based on alignment, offset component
			if (direction == PanelDirection.Horizontal)
				switch (args.Alignment) {
				case TextAnchor.LowerCenter:
				case TextAnchor.MiddleCenter:
				case TextAnchor.UpperCenter:
					offset = delta * 0.5f;
					break;
				case TextAnchor.LowerRight:
				case TextAnchor.MiddleRight:
				case TextAnchor.UpperRight:
					offset = delta;
					break;
				default:
					break;
				}
			else
				switch (args.Alignment) {
				case TextAnchor.MiddleLeft:
				case TextAnchor.MiddleCenter:
				case TextAnchor.MiddleRight:
					offset = delta * 0.5f;
					break;
				case TextAnchor.LowerLeft:
				case TextAnchor.LowerCenter:
				case TextAnchor.LowerRight:
					offset = delta;
					break;
				default:
					break;
				}
			return offset;
		}

		/// <summary>
		/// Calculates the size of a single game object.
		/// </summary>
		/// <param name="obj">The object to calculate.</param>
		/// <param name="direction">The direction to calculate.</param>
		/// <param name="elements">The layout eligible components of this game object.</param>
		/// <returns>The object's minimum and preferred size.</returns>
		private static LayoutSizes GetSize(GameObject obj, PanelDirection direction,
				IEnumerable<ILayoutElement> elements) {
			float min = 0.0f, preferred = 0.0f, flexible = 0.0f;
			int minPri = int.MinValue, prefPri = int.MinValue, flexPri = int.MinValue;
			foreach (var component in elements)
				if (((component as Behaviour)?.isActiveAndEnabled ?? false) && !IgnoreLayout(
						component)) {
					// Calculate must come first
					if (direction == PanelDirection.Horizontal)
						component.CalculateLayoutInputHorizontal();
					else // if (direction == PanelDirection.Vertical)
						component.CalculateLayoutInputVertical();
					int lp = component.layoutPriority;
					// Larger values win
					if (direction == PanelDirection.Horizontal) {
						PriValue(ref min, component.minWidth, lp, ref minPri);
						PriValue(ref preferred, component.preferredWidth, lp, ref prefPri);
						PriValue(ref flexible, component.flexibleWidth, lp, ref flexPri);
					} else {
						PriValue(ref min, component.minHeight, lp, ref minPri);
						PriValue(ref preferred, component.preferredHeight, lp, ref prefPri);
						PriValue(ref flexible, component.flexibleHeight, lp, ref flexPri);
					}
				}
			return new LayoutSizes(obj, min, Math.Max(min, preferred), flexible);
		}

		/// <summary>
		/// Reports whether the component should be ignored for layout.
		/// </summary>
		/// <param name="component">The component to check.</param>
		/// <returns>true if it specifies to ignore layout, or false otherwise.</returns>
		private static bool IgnoreLayout(object component) {
			return (component as ILayoutIgnorer)?.ignoreLayout ?? false;
		}

		/// <summary>
		/// Without adding a BoxLayoutGroup component to the specified object, lays it out
		/// based on its current child and layout element sizes, then updates its preferred
		/// and minimum sizes based on the results. The component will be laid out at a fixed
		/// size equal to its preferred size.
		/// 
		/// UI elements should be active before any layouts are added, especially if they are
		/// to be frozen.
		/// </summary>
		/// <param name="obj">The object to lay out immediately.</param>
		/// <param name="parameters">The layout parameters to use.</param>
		/// <param name="size">The minimum component size.</param>
		/// <returns>obj for call chaining</returns>
		public static GameObject LayoutNow(GameObject obj, BoxLayoutParams parameters = null,
				Vector2 size = default) {
			if (obj == null)
				throw new ArgumentNullException("obj");
			var args = parameters ?? new BoxLayoutParams();
			var margin = args.Margin ?? new RectOffset();
			// Calculate
			var horizontal = Calc(obj, args, PanelDirection.Horizontal);
			var vertical = Calc(obj, args, PanelDirection.Vertical);
			// Update or create fixed layout element
			var layoutElement = obj.AddOrGet<LayoutElement>();
			float hmin = horizontal.total.preferred + margin.left + margin.right,
				vmin = vertical.total.preferred + margin.top + margin.bottom,
				hsize = Math.Max(size.x, hmin), vsize = Math.Max(size.y, vmin);
			layoutElement.minWidth = hsize;
			layoutElement.preferredWidth = hsize;
			layoutElement.flexibleWidth = 0.0f;
			layoutElement.minHeight = vsize;
			layoutElement.preferredHeight = vsize;
			layoutElement.flexibleHeight = 0.0f;
			// Size the object now
			var rt = obj.rectTransform();
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hsize);
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, vsize);
			// Allocate
			DoLayout(args, horizontal, hsize);
			DoLayout(args, vertical, vsize);
			return obj;
		}

		/// <summary>
		/// Aggregates layout values, replacing the value if a higher priority value is given
		/// and otherwise taking the largest value.
		/// </summary>
		/// <param name="value">The current value.</param>
		/// <param name="newValue">The candidate new value. No operation if this is less than zero.</param>
		/// <param name="newPri">The new value's layout priority.</param>
		/// <param name="pri">The current value's priority</param>
		private static void PriValue(ref float value, float newValue, int newPri, ref int pri)
		{
			int thisPri = pri;
			if (newValue >= 0.0f) {
				if (newPri > thisPri) {
					// Priority override?
					pri = newPri;
					value = newValue;
				} else if (newValue > value && newPri == thisPri)
					// Same priority and higher value?
					value = newValue;
			}
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
		/// The parameters used to set up this box layout.
		/// </summary>
		public BoxLayoutParams Params {
			get {
				return parameters;
			}
			set {
				parameters = value ?? throw new ArgumentNullException("Params");
			}
		}

		/// <summary>
		/// Results from the horizontal calculation pass.
		/// </summary>
		private LayoutResults horizontal;

		/// <summary>
		/// The parameters used to set up this box layout.
		/// </summary>
		private BoxLayoutParams parameters;

		/// <summary>
		/// Results from the vertical calculation pass.
		/// </summary>
		private LayoutResults vertical;

		internal BoxLayoutGroup() {
			horizontal = null;
			layoutPriority = 1;
			parameters = new BoxLayoutParams();
			vertical = null;
		}

		public void CalculateLayoutInputHorizontal() {
			var margin = parameters.Margin;
			float gap = (margin == null) ? 0.0f : margin.left + margin.right;
			horizontal = Calc(gameObject, parameters, PanelDirection.Horizontal);
			var hTotal = horizontal.total;
			minWidth = hTotal.min + gap;
			preferredWidth = hTotal.preferred + gap;
		}

		public void CalculateLayoutInputVertical() {
			var margin = parameters.Margin;
			float gap = (margin == null) ? 0.0f : margin.top + margin.bottom;
			vertical = Calc(gameObject, parameters, PanelDirection.Vertical);
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

		public void SetLayoutHorizontal() {
#if DEBUG
			if (horizontal == null)
				throw new InvalidOperationException("SetLayoutHorizontal before CalculateLayoutInputHorizontal");
#endif
			if (horizontal != null) {
				var rt = gameObject.rectTransform();
				DoLayout(parameters, horizontal, rt.rect.size.x);
			}
		}

		public void SetLayoutVertical() {
#if DEBUG
			if (vertical == null)
				throw new InvalidOperationException("SetLayoutVertical before CalculateLayoutInputVertical");
#endif
			if (vertical != null) {
				var rt = gameObject.rectTransform();
				DoLayout(parameters, vertical, rt.rect.size.y);
			}
		}

		/// <summary>
		/// A class which stores the results of a single layout calculation pass.
		/// </summary>
		private sealed class LayoutResults {
			/// <summary>
			/// The components which were laid out.
			/// </summary>
			public readonly ICollection<LayoutSizes> children;

			/// <summary>
			/// The current direction of flow.
			/// </summary>
			public readonly PanelDirection direction;

			/// <summary>
			/// Whether any spaces have been added yet for minimum size.
			/// </summary>
			private bool haveMinSpace;

			/// <summary>
			/// Whether any spaces have been added yet for preferred size.
			/// </summary>
			private bool havePrefSpace;

			/// <summary>
			/// The total sizes.
			/// </summary>
			public LayoutSizes total;

			internal LayoutResults(PanelDirection direction, int presize) {
				children = new List<LayoutSizes>(presize);
				this.direction = direction;
				haveMinSpace = false;
				havePrefSpace = false;
				total = new LayoutSizes();
			}

			/// <summary>
			/// Accumulates another component into the results.
			/// </summary>
			/// <param name="sizes">The size of the component to add.</param>
			/// <param name="spacing">The component spacing.</param>
			public void Accum(LayoutSizes sizes, float spacing) {
				float newMin = sizes.min, newPreferred = sizes.preferred;
				if (newMin > 0.0f) {
					// Skip one space
					if (haveMinSpace)
						newMin += spacing;
					haveMinSpace = true;
				}
				total.min += newMin;
				if (newPreferred > 0.0f) {
					// Skip one space
					if (havePrefSpace)
						newPreferred += spacing;
					havePrefSpace = true;
				}
				total.preferred += newPreferred;
				total.flexible += sizes.flexible;
			}

			/// <summary>
			/// Expands the results around another component.
			/// </summary>
			/// <param name="sizes">The size of the component to expand to.</param>
			public void Expand(LayoutSizes sizes) {
				float newMin = sizes.min, newPreferred = sizes.preferred, newFlexible =
					sizes.flexible;
				if (newMin > total.min)
					total.min = newMin;
				if (newPreferred > total.preferred)
					total.preferred = newPreferred;
				if (newFlexible > total.flexible)
					total.flexible = newFlexible;
			}

			public override string ToString() {
				return direction + " " + total;
			}
		}

		/// <summary>
		/// A class representing the size sets of a particular component.
		/// </summary>
		private struct LayoutSizes {
			/// <summary>
			/// The flexible dimension value.
			/// </summary>
			public float flexible;

			/// <summary>
			/// The minimum dimension value.
			/// </summary>
			public float min;

			/// <summary>
			/// The preferred dimension value.
			/// </summary>
			public float preferred;

			/// <summary>
			/// The source of these values.
			/// </summary>
			public readonly GameObject source;

			internal LayoutSizes(GameObject source, float min, float preferred,
					float flexible) {
				this.source = source;
				this.flexible = flexible;
				this.min = min;
				this.preferred = preferred;
			}

			public override string ToString() {
				return "LayoutSizes[min={0:F2},preferred={1:F2},flexible={2:F2}]".F(min,
					preferred, flexible);
			}
		}

		/// <summary>
		/// Maintains the status of a layout in progress.
		/// </summary>
		private sealed class LayoutStatus {
			/// <summary>
			/// The current direction of flow.
			/// </summary>
			public readonly PanelDirection direction;

			/// <summary>
			/// The edge from where layout started.
			/// </summary>
			public readonly RectTransform.Edge edge;

			/// <summary>
			/// The next component's offset.
			/// </summary>
			public readonly float offset;

			/// <summary>
			/// The component size in that direction minus margins.
			/// </summary>
			public readonly float size;

			internal LayoutStatus(PanelDirection direction, RectOffset margins, float size) {
				this.direction = direction;
				switch (direction) {
				case PanelDirection.Horizontal:
					edge = RectTransform.Edge.Left;
					offset = margins.left;
					this.size = size - offset - margins.right;
					break;
				case PanelDirection.Vertical:
					edge = RectTransform.Edge.Top;
					offset = margins.top;
					this.size = size - offset - margins.bottom;
					break;
				default:
					throw new ArgumentException("direction");
				}
			}
		}
	}

	/// <summary>
	/// The parameters actually used for laying out a box layout.
	/// </summary>
	public sealed class BoxLayoutParams {
		/// <summary>
		/// The alignment to use for components that are not big enough to fit and have no
		/// flexible width.
		/// </summary>
		public TextAnchor Alignment { get; set; }

		/// <summary>
		/// The direction of layout.
		/// </summary>
		public PanelDirection Direction { get; set; }

		/// <summary>
		/// The margin between the children and the component edge.
		/// </summary>
		public RectOffset Margin { get; set; }

		/// <summary>
		/// The spacing between components.
		/// </summary>
		public float Spacing { get; set; }

		public BoxLayoutParams() {
			Alignment = TextAnchor.MiddleCenter;
			Direction = PanelDirection.Horizontal;
			Margin = null;
			Spacing = 0.0f;
		}

		public override string ToString() {
			return "BoxLayoutParams[Alignment={0},Direction={1},Spacing={2:F2}]".F(Alignment,
				Direction, Spacing);
		}
	}
}
