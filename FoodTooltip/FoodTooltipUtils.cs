/*
 * Copyright 2024 Peter Han
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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FoodTooltip {
	/// <summary>
	/// Utility functions used in Food Supply Tooltips.
	/// </summary>
	internal static class FoodTooltipUtils {
		/// <summary>
		/// How many cycles are in the third line of the food consumption tooltips.
		/// </summary>
		private const int CYCLES_FOR_SUMMARY = 5;

		/// <summary>
		/// Adds the correct descriptors to a plant info screen.
		/// </summary>
		/// <param name="crop">The plant to query.</param>
		/// <param name="descriptors">The location where the descriptors should be placed.</param>
		internal static void AddCropDescriptors(Crop crop, IList<Descriptor> descriptors) {
			var cropVal = crop.cropVal;
			var maturity = Db.Get().Amounts.Maturity.Lookup(crop.gameObject);
			if (maturity != null)
				CreateDescriptors(TagManager.Create(cropVal.cropId), descriptors,
					maturity.GetDelta() * Constants.SECONDS_PER_CYCLE * cropVal.numProduced /
					maturity.GetMax(), FoodDescriptorTexts.PLANTS);
		}

		/// <summary>
		/// Adds the correct descriptors to a critter info screen.
		/// </summary>
		/// <param name="critter">The critter to query.</param>
		/// <param name="descriptors">The location where the descriptors should be placed.</param>
		internal static void AddCritterDescriptors(GameObject critter,
				IList<Descriptor> descriptors) {
			string[] drops;
			// Check the meat it drops
			if (critter != null && critter.TryGetComponent(out Butcherable butcher) &&
					(drops = butcher.drops).Length > 0) {
				var dict = DictionaryPool<string, int, FoodRecipeCache>.Allocate();
				GetEggsPerCycle(critter, out float replacement, out float noReplacement);
				// Find out what it drops when it dies - critters always die so the
				// no-replacement rate must be positive
				CollectDrops(drops, dict);
				foreach (var pair in dict)
					CreateDescriptors(TagManager.Create(pair.Key), descriptors,
						pair.Value * noReplacement, FoodDescriptorTexts.CRITTERS);
				dict.Recycle();
				// How much omelette can the egg be made into? Babies are excluded here
				var fertDef = critter.GetDef<FertilityMonitor.Def>();
				if (fertDef != null)
					CreateDescriptors(fertDef.eggPrefab, descriptors, replacement,
						FoodDescriptorTexts.CRITTERS);
			}
		}

		/// <summary>
		/// Collects critter drops to determine the correct quantity of each, since the game
		/// duplicates the drops N times if more than one is to be dropped.
		/// </summary>
		/// <param name="drops">The tag IDs of the dropped items.</param>
		/// <param name="result">The location where the dropped items will be stored.</param>
		private static void CollectDrops(string[] drops, IDictionary<string, int> result) {
			result.Clear();
			foreach (string drop in drops) {
				if (!result.TryGetValue(drop, out int qty))
					qty = 0;
				result[drop] = qty + 1;
			}
		}

		/// <summary>
		/// Creates the descriptors for the total kcal yield of each relevant product.
		/// </summary>
		/// <param name="drop">The item dropped that could be used in food.</param>
		/// <param name="descriptors">The location where the descriptors should be placed.</param>
		/// <param name="dropRate">The quantity dropped per cycle.</param>
		/// <param name="text">The text to be displayed.</param>
		private static void CreateDescriptors(Tag drop, ICollection<Descriptor> descriptors,
				float dropRate, FoodDescriptorText text) {
			if (text == null)
				throw new ArgumentNullException(nameof(text));
			string dropName = drop.ProperName();
			foreach (var food in FoodRecipeCache.Instance.Lookup(drop)) {
				string foodName = food.Result.ProperName();
				if (dropRate > 0.0f) {
					// Determine total yield in kcal and convert to /cycle
					string perCycle = GameUtil.AddTimeSliceText(GameUtil.GetFormattedCalories(
						food.Calories * dropRate * food.Quantity), TimeSlice.PerCycle);
					descriptors.Add(new Descriptor(text.PerCycle.F(foodName, perCycle,
						dropName), text.PerCycleTooltip.F(foodName, perCycle, dropName)));
				} else
					descriptors.Add(new Descriptor(text.Stifled.F(foodName), text.
						StifledTooltip));
			}
		}

		/// <summary>
		/// Creates the text which describes the calorie delta for one cycle.
		/// </summary>
		/// <param name="text">The description text.</param>
		/// <param name="produced">The kcal produced.</param>
		/// <param name="consumed">The kcal consumed.</param>
		/// <returns>The description text with the formatted kcal values substituted.</returns>
		private static string FormatDeltaTooltip(string text, float produced, float consumed) {
			return text.F(GameUtil.GetFormattedCalories(produced), GameUtil.
				GetFormattedCalories(consumed));
		}

		/// <summary>
		/// Calculates how many calories were produced and consumed in the specified cycle.
		/// </summary>
		/// <param name="report">The report to analyze.</param>
		/// <param name="produced">The location where the produced kcal will be stored.</param>
		/// <param name="consumed">The location where the consumed kcal will be stored.</param>
		private static void GetCalorieDeltas(ReportManager.DailyReport report,
				out float produced, out float consumed) {
			ReportManager.ReportEntry entry;
			if ((entry = report?.GetEntry(ReportManager.ReportType.CaloriesCreated)) != null) {
				// Consumption is negative
				produced = entry.accPositive;
				consumed = -entry.accNegative;
			} else {
				produced = 0.0f;
				consumed = 0.0f;
			}
		}

		/// <summary>
		/// Gets the number of extra eggs, before and after replacement, that a critter can
		/// lay in its current state.
		/// </summary>
		/// <param name="obj">The critter to calculate.</param>
		/// <param name="noReplacement">The number of eggs laid before death without needing replacement.</param>
		/// <param name="replacement">The number of extra eggs after replacement laid before death.</param>
		private static void GetEggsPerCycle(GameObject obj, out float replacement,
				out float noReplacement) {
			var amounts = Db.Get().Amounts;
			var fertility = amounts.Fertility.Lookup(obj);
			var age = amounts.Age.Lookup(obj);
			float delta;
			// Get the reproduction rate and calculate eggs laid before dying
			// Age is in cycles
			if (fertility != null && age != null && (delta = fertility.GetDelta()) > 0.0f) {
				float maxAge = age.GetMax(), totalEggs = maxAge * delta * Constants.
					SECONDS_PER_CYCLE / fertility.GetMax();
				// You cannot lay half an egg
				noReplacement = Mathf.Floor(totalEggs) / maxAge;
				replacement = Mathf.Floor(Mathf.Max(0.0f, totalEggs - 1.0f)) / maxAge;
			} else {
				replacement = 0.0f;
				noReplacement = 0.0f;
			}
		}

		/// <summary>
		/// Shows food consumption and production stats for the current cycle, last cycle, and
		/// last 5 cycle average.
		/// </summary>
		/// <param name="screen">The screen to add the stats.</param>
		internal static void ShowFoodUseStats(MeterScreen screen) {
			var tooltip = screen.RationsTooltip;
			var style = screen.ToolTipStyle_Property;
			var reports = ReportManager.Instance;
			if (tooltip != null && reports != null) {
				GetCalorieDeltas(reports.TodaysReport, out float produced, out float consumed);
				tooltip.AddMultiStringTooltip(FormatDeltaTooltip(FoodTooltipStrings.
					FOOD_RATE_CURRENT, produced, consumed), style);
				// Returns null if not present
				GetCalorieDeltas(reports.YesterdaysReport, out produced, out consumed);
				tooltip.AddMultiStringTooltip(FormatDeltaTooltip(FoodTooltipStrings.
					FOOD_RATE_LAST1, produced, consumed), style);
				int days = 0, cycle = GameUtil.GetCurrentCycle();
				float totalProduced = 0.0f, totalConsumed = 0.0f;
				// Last 5 cycles, arithmetic average
				foreach (var report in reports.reports)
					if (report.day >= cycle - CYCLES_FOR_SUMMARY) {
						GetCalorieDeltas(report, out produced, out consumed);
						totalProduced += produced;
						totalConsumed += consumed;
						days++;
					}
				// Do not divide by zero
				if (days == 0)
					days = 1;
				tooltip.AddMultiStringTooltip(FormatDeltaTooltip(FoodTooltipStrings.
					FOOD_RATE_LAST5, totalProduced / days, totalConsumed / days), style);
			}
		}
	}
}
