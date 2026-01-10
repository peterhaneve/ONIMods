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

using Database;
using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;

using AS = PeterHan.MoreAchievements.AchievementStrings;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Lists all achievements added by this mod.
	/// </summary>
	internal static class Achievements {
		private const string PROPS_PATH = "PeterHan.MoreAchievements.PoiProps.txt";

		/// <summary>
		/// The achievement list.
		/// </summary>
		internal static AD[] AllAchievements { get; private set; }

		/// <summary>
		/// The props eligible for melting.
		/// </summary>
		internal static readonly HashSet<string> POI_PROPS = new HashSet<string>();

		/// <summary>
		/// Creates a "tame critter type" achievement requirement.
		/// </summary>
		/// <param name="ids">The critter tag IDs which must be tamed.</param>
		/// <returns>A requirement which requries taming these critter prefab IDs.</returns>
		private static CritterTypesWithTraits CritterTypeRequirement(params string[] ids) {
			if (ids == null || ids.Length < 1)
				throw new ArgumentException("No critter IDs specified");
			// Convert to tags
			var tags = new List<Tag>(ids.Length);
			foreach (string id in ids)
				tags.Add(TagManager.Create(id));
			return new CritterTypesWithTraits(tags);
		}

		/// <summary>
		/// Initializes the achievement list, after the Db has been initialized.
		/// </summary>
		internal static void InitAchievements() {
			var db = Db.Get();
			var dietRequirements = new ColonyAchievementRequirement[] {
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FieldRationConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MushBarConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FriedMushBarConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BasicPlantFoodConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BasicPlantBarConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, PickledMealConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, PrickleFruitConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, GrilledPrickleFruitConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, SalsaConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, CookedEggConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MeatConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, CookedMeatConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FishMeatConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, CookedFishConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, SurfAndTurfConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, ColdWheatBreadConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, SpiceBreadConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FruitCakeConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MushroomConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FriedMushroomConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MushroomWrapConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, LettuceConfig.ID),
				new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BurgerConfig.ID)
			};
			// If in DLC, require Grubfruit, Spindly Grubfruit, Roast Grubfruit Nut,
			// Grubfruit Preserve, and Mixed Berry Pie
			if (DlcManager.IsExpansion1Active()) {
				var dlcDietRequirements = new ColonyAchievementRequirement[] {
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, WormBasicFruitConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, WormBasicFoodConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, WormSuperFruitConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, WormSuperFoodConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BerryPieConfig.ID),
				};
				int n1 = dietRequirements.Length, n2 = dlcDietRequirements.Length;
				var temp = new ColonyAchievementRequirement[n1 + n2];
				Array.Copy(dietRequirements, temp, n1);
				Array.Copy(dlcDietRequirements, 0, temp, n1, n2);
				dietRequirements = temp;
			}
			AllAchievements = new AD[] {
				new AD("EmpireBuilder", "build_2500", new BuildNBuildings(AS.EMPIREBUILDER.
					QUANTITY)),
				new AD("JohnHenry", "dig_10k", new DigNTiles(AS.JOHNHENRY.QUANTITY)),
				new AD("TestOfTime", "reach_cycle1000", new CycleNumber(AS.TESTOFTIME.CYCLE)),
				new AD("ThinkingAhead", "thinking_ahead", new UseGeneShufflerNTimes(AS.
					THINKINGAHEAD.QUANTITY)),
				new AD("ChutesAndLadders", "firepole_travel", new TravelXUsingTransitTubes(
					NavType.Pole, AS.CHUTESANDLADDERS.DISTANCE)),
				new AD("ImGonnaBe", "im_gonna_be", new TravelXUsingTransitTubes(NavType.Floor,
					AS.IMGONNABE.DISTANCE)),
				new AD("SmallWorld", "small_world", new NumberOfDupes(AS.SMALLWORLD.QUANTITY)),
				new AD("YouMonster", "youmonster", new KillNCritters(AS.YOUMONSTER.QUANTITY)),
				new AD("BelongsInAMuseum", "all_artifacts", new CollectNArtifacts(28)),
				new AD("WholeNewWorlds", "rocket", new VisitAllPlanets()),
				new AD(AS.FINALBREATH.ID, "final_breath", new TriggerEvent(AS.FINALBREATH.ID)),
				new AD(AS.SAVINGMEEP.ID, "saving_meep", new TriggerEvent(AS.SAVINGMEEP.ID)),
				new AD("PowerOverwhelming", "power_overwhelm", new OverloadWire(Wire.
					WattageRating.Max50000)),
				new AD("IsItHotInHere", "isithot", new HeatBuildingToXKelvin(AS.ISITHOTINHERE.
					TEMPERATURE)),
				new AD(AS.WATCHTHEWORLDBURN.ID, "burn_gravitas", new TriggerEvent(AS.
					WATCHTHEWORLDBURN.ID)),
				new AD("SafeSpace", "safe_space", new NoDeathsForNCycles(AS.SAFESPACE.CYCLES),
					new CycleNumber(AS.SAFESPACE.CYCLES)),
				new AD("CritterSinger", "Animal_friends",
					CritterTypeRequirement(HatchConfig.ID, HatchHardConfig.ID,
					HatchMetalConfig.ID, HatchVeggieConfig.ID),
					CritterTypeRequirement(PacuConfig.ID, PacuTropicalConfig.ID,
					PacuCleanerConfig.ID),
					CritterTypeRequirement(LightBugConfig.ID, LightBugBlueConfig.ID,
					LightBugOrangeConfig.ID, LightBugPinkConfig.ID, LightBugPurpleConfig.ID,
					LightBugCrystalConfig.ID, LightBugBlackConfig.ID),
					CritterTypeRequirement(PuftConfig.ID, PuftOxyliteConfig.ID,
					PuftBleachstoneConfig.ID, PuftAlphaConfig.ID),
					CritterTypeRequirement(DreckoConfig.ID, DreckoPlasticConfig.ID),
					CritterTypeRequirement(OilFloaterConfig.ID, OilFloaterDecorConfig.ID,
					OilFloaterHighTempConfig.ID),
					CritterTypeRequirement(MoleConfig.ID),
					CritterTypeRequirement(MooConfig.ID),
					CritterTypeRequirement(CrabConfig.ID),
					CritterTypeRequirement(SquirrelConfig.ID)),
				new AD("MasterOfDisaster", "master_of_disaster",
					new DeathFromCause(db.Deaths.Overheating.Id),
					new DeathFromCause(db.Deaths.Slain.Id),
					new DeathFromCause(db.Deaths.Suffocation.Id),
					new DeathFromCause(db.Deaths.Starvation.Id)),
				new AD("ABalancedDiet", "balanced_diet", dietRequirements),
				new AD("JackOfAllTrades", "well_rounded", new ReachXAllAttributes(AS.
					JACKOFALLTRADES.LEVEL)),
				new AD("DestroyerOfWorlds", "dig_20", new ReachXAttributeValue(db.Attributes.
					Digging.Id, AS.DESTROYEROFWORLDS.LEVEL)),
				new AD("SmoothOperator", "operate_20", new ReachXAttributeValue(db.Attributes.
					Machinery.Id, AS.SMOOTHOPERATOR.LEVEL)),
				new AD("Olympian", "athletics_20", new ReachXAttributeValue(db.Attributes.Athletics.
					Id, AS.OLYMPIAN.LEVEL)),
				new AD("AdaLovelace", "science_20", new ReachXAttributeValue(db.Attributes.
					Learning.Id, AS.ADALOVELACE.LEVEL)),
				new AD("MasterChef", "cook_20", new ReachXAttributeValue(db.Attributes.
					Cooking.Id, AS.MASTERCHEF.LEVEL)),
				new AD("MasterBuilder", "build_20", new ReachXAttributeValue(db.Attributes.
					Construction.Id, AS.MASTERBUILDER.LEVEL)),
				new AD("MountainMover", "athletics_20", new ReachXAttributeValue(db.Attributes.
					Strength.Id, AS.MOUNTAINMOVER.LEVEL)),
				new AD("Cowboy", "ranch_20", new ReachXAttributeValue(db.Attributes.
					Ranching.Id, AS.COWBOY.LEVEL)),
				new AD("MotherEarth", "farm_20", new ReachXAttributeValue(db.Attributes.
					Botanist.Id, AS.MOTHEREARTH.LEVEL)),
				new AD("Michelangelo", "art_20", new ReachXAttributeValue(db.Attributes.
					Art.Id, AS.MICHELANGELO.LEVEL)),
				new AD("FirstDoNoHarm", "care_20", new ReachXAttributeValue(db.Attributes.
					Caring.Id, AS.FIRSTDONOHARM.LEVEL)),
				new AD("TotallyEcstatic", "ecstatic", new ReachXMoraleValue(AS.TOTALLYECSTATIC.
					MORALE)),
				new AD(AS.HAVEIWONYET.ID, "reach_cycle4000", new CycleNumber(AS.HAVEIWONYET.
					CYCLE)),
				new AD(AS.ALLTHEDUPLICANTS.ID, "dupes_100", new NumberOfDupes(100)),
				new AD(AS.ISEEWHATYOUDIDTHERE.ID, "cheat", new TriggerEvent(AS.
					ISEEWHATYOUDIDTHERE.ID)),
			};
			LoadProps();
		}

		/// <summary>
		/// Loads the meltable props list. Unfortunately the IDs are not constants; exclude
		/// anything made of Neutronium (you hacker!)
		/// </summary>
		private static void LoadProps() {
			using (var stream = typeof(Achievements).Assembly.GetManifestResourceStream(
					PROPS_PATH)) {
				if (stream == null)
					PUtil.LogWarning("Unable to load POI props list");
				else {
					var reader = new System.IO.StreamReader(stream);
					string line;
					while (!string.IsNullOrEmpty(line = reader.ReadLine()))
						POI_PROPS.Add(line.Trim());
					PUtil.LogDebug("Loaded " + POI_PROPS.Count + " props");
				}
			}
		}
	}
}
