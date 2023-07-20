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

using Database;
using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A component added to Game to track the status of a few slow achievements.
	/// 
	/// AutomateABuilding will be achieved quickly enough once a few networks are built
	/// that it is not worth
	/// ActivateLorePOI does not hit enough POIs to be worth
	/// Nobody spams low quality paintings, reactors, monument parts
	/// UpgradeAllBasicBuildings is not slow enough to matter
	/// VentXKG generally completes quickly once there are enough vents for it to matter
	/// BuildALaunchPad will complete before the number of pads gets big enough to matter
	/// 
	/// You should totally play on SNDST-C-495240352-9WS
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class AchievementPatches : KMonoBehaviour {
		/// <summary>
		/// Returns true if the achievement optimization patches should be added.
		/// </summary>
		internal static bool ShouldRun() {
			var options = FastTrackOptions.Instance;
			return options.MiscOpts && options.DisableAchievements != FastTrackOptions.
				AchievementDisable.Always;
		}

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static AchievementPatches Instance { get; private set; }

		/// <summary>
		/// The achievements that have run their check at least once. The slow check needs to
		/// happen one time to catch state from load.
		/// </summary>
		private readonly ISet<string> achievementsRun;

		/// <summary>
		/// Whether a building was built outside of the starting biome this session.
		/// </summary>
		internal bool builtOutside;

		/// <summary>
		/// The prefab IDs of each critter that has hatched this session.
		/// </summary>
		internal readonly ISet<Tag> crittersHatched;

		/// <summary>
		/// The foods to target for the EatXKcalProducedByY achievement.
		/// </summary>
		internal readonly List<string> targetFoods;

		/// <summary>
		/// Stores the computed starting world extents.
		/// </summary>
		internal Extents startWorldExtents;

		/// <summary>
		/// The total number of cells on the start world.
		/// </summary>
		internal int targetTiles;

		/// <summary>
		/// The number of cells revealed on the start world.
		/// </summary>
		internal int tilesRevealed;

		/// <summary>
		/// The number of times "Tune Up" was completed this session.
		/// </summary>
		internal int tuneUps;

		internal AchievementPatches() {
			achievementsRun = new HashSet<string>();
			builtOutside = false;
			crittersHatched = new HashSet<Tag>();
			startWorldExtents = new Extents(0, 0, Grid.WidthInCells, Grid.HeightInCells);
			targetFoods = new List<string>();
			targetTiles = 1;
			tilesRevealed = 0;
			tuneUps = 0;
		}

		/// <summary>
		/// Drops the pointer to the singleton instance if it exists.
		/// </summary>
		internal static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// Checks to see if an achievement has run before this session.
		/// </summary>
		/// <param name="achievement">The achievement ID or name to check.</param>
		/// <returns>true if it ran before, or false otherwise.</returns>
		internal bool IsFirstTime(string achievement) {
			return achievementsRun.Add(achievement);
		}

		public override void OnCleanUp() {
			achievementsRun.Clear();
			crittersHatched.Clear();
			DestroyInstance();
			base.OnCleanUp();
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			achievementsRun.Clear();
			targetFoods.Clear();
			var foodList = new HashSet<string>();
			var foodProducers = new List<Tag>(4);
			// Use the default "It's Not Raw" achievement
			var achieve = Db.Get().ColonyAchievements.EatCookedFood;
			if (achieve != null)
				foreach (var requirement in achieve.requirementChecklist)
					if (requirement is EatXKCalProducedByY eatIt)
						foodProducers.AddRange(eatIt.foodProducers);
			foreach (var recipe in ComplexRecipeManager.Get().recipes)
				foreach (var fabricator in recipe.fabricators)
					// Only 2 elements!
					if (foodProducers.Contains(fabricator))
						foodList.Add(recipe.FirstResult.ToString());
			targetFoods.AddRange(foodList);
			foodList.Clear();
#if DEBUG
			PUtil.LogDebug("Foods allowed for It's Not Raw: " + targetFoods.Join(", "));
#endif
			Instance = this;
		}

		public override void OnSpawn() {
			base.OnSpawn();
			// Check the start world
			var inst = ClusterManager.Instance;
			WorldContainer startWorld;
			if (inst != null && (startWorld = inst.GetStartWorld()) != null) {
				Vector2 min = startWorld.minimumBounds, max = startWorld.maximumBounds;
				int xMin = Mathf.RoundToInt(min.x), xMax = Mathf.RoundToInt(max.x),
					yMin = Mathf.RoundToInt(min.y), yMax = Mathf.RoundToInt(max.y),
					revealed = 0;
				int width = xMax - xMin + 1, height = yMax - yMin + 1;
				startWorldExtents.x = xMin;
				startWorldExtents.y = yMin;
				startWorldExtents.width = width;
				startWorldExtents.height = height;
				targetTiles = Math.Max(1, width * height);
				for (int x = xMin; x <= xMax; x++)
					for (int y = yMin; y <= yMax; y++)
						if (Grid.Visible[Grid.XYToCell(x, y)] > 0)
							revealed++;
				tilesRevealed = revealed;
#if DEBUG
				PUtil.LogDebug("{0:D} tiles revealed out of {1:D}".F(tilesRevealed,
					targetTiles));
#endif
			} else
				PUtil.LogWarning("Unable to find starting world!");
		}
	}

	/// <summary>
	/// Applied to AtLeastOneBuildingForEachDupe to fix a bug in it and make it faster.
	/// </summary>
	[HarmonyPatch(typeof(AtLeastOneBuildingForEachDupe), nameof(AtLeastOneBuildingForEachDupe.
		Success))]
	public static class AtLeastOneBuildingForEachDupe_Success_Patch {
		private static readonly Tag OUTHOUSE_TAG = new Tag(OuthouseConfig.ID);

		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(AtLeastOneBuildingForEachDupe __instance,
				ref bool __result) {
			int dupeCount = Components.LiveMinionIdentities.Items.Count;
			bool success = false;
			// You need at least one Duplicant
			if (dupeCount > 0) {
				var items = Components.BasicBuildings.Items;
				int buildingCount = 0, n = items.Count;
				var validTypes = __instance.validBuildingTypes;
				// For the toilet achievement, there need be only one
				if (validTypes.Contains(OUTHOUSE_TAG))
					dupeCount = 1;
				for (int i = 0; i < n; i++)
					// The building types list is only ever 2 elements long
					if (items[i].transform.TryGetComponent(out KPrefabID id) && validTypes.
							Contains(id.PrefabTag)) {
						buildingCount++;
						if (buildingCount >= dupeCount) {
							success = true;
							break;
						}
					}
			}
			__result = success;
			return false;
		}
	}

	/// <summary>
	/// Applied to BuildingComplete to check if a building was built outside the start biome.
	/// </summary>
	[HarmonyPatch(typeof(BuildingComplete), nameof(BuildingComplete.OnSpawn))]
	public static class BuildingComplete_OnSpawn_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(BuildingComplete __instance) {
			var inst = AchievementPatches.Instance;
			if (!__instance.prefabid.HasTag(GameTags.TemplateBuilding) && inst != null &&
					!inst.builtOutside) {
				var cells = SaveLoader.Instance.clusterDetailSave.overworldCells;
				int n = cells.Count;
				for (int i = 0; i < n; i++) {
					var cell = cells[i];
					var tags = cell.tags;
					// Is it a non-start world biome hex that has the building inside it?
					if (tags != null && !tags.Contains(ProcGen.WorldGenTags.StartWorld) &&
							cell.poly.PointInPolygon(__instance.transform.position)) {
						inst.builtOutside = true;
						break;
					}
				}
			}
		}
	}

	/// <summary>
	/// Applied to BuildOutsideStartBiome to not have it iterate every building every check.
	/// </summary>
	[HarmonyPatch(typeof(BuildOutsideStartBiome), nameof(BuildOutsideStartBiome.Success))]
	public static class BuildOutsideStartBiome_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref bool __result) {
			var inst = AchievementPatches.Instance;
			bool cont = inst == null || inst.IsFirstTime(nameof(BuildOutsideStartBiome));
			if (!cont) {
				// Retro checked all the previous buildings
				bool outside = inst.builtOutside;
				if (outside)
					// This is probably dead but for compatibility...
					Game.Instance.unlocks.Unlock("buildoutsidestartingbiome");
				__result = outside;
			}
			return cont;
		}
	}

	/// <summary>
	/// Applied to ColonyAchievementTracker to replace a silly linear search with a proper
	/// dictionary lookup.
	/// </summary>
	[HarmonyPatch(typeof(ColonyAchievementTracker), nameof(ColonyAchievementTracker.
		IsAchievementUnlocked))]
	public static class ColonyAchievementTracker_IsAchievementUnlocked_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before IsAchievementUnlocked runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ColonyAchievementTracker __instance, ref bool __result,
				ColonyAchievement achievement) {
			bool achieved = false;
			if (__instance.achievements.TryGetValue(achievement.Id, out var status)) {
				achieved = status.success;
				if (!achieved) {
					status.UpdateAchievement();
					achieved = status.success;
				}
			}
			__result = achieved;
			return false;
		}
	}

	/// <summary>
	/// Applied to CritterTypeExists to not iterate every critter on the map to check it.
	/// </summary>
	[HarmonyPatch(typeof(CritterTypeExists), nameof(CritterTypeExists.Success))]
	internal static class CritterTypeExists_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(CritterTypeExists __instance, ref bool __result) {
			var inst = AchievementPatches.Instance;
			var types = __instance.critterTypes;
			bool cont = inst == null || types == null;
			if (!cont) {
				var hatched = inst.crittersHatched;
				int n = types.Count;
				__result = false;
				for (int i = 0; i < n; i++)
					if (hatched.Contains(types[i])) {
						__result = true;
						break;
					}
			}
			return cont;
		}
	}

	/// <summary>
	/// Applied to EatXKCalProducedByY to avoid re-indexing the list of recipes every call.
	/// </summary>
	[HarmonyPatch(typeof(EatXKCalProducedByY), nameof(EatXKCalProducedByY.Success))]
	public static class EatXKCalProducedByY_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref bool __result, EatXKCalProducedByY __instance) {
			var inst = AchievementPatches.Instance;
			// If this is the not-raw achievement
			bool cont = inst == null || !Db.Get().ColonyAchievements.EatCookedFood.
				requirementChecklist.Contains(__instance);
			if (!cont)
				__result = RationTracker.Get().GetCaloiresConsumedByFood(inst.targetFoods) *
					0.001f > __instance.numCalories;
			return cont;
		}
	}

	/// <summary>
	/// Applied to Grid to track revealed cells much more intelligently.
	/// </summary>
	[HarmonyPatch(typeof(Grid), nameof(Grid.Reveal))]
	public static class Grid_Reveal_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Reveal runs.
		/// </summary>
		internal static void Prefix(int cell, byte visibility) {
			var inst = AchievementPatches.Instance;
			if (Grid.Visible[cell] == 0 && visibility > 0 && inst != null && !Grid.
					PreventFogOfWarReveal[cell] && inst.startWorldExtents.Contains(Grid.
					CellToXY(cell)))
				inst.tilesRevealed++;
		}
	}

	/// <summary>
	/// Applied to Navigator to track the prefab IDs of anything that hatches or is spawned
	/// into the world.
	/// </summary>
	[HarmonyPatch(typeof(Navigator), nameof(Navigator.OnSpawn))]
	public static class Navigator_OnSpawn_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(Navigator __instance) {
			var instance = AchievementPatches.Instance;
			var tag = __instance.PrefabID();
			if (tag != GameTags.Minion && __instance.sceneLayer == Grid.SceneLayer.
					Creatures && instance != null)
				instance.crittersHatched.Add(tag);
		}
	}

	/// <summary>
	/// Applied to ProduceXEngeryWithoutUsingYList to convert an O(n^2) algorithm to O(n)...
	/// </summary>
	[HarmonyPatch(typeof(ProduceXEngeryWithoutUsingYList), nameof(
		ProduceXEngeryWithoutUsingYList.Success))]
	public static class ProduceXEngeryWithoutUsingYList_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ProduceXEngeryWithoutUsingYList __instance,
				ref bool __result) {
			float total = 0f;
			var disallow = HashSetPool<Tag, ProduceXEngeryWithoutUsingYList>.Allocate();
			var dbl = __instance.disallowedBuildings;
			int n = dbl.Count;
			// Set is faster than a List here, as the test is expected to fail most times if
			// this achievement is still achievable
			for (int i = 0; i < n; i++)
				disallow.Add(dbl[i]);
			foreach (var pair in Game.Instance.savedInfo.powerCreatedbyGeneratorType)
				if (!disallow.Contains(pair.Key))
					total += pair.Value;
			disallow.Recycle();
			__result = total * 0.001f > __instance.amountToProduce;
			return false;
		}
	}

	/// <summary>
	/// Applied to RevealAsteriod to not scan the grid every check... The typo is intentional!
	/// </summary>
	[HarmonyPatch(typeof(RevealAsteriod), nameof(RevealAsteriod.Success))]
	public static class RevealAsteriod_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref bool __result, RevealAsteriod __instance) {
			var inst = AchievementPatches.Instance;
			bool cont = inst == null;
			if (!cont) {
				float reveal = (float)inst.tilesRevealed / inst.targetTiles;
				__instance.amountRevealed = reveal;
				__result = reveal >= __instance.percentToReveal;
			}
			return cont;
		}
	}

	/// <summary>
	/// Applied to Tinkerable to record a generator tune-up on completion.
	/// </summary>
	[HarmonyPatch(typeof(Tinkerable), nameof(Tinkerable.OnCompleteWork))]
	public static class Tinkerable_OnCompleteWork_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied after OnCompleteWork runs.
		/// </summary>
		internal static void Postfix(Tinkerable __instance) {
			var inst = AchievementPatches.Instance;
			if (__instance.tinkerMaterialTag == PowerStationToolsConfig.tag && inst != null)
				inst.tuneUps++;
		}
	}

	/// <summary>
	/// Applied to TuneUpGenerator to not iterate every colony report every check.
	/// </summary>
	[HarmonyPatch(typeof(TuneUpGenerator), nameof(TuneUpGenerator.Success))]
	public static class TuneUpGenerator_Success_Patch {
		internal static bool Prepare() => AchievementPatches.ShouldRun();

		/// <summary>
		/// Applied before Success runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref bool __result, TuneUpGenerator __instance) {
			var inst = AchievementPatches.Instance;
			bool cont = inst == null || inst.IsFirstTime(nameof(TuneUpGenerator));
			if (!cont) {
				// ___choresCompleted is now initialized
				int tuneUps = Mathf.RoundToInt(__instance.choresCompleted), last = inst.
					tuneUps;
				if (last > tuneUps)
					tuneUps = last;
				// Take the highest
				inst.tuneUps = tuneUps;
				__instance.choresCompleted = tuneUps;
				__result = tuneUps >= __instance.numChoreseToComplete;
			}
			return cont;
		}
	}
}
