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

namespace PeterHan.FoodTooltip {
	/// <summary>
	/// Stores the strings used in Food Supply Tooltips.
	/// </summary>
	public static class FoodTooltipStrings {
		// Descriptor text for critter yields (per food type)
		public static LocString CRITTER_PER_CYCLE = "{0}: {1}";
		public static LocString CRITTER_PER_CYCLE_TOOLTIP = "This critter's {2} will provide {1} if used to make {0}";

		public static LocString CRITTER_INFERTILE = "{0}: Insufficient reproduction";
		public static LocString CRITTER_INFERTILE_TOOLTIP = "This critter is not reproducing enough to provide this item";

		// Descriptor text for plant yields (per food type)
		public static LocString PLANT_PER_CYCLE = "{0}: {1}";
		public static LocString PLANT_PER_CYCLE_TOOLTIP = "This plant will provide {1} if used to make {0}";

		public static LocString PLANT_WILTING = "{0}: Not growing";
		public static LocString PLANT_WILTING_TOOLTIP = "This plant is not currently growing";

		// Tool tip text for food consumption rates
		private const string PRE_CONSUMPTION = "<color=#f44a47>";
		private const string PRE_PRODUCTION = "<color=#57b95e>";
		private const string PST = "</color>";

		public static LocString FOOD_RATE_CURRENT = "This Cycle: " + PRE_PRODUCTION + "+{0}" +
			PST + " " + PRE_CONSUMPTION + "-{1}" + PST;
		public static LocString FOOD_RATE_LAST1 = "Last Cycle: " + PRE_PRODUCTION + "+{0}" +
			PST + " " + PRE_CONSUMPTION + "-{1}" + PST;
		public static LocString FOOD_RATE_LAST5 = "5 Cycle Average: " + PRE_PRODUCTION +
			"+{0}" + PST + " " + PRE_CONSUMPTION + "-{1}" + PST;
	}

	/// <summary>
	/// Stores the grouped text for each critter and plant info screen.
	/// </summary>
	internal static class FoodDescriptorTexts {
		/// <summary>
		/// The text to display for critter info screens.
		/// </summary>
		internal static readonly FoodDescriptorText CRITTERS = new FoodDescriptorText(
			FoodTooltipStrings.CRITTER_PER_CYCLE, FoodTooltipStrings.CRITTER_PER_CYCLE_TOOLTIP,
			FoodTooltipStrings.CRITTER_INFERTILE, FoodTooltipStrings.CRITTER_INFERTILE_TOOLTIP
		);

		/// <summary>
		/// The text to display for plant info screens.
		/// </summary>
		internal static readonly FoodDescriptorText PLANTS = new FoodDescriptorText(
			FoodTooltipStrings.PLANT_PER_CYCLE, FoodTooltipStrings.PLANT_PER_CYCLE_TOOLTIP,
			FoodTooltipStrings.PLANT_WILTING, FoodTooltipStrings.PLANT_WILTING_TOOLTIP
		);
	}

	/// <summary>
	/// A wrapper class to abstract the text and tooltips for plants and critters.
	/// </summary>
	internal sealed class FoodDescriptorText {
		/// <summary>
		/// The text to display to show how much kcal is produced per cycle.
		/// </summary>
		public string PerCycle { get; }

		/// <summary>
		/// The tooltip to display to explain how much kcal is produced per cycle.
		/// </summary>
		public string PerCycleTooltip { get; }

		/// <summary>
		/// The text to display when no kcal can be produced.
		/// </summary>
		public string Stifled { get; }

		/// <summary>
		/// The tooltip to explain why no kcal can be produced.
		/// </summary>
		public string StifledTooltip { get; }

		internal FoodDescriptorText(string perCycle, string perCycleTooltip, string stifled,
				string stifledTooltip) {
			PerCycle = perCycle;
			PerCycleTooltip = perCycleTooltip;
			Stifled = stifled;
			StifledTooltip = stifledTooltip;
		}
	}
}
