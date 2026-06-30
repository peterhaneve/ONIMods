/*
 * Copyright 2026 Peter Han
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

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Pure cell/grid-window geometry used by the path cache's dirty-cell invalidation.
	/// Deliberately has no UnityEngine/Grid/game-type dependency so it can be unit tested
	/// outside the game assembly.
	/// </summary>
	public static class PathCacheGeometry {
		/// <summary>
		/// Checks whether cell (cx, cy) lies inside the grid window rooted at
		/// (rootX, rootY) with the given width/height. Matches the half-open window
		/// convention PathCacher itself uses for a bounded (applyOffset) PathGrid:
		/// the window covers [rootX, rootX + width) x [rootY, rootY + height), the same
		/// convention used by PathCacher.CheckCache's center calculation
		/// (XYToCell(rootX + width/2, rootY + height/2)) and by the AABB overlap test
		/// that InvalidateRegion used to perform.
		/// </summary>
		/// <param name="cx">The cell's X coordinate.</param>
		/// <param name="cy">The cell's Y coordinate.</param>
		/// <param name="rootX">The grid window's root (minimum) X coordinate.</param>
		/// <param name="rootY">The grid window's root (minimum) Y coordinate.</param>
		/// <param name="width">The grid window's width in cells.</param>
		/// <param name="height">The grid window's height in cells.</param>
		/// <returns>true if (cx, cy) lies inside the window, or false otherwise.</returns>
		public static bool CellInWindow(int cx, int cy, int rootX, int rootY, int width,
				int height) {
			return cx >= rootX && cx < rootX + width && cy >= rootY && cy < rootY + height;
		}
	}
}
