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
using UnityEngine;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// A class which stores the results of a single box layout calculation pass.
	/// </summary>
	internal sealed class BoxLayoutResults {
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

		internal BoxLayoutResults(PanelDirection direction, int presize) {
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
			float newMin = sizes.min, newPreferred = sizes.preferred, newFlexible = sizes.
				flexible;
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
	/// Maintains the status of a layout in progress.
	/// </summary>
	internal sealed class BoxLayoutStatus {
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

		internal BoxLayoutStatus(PanelDirection direction, RectOffset margins, float size) {
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
