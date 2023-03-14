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

using UnityEngine;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Extension methods to deal with TextAnchor alignments.
	/// </summary>
	public static class TextAnchorUtils {
		/// <summary>
		/// Returns true if this text alignment is at the left.
		/// </summary>
		/// <param name="anchor">The anchoring to check.</param>
		/// <returns>true if it denotes a Left alignment, or false otherwise.</returns>
		public static bool IsLeft(this TextAnchor anchor) {
			return anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft ||
				anchor == TextAnchor.LowerLeft;
		}

		/// <summary>
		/// Returns true if this text alignment is at the bottom.
		/// </summary>
		/// <param name="anchor">The anchoring to check.</param>
		/// <returns>true if it denotes a Lower alignment, or false otherwise.</returns>
		public static bool IsLower(this TextAnchor anchor) {
			return anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerLeft ||
				anchor == TextAnchor.LowerRight;
		}

		/// <summary>
		/// Returns true if this text alignment is at the right.
		/// </summary>
		/// <param name="anchor">The anchoring to check.</param>
		/// <returns>true if it denotes a Right alignment, or false otherwise.</returns>
		public static bool IsRight(this TextAnchor anchor) {
			return anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight ||
				anchor == TextAnchor.LowerRight;
		}

		/// <summary>
		/// Returns true if this text alignment is at the top.
		/// </summary>
		/// <param name="anchor">The anchoring to check.</param>
		/// <returns>true if it denotes an Upper alignment, or false otherwise.</returns>
		public static bool IsUpper(this TextAnchor anchor) {
			return anchor == TextAnchor.UpperCenter || anchor == TextAnchor.UpperLeft ||
				anchor == TextAnchor.UpperRight;
		}

		/// <summary>
		/// Mirrors a text alignment horizontally. UpperLeft becomes UpperRight, MiddleLeft
		/// becomes MiddleRight, and so forth.
		/// </summary>
		/// <param name="anchor">The anchoring to mirror.</param>
		/// <returns>The horizontally reflected version of that mirror.</returns>
		public static TextAnchor MirrorHorizontal(this TextAnchor anchor) {
			int newAnchor = (int)anchor;
			// UL UC UR ML MC MR LL LC LR
			// Danger will robinson!
			newAnchor = 3 * (newAnchor / 3) + 2 - newAnchor % 3;
			return (TextAnchor)newAnchor;
		}

		/// <summary>
		/// Mirrors a text alignment vertically. UpperLeft becomes LowerLeft, LowerCenter
		/// becomes UpperCenter, and so forth.
		/// </summary>
		/// <param name="anchor">The anchoring to mirror.</param>
		/// <returns>The vertically reflected version of that mirror.</returns>
		public static TextAnchor MirrorVertical(this TextAnchor anchor) {
			int newAnchor = (int)anchor;
			newAnchor = 6 - 3 * (newAnchor / 3) + newAnchor % 3;
			return (TextAnchor)newAnchor;
		}
	}
}
