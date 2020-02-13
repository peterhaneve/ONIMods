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

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Strings used in One Giant Leap.
	/// </summary>
	public static class AchievementStrings {
		public static LocString DEFAULT_PROGRESS = "Complete requirement";

		// A Balanced Diet
		public static class ABALANCEDDIET {
			public static LocString NAME = "A Balanced Diet";
			public static LocString DESC = "Eat " + GameUtil.GetFormattedCalories(KCAL *
				1000.0f) + " of each Food type available on all basic asteroid types.";
			public static LocString PROGRESS = "Calories from {0}: {1} / {2}";

			public const float KCAL = 1000.0f;
		}

		// Ada Lovelace
		public static class ADALOVELACE {
			public static LocString NAME = "Ada Lovelace";
			public static LocString DESC = "Raise a Duplicant's Science attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// That Belongs in a Museum
		public static class BELONGSINAMUSEUM {
			public static LocString NAME = "That Belongs in a Museum";
			public static LocString DESC = "Obtain at least one of each space artifact.";
			public static LocString PROGRESS = "Artifacts obtained: {0:D} / {1:D}";
		}

		// Chutes and Ladders
		public static class CHUTESANDLADDERS {
			public static LocString NAME = "Chutes and Ladders";
			public static LocString DESC = "Have Duplicants travel " + GameUtil.
				GetFormattedSimple(DISTANCE) + STRINGS.UI.UNITSUFFIXES.DISTANCE.METER +
				" by Fire Pole.";

			public const int DISTANCE = 10000;
		}

		// Cowboy
		public static class COWBOY {
			public static LocString NAME = "Cowboy";
			public static LocString DESC = "Raise a Duplicant's Ranching attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Destroyer of Worlds
		public static class DESTROYEROFWORLDS {
			public static LocString NAME = "Destroyer of Worlds";
			public static LocString DESC = "Raise a Duplicant's Excavation attribute to " +
				LEVEL + ".";
			public static LocString PROGRESS = "Highest attribute value: {0:F0} / {1:F0}";

			public const int LEVEL = 20;
		}

		// Empire Builder
		public static class EMPIREBUILDER {
			public static LocString NAME = "Empire Builder";
			public static LocString DESC = "Build " + GameUtil.GetFormattedSimple(QUANTITY) +
				" buildings.";
			public static LocString PROGRESS = "Buildings constructed: {0:D} / {1:D}";

			public const int QUANTITY = 2500;
		}

		// First Do No Harm
		public static class FIRSTDONOHARM {
			public static LocString NAME = "First Do No Harm";
			public static LocString DESC = "Raise a Duplicant's Doctoring attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Have I Won Yet?
		public static class HAVEIWONYET {
			public static LocString NAME = "Have I Won Yet?";
			public static LocString DESC = "Reach cycle " + GameUtil.GetFormattedSimple(
				CYCLE) + " with at least one living Duplicant.";

			public const int CYCLE = 4000;
		}

		// I'm Gonna Be
		public static class IMGONNABE {
			public static LocString NAME = "I'm Gonna Be";
			public static LocString DESC = "Have Duplicants travel " + GameUtil.
				GetFormattedSimple(DISTANCE) + STRINGS.UI.UNITSUFFIXES.DISTANCE.METER +
				" by walking.";

			public const int DISTANCE = 1609000;
		}

		// I See What You Did There
		public static class ISEEWHATYOUDIDTHERE {
			public const string ID = "ISeeWhatYouDidThere";
			public static LocString NAME = "I See What You Did There";
			public static LocString DESC = "Obtain a chunk of Neutronium.";
			public static LocString PROGRESS = "Obtain the unobtainium";
		}

		// Is It Hot in Here?
		public static class ISITHOTINHERE {
			public static LocString NAME = "Is It Hot in Here?";
			public static LocString DESC = "Increase the temperature of a building to " +
				GameUtil.GetFormattedTemperature(TEMPERATURE) + ".";
			public static LocString PROGRESS = "Hottest building: {0} / {1}";

			public const float TEMPERATURE = 2500.0f;
		}

		// John Henry
		public static class JOHNHENRY {
			public static LocString NAME = "John Henry";
			public static LocString DESC = "Dig " + GameUtil.GetFormattedSimple(QUANTITY) +
				" tiles.";
			public static LocString PROGRESS = "Tiles dug by Duplicants: {0:D} / {1:D}";

			public const int QUANTITY = 10000;
		}

		// Master Builder
		public static class MASTERBUILDER {
			public static LocString NAME = "Master Builder";
			public static LocString DESC = "Raise a Duplicant's Construction attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// MasterChef
		public static class MASTERCHEF {
			public static LocString NAME = "MasterChef";
			public static LocString DESC = "Raise a Duplicant's Cooking attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Michelangelo
		public static class MICHELANGELO {
			public static LocString NAME = "Michelangelo";
			public static LocString DESC = "Raise a Duplicant's Creativity attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Mother Earth
		public static class MOTHEREARTH {
			public static LocString NAME = "Mother Earth";
			public static LocString DESC = "Raise a Duplicant's Agriculture attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Mountain Mover
		public static class MOUNTAINMOVER {
			public static LocString NAME = "Mountain Mover";
			public static LocString DESC = "Raise a Duplicant's Strength attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Olympian
		public static class OLYMPIAN {
			public static LocString NAME = "Olympian";
			public static LocString DESC = "Raise a Duplicant's Athletics attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// Small World After All
		public static class SMALLWORLD {
			public static LocString NAME = "Small World After All";
			public static LocString DESC = "Have at least " + QUANTITY +
				" living Duplicants living in the colony at one time.";

			public const int QUANTITY = 35;
		}

		// Smooth Operator
		public static class SMOOTHOPERATOR {
			public static LocString NAME = "Smooth Operator";
			public static LocString DESC = "Raise a Duplicant's Machinery attribute to " +
				LEVEL + ".";

			public const int LEVEL = 20;
		}

		// To Stand the Test of Time
		public static class TESTOFTIME {
			public static LocString NAME = "To Stand the Test of Time";
			public static LocString DESC = "Reach cycle " + GameUtil.GetFormattedSimple(
				CYCLE) + " with at least one living Duplicant.";

			public const int CYCLE = 1000;
		}

		// Thinking Ahead
		public static class THINKINGAHEAD {
			public static LocString NAME = "Thinking Ahead";
			public static LocString DESC = "Use the Neural Vacillator " + QUANTITY + " times.";
			public static LocString PROGRESS = "Neural Vacillator used: {0:D} / {1:D}";

			public const int QUANTITY = 10;
		}

		// Totally Ecstatic
		public static class TOTALLYECSTATIC {
			public static LocString NAME = "Totally Ecstatic";
			public static LocString DESC = "Raise a Duplicant's Morale to " + MORALE + ".";
			public static LocString PROGRESS = "Highest Morale: {0:F0} / {1:F0}";

			public const int MORALE = 60;
		}

		// Watch the World Burn
		public static class WATCHTHEWORLDBURN {
			public const string ID = "WatchTheWorldBurn";
			public static LocString NAME = "Watch the World Burn";
			public static LocString DESC = "Melt a building generated in a point of interest.";
			public static LocString PROGRESS = "Melt a POI building";
		}

		// You Monster
		public static class YOUMONSTER {
			public static LocString NAME = "You Monster";
			public static LocString DESC = "Kill " + QUANTITY +
				" critters before they die of old age.";
			public static LocString PROGRESS = "Critters killed by non-natural causes: {0:D} / {1:D}";

			public const int QUANTITY = 100;
		}
	}
}
