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

using System.Collections.Generic;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// A class which stores the results of a single card layout calculation pass.
	/// </summary>
	internal sealed class CardLayoutResults {
		/// <summary>
		/// The components which were laid out.
		/// </summary>
		public readonly ICollection<LayoutSizes> children;

		/// <summary>
		/// The current direction of flow.
		/// </summary>
		public readonly PanelDirection direction;

		/// <summary>
		/// The total sizes.
		/// </summary>
		public LayoutSizes total;

		internal CardLayoutResults(PanelDirection direction, int presize) {
			children = new List<LayoutSizes>(presize);
			this.direction = direction;
			total = new LayoutSizes();
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
}
