/*
 * Copyright 2020 Peter Han
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

using AS = PeterHan.MoreAchievements.AchievementStrings;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Lists all achievements added by this mod.
	/// </summary>
	internal static class Achievements {
		/// <summary>
		/// The achievement list.
		/// </summary>
		internal static AD[] AllAchievements { get; private set; }

		/// <summary>
		/// Initializes the achievement list, after the Db has been initialized.
		/// </summary>
		internal static void InitAchievements() {
			var db = Db.Get();
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
				new AD("IsItHotInHere", "isithot", new HeatBuildingToXKelvin(AS.ISITHOTINHERE.
					TEMPERATURE)),
				new AD("YouMonster", "youmonster", new KillNCritters(AS.YOUMONSTER.QUANTITY)),
				new AD("BelongsInAMuseum", "all_artifacts", new CollectNArtifacts(28)),
				new AD("ABalancedDiet", "balanceddiet",
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FieldRationConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MushBarConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FriedMushBarConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BasicPlantFoodConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, BasicPlantBarConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, PickledMealConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, PrickleFruitConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, GrilledPrickleFruitConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, CookedEggConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, MeatConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, CookedMeatConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, ColdWheatBreadConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, SpiceBreadConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, FruitCakeConfig.ID),
					new EatXCaloriesOfFood(AS.ABALANCEDDIET.KCAL, SalsaConfig.ID)),
				new AD("DestroyerOfWorlds", "check", new ReachXAttributeValue(db.Attributes.
					Digging.Id, AS.DESTROYEROFWORLDS.LEVEL)),
				new AD("SmoothOperator", "check", new ReachXAttributeValue(db.Attributes.
					Machinery.Id, AS.SMOOTHOPERATOR.LEVEL)),
				new AD("Olympian", "check", new ReachXAttributeValue(db.Attributes.Athletics.
					Id, AS.OLYMPIAN.LEVEL)),
				new AD("AdaLovelace", "check", new ReachXAttributeValue(db.Attributes.
					Learning.Id, AS.ADALOVELACE.LEVEL)),
				new AD("MasterChef", "check", new ReachXAttributeValue(db.Attributes.
					Cooking.Id, AS.MASTERCHEF.LEVEL)),
				new AD("MasterBuilder", "check", new ReachXAttributeValue(db.Attributes.
					Construction.Id, AS.MASTERBUILDER.LEVEL)),
				new AD("MountainMover", "check", new ReachXAttributeValue(db.Attributes.
					Strength.Id, AS.MOUNTAINMOVER.LEVEL)),
				new AD("Cowboy", "Animal_friends", new ReachXAttributeValue(db.Attributes.
					Ranching.Id, AS.COWBOY.LEVEL)),
				new AD("MotherEarth", "check", new ReachXAttributeValue(db.Attributes.
					Botanist.Id, AS.MOTHEREARTH.LEVEL)),
				new AD("Michelangelo", "check", new ReachXAttributeValue(db.Attributes.
					Art.Id, AS.MICHELANGELO.LEVEL)),
				new AD("FirstDoNoHarm", "check", new ReachXAttributeValue(db.Attributes.
					Caring.Id, AS.FIRSTDONOHARM.LEVEL)),
				new AD("TotallyEcstatic", "check", new ReachXMoraleValue(AS.TOTALLYECSTATIC.
					MORALE)),
				new AD("HaveIWonYet", "reach_cycle4000", new CycleNumber(AS.HAVEIWONYET.CYCLE)),
				new AD(AS.ISEEWHATYOUDIDTHERE.ID, "cheat", new TriggerEvent(AS.
					ISEEWHATYOUDIDTHERE.ID, AS.ISEEWHATYOUDIDTHERE.PROGRESS))
			};
		}
	}
}
