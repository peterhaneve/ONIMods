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
using System.Collections.Generic;
using UnityEngine;

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
		nameof(GasAndLiquidConsumerMonitor.Instance.FindFood))]
	public static class GasAndLiquidConsumerMonitor_Instance_FindFood_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CritterConsumers;

		/// <summary>
		/// Applied before FindFood runs.
		/// </summary>
		internal static bool Prefix(GasAndLiquidConsumerMonitor.Instance __instance) {
			int targetCell;
			// The original query limited to 25 results, pufts have a typical path cost of 2
			// for a move and slicksters 1, but pufts can go 4 directions while slicksters only
			// 2. Go with a 15 cost limit which is 7 tiles (pufts) or 15 tiles (slicksters).
			var query = new ConsumableCellQuery(__instance, 15);
			__instance.navigator.RunQuery(query);
			targetCell = query.TargetCell;
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
	/// critter to prematurely starve.
	/// </summary>
	[HarmonyPatch(typeof(GasAndLiquidConsumerMonitor), nameof(GasAndLiquidConsumerMonitor.
		InitializeStates))]
	public static class GasAndLiquidConsumerMonitor_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CritterConsumers;

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		internal static void Postfix(GasAndLiquidConsumerMonitor __instance) {
			var lookingForFood = __instance.lookingforfood;
			var actions = lookingForFood?.updateActions;
			if (actions != null)
				lookingForFood.Enter((smi) => smi.FindFood());
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

	/// <summary>
	/// A replacement for SolidConsumerMonitor.FindFood which is far more intelligent about
	/// the types of food for which it searches.
	/// </summary>
	internal static class SolidConsumerMonitorFoodFinder {
		/// <summary>
		/// The scannable radius for critter edibles.
		/// </summary>
		private const int RADIUS = 8;

		/// <summary>
		/// Checks to see if the item can be eaten by a critter. This overload assumes that
		/// the item is not a plant.
		/// </summary>
		/// <param name="item">The item to check.</param>
		/// <param name="diet">The critter's diet.</param>
		/// <returns>true if the item is edible, or false otherwise.</returns>
		private static bool CanEatItem(KPrefabID item, Diet diet) {
			return !item.HasAnyTags(ref SolidConsumerMonitor.creatureMask) && diet.GetDietInfo(
				item.PrefabTag) != null;
		}

		/// <summary>
		/// Checks to see if the item can be eaten by a critter.
		/// </summary>
		/// <param name="item">The item to check.</param>
		/// <param name="diet">The critter's diet.</param>
		/// <returns>true if the item is edible, or false otherwise.</returns>
		private static bool CanEatItem(KMonoBehaviour item, Diet diet) {
			bool edible = false;
			if (item.TryGetComponent(out KPrefabID pid) && (edible = CanEatItem(pid, diet)) &&
					pid.HasAnyTags_AssumeLaundered(ref SolidConsumerMonitor.plantMask)) {
				float grown = 0.0f;
				// Trees are special cased in the Klei code
				if (item.TryGetComponent(out BuddingTrunk trunk))
					grown = trunk.GetMaxBranchMaturity();
				else {
					var maturity = Db.Get().Amounts.Maturity.Lookup(item);
					if (maturity != null)
						grown = maturity.value / maturity.GetMax();
				}
				// Could not find this hardcoded constant in the Klei code
				if (grown < 0.25f)
					edible = false;
			}
			return edible;
		}

		/// <summary>
		/// Run on entry to SolidConsumerMonitor.lookingforfood to ensure that the critter
		/// has a tile when they initially become hungry.
		/// </summary>
		public static void FindFood(SolidConsumerMonitor.Instance smi) => FindFood(smi, 0.0f);

		/// <summary>
		/// Replaces SolidConsumerMonitor.FindFood to more efficiently find food for critters.
		/// </summary>
		public static void FindFood(SolidConsumerMonitor.Instance smi, float _) {
			var closest = new ClosestEdible(smi.GetComponent<DrowningMonitor>(),
				smi.GetComponent<Navigator>());
			// Check the diet type first
			var diet = smi.def.diet;
			Grid.PosToXY(smi.gameObject.transform.GetPosition(), out int x, out int y);
			if (!diet.eatsPlantsDirectly) {
				// Check Critter Feeder with priority
				var storages = ListPool<Storage, SolidConsumerMonitor>.Allocate();
				foreach (var creatureFeeder in Components.CreatureFeeders.Items) {
					var go = creatureFeeder.gameObject;
					if (go != null) {
						Grid.PosToXY(go.transform.GetPosition(), out int cx, out int cy);
						// Only check critter feeders that are somewhat nearby
						if (Math.Abs(x - cx) <= RADIUS && Math.Abs(y - cy) <= RADIUS) {
							storages.Clear();
							go.GetComponents(storages);
							FindFood(storages, diet, ref closest);
						}
					}
				}
				storages.Recycle();
			}
			var gsp = GameScenePartitioner.Instance;
			var nearby = ListPool<ScenePartitionerEntry, GameScenePartitioner>.Allocate();
			gsp.GatherEntries(x - RADIUS, y - RADIUS, RADIUS << 1, RADIUS << 1, diet.
				eatsPlantsDirectly ? gsp.plants : gsp.pickupablesLayer, nearby);
			// Add plants or critters
			int n = nearby.Count;
			for (int i = 0; i < n; i++)
				if (nearby[i].obj is KMonoBehaviour item && CanEatItem(item, diet))
					closest.CheckUpdate(item);
			nearby.Recycle();
			smi.targetEdible = closest.target;
		}

		/// <summary>
		/// Searches critter feeder storage for items to eat.
		/// </summary>
		/// <param name="storages">The storages to search for food.</param>
		/// <param name="diet">The foods that can be eaten.</param>
		/// <param name="closest">The location where the closest valid food will be stored.</param>
		private static void FindFood(List<Storage> storages, Diet diet,
				ref ClosestEdible closest) {
			int n = storages.Count;
			for (int i = 0; i < n; i++) {
				var storage = storages[i];
				if (storage != null) {
					var items = storage.items;
					int ni = items.Count;
					for (int j = 0; j < ni; j++) {
						var item = items[j];
						if (item != null && item.TryGetComponent(out KPrefabID prefabID) &&
								CanEatItem(prefabID, diet))
							closest.CheckUpdate(prefabID);
					}
				}
			}
		}

		/// <summary>
		/// Stores the current closest edible food item.
		/// </summary>
		private struct ClosestEdible {
			/// <summary>
			/// The path cost to the item.
			/// </summary>
			public int distance;

			/// <summary>
			/// The drowning monitor to check if the tile may be submerged.
			/// </summary>
			private readonly DrowningMonitor drowning;

			/// <summary>
			/// The navigator which is trying to find food.
			/// </summary>
			private readonly Navigator navigator;

			/// <summary>
			/// The item to eat.
			/// </summary>
			public GameObject target;

			public ClosestEdible(DrowningMonitor drowning, Navigator navigator) {
				distance = int.MaxValue;
				this.drowning = drowning;
				this.navigator = navigator;
				target = null;
			}

			/// <summary>
			/// Checks a potential item and updates the best found if it is closer and
			/// suitable.
			/// </summary>
			/// <param name="item">The item to be eaten.</param>
			public void CheckUpdate(KMonoBehaviour item) {
				var go = item.gameObject;
				// Was already null checked
				int cell = Grid.PosToCell(item.transform.position);
				if (drowning == null || !drowning.canDrownToDeath || drowning.
						livesUnderWater || drowning.IsCellSafe(cell)) {
					// Is it closer?
					int cost = navigator.GetNavigationCost(cell);
					if (cost >= 0 && cost < distance) {
						distance = cost;
						target = go;
					}
				}
			}

			public override bool Equals(object obj) {
				return obj is ClosestEdible other && other.target == target;
			}

			public override int GetHashCode() {
				return target.GetHashCode();
			}

			public override string ToString() {
				return "{0}, {1:D} away".F(target, distance);
			}
		}
	}

	/// <summary>
	/// Applied to SolidConsumerMonitor to reduce food search frequency to 4 seconds. While
	/// this could make critters starve with food nearby, that is mostly due to the brains
	/// not being iterated often enough to begin the eat behavior, not a lack of a target
	/// food item.
	/// </summary>
	[HarmonyPatch(typeof(SolidConsumerMonitor), nameof(SolidConsumerMonitor.InitializeStates))]
	public static class SolidConsumerMonitor_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CritterConsumers;

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		internal static void Postfix(SolidConsumerMonitor __instance) {
			var lookingForFood = __instance.lookingforfood;
			var actions = lookingForFood?.updateActions;
			if (actions != null)
				lookingForFood.Enter(SolidConsumerMonitorFoodFinder.FindFood);
		}

		/// <summary>
		/// Transpiles InitializeStates to convert the 1000ms to 4000ms.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceConstant(method, (int)UpdateRate.SIM_1000ms, (int)
				UpdateRate.SIM_4000ms, true);
		}
	}
}
