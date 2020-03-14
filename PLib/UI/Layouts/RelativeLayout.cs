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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PeterHan.PLib.UI.Layouts;

using RelativeLayoutData = ListPool<PeterHan.PLib.UI.Layouts.RelativeLayoutResults,
	PeterHan.PLib.UI.RelativeLayout>;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A class that lays out raw game objects relative to each other, creating a final layout
	/// that depends only on the Unity anchor primitives. Creates the highest performance
	/// layouts of any layout manager, equivalent to those hand designed in Unity, but has
	/// limited flexibility for adapting to changes in size.
	/// 
	/// Objects must be added to the supplied game object manually in addition to adding their
	/// constraints in this layout manager. Objects which lack constraints for any edge will
	/// have that edge automatically constrained to the edge of the parent object, including
	/// any insets.
	/// 
	/// The resulting layout may not update correctly if the sizes of child components change,
	/// particularly if the minimum bounds of the parent must be resized. To allow more
	/// flexibility in these circumstances, use RelativeLayoutGroup.
	/// </summary>
	[Obsolete("This class is obsolete. Use RelativeLayoutGroup.LockLayout().")]
	public sealed class RelativeLayout : IRelativeLayout<RelativeLayout, GameObject> {
		/// <summary>
		/// The parent game object where the layout will be performed.
		/// </summary>
		internal GameObject Parent { get; }

		/// <summary>
		/// The margin added around all components in the layout. This is in addition to any
		/// margins around the components.
		/// 
		/// Note that this margin is not taken into account with percentage based anchors.
		/// Items anchored to the extremes will always work fine. Items anchored in the middle
		/// will use the middle <b>before</b> margins are effective.
		/// </summary>
		public RectOffset OverallMargin { get; set; }

		/// <summary>
		/// Constraints for each object are stored here.
		/// </summary>
		private readonly IDictionary<GameObject, RelativeLayoutParams> locConstraints;

		/// <summary>
		/// Creates a new relative layout. This class is not a layout group as it does not
		/// remain attached to the parent post execution.
		/// </summary>
		/// <param name="parent">The object to lay out.</param>
		public RelativeLayout(GameObject parent) {
			OverallMargin = null;
			Parent = parent ?? throw new ArgumentNullException("parent");
			locConstraints = new Dictionary<GameObject, RelativeLayoutParams>(32);
		}

		/// <summary>
		/// Retrieves the parameters for a child game object. Creates an entry if none exists
		/// for this component.
		/// </summary>
		/// <param name="item">The item to look up.</param>
		/// <returns>The parameters for that object.</returns>
		private RelativeLayoutParams AddOrGet(GameObject item) {
			if (!locConstraints.TryGetValue(item, out RelativeLayoutParams param))
				locConstraints[item] = param = new RelativeLayoutParams();
			return param;
		}

		public RelativeLayout AnchorXAxis(GameObject item, float anchor = 0.5f) {
			SetLeftEdge(item, fraction: anchor);
			return SetRightEdge(item, fraction: anchor);
		}

		public RelativeLayout AnchorYAxis(GameObject item, float anchor = 0.5f) {
			SetTopEdge(item, fraction: anchor);
			return SetBottomEdge(item, fraction: anchor);
		}

		/// <summary>
		/// Executes the relative layout with the current constraints and children. The
		/// objects will be arranged using Unity anchors which allows the layout to adapt to
		/// changes in size without rebuilding or invoking auto-layout again, increasing
		/// performance greatly over layout managers.
		/// </summary>
		/// <param name="addLayoutElement">If true, adds a LayoutElement to the parent
		/// indicating its preferred and minimum size. Even if the resulting layout could be
		/// expanded, its flexible size will always default to zero, although it can be changed
		/// after construction using SetFlexUISize.</param>
		/// <exception cref="InvalidOperationException">If the layout constraints cannot
		/// be successfully resolved to final positions - for example, if components depend
		/// on each other in a cycle.</exception>
		/// <returns>The parent game object.</returns>
		public GameObject Execute(bool addLayoutElement = false) {
			if (Parent == null)
				throw new InvalidOperationException("Parent was disposed");
			var group = Parent.AddOrGet<RelativeLayoutGroup>();
			group.Import(locConstraints);
			if (addLayoutElement)
				group.LockLayout();
			return Parent;
		}

		public RelativeLayout OverrideSize(GameObject item, Vector2 size) {
			if (item != null)
				AddOrGet(item).OverrideSize = size;
			return this;
		}

		public RelativeLayout SetBottomEdge(GameObject item, float fraction = -1.0f,
				GameObject above = null) {
			if (item != null) {
				if (above == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).BottomEdge, fraction, above);
			}
			return this;
		}

		/// <summary>
		/// Sets a component's edge constraint.
		/// </summary>
		/// <param name="edge">The edge to set.</param>
		/// <param name="fraction">The fraction of the parent to anchor.</param>
		/// <param name="child">The other component to anchor.</param>
		private void SetEdge(RelativeLayoutParams.EdgeStatus edge, float fraction,
				GameObject child) {
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

		public RelativeLayout SetLeftEdge(GameObject item, float fraction = -1.0f,
				GameObject toRight = null) {
			if (item != null) {
				if (toRight == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).LeftEdge, fraction, toRight);
			}
			return this;
		}

		public RelativeLayout SetMargin(GameObject item, RectOffset insets) {
			if (item != null)
				AddOrGet(item).Insets = insets;
			return this;
		}

		public RelativeLayout SetRightEdge(GameObject item, float fraction = -1.0f,
				GameObject toLeft = null) {
			if (item != null) {
				if (toLeft == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).RightEdge, fraction, toLeft);
			}
			return this;
		}

		public RelativeLayout SetTopEdge(GameObject item, float fraction = -1.0f,
				GameObject below = null) {
			if (item != null) {
				if (below == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).TopEdge, fraction, below);
			}
			return this;
		}
	}
}
