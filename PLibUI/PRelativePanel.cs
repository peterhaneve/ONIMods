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
using PeterHan.PLib.UI.Layouts;
using System;
using System.Collections.Generic;
using UnityEngine;

using RelativeConfig = PeterHan.PLib.UI.Layouts.RelativeLayoutParamsBase<PeterHan.PLib.UI.
	IUIComponent>;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A panel which lays out its components using relative constraints.
	/// 
	/// This layout manager is the fastest of all panels when laid out, especially since it
	/// can function properly when frozen even on dynamically sized items. However, it is also
	/// the most difficult to set up and cannot handle all layouts.
	/// </summary>
	public class PRelativePanel : PContainer, IDynamicSizable {
		/// <summary>
		/// Constraints for each object are stored here.
		/// </summary>
		private readonly IDictionary<IUIComponent, RelativeConfig> constraints;

		public bool DynamicSize { get; set; }

		public PRelativePanel() : this(null) {
			DynamicSize = true;
		}

		public PRelativePanel(string name) : base(name ?? "RelativePanel") {
			constraints = new Dictionary<IUIComponent, RelativeConfig>(16);
			Margin = null;
		}

		/// <summary>
		/// Adds a child to this panel. Children must be added to the panel before they are
		/// referenced in a constraint.
		/// </summary>
		/// <param name="child">The child to add.</param>
		/// <returns>This panel for call chaining.</returns>
		public PRelativePanel AddChild(IUIComponent child) {
			if (child == null)
				throw new ArgumentNullException(nameof(child));
			if (!constraints.ContainsKey(child))
				constraints.Add(child, new RelativeConfig());
			return this;
		}

		/// <summary>
		/// Adds a handler when this panel is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This panel for call chaining.</returns>
		public PRelativePanel AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		/// <summary>
		/// Anchors the component's pivot in the X axis to the specified anchor position.
		/// The component will be laid out at its preferred (or overridden) width with its
		/// pivot locked to the specified relative fraction of the parent component's width.
		/// 
		/// Any other existing left or right edge constraints will be overwritten. This method
		/// is equivalent to setting both the left and right edges to the same fraction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="anchor">The fraction to which to align the pivot, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel AnchorXAxis(IUIComponent item, float anchor = 0.5f) {
			SetLeftEdge(item, fraction: anchor);
			return SetRightEdge(item, fraction: anchor);
		}

		/// <summary>
		/// Anchors the component's pivot in the Y axis to the specified anchor position.
		/// The component will be laid out at its preferred (or overridden) height with its
		/// pivot locked to the specified relative fraction of the parent component's height.
		/// 
		/// Any other existing top or bottom edge constraints will be overwritten. This method
		/// is equivalent to setting both the top and bottom edges to the same fraction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="anchor">The fraction to which to align the pivot, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel AnchorYAxis(IUIComponent item, float anchor = 0.5f) {
			SetTopEdge(item, fraction: anchor);
			return SetBottomEdge(item, fraction: anchor);
		}

		public override GameObject Build() {
			var panel = PUIElements.CreateUI(null, Name);
			var mapping = DictionaryPool<IUIComponent, GameObject, PRelativePanel>.Allocate();
			SetImage(panel);
			// Realize each component and add them to the panel
			foreach (var pair in constraints) {
				var component = pair.Key;
				var realized = component.Build();
				realized.SetParent(panel);
				// We were already guaranteed that there were no duplicate keys
				mapping[component] = realized;
			}
			// Add layout component
			var layout = panel.AddComponent<RelativeLayoutGroup>();
			layout.Margin = Margin;
			foreach (var pair in constraints) {
				var realized = mapping[pair.Key];
				var rawParams = pair.Value;
				var newParams = new RelativeLayoutParams();
				// Copy all of the settings
				Resolve(newParams.TopEdge, rawParams.TopEdge, mapping);
				Resolve(newParams.BottomEdge, rawParams.BottomEdge, mapping);
				Resolve(newParams.LeftEdge, rawParams.LeftEdge, mapping);
				Resolve(newParams.RightEdge, rawParams.RightEdge, mapping);
				newParams.OverrideSize = rawParams.OverrideSize;
				newParams.Insets = rawParams.Insets;
				layout.SetRaw(realized, newParams);
			}
			if (!DynamicSize)
				layout.LockLayout();
			mapping.Recycle();
			// Set flex size
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			InvokeRealize(panel);
			return panel;
		}

		/// <summary>
		/// Retrieves the constraints for a component, or throws an exception if the component
		/// has not yet been added.
		/// </summary>
		/// <param name="item">The unrealized component to look up.</param>
		/// <returns>The constraints for that component.</returns>
		/// <exception cref="ArgumentException">If the component has not yet been added to the panel.</exception>
		private RelativeConfig GetOrThrow(IUIComponent item) {
			if (item == null)
				throw new ArgumentNullException(nameof(item));
			if (!constraints.TryGetValue(item, out RelativeConfig value))
				throw new ArgumentException("Components must be added to the panel before using them in a constraint");
			return value;
		}

		/// <summary>
		/// Overrides the preferred size of a component. If set, instead of looking at layout
		/// sizes of the component, the specified size will be used instead.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="size">The size to apply. Only dimensions greater than zero will be used.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel OverrideSize(IUIComponent item, Vector2 size) {
			if (item != null)
				GetOrThrow(item).OverrideSize = size;
			return this;
		}

		/// <summary>
		/// Converts the edge settings configured in this component to settings for the
		/// relative panel.
		/// </summary>
		/// <param name="dest">The location where the converted settings will be stored.</param>
		/// <param name="status">The original component edge configuration.</param>
		/// <param name="mapping">The mapping from PLib UI components to Unity objects.</param>
		private void Resolve(RelativeLayoutParams.EdgeStatus dest, RelativeConfig.EdgeStatus
				status, IDictionary<IUIComponent, GameObject> mapping) {
			var c = status.FromComponent;
			dest.FromAnchor = status.FromAnchor;
			if (c != null)
				dest.FromComponent = mapping[c];
			dest.Constraint = status.Constraint;
			dest.Offset = status.Offset;
		}

		/// <summary>
		/// Sets the bottom edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the bottom edge anchored to that fraction of the parent's
		/// height. If a component is specified and no fraction is specified, the component
		/// will be anchored with its bottom edge above the top edge of that component.
		/// If neither is specified, all bottom edge constraints will be removed.
		/// 
		/// Any other existing bottom edge constraint will be overwritten.
		/// 
		/// Remember that +Y is in the upwards direction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the bottom edge, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <param name="above">The game object which this component must be above.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel SetBottomEdge(IUIComponent item, float fraction = -1.0f,
				IUIComponent above = null) {
			if (item != null) {
				if (above == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(GetOrThrow(item).BottomEdge, fraction, above);
			}
			return this;
		}

		/// <summary>
		/// Sets a component's edge constraint.
		/// </summary>
		/// <param name="edge">The edge to set.</param>
		/// <param name="fraction">The fraction of the parent to anchor.</param>
		/// <param name="child">The other component to anchor.</param>
		private void SetEdge(RelativeConfig.EdgeStatus edge, float fraction,
				IUIComponent child) {
			if (fraction >= 0.0f && fraction <= 1.0f) {
				edge.Constraint = RelativeConstraintType.ToAnchor;
				edge.FromAnchor = fraction;
				edge.FromComponent = null;
			} else if (child != null) {
				edge.Constraint = RelativeConstraintType.ToComponent;
				edge.FromComponent = child;
			} else {
				edge.Constraint = RelativeConstraintType.Unconstrained;
				edge.FromComponent = null;
			}
		}

		/// <summary>
		/// Sets the left edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the left edge anchored to that fraction of the parent's
		/// width. If a component is specified and no fraction is specified, the component
		/// will be anchored with its left edge to the right of that component.
		/// If neither is specified, all left edge constraints will be removed.
		/// 
		/// Any other existing left edge constraint will be overwritten.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the left edge, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <param name="toLeft">The game object which this component must be to the right of.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel SetLeftEdge(IUIComponent item, float fraction = -1.0f,
				IUIComponent toRight = null) {
			if (item != null) {
				if (toRight == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(GetOrThrow(item).LeftEdge, fraction, toRight);
			}
			return this;
		}

		/// <summary>
		/// Sets the insets of a component from its anchor points. A positive number insets the
		/// component away from the edge, whereas a negative number out-sets the component
		/// across the edge.
		/// 
		/// All components default to no insets.
		/// 
		/// Any reference to a component's edge using other constraints always refers to its
		/// edge <b>before</b> insets are applied.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="insets">The insets to apply. If null, the insets will be set to zero.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel SetMargin(IUIComponent item, RectOffset insets) {
			if (item != null)
				GetOrThrow(item).Insets = insets;
			return this;
		}

		/// <summary>
		/// Sets the right edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the right edge anchored to that fraction of the parent's
		/// width. If a component is specified and no fraction is specified, the component
		/// will be anchored with its right edge to the left of that component.
		/// If neither is specified, all right edge constraints will be removed.
		/// 
		/// Any other existing right edge constraint will be overwritten.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the right edge, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <param name="toLeft">The game object which this component must be to the left of.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel SetRightEdge(IUIComponent item, float fraction = -1.0f,
				IUIComponent toLeft = null) {
			if (item != null) {
				if (toLeft == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(GetOrThrow(item).RightEdge, fraction, toLeft);
			}
			return this;
		}

		/// <summary>
		/// Sets the top edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the top edge anchored to that fraction of the parent's
		/// height. If a component is specified and no fraction is specified, the component
		/// will be anchored with its top edge above the bottom edge of that component.
		/// If neither is specified, all top edge constraints will be removed.
		/// 
		/// Any other existing top edge constraint will be overwritten.
		/// 
		/// Remember that +Y is in the upwards direction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the top edge, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <param name="below">The game object which this component must be below.</param>
		/// <returns>This object, for call chaining.</returns>
		public PRelativePanel SetTopEdge(IUIComponent item, float fraction = -1.0f,
				IUIComponent below = null) {
			if (item != null) {
				if (below == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(GetOrThrow(item).TopEdge, fraction, below);
			}
			return this;
		}

		public override string ToString() {
			return string.Format("PRelativePanel[Name={0}]", Name);
		}
	}
}
