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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A layout group based on the constraints defined in RelativeLayout. Allows the same
	/// fast relative positioning that RelativeLayout does, but can respond to changes in the
	/// size of its containing components.
	/// </summary>
	public sealed class RelativeLayoutGroup : AbstractLayoutGroup,
			ISerializationCallbackReceiver {
		/// <summary>
		/// The margin added around all components in the layout. This is in addition to any
		/// margins around the components.
		/// 
		/// Note that this margin is not taken into account with percentage based anchors.
		/// Items anchored to the extremes will always work fine. Items anchored in the middle
		/// will use the middle <b>before</b> margins are effective.
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
		/// Constraints for each object are stored here.
		/// </summary>
		private readonly IDictionary<GameObject, RelativeLayoutParams> locConstraints;

		/// <summary>
		/// The serialized constraints.
		/// </summary>
		[SerializeField]
		private IList<KeyValuePair<GameObject, RelativeLayoutParams>> serialConstraints;

		/// <summary>
		/// The margin around the components as a whole.
		/// </summary>
		[SerializeField]
		private RectOffset margin;

		/// <summary>
		/// The results of the layout in progress.
		/// </summary>
		private readonly IList<RelativeLayoutResults> results;

		internal RelativeLayoutGroup() {
			layoutPriority = 1;
			locConstraints = new Dictionary<GameObject, RelativeLayoutParams>(32);
			results = new List<RelativeLayoutResults>(32);
			serialConstraints = null;
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

		public RelativeLayoutGroup AnchorXAxis(GameObject item, float anchor = 0.5f) {
			SetLeftEdge(item, fraction: anchor);
			return SetRightEdge(item, fraction: anchor);
		}

		public RelativeLayoutGroup AnchorYAxis(GameObject item, float anchor = 0.5f) {
			SetTopEdge(item, fraction: anchor);
			return SetBottomEdge(item, fraction: anchor);
		}

		public override void CalculateLayoutInputHorizontal() {
			var all = gameObject?.rectTransform();
			if (all != null && !locked) {
				int ml, mr, passes, limit;
				if (Margin == null)
					ml = mr = 0;
				else {
					ml = Margin.left;
					mr = Margin.right;
				}
				// X layout
				results.CalcX(all, locConstraints);
				if (results.Count > 0) {
					limit = 2 * results.Count;
					for (passes = 0; passes < limit && !results.RunPassX(); passes++) ;
					if (passes >= limit)
						results.ThrowUnresolvable(passes, PanelDirection.Horizontal);
				}
				minWidth = preferredWidth = results.GetMinSizeX() + ml + mr;
			}
		}

		public override void CalculateLayoutInputVertical() {
			var all = gameObject?.rectTransform();
			if (all != null && !locked) {
				int passes, limit = 2 * results.Count, mt, mb;
				if (Margin == null)
					mt = mb = 0;
				else {
					mt = Margin.top;
					mb = Margin.bottom;
				}
				// Y layout
				if (results.Count > 0) {
					results.CalcY();
					for (passes = 0; passes < limit && !results.RunPassY(); passes++) ;
					if (passes >= limit)
						results.ThrowUnresolvable(passes, PanelDirection.Vertical);
				}
				minHeight = preferredHeight = results.GetMinSizeY() + mt + mb;
			}
		}

		/// <summary>
		/// Imports the data from RelativeLayout for compatibility.
		/// </summary>
		/// <param name="values">The raw data to import.</param>
		internal void Import(IDictionary<GameObject, RelativeLayoutParams> values) {
			locConstraints.Clear();
			foreach (var pair in values)
				locConstraints[pair.Key] = pair.Value;
		}

		public void OnBeforeSerialize() {
			int n = locConstraints.Count;
			if (n > 0) {
				serialConstraints = new List<KeyValuePair<GameObject, RelativeLayoutParams>>(n);
				foreach (var pair in locConstraints)
					serialConstraints.Add(pair);
			}
		}

		public void OnAfterDeserialize() {
			if (serialConstraints != null) {
				locConstraints.Clear();
				foreach (var pair in serialConstraints)
					locConstraints[pair.Key] = pair.Value;
				serialConstraints = null;
			}
		}

		public RelativeLayoutGroup OverrideSize(GameObject item, Vector2 size) {
			if (item != null)
				AddOrGet(item).OverrideSize = size;
			return this;
		}

		public RelativeLayoutGroup SetBottomEdge(GameObject item, float fraction = -1.0f,
				GameObject above = null) {
			if (item != null) {
				if (above == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).BottomEdge, fraction, above);
			}
			return this;
		}

		protected override void SetDirty() {
			if (!locked)
				base.SetDirty();
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

		public override void SetLayoutHorizontal() {
			if (!locked && results.Count > 0) {
				var components = ListPool<ILayoutController, RelativeLayoutGroup>.Allocate();
				int ml, mr;
				if (Margin == null)
					ml = mr = 0;
				else {
					ml = Margin.left;
					mr = Margin.right;
				}
				// Lay out children
				results.ExecuteX(components, ml, mr);
				components.Recycle();
			}
		}

		public override void SetLayoutVertical() {
			if (!locked && results.Count > 0) {
				var components = ListPool<ILayoutController, RelativeLayoutGroup>.Allocate();
				int mt, mb;
				if (Margin == null)
					mt = mb = 0;
				else {
					mt = Margin.top;
					mb = Margin.bottom;
				}
				// Lay out children
				results.ExecuteY(components, mt, mb);
				components.Recycle();
			}
		}

		public RelativeLayoutGroup SetLeftEdge(GameObject item, float fraction = -1.0f,
				GameObject toRight = null) {
			if (item != null) {
				if (toRight == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).LeftEdge, fraction, toRight);
			}
			return this;
		}

		public RelativeLayoutGroup SetMargin(GameObject item, RectOffset insets) {
			if (item != null)
				AddOrGet(item).Insets = insets;
			return this;
		}

		/// <summary>
		/// Sets all layout parameters of an object at once.
		/// </summary>
		/// <param name="item">The item to configure.</param>
		/// <param name="rawParams">The raw parameters to use.</param>
		internal void SetRaw(GameObject item, RelativeLayoutParams rawParams) {
			if (item != null && rawParams != null)
				locConstraints[item] = rawParams;
		}

		public RelativeLayoutGroup SetRightEdge(GameObject item, float fraction = -1.0f,
				GameObject toLeft = null) {
			if (item != null) {
				if (toLeft == item)
					throw new ArgumentException("Component cannot refer directly to itself");
				SetEdge(AddOrGet(item).RightEdge, fraction, toLeft);
			}
			return this;
		}

		public RelativeLayoutGroup SetTopEdge(GameObject item, float fraction = -1.0f,
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
