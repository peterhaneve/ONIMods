/*
 * Copyright 2019 Peter Han
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

using STRINGS;

namespace PeterHan.TraitRework {
	/// <summary>
	/// Stores the strings used in Trait Rework.
	/// </summary>
	public static class TraitStrings {
		// Effect for eating in a lit area
		public static LocString EATING_LIT = "Eating in Lit Area";

		// Effect for hearing a loud sleeper while awake
		public static LocString DISTURBED_NAME = "Disturbed";

		public static LocString DISTURBED_DESC = "This Duplicant was disturbed by the loud sleeping of another Duplicant";

		// Tooltip displayed for Duplicants who cannot eat certain foods
		public static LocString NOFOOD_SHORTTOOLTIP = "This Duplicant will not eat these items";

		// Extended trait tooltips
		public static LocString CANTCOOK_SHORTDESC = "Will not eat " + UI.PRE_KEYWORD +
			"Food" + UI.PST_KEYWORD + " prepared in " + UI.PRE_KEYWORD + BUILDINGS.
			PREFABS.GOURMETCOOKINGSTATION.NAME + UI.PST_KEYWORD;
		public static LocString CANTCOOK_EXT = "• Will not eat " + UI.FormatAsLink(
			RESEARCH.TREES.TITLE_FOOD, "FOOD") + " prepared in " + UI.FormatAsLink(BUILDINGS.
			PREFABS.GOURMETCOOKINGSTATION.NAME, GourmetCookingStationConfig.ID);

		public static LocString NARCOLEPSY_SHORTDESC = "Immune to " + UI.PRE_KEYWORD +
			"Sore Back" + UI.PST_KEYWORD;
		public static LocString NARCOLEPSY_TOOLTIP = "Sleeping on the ground will not affect this Duplicant";

		public static LocString SCAREDYCAT_SHORTDESC = "Will not eat " + UI.PRE_KEYWORD +
			"Meat" + UI.PST_KEYWORD + " or " + UI.PRE_KEYWORD + "Fish" + UI.PST_KEYWORD +
			" products";
		public static LocString SCAREDYCAT_EXT = "• Will not eat " + UI.FormatAsLink(
			ITEMS.FOOD.MEAT.NAME, MeatConfig.ID) + " or " + UI.FormatAsLink(ITEMS.FOOD.
			FISHMEAT.NAME, PacuFilletConfig.ID) + " products";

		/// <summary>
		/// Updated trait descriptions are stored here.
		/// </summary>
		public static class TraitDescriptions {
			public static LocString CALORIEBURNER = "This Duplicant might actually be several black holes in a trench coat, but the extra food makes them stronger";

			public static LocString CANTCOOK = "This Duplicant has a deep-seated distrust of the culinary arts and will only eat simple foods";

			public static LocString DIVERSLUNG = "This Duplicant could have been a talented opera singer in another life, but has a slower metabolism";

			public static LocString HEMOPHOBIA = "This Duplicant has a frail body and delicate disposition, and cannot tend to the sick";

			public static LocString MOUTHBREATHER = "This Duplicant sucks up way more than their fair share of " + ELEMENTS.OXYGEN.NAME + ", but the extra air makes them faster";

			public static LocString NARCOLEPSY = "This Duplicant can and will fall asleep at any time, but does not mind sleeping on the ground";

			public static LocString SCAREDYCAT = "This Duplicant abhors violence, and will not eat meat or fish products";

			public static LocString SNORER = "In space, everyone can hear you snore, and that is not pleasant";
		}
	}
}
