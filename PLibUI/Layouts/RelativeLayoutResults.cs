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
using UnityEngine;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// Parameters used to store the dynamic data of an object during a relative layout.
	/// </summary>
	internal sealed class RelativeLayoutResults : RelativeLayoutParamsBase<GameObject> {
		/// <summary>
		/// A set of insets that are always zero.
		/// </summary>
		private static readonly RectOffset ZERO = new RectOffset();

		/// <summary>
		/// The instance parameters of the bottom edge's component.
		/// </summary>
		internal RelativeLayoutResults BottomParams { get; set; }

		/// <summary>
		/// The height of the component plus its margin box.
		/// </summary>
		internal float EffectiveHeight { get; private set; }

		/// <summary>
		/// The width of the component plus its margin box.
		/// </summary>
		internal float EffectiveWidth { get; private set; }

		/// <summary>
		/// The instance parameters of the left edge's component.
		/// </summary>
		internal RelativeLayoutResults LeftParams { get; set; }

		/// <summary>
		/// The preferred height at which this component will be laid out, unless both
		/// edges are constrained.
		/// </summary>
		internal float PreferredHeight {
			get {
				return prefSize.y;
			}
			set {
				prefSize.y = value;
				EffectiveHeight = value + Insets.top + Insets.bottom;
			}
		}

		/// <summary>
		/// The preferred width at which this component will be laid out, unless both
		/// edges are constrained.
		/// </summary>
		internal float PreferredWidth {
			get {
				return prefSize.x;
			}
			set {
				prefSize.x = value;
				EffectiveWidth = value + Insets.left + Insets.right;
			}
		}

		/// <summary>
		/// The instance parameters of the right edge's component.
		/// </summary>
		internal RelativeLayoutResults RightParams { get; set; }

		/// <summary>
		/// The instance parameters of the top edge's component.
		/// </summary>
		internal RelativeLayoutResults TopParams { get; set; }

		/// <summary>
		/// The object to lay out.
		/// </summary>
		internal RectTransform Transform { get; set; }

		/// <summary>
		/// Whether the size delta should be used in the X direction (as opposed to offsets).
		/// </summary>
		internal bool UseSizeDeltaX { get; set; }

		/// <summary>
		/// Whether the size delta should be used in the Y direction (as opposed to offsets).
		/// </summary>
		internal bool UseSizeDeltaY { get; set; }

		/// <summary>
		/// The preferred size of this component.
		/// </summary>
		private Vector2 prefSize;

		internal RelativeLayoutResults(RectTransform transform, RelativeLayoutParams other) {
			Transform = transform ?? throw new ArgumentNullException(nameof(transform));
			if (other != null) {
				BottomEdge.CopyFrom(other.BottomEdge);
				TopEdge.CopyFrom(other.TopEdge);
				RightEdge.CopyFrom(other.RightEdge);
				LeftEdge.CopyFrom(other.LeftEdge);
				Insets = other.Insets ?? ZERO;
				OverrideSize = other.OverrideSize;
			} else
				Insets = ZERO;
			BottomParams = LeftParams = TopParams = RightParams = null;
			PreferredWidth = PreferredHeight = 0.0f;
			UseSizeDeltaX = UseSizeDeltaY = false;
		}

		public override string ToString() {
			var go = Transform.gameObject;
			return string.Format("component={0} {1:F2}x{2:F2}", (go == null) ? "null" : go.
				name, prefSize.x, prefSize.y);
		}
	}
}
