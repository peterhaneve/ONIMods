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
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using ConstraintType = PeterHan.PLib.UI.Layouts.RelativeLayoutParams.ConstraintType;
using EdgeStatus = PeterHan.PLib.UI.Layouts.RelativeLayoutParams.EdgeStatus;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// A helper class for RelativeLayout.
	/// </summary>
	internal static class RelativeLayoutUtil {
		/// <summary>
		/// Avoids a crash when attempting to lay out very wide panels by limiting anchor
		/// ratios to this value.
		/// </summary>
		private const float MIN_SIZE_RATIO = 0.01f;

		/// <summary>
		/// Initializes and computes horizontal sizes for the components in this relative
		/// layout.
		/// </summary>
		/// <param name="children">The location to store information about these components.</param>
		/// <param name="all">The components to lay out.</param>
		/// <param name="constraints">The constraints defined for these components.</param>
		internal static void CalcX(this ICollection<RelativeLayoutIP> children,
				RectTransform all, IDictionary<GameObject, RelativeLayoutParams> constraints) {
			var comps = ListPool<Component, RelativeLayout>.Allocate();
			var paramMap = DictionaryPool<GameObject, RelativeLayoutIP, RelativeLayout>.
				Allocate();
			int n = all.childCount;
			for (int i = 0; i < n; i++) {
				var child = all.GetChild(i)?.gameObject;
				if (child != null) {
					comps.Clear();
					// Calculate the preferred size using all layout components
					child.GetComponents(comps);
					var horiz = PUIUtils.CalcSizes(child, PanelDirection.Horizontal, comps);
					if (!horiz.ignore) {
						RelativeLayoutIP ip;
						float w = horiz.preferred;
						if (constraints.TryGetValue(child, out RelativeLayoutParams cons)) {
							ip = new RelativeLayoutIP(child.rectTransform(), cons);
							// Set override size by axis if necessary
							var overrideSize = ip.OverrideSize;
							if (overrideSize.x > 0.0f)
								w = overrideSize.x;
							ip.PreferredWidth = w;
							paramMap[child] = ip;
						} else
							// Default its layout to fill all
							ip = new RelativeLayoutIP(child.rectTransform(), null);
						children.Add(ip);
					}
				}
			}
			// Resolve object references to other children
			foreach (var ip in children) {
				ip.TopParams = InitResolve(ip.TopEdge, paramMap);
				ip.BottomParams = InitResolve(ip.BottomEdge, paramMap);
				ip.LeftParams = InitResolve(ip.LeftEdge, paramMap);
				ip.RightParams = InitResolve(ip.RightEdge, paramMap);
				// All of these will die simultaneously when the list is recycled
			}
			paramMap.Recycle();
			comps.Recycle();
		}

		/// <summary>
		/// Computes vertical sizes for the components in this relative layout.
		/// </summary>
		/// <param name="children">The location to store information about these components.</param>
		internal static void CalcY(this ICollection<RelativeLayoutIP> children) {
			var comps = ListPool<Component, RelativeLayout>.Allocate();
			foreach (var ip in children) {
				var child = ip.Transform.gameObject;
				var overrideSize = ip.OverrideSize;
				comps.Clear();
				// Calculate the preferred size using all layout components
				child.gameObject.GetComponents(comps);
				float h = PUIUtils.CalcSizes(child, PanelDirection.Vertical, comps).preferred;
				// Set override size by axis if necessary
				if (overrideSize.y > 0.0f)
					h = overrideSize.y;
				ip.PreferredHeight = h;
			}
			comps.Recycle();
		}

		/// <summary>
		/// Executes the horizontal layout.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <param name="scratch">The location where components will be temporarily stored.</param>
		/// <param name="mLeft">The left margin.</param>
		/// <param name="mRight">The right margin.</param>
		internal static void ExecuteX(this IEnumerable<RelativeLayoutIP> children,
				List<ILayoutController> scratch, float mLeft = 0.0f, float mRight = 0.0f) {
			foreach (var child in children) {
				var rt = child.Transform;
				var insets = child.Insets;
				EdgeStatus l = child.LeftEdge, r = child.RightEdge;
				// Set corner positions
				rt.anchorMin = new Vector2(l.FromAnchor, 0.0f);
				rt.anchorMax = new Vector2(r.FromAnchor, 1.0f);
				// If anchored by pivot, resize with current anchors
				if (child.UseSizeDeltaX)
					rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, child.
						PreferredWidth);
				else {
					// Left
					rt.offsetMin = new Vector2(l.Offset + insets.left + (l.FromAnchor <= 0.0f ?
						mLeft : 0.0f), rt.offsetMin.y);
					// Right
					rt.offsetMax = new Vector2(r.Offset - insets.right - (r.FromAnchor >=
						1.0f ? mRight : 0.0f), rt.offsetMax.y);
				}
				// Execute layout controllers if present
				scratch.Clear();
				rt.gameObject.GetComponents(scratch);
				foreach (var component in scratch)
					component.SetLayoutHorizontal();
			}
		}

		/// <summary>
		/// Executes the vertical layout.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <param name="scratch">The location where components will be temporarily stored.</param>
		/// <param name="mBottom">The bottom margin.</param>
		/// <param name="mTop">The top margin.</param>
		internal static void ExecuteY(this IEnumerable<RelativeLayoutIP> children,
				List<ILayoutController> scratch, float mBottom = 0.0f, float mTop = 0.0f) {
			foreach (var child in children) {
				var rt = child.Transform;
				var insets = child.Insets;
				EdgeStatus t = child.TopEdge, b = child.BottomEdge;
				// Set corner positions
				rt.anchorMin = new Vector2(rt.anchorMin.x, b.FromAnchor);
				rt.anchorMax = new Vector2(rt.anchorMax.x, t.FromAnchor);
				// If anchored by pivot, resize with current anchors
				if (child.UseSizeDeltaY)
					rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, child.
						PreferredHeight);
				else {
					// Bottom
					rt.offsetMin = new Vector2(rt.offsetMin.x, b.Offset + insets.bottom +
						(b.FromAnchor <= 0.0f ? mBottom : 0.0f));
					// Top
					rt.offsetMax = new Vector2(rt.offsetMax.x, t.Offset - insets.top -
						(t.FromAnchor >= 1.0f ? mTop : 0.0f));
				}
				// Execute layout controllers if present
				scratch.Clear();
				rt.gameObject.GetComponents(scratch);
				foreach (var component in scratch)
					component.SetLayoutVertical();
			}
		}

		/// <summary>
		/// Calculates the minimum size in the X direction.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <returns>The minimum horizontal size.</returns>
		internal static float GetMinSizeX(this IEnumerable<RelativeLayoutIP> children) {
			float maxWidth = 0.0f, width;
			foreach (var child in children) {
				var insets = child.Insets;
				EdgeStatus left = child.LeftEdge, right = child.RightEdge;
				float aMin = left.FromAnchor, aMax = right.FromAnchor;
				if (aMax > aMin)
					// "Elbow room" method
					width = (child.EffectiveWidth + left.Offset - right.Offset) /
						(aMax - aMin);
				else {
					// Anchors are together
					float oMin = left.Offset, oMax = right.Offset;
					if (oMin == oMax)
						oMin = oMax = child.EffectiveWidth * 0.5f;
					if (oMin < 0.0f)
						oMin /= -Math.Max(MIN_SIZE_RATIO, aMax);
					else
						oMin /= Math.Max(MIN_SIZE_RATIO, 1.0f - aMax);
					if (oMax < 0.0f)
						oMax /= -Math.Max(MIN_SIZE_RATIO, aMax);
					else
						oMax /= Math.Max(MIN_SIZE_RATIO, 1.0f - aMax);
					width = Math.Max(oMin, oMax);
				}
				if (width > maxWidth) maxWidth = width;
			}
			return maxWidth;
		}

		/// <summary>
		/// Calculates the minimum size in the Y direction.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <returns>The minimum vertical size.</returns>
		internal static float GetMinSizeY(this IEnumerable<RelativeLayoutIP> children) {
			float maxHeight = 0.0f, height;
			foreach (var child in children) {
				var insets = child.Insets;
				EdgeStatus bottom = child.BottomEdge, top = child.TopEdge;
				float aMin = bottom.FromAnchor, aMax = top.FromAnchor;
				if (aMax > aMin)
					// "Elbow room" method
					height = (child.EffectiveHeight + bottom.Offset - top.Offset) /
						(aMax - aMin);
				else {
					// Anchors are together
					float oMin = bottom.Offset, oMax = top.Offset;
					if (oMin == oMax)
						oMin = oMax = child.EffectiveHeight * 0.5f;
					if (oMin < 0.0f)
						oMin /= -Math.Max(MIN_SIZE_RATIO, aMax);
					else
						oMin /= Math.Max(MIN_SIZE_RATIO, 1.0f - aMax);
					if (oMax < 0.0f)
						oMax /= -Math.Max(MIN_SIZE_RATIO, aMax);
					else
						oMax /= Math.Max(MIN_SIZE_RATIO, 1.0f - aMax);
					height = Math.Max(oMin, oMax);
				}
				if (height > maxHeight) maxHeight = height;
			}
			return maxHeight;
		}

		/// <summary>
		/// Resolves a component reference if needed.
		/// </summary>
		/// <param name="edge">The edge to resolve.</param>
		/// <param name="lookup">The location where the component can be looked up.</param>
		/// <returns>The linked parameters for that edge if needed.</returns>
		private static RelativeLayoutIP InitResolve(EdgeStatus edge,
				IDictionary<GameObject, RelativeLayoutIP> lookup) {
			RelativeLayoutIP result = null;
			if (edge.Constraint == ConstraintType.ToComponent)
				if (!lookup.TryGetValue(edge.FromComponent, out result))
					edge.Constraint = ConstraintType.Unconstrained;
			return result;
		}

		/// <summary>
		/// Locks both edges if they are constrained to the same anchor.
		/// </summary>
		/// <param name="edge">The edge to check.</param>
		/// <param name="otherEdge">The other edge to check.</param>
		/// <returns>true if it was able to lock, or false otherwise.</returns>
		private static bool LockEdgeAnchor(EdgeStatus edge, EdgeStatus otherEdge) {
			bool useDelta = edge.Constraint == ConstraintType.ToAnchor && otherEdge.
				Constraint == ConstraintType.ToAnchor && edge.FromAnchor == otherEdge.
				FromAnchor;
			if (useDelta) {
				edge.Constraint = ConstraintType.Locked;
				otherEdge.Constraint = ConstraintType.Locked;
				edge.Offset = 0.0f;
				otherEdge.Offset = 0.0f;
			}
			return useDelta;
		}

		/// <summary>
		/// Locks an edge if it is constrained to an anchor.
		/// </summary>
		/// <param name="edge">The edge to check.</param>
		private static void LockEdgeAnchor(EdgeStatus edge) {
			if (edge.Constraint == ConstraintType.ToAnchor) {
				edge.Constraint = ConstraintType.Locked;
				edge.Offset = 0.0f;
			}
		}

		/// <summary>
		/// Locks an edge if it can be determined from another component.
		/// </summary>
		/// <param name="edge">The edge to check.</param>
		/// <param name="offset">The component's offset in that direction.</param>
		/// <param name="otherEdge">The opposing edge of the referenced component.</param>
		private static void LockEdgeComponent(EdgeStatus edge, EdgeStatus otherEdge) {
			if (edge.Constraint == ConstraintType.ToComponent && otherEdge.Locked) {
				edge.Constraint = ConstraintType.Locked;
				edge.FromAnchor = otherEdge.FromAnchor;
				edge.Offset = otherEdge.Offset;
			}
		}

		/// <summary>
		/// Locks an edge if it can be determined from the other edge.
		/// </summary>
		/// <param name="edge">The edge to check.</param>
		/// <param name="size">The component's effective size in that direction.</param>
		/// <param name="opposing">The component's other edge.</param>
		private static void LockEdgeRelative(EdgeStatus edge, float size, EdgeStatus opposing)
		{
			if (edge.Constraint == ConstraintType.Unconstrained) {
				if (opposing.Locked) {
					edge.Constraint = ConstraintType.Locked;
					edge.FromAnchor = opposing.FromAnchor;
					edge.Offset = opposing.Offset + size;
				} else if (opposing.Constraint == ConstraintType.Unconstrained) {
					// Both unconstrained, full size
					edge.Constraint = ConstraintType.Locked;
					edge.FromAnchor = 0.0f;
					edge.Offset = 0.0f;
					opposing.Constraint = ConstraintType.Locked;
					opposing.FromAnchor = 1.0f;
					opposing.Offset = 0.0f;
				}
			}
		}

		/// <summary>
		/// Runs a layout pass in the X direction, resolving edges that can be resolved.
		/// </summary>
		/// <param name="children">The children to resolve.</param>
		/// <returns>true if all children have all X edges constrained, or false otherwise.</returns>
		internal static bool RunPassX(this IEnumerable<RelativeLayoutIP> children) {
			bool done = true;
			foreach (var child in children) {
				float width = child.EffectiveWidth;
				EdgeStatus l = child.LeftEdge, r = child.RightEdge;
				if (LockEdgeAnchor(l, r))
					child.UseSizeDeltaX = true;
				LockEdgeAnchor(l);
				LockEdgeAnchor(r);
				LockEdgeRelative(l, -width, r);
				LockEdgeRelative(r, width, l);
				// Lock to other components
				if (child.LeftParams != null)
					LockEdgeComponent(l, child.LeftParams.RightEdge);
				if (child.RightParams != null)
					LockEdgeComponent(r, child.RightParams.LeftEdge);
				if (!l.Locked || !r.Locked) done = false;
			}
			return done;
		}

		/// <summary>
		/// Runs a layout pass in the Y direction, resolving edges that can be resolved.
		/// </summary>
		/// <param name="children">The children to resolve.</param>
		/// <returns>true if all children have all Y edges constrained, or false otherwise.</returns>
		internal static bool RunPassY(this IEnumerable<RelativeLayoutIP> children) {
			bool done = true;
			foreach (var child in children) {
				float height = child.EffectiveHeight;
				EdgeStatus t = child.TopEdge, b = child.BottomEdge;
				if (LockEdgeAnchor(t, b))
					child.UseSizeDeltaY = true;
				LockEdgeAnchor(b);
				LockEdgeAnchor(t);
				LockEdgeRelative(b, -height, t);
				LockEdgeRelative(t, height, b);
				// Lock to other components
				if (child.BottomParams != null)
					LockEdgeComponent(b, child.BottomParams.TopEdge);
				if (child.TopParams != null)
					LockEdgeComponent(t, child.TopParams.BottomEdge);
				if (!t.Locked || !b.Locked) done = false;
			}
			return done;
		}

		/// <summary>
		/// Throws an error when resolution fails.
		/// </summary>
		/// <param name="children">The children, some of which failed to resolve.</param>
		/// <param name="limit">The number of passes executed before failing.</param>
		/// <param name="direction">The direction that failed.</param>
		internal static void ThrowUnresolvable(this IEnumerable<RelativeLayoutIP> children,
				int limit, PanelDirection direction) {
			var message = new StringBuilder(256);
			message.Append("After ").Append(limit);
			message.Append(" passes, unable to complete resolution of RelativeLayout:");
			foreach (var child in children) {
				string name = child.Transform.gameObject.name;
				if (direction == PanelDirection.Horizontal) {
					if (!child.LeftEdge.Locked) {
						message.Append(' ');
						message.Append(name);
						message.Append(".Left");
					}
					if (!child.RightEdge.Locked) {
						message.Append(' ');
						message.Append(name);
						message.Append(".Right");
					}
				} else {
					if (!child.BottomEdge.Locked) {
						message.Append(' ');
						message.Append(name);
						message.Append(".Bottom");
					}
					if (!child.TopEdge.Locked) {
						message.Append(' ');
						message.Append(name);
						message.Append(".Top");
					}
				}
			}
			throw new InvalidOperationException(message.ToString());
		}
	}
}
