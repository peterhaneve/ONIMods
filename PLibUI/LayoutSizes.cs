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

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A class representing the size sets of a particular component.
	/// </summary>
	internal struct LayoutSizes {
		/// <summary>
		/// The flexible dimension value.
		/// </summary>
		public float flexible;

		/// <summary>
		/// If true, this component should be ignored completely.
		/// </summary>
		public bool ignore;

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

		internal LayoutSizes(GameObject source) : this(source, 0.0f, 0.0f, 0.0f) { }

		internal LayoutSizes(GameObject source, float min, float preferred,
				float flexible) {
			ignore = false;
			this.source = source;
			this.flexible = flexible;
			this.min = min;
			this.preferred = preferred;
		}

		/// <summary>
		/// Adds another set of layout sizes to this one.
		/// </summary>
		/// <param name="other">The size values to add.</param>
		public void Add(LayoutSizes other) {
			flexible += other.flexible;
			min += other.min;
			preferred += other.preferred;
		}

		/// <summary>
		/// Enlarges this layout size, if necessary, using the values from another.
		/// </summary>
		/// <param name="other">The minimum size values to enforce.</param>
		public void Max(LayoutSizes other) {
			flexible = Math.Max(flexible, other.flexible);
			min = Math.Max(min, other.min);
			preferred = Math.Max(preferred, other.preferred);
		}

		public override string ToString() {
			return string.Format("LayoutSizes[min={0:F2},preferred={1:F2},flexible={2:F2}]",
				min, preferred, flexible);
		}
	}
}
