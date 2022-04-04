/*
 * Copyright 2022 Peter Han
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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to DecorProvider to not allocate 4 KB of memory in the constructor that will
	/// almost never be used.
	/// 
	/// If Decor Reimagined is installed, it ignores the array anyways.
	/// </summary>
	[HarmonyPatch(typeof(DecorProvider), MethodType.Constructor)]
	public static class DecorProvider_Constructor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Transpiles the constructor to resize the array to 26 slots by default.
		/// Most decor providers have 1 or 2 radius which is 1 and 9 tiles respectively,
		/// 26 handles up to 3 without a resize.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceConstant(instructions, 512, 26, true);
		}
	}

	/// <summary>
	/// Applied to DecorProvider.Splat to resize the array to be bigger if necessary.
	/// 
	/// If Decor Reimagined is installed, it already replaced this struct with another one.
	/// </summary>
	[HarmonyPatch(typeof(DecorProvider.Splat), nameof(DecorProvider.Splat.AddDecor))]
	public static class DecorProvider_Splat_AddDecor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before AddDecor runs.
		/// </summary>
		internal static bool Prefix(ref DecorProvider.Splat __instance) {
			int cell = Grid.PosToCell(__instance.provider);
			float decor = __instance.decor;
			var extents = __instance.extents;
			var provider = __instance.provider;
			int[] cells = provider.cells;
			// VisibilityTest works in absolute coords
			int xMin = extents.x, yMin = extents.y, xMax = Math.Min(Grid.WidthInCells, xMin +
				extents.width), yMax = Math.Min(Grid.HeightInCells, yMin + extents.height);
			int count = provider.cellCount, worst, n = cells.Length;
			if (xMin < 0)
				xMin = 0;
			if (yMin < 0)
				yMin = 0;
			Grid.CellToXY(cell, out int centerX, out int centerY);
			// Resize the provider array if it is too small
			worst = (xMax - xMin + 1) * (yMax - yMin + 1);
			if (n < worst) {
				provider.cells = cells = new int[worst];
				n = worst;
			}
			for (int x = xMin; x < xMax; x++)
				for (int y = yMin; y < yMax; y++) {
					cell = Grid.XYToCell(x, y);
					if (Grid.IsValidCell(cell) && Grid.VisibilityTest(centerX, centerY, x, y))
					{
						// Add to grid
						Grid.Decor[cell] += decor;
						// Add to cell list
						if (count >= 0 && count < n)
							cells[count++] = cell;
					}
				}
			provider.cellCount = count;
			return false;
		}
	}
}
