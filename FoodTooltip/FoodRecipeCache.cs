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

namespace PeterHan.FoodTooltip {
	/// <summary>
	/// A class which looks up and caches recipes which use a particular plant or critter drop.
	/// 
	/// This class is not thread safe.
	/// </summary>
	public sealed class FoodRecipeCache : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static FoodRecipeCache Instance { get; private set; }

		/// <summary>
		/// Breaks a loop that would confuse the mod since plant products can be used in the
		/// Plant Pulverizer to generate Brackene -> Brackwax -> Brine -> Water.
		/// </summary>
		private static readonly Tag MILK_TAG = SimHashes.Milk.CreateTag();

		/// <summary>
		/// Creates the singleton instance.
		/// </summary>
		public static void CreateInstance() {
			Instance = new FoodRecipeCache();
		}

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		public static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}
		
		/// <summary>
		/// Recursively iterates the recipe list looking for foods that can be made with this
		/// item.
		/// </summary>
		/// <param name="item">The item to search.</param>
		/// <param name="found">The foods found so far.</param>
		/// <param name="seen">The items already seen, to prevent recipe loops from crashing.</param>
		/// <param name="quantity">The quantity of the base item.</param>
		private static void SearchForRecipe(Tag item, ICollection<FoodResult> found,
				ISet<Tag> seen, float quantity) {
			var prefab = Assets.GetPrefab(item);
			if (prefab != null && quantity > 0.0f && seen.Add(item) && item != MILK_TAG) {
				float kcal;
				// Item itself is usable as food
				if (prefab.TryGetComponent(out Edible edible) && (kcal = edible.FoodInfo.
						CaloriesPerUnit) > 0.0f)
					found.Add(new FoodResult(kcal, quantity, item));
				// Search for recipes using this item
				foreach (var recipe in RecipeManager.Get().recipes) {
					float amount = 0.0f;
					foreach (var ingredient in recipe.Ingredients)
						// Search for this item in the recipe
						if (ingredient.tag == item) {
							amount = ingredient.amount;
							break;
						}
					if (amount > 0.0f)
						SearchForRecipe(recipe.Result, found, seen, recipe.OutputUnits *
							quantity / amount);
				}
				// And complex ones too
				foreach (var recipe in ComplexRecipeManager.Get().recipes)
					// Dehydrated foods are not how you are supposed to get water!
					// (prevents Mush Bar from showing up spuriously)
					if (!recipe.fabricators.Contains(FoodDehydratorConfig.ID)) {
						float amount = 0.0f;
						foreach (var ingredient in recipe.ingredients)
							// Search for this item in the recipe
							if (ingredient.material == item) {
								amount = ingredient.amount;
								break;
							}
						if (amount > 0.0f)
							// Check all results of the recipe
							foreach (var result in recipe.results)
								SearchForRecipe(result.material, found, seen, result.amount *
									quantity / amount);
					}
			}
		}

		/// <summary>
		/// The cached recipe results for each food item.
		/// </summary>
		private readonly IDictionary<Tag, IList<FoodResult>> cache;

		private FoodRecipeCache() {
			cache = new Dictionary<Tag, IList<FoodResult>>(64);
		}

		/// <summary>
		/// Looks up a particular item tag to see what foods it can be used to produce.
		/// </summary>
		/// <param name="tag">The item tag to look up.</param>
		/// <returns>The foods for which it can be used, or an empty array if it cannot be used
		/// for any foods.</returns>
		public FoodResult[] Lookup(Tag tag) {
			if (tag == null)
				throw new ArgumentNullException(nameof(tag));
			// Check for existing list
			if (!cache.TryGetValue(tag, out var items)) {
				var seen = HashSetPool<Tag, FoodRecipeCache>.Allocate();
				try {
					items = new List<FoodResult>();
					SearchForRecipe(tag, items, seen, 1.0f);
					cache.Add(tag, items);
				} finally {
					seen.Recycle();
				}
			}
			// Create a copy of the results
			int n = items.Count;
			var result = new FoodResult[n];
			if (n > 0)
				items.CopyTo(result, 0);
			return result;
		}

		public void Dispose() {
			cache.Clear();
		}

		/// <summary>
		/// The results of a recipe as pertinent to a particular food.
		/// </summary>
		public readonly struct FoodResult {
			/// <summary>
			/// The calories provided per unit of completed recipe (not per input unit!)
			/// </summary>
			public float Calories { get; }

			/// <summary>
			/// The quantity of the recipe produced per unit of input.
			/// </summary>
			public float Quantity { get; }

			/// <summary>
			/// The recipe result tag.
			/// </summary>
			public Tag Result { get; }

			public FoodResult(float kcal, float quantity, Tag result) {
				Calories = kcal;
				Quantity = quantity;
				Result = result;
			}

			public override string ToString() {
				return "FoodResult[{2},qty={0:F2},kcal={1:F0}]".F(Quantity, Calories, Result);
			}
		}
	}
}
