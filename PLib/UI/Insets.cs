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

namespace PeterHan.PLib {
	/// <summary>
	/// Used to initialize a GUI component's insets from its parent.
	/// </summary>
	public sealed class Insets {
		/// <summary>
		/// The left inset.
		/// </summary>
		public float Left { get; set; }

		/// <summary>
		/// The right inset.
		/// </summary>
		public float Right { get; set; }

		/// <summary>
		/// The top inset.
		/// </summary>
		public float Top { get; set; }

		/// <summary>
		/// The bottom inset.
		/// </summary>
		public float Bottom { get; set; }

		public Insets() : this(0.0f, 0.0f, 0.0f, 0.0f) {
		}

		public Insets(float left, float right, float top, float bottom) {
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
		}

		/// <summary>
		/// Retrieves the minimum offset.
		/// </summary>
		/// <returns>The offsets of the top left corner.</returns>
		public Vector2f GetOffsetMin() {
			return new Vector2f(Left, Top);
		}

		/// <summary>
		/// Retrieves the maximum offset.
		/// </summary>
		/// <returns>The offsets of the bottom right corner.</returns>
		public Vector2f GetOffsetMax() {
			return new Vector2f(Right, Bottom);
		}

		public override string ToString() {
			return "Insets[Left={0:F2},Top={1:F2},Right={2:F2},Bottom={3:F2}]".F(Left, Top,
				Right, Bottom);
		}
	}
}
