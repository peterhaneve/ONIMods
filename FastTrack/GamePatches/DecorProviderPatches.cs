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
using System.Reflection;
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
	/// Applied to DecorProvider to reduce the effect of the Tropical Pacu bug by instead of
	/// triggering a full room rebuild, just refreshing the room constraints.
	/// 
	/// If Decor Reimagined is installed, it will override the auto patch, the conditional one
	/// will be used instead.
	/// </summary>
	public static class DecorProviderRefreshFix {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Attempts to also patch the Decor Reimagined implementation of DecorProvider.
		/// Refresh.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void ApplyPatch(Harmony harmony) {
			var patchMethod = new HarmonyMethod(typeof(DecorProviderRefreshFix), nameof(
				Transpiler));
			var targetMethod = PPatchTools.GetTypeSafe(
				"ReimaginationTeam.DecorRework.DecorSplatNew", "DecorReimagined")?.
				GetMethodSafe("RefreshDecor", false, PPatchTools.AnyArguments);
			if (targetMethod != null) {
				PUtil.LogDebug("Patching Decor Reimagined for DecorProvider.RefreshDecor");
				harmony.Patch(targetMethod, transpiler: patchMethod);
			}
			PUtil.LogDebug("Patching DecorProvider.Refresh");
			harmony.Patch(typeof(DecorProvider).GetMethodSafe(nameof(DecorProvider.Refresh),
				false, PPatchTools.AnyArguments), transpiler: patchMethod);
		}

		/// <summary>
		/// Instead of triggering a full solid change of the room, merely retrigger the
		/// conditions.
		/// </summary>
		/// <param name="prober">The current room prober.</param>
		/// <param name="cell">The cell of the room that will be updated.</param>
		private static void SolidNotChangedEvent(RoomProber prober, int cell, bool _) {
			if (prober != null) {
				var cavity = prober.GetCavityForCell(cell);
				if (cavity != null)
					prober.UpdateRoom(cavity);
				// else: If the critter is not currently in any cavity, they will be added
				// when the cavity is created by OvercrowdingMonitor, at which point the room
				// conditions will be evaluated again anyways
			}
		}

		/// <summary>
		/// Transpiles the constructor to resize the array to 26 slots by default.
		/// Most decor providers have 1 or 2 radius which is 1 and 9 tiles respectively,
		/// 26 handles up to 3 without a resize.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(RoomProber).
				GetMethodSafe(nameof(RoomProber.SolidChangedEvent), false, typeof(int),
				typeof(bool)), typeof(DecorProviderRefreshFix).GetMethodSafe(nameof(
				SolidNotChangedEvent), true, typeof(RoomProber), typeof(int), typeof(bool)));
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
