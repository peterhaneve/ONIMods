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

namespace PeterHan.FastTrack.CritterPatches {
	/// <summary>
	/// A class used to query locations for GasAndLiquidConsumerMonitor to consume cells. This
	/// is way faster and more optimal for target cells that are close by, but a little slower
	/// than the stock version if no matches or the cells will be far away.
	/// </summary>
	internal sealed class ConsumableCellQuery : PathFinderQuery {
		/// <summary>
		/// The best cost found so far.
		/// </summary>
		private int bestCost;

		/// <summary>
		/// Used to query whether the cell is consumable.
		/// </summary>
		private readonly GasAndLiquidConsumerMonitor.Instance smi;

		/// <summary>
		/// The cell to consume found by this query.
		/// </summary>
		public int TargetCell { get; private set; }

		/// <summary>
		/// The element found by this query.
		/// </summary>
		public Element TargetElement { get; private set; }

		public ConsumableCellQuery(GasAndLiquidConsumerMonitor.Instance smi, int maxCost) {
			this.smi = smi ?? throw new ArgumentNullException(nameof(smi));
			TargetCell = Grid.InvalidCell;
			TargetElement = null;
			// IsConsumableCell iterates the diet infos looking for a match, but IsMatch
			// already uses a fast Set, and the only ONI critters with gas and liquid diets
			// eat 1 element apiece
			bestCost = maxCost;
		}

		public override bool IsMatch(int cell, int parent_cell, int cost) {
			int above = Grid.CellAbove(cell);
			// The base game also checks just the cell and above
			if (cost < bestCost && (smi.IsConsumableCell(cell, out Element element) ||
					(Grid.IsValidCell(above) && smi.IsConsumableCell(above, out element)))) {
				bestCost = cost;
				TargetCell = cell;
				TargetElement = element;
			}
			return cost > bestCost;
		}
	}

	/// <summary>
	/// Applied to GasAndLiquidConsumerMonitor.Instance to replace the cell search with a
	/// more sensible version that is faster.
	/// </summary>
	[HarmonyPatch(typeof(GasAndLiquidConsumerMonitor.Instance),
		nameof(GasAndLiquidConsumerMonitor.Instance.FindTargetCell))]
	public static class GasAndLiquidConsumerMonitor_Instance_FindTargetCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CritterConsumers;

		/// <summary>
		/// Applied before FindTargetCell runs.
		/// </summary>
		internal static bool Prefix(GasAndLiquidConsumerMonitor.Instance __instance) {
			// The original query limited to 25 results, pufts have a typical path cost of 2
			// for a move and slicksters 1, but pufts can go 4 directions while slicksters only
			// 2. Go with a 15 cost limit which is 7 tiles (pufts) or 15 tiles (slicksters).
			var query = new ConsumableCellQuery(__instance, 15);
			__instance.navigator.RunQuery(query);
			int targetCell = query.TargetCell;
			if (Grid.IsValidCell(targetCell)) {
				__instance.targetCell = targetCell;
				__instance.targetElement = query.TargetElement;
			}
			// Stop the slow original from running
			return false;
		}
	}

	/// <summary>
	/// Applied to GasAndLiquidConsumerMonitor to reduce food search frequency to 4 seconds.
	/// See comment on SolidConsumerMonitor patch for why we believe that this will not cause
	/// critters to prematurely starve.
	/// </summary>
	[HarmonyPatch(typeof(GasAndLiquidConsumerMonitor), nameof(GasAndLiquidConsumerMonitor.
		InitializeStates))]
	public static class GasAndLiquidConsumerMonitor_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CritterConsumers;

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		internal static void Postfix(GasAndLiquidConsumerMonitor __instance) {
			var lookingForFood = __instance.looking;
			var actions = lookingForFood?.updateActions;
			if (actions != null)
				lookingForFood.Enter(smi => smi.FindTargetCell());
		}

		/// <summary>
		/// Transpiles InitializeStates to convert the 1000ms to 4000ms. Note that postfixing
		/// and swapping is not enough, Update mutates the buckets for the singleton state
		/// machine updater, CLAY PLEASE.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceConstant(method, (int)UpdateRate.SIM_1000ms, (int)
				UpdateRate.SIM_4000ms, true);
		}
	}
}
