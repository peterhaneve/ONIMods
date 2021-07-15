/*
 * Copyright 2021 Peter Han
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
using System;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires the consumption of a specific quantity of calories of a given food type.
	/// </summary>
	public class EatXCaloriesOfFood : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The food tag which must be consumed.
		/// </summary>
		private string foodTag;

		/// <summary>
		/// The number of calories which must be eaten.
		/// </summary>
		private float numCalories;

		public EatXCaloriesOfFood(float numKCal, string foodTag) {
			if (string.IsNullOrEmpty(foodTag))
				throw new ArgumentNullException("foodTag");
			// kcal to cal
			numCalories = Math.Max(1.0f, numKCal) * 1000.0f;
			this.foodTag = foodTag;
		}

		public void Deserialize(IReader reader) {
			numCalories = Math.Max(1.0f, reader.ReadSingle());
			foodTag = reader.ReadKleiString();
		}

		/// <summary>
		/// Retrieves the number of calories consumed of this food type.
		/// </summary>
		/// <returns>The calories consumed of this food type for the current colony.</returns>
		private float GetCaloriesConsumed() {
			var byFood = RationTracker.Get()?.caloriesConsumedByFood;
			if (byFood == null || !byFood.TryGetValue(foodTag, out float calories))
				calories = 0.0f;
			return calories;
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.ABALANCEDDIET.PROGRESS,
				TagManager.Create(foodTag).ProperName(), GameUtil.GetFormattedCalories(
				complete ? numCalories : GetCaloriesConsumed()), GameUtil.GetFormattedCalories(
				numCalories));
		}

		public override bool Success() {
			return GetCaloriesConsumed() > numCalories;
		}
	}
}
