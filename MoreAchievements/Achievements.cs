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
				new AD("EmpireBuilder", "build_2500", new BuildNBuildings(2500)),
				new AD("JohnHenry", "dig_10k", new DigNTiles(10000)),
				new AD("TestOfTime", "reach_cycle1000", new CycleNumber(1000)),
				new AD("ThinkingAhead", "thinking_ahead", new UseGeneShufflerNTimes(10)),
				new AD("ChutesAndLadders", "firepole_travel", new TravelXUsingTransitTubes(
					NavType.Pole, 10000)),
				new AD("ImGonnaBe", "im_gonna_be", new TravelXUsingTransitTubes(NavType.Floor,
					1609000)),
				new AD("SmallWorld", "small_world", new NumberOfDupes(35)),
				new AD("IsItHotInHere", "isithot", new HeatBuildingToXKelvin(2500.0f)),
				new AD("YouMonster", "youmonster", new KillNCritters(100)),
				new AD("DestroyerOfWorlds", "check", new ReachXAttributeValue(db.Attributes.
					Digging.Id, 20.0f)),
				new AD("SmoothOperator", "check", new ReachXAttributeValue(db.Attributes.
					Machinery.Id, 20.0f)),
				new AD("Olympian", "check", new ReachXAttributeValue(db.Attributes.Athletics.
					Id, 20.0f)),
				new AD("AdaLovelace", "check", new ReachXAttributeValue(db.Attributes.
					Learning.Id, 20.0f)),
				new AD("MasterChef", "check", new ReachXAttributeValue(db.Attributes.
					Cooking.Id, 20.0f)),
				new AD("MasterBuilder", "check", new ReachXAttributeValue(db.Attributes.
					Construction.Id, 20.0f)),
				new AD("MountainMover", "check", new ReachXAttributeValue(db.Attributes.
					Strength.Id, 20.0f)),
				new AD("Cowboy", "Animal_friends", new ReachXAttributeValue(db.Attributes.
					Ranching.Id, 20.0f)),
				new AD("MotherEarth", "check", new ReachXAttributeValue(db.Attributes.
					Botanist.Id, 20.0f)),
				new AD("Michelangelo", "check", new ReachXAttributeValue(db.Attributes.
					Art.Id, 20.0f)),
				new AD("FirstDoNoHarm", "check", new ReachXAttributeValue(db.Attributes.
					Caring.Id, 20.0f)),
				new AD("TotallyEcstatic", "check", new ReachXMoraleValue(60.0f)),
				new AD("HaveIWonYet", "reach_cycle4000", new CycleNumber(4000)),
			};
		}
	}
}
