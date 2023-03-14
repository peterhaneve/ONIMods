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

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using EdgeStatus = PeterHan.PLib.UI.Layouts.RelativeLayoutParams.EdgeStatus;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// A helper class for RelativeLayout.
	/// </summary>
	internal static class RelativeLayoutUtil {
		/// <summary>
		/// Initializes and computes horizontal sizes for the components in this relative
		/// layout.
		/// </summary>
		/// <param name="children">The location to store information about these components.</param>
		/// <param name="all">The components to lay out.</param>
		/// <param name="constraints">The constraints defined for these components.</param>
		internal static void CalcX(this ICollection<RelativeLayoutResults> children,
				RectTransform all, IDictionary<GameObject, RelativeLayoutParams> constraints) {
			var comps = ListPool<Component, RelativeLayoutGroup>.Allocate();
			var paramMap = DictionaryPool<GameObject, RelativeLayoutResults,
				RelativeLayoutGroup>.Allocate();
			int n = all.childCount;
			children.Clear();
			for (int i = 0; i < n; i++) {
				var child = all.GetChild(i)?.gameObject;
				if (child != null) {
					comps.Clear();
					// Calculate the preferred size using all layout components
					child.GetComponents(comps);
					var horiz = PUIUtils.CalcSizes(child, PanelDirection.Horizontal, comps);
					if (!horiz.ignore) {
						RelativeLayoutResults ip;
						float w = horiz.preferred;
						if (constraints.TryGetValue(child, out RelativeLayoutParams cons)) {
							ip = new RelativeLayoutResults(child.rectTransform(), cons);
							// Set override size by axis if necessary
							var overrideSize = ip.OverrideSize;
							if (overrideSize.x > 0.0f)
								w = overrideSize.x;
							paramMap[child] = ip;
						} else
							// Default its layout to fill all
							ip = new RelativeLayoutResults(child.rectTransform(), null);
						ip.PreferredWidth = w;
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
		internal static void CalcY(this ICollection<RelativeLayoutResults> children) {
			var comps = ListPool<Component, RelativeLayoutGroup>.Allocate();
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
		/// Calculates the minimum size the component must be to support a specific child
		/// component.
		/// </summary>
		/// <param name="min">The lower edge constraint.</param>
		/// <param name="max">The upper edge constraint.</param>
		/// <param name="effective">The component size in that dimension plus margins.</param>
		/// <returns>The minimum parent component size to fit the child.</returns>
		internal static float ElbowRoom(EdgeStatus min, EdgeStatus max, float effective) {
			float aMin = min.FromAnchor, aMax = max.FromAnchor, result, offMin = min.Offset,
				offMax = max.Offset;
			if (aMax > aMin)
				// "Elbow room" method
				result = (effective + offMin - offMax) / (aMax - aMin);
			else
				// Anchors are together
				result = Math.Max(effective, Math.Max(Math.Abs(offMin), Math.Abs(offMax)));
			return result;
		}

		/// <summary>
		/// Executes the horizontal layout.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <param name="scratch">The location where components will be temporarily stored.</param>
		/// <param name="mLeft">The left margin.</param>
		/// <param name="mRight">The right margin.</param>
		internal static void ExecuteX(this IEnumerable<RelativeLayoutResults> children,
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
		internal static void ExecuteY(this IEnumerable<RelativeLayoutResults> children,
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
		internal static float GetMinSizeX(this IEnumerable<RelativeLayoutResults> children) {
			float maxWidth = 0.0f;
			foreach (var child in children) {
				float width = ElbowRoom(child.LeftEdge, child.RightEdge, child.EffectiveWidth);
				if (width > maxWidth) maxWidth = width;
			}
			return maxWidth;
		}

		/// <summary>
		/// Calculates the minimum size in the Y direction.
		/// </summary>
		/// <param name="children">The components to lay out.</param>
		/// <returns>The minimum vertical size.</returns>
		internal static float GetMinSizeY(this IEnumerable<RelativeLayoutResults> children) {
			float maxHeight = 0.0f;
			foreach (var child in children) {
				float height = ElbowRoom(child.BottomEdge, child.TopEdge, child.
					EffectiveHeight);
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
		private static RelativeLayoutResults InitResolve(EdgeStatus edge,
				IDictionary<GameObject, RelativeLayoutResults> lookup) {
			RelativeLayoutResults result = null;
			if (edge.Constraint == RelativeConstraintType.ToComponent)
				if (!lookup.TryGetValue(edge.FromComponent, out result))
					edge.Constraint = RelativeConstraintType.Unconstrained;
			return result;
		}

		/// <summary>
		/// Locks both edges if they are constrained to the same anchor.
		/// </summary>
		/// <param name="edge">The edge to check.</param>
		/// <param name="otherEdge">The other edge to check.</param>
		/// <returns>true if it was able to lock, or false otherwise.</returns>
		private static bool LockEdgeAnchor(EdgeStatus edge, EdgeStatus otherEdge) {
			bool useDelta = edge.Constraint == RelativeConstraintType.ToAnchor && otherEdge.
				Constraint == RelativeConstraintType.ToAnchor && edge.FromAnchor == otherEdge.
				FromAnchor;
			if (useDelta) {
				edge.Constraint = RelativeConstraintType.Locked;
				otherEdge.Constraint = RelativeConstraintType.Locked;
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
			if (edge.Constraint == RelativeConstraintType.ToAnchor) {
				edge.Constraint = RelativeConstraintType.Locked;
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
			if (edge.Constraint == RelativeConstraintType.ToComponent && otherEdge.Locked) {
				edge.Constraint = RelativeConstraintType.Locked;
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
			if (edge.Constraint == RelativeConstraintType.Unconstrained) {
				if (opposing.Locked) {
					edge.Constraint = RelativeConstraintType.Locked;
					edge.FromAnchor = opposing.FromAnchor;
					edge.Offset = opposing.Offset + size;
				} else if (opposing.Constraint == RelativeConstraintType.Unconstrained) {
					// Both unconstrained, full size
					edge.Constraint = RelativeConstraintType.Locked;
					edge.FromAnchor = 0.0f;
					edge.Offset = 0.0f;
					opposing.Constraint = RelativeConstraintType.Locked;
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
		internal static bool RunPassX(this IEnumerable<RelativeLayoutResults> children) {
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
		internal static bool RunPassY(this IEnumerable<RelativeLayoutResults> children) {
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
		internal static void ThrowUnresolvable(this IEnumerable<RelativeLayoutResults> children,
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
