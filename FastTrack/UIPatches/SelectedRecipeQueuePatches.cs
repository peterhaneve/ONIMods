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

using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using DescriptorWithSprite = SelectedRecipeQueueScreen.DescriptorWithSprite;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to the recipe screen used for complex fabricators.
	/// Classified as AllocOpts because it reuses several of the descriptor optimizations.
	/// </summary>
	internal static class SelectedRecipeQueuePatches {
		/// <summary>
		/// Avoids running String.Format for each ingredient every update.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

		private static string HEP_FORMAT;

		private static string HEP_TOOLTIP;

		private static Tuple<Sprite, Color> RADBOLT_ICON;

		private static string RECIPE_PRODUCT;

		private static string RECIPE_TOOLTIP;

		private static string RECIPE_REQUIREMENT;

		// These values are all hardcoded by the base game
		private const string INGREDIENT_ENOUGH_PRE = "<size=12>";
		private const string INGREDIENT_ENOUGH_POST = "</size>";
		private const string INGREDIENT_NOTENOUGH_PRE = "<size=12><color=#E68280>";
		private const string INGREDIENT_NOTENOUGH_POST = "</color></size>";

		private static readonly Color UNDISCOVERED_COLOR = new Color(0.891f, 0.855f, 0.851f);
		private static readonly Color INSUFFICIENT_TEXT = new Color(0.2f, 0.2f, 0.2f);
		private static readonly Color INSUFFICIENT_TINT = new Color(1f, 1f, 1f, 0.55f);
		private static readonly Color SELECTED_INSUFFICIENT = new Color(0.984f, 0.914f, 0.922f);
		private static readonly Color SELECTED_SUFFICIENT = new Color(0.941f, 0.965f, 0.988f);
		private static readonly Color HEADER_INSUFFICIENT = new Color(0.851f, 0.855f, 0.891f);
		private static readonly Color HEADER_SUFFICIENT = new Color(0.891f, 0.855f, 0.851f);

		/// <summary>
		/// Calculates which recipe has been selected by the user, using one of the worst
		/// methods possible (but imitates the base game). To make it worse, whoever wrote the
		/// original method named it appropriately to imply that it does work and is slow, but
		/// another person wrapped it in a property accessor to hide that very fact, causing
		/// it to be abused in situations like a loop condition where it could be called
		/// hundreds of times.
		/// </summary>
		/// <param name="recipes">The list of matching recipes.</param>
		/// <param name="selectedOptions">The materials selected by the user.</param>
		/// <returns>The selected recipe, or null if no recipes matched.</returns>
		private static ComplexRecipe CalculateSelectedRecipe(IList<ComplexRecipe> recipes,
				IList<Tag> selectedOptions) {
			int nr = recipes.Count, no = selectedOptions.Count;
			for (int i = 0; i < nr; i++) {
				var recipe = recipes[i];
				bool match = true;
				for (int j = 0; j < no && match; j++)
					if (recipe.ingredients[j].material != selectedOptions[j])
						match = false;
				if (match)
					return recipe;
			}
			return null;
		}
		
		/// <summary>
		/// Gets the first complex recipe that matches the specified category.
		/// </summary>
		/// <param name="target">The fabricator to search.</param>
		/// <param name="categoryID">The category ID that the recipe must match.</param>
		/// <returns>The first matching recipe, or null if no recipes matched.</returns>
		private static ComplexRecipe GetFirstRecipe(ComplexFabricator target,
				string categoryID) {
			var recipes = target.recipe_list;
			int n = recipes.Length;
			for (int i = 0; i < n; i++) {
				var recipe = recipes[i];
				if (recipe.recipeCategoryID == categoryID)
					return recipe;
			}
			return null;
		}
		
		/// <summary>
		/// Gets the description to display for a recipe ingredient.
		/// </summary>
		/// <param name="recipe">The recipe to describe.</param>
		/// <param name="inventory">The inventory to be used to determine the amount.</param>
		/// <param name="result">The location where the ingredients will be stored.</param>
		private static string GetIngredientDescription(ComplexRecipe.RecipeElement ingredient,
				WorldInventory inventory, out bool hasEnoughMaterial) {
			var material = ingredient.material;
			var prefab = Assets.GetPrefab(material);
			float available = inventory == null ? 0.0f : inventory.GetAmount(material, true);
			string name = prefab.GetProperName(), amountText = GameUtil.GetFormattedByTag(
				material, ingredient.amount), availableText = GameUtil.GetFormattedByTag(
				material, available), template = RECIPE_REQUIREMENT;
			var text = CACHED_BUILDER.Clear();
			bool hasEnough = available >= ingredient.amount;
			if (template == null)
				text.Append(name).Append(": ").Append(amountText).AppendLine();
			else
				text.Append(template).Replace("{0}", name).Replace("{1}", amountText);
			text.Append(hasEnough ? INGREDIENT_ENOUGH_PRE : INGREDIENT_NOTENOUGH_PRE).Append(
				STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE).Replace("{0}",
				availableText).Append(hasEnough ? INGREDIENT_ENOUGH_POST :
				INGREDIENT_NOTENOUGH_POST);
			hasEnoughMaterial = hasEnough;
			return text.ToString();
		}

		/// <summary>
		/// Gets all complex recipes that match the specified category.
		/// </summary>
		/// <param name="target">The fabricator to search.</param>
		/// <param name="categoryID">The category ID that the recipe must match.</param>
		/// <param name="results">The location where matching recipes will be stored.</param>
		/// <returns>true if recipes were found, or false otherwise.</returns>
		private static bool GetMatchingRecipes(ComplexFabricator target, string categoryID,
				IList<ComplexRecipe> results) {
			var recipes = target.recipe_list;
			int n = recipes.Length;
			bool found = false;
			for (int i = 0; i < n; i++) {
				var recipe = recipes[i];
				if (recipe.recipeCategoryID == categoryID) {
					results.Add(recipe);
					found = true;
				}
			}
			return found;
		}

		/// <summary>
		/// Gets a list of all the product descriptors for a recipe.
		/// </summary>
		/// <param name="recipe">The recipe to describe.</param>
		/// <param name="result">The location where the products will be stored.</param>
		private static void GetResultDescriptions(ComplexRecipe recipe,
				ICollection<DescriptorWithSprite> result) {
			var results = recipe.results;
			int n = results.Length;
			string template = RECIPE_PRODUCT, description;
			var text = CACHED_BUILDER;
			if (recipe.producedHEP > 0) {
				description = text.Clear().Append(HEP_TOOLTIP).Append(recipe.
					producedHEP).ToString();
				result.Add(new DescriptorWithSprite(new Descriptor(description, description,
					Descriptor.DescriptorType.Requirement), RADBOLT_ICON));
			}
			var desc = ListPool<Descriptor, SelectedRecipeQueueScreen>.Allocate();
			for (int i = 0; i < n; i++) {
				var item = results[i];
				var material = item.material;
				var prefab = Assets.GetPrefab(material);
				string amountText = GameUtil.GetFormattedByTag(material, item.amount),
					name = TagManager.GetProperName(material), facade = item.facadeID;
				var element = ElementLoader.GetElement(material);
				string product = string.IsNullOrWhiteSpace(facade) ? name : TagManager.
					GetProperName(facade);
				desc.Clear();
				text.Clear();
				if (template == null)
					description = text.Append(product).Append(": ").Append(amountText).
						ToString();
				else
					description = text.Append(template).Replace("{0}", product).
						Replace("{1}", amountText).ToString();
				text.Clear().Append(RECIPE_TOOLTIP).Replace("{0}", name).Replace("{1}",
					amountText);
				result.Add(new DescriptorWithSprite(new Descriptor(description, text.
					ToString(), Descriptor.DescriptorType.Requirement), Def.GetUISprite(
					material, facade)));
				if (element != null) {
					DescriptorAllocPatches.GetMaterialDescriptors(element.attributeModifiers,
						desc);
					int nd = desc.Count;
					for (int j = 0; j < nd; j++) {
						var descriptor = desc[j];
						descriptor.IncreaseIndent();
						result.Add(new DescriptorWithSprite(descriptor, null));
					}
				} else {
					DescriptorAllocPatches.GetAllDescriptors(prefab, false, desc);
					int nd = desc.Count;
					for (int j = 0; j < nd; j++) {
						var descriptor = desc[j];
						if (descriptor.IsEffectDescriptor()) {
							descriptor.IncreaseIndent();
							result.Add(new DescriptorWithSprite(descriptor, null));
						}
					}
				}
			}
			desc.Recycle();
		}

		/// <summary>
		/// Initializes static formatted strings to avoid reallocating every update.
		/// </summary>
		internal static void Init() {
			string product = STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPEPRODUCT;
			string req = STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_REQUIREMENT;
			// Another typo, KLEI PLEASE
			HEP_FORMAT = "<b>" + STRINGS.UI.FormatAsLink(STRINGS.ITEMS.RADIATION.
				HIGHENERGYPARITCLE.NAME, "HEP") + "</b>: ";
			HEP_TOOLTIP = "<b>" + STRINGS.ITEMS.RADIATION.HIGHENERGYPARITCLE.NAME +
				"</b>: ";
			RADBOLT_ICON = new Tuple<Sprite, Color>(Assets.GetSprite("radbolt"), Color.white);
			RECIPE_PRODUCT = (product == "{0}: {1}") ? null : product;
			RECIPE_TOOLTIP = STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.TOOLTIPS.
				RECIPEPRODUCT;
			// Fast case for most languages
			RECIPE_REQUIREMENT = (req == "{0}: {1}") ? null : req;
		}

		/// <summary>
		/// Refreshes the ingredients available for one recipe.
		/// </summary>
		/// <param name="instance">The screen to refresh.</param>
		/// <param name="selectedRecipes">The currently selected recipes.</param>
		/// <param name="index">The index of the recipe in the fabricator list.</param>
		/// <param name="ingredientRow">The visual row to parent the choices.</param>
		private static void RefreshIngredientChoices(SelectedRecipeQueueScreen instance,
				IList<ComplexRecipe> selectedRecipes, int index, GameObject ingredientRow) {
			var seen = HashSetPool<Tag, ComplexRecipe>.Allocate();
			var unknownResources = HashSetPool<Tag, SelectedRecipeQueueScreen>.Allocate();
			var ingredientCounts = DictionaryPool<Tag, int, SelectedRecipeQueueScreen>.
				Allocate();
			var prefab = instance.materialFilterRowPrefab;
			var dr = DiscoveredResources.Instance;
			Color headerColor = HEADER_INSUFFICIENT;
			var rbc = instance.materialSelectionRowsByContainer;
			if (!rbc.TryGetValue(ingredientRow, out List<GameObject> ingredientRows))
				rbc.Add(ingredientRow, ingredientRows = new List<GameObject>());
			int n = selectedRecipes.Count, oldRows = ingredientRows.Count, knownCount = 0;
			var state = new RecipeState(instance, ingredientCounts);
			state.CountIngredientQueue(selectedRecipes);
			for (int i = 0; i < n; i++) {
				var recipe = selectedRecipes[i];
				var ingredient = recipe.ingredients[index];
				var materialTag = ingredient.material;
				// As IsDiscovered and InstantBuildMode can be assumed to be loop
				// invariant, evaluating undiscovered resources again will gain nothing
				if (seen.Add(materialTag)) {
					bool discovered = dr.IsDiscovered(materialTag);
					if (!discovered)
						unknownResources.Add(materialTag);
					if (discovered || DebugHandler.InstantBuildMode) {
						GameObject choice;
						if (knownCount < oldRows)
							choice = ingredientRows[knownCount];
						else
							ingredientRows.Add(choice = Util.KInstantiateUI(prefab,
								ingredientRow, true));
						if (UpdateIngredient(ref state, choice, ingredient, index))
							headerColor = HEADER_SUFFICIENT;
						knownCount++;
					}
				}
			}
			ingredientCounts.Recycle();
			seen.Recycle();
			// Clean up old rows; as the number of recipes should not decrease, this should
			// only ever be necessary if instant build mode/sandbox is toggled
			for (int i = knownCount; i < oldRows; i++)
				Util.KDestroyGameObject(ingredientRows[i]);
			if (oldRows > knownCount)
				ingredientRows.RemoveRange(knownCount, oldRows - knownCount);
			if (ingredientRow.TryGetComponent(out HierarchyReferences hr)) {
				UpdateSummary(hr, index, unknownResources, knownCount);
				hr.GetReference<Image>("HeaderBG").color = headerColor;
			}
			unknownResources.Recycle();
		}

		/// <summary>
		/// As the ingredients no longer have actual descriptors, refresh the new buttons.
		/// </summary>
		/// <param name="instance">The screen to refresh.</param>
		/// <param name="selectedRecipes">The currently selected recipes.</param>
		private static void RefreshIngredients(SelectedRecipeQueueScreen instance,
				IList<ComplexRecipe> selectedRecipes) {
			var parent = instance.IngredientsDescriptorPanel.gameObject;
			var containers = instance.materialSelectionContainers;
			int newRows = selectedRecipes[0].ingredients.Length, oldRows = containers.Count;
			var rbc = instance.materialSelectionRowsByContainer;
			var selectedRecipe = CalculateSelectedRecipe(selectedRecipes, instance.
				selectedMaterialOption);
			for (int i = 0; i < newRows; i++) {
				GameObject ingredientRow;
				if (i < oldRows)
					// Indexing dictionaries by a game object key is bad, but ONI does it...
					ingredientRow = containers[i];
				else
					containers.Add(ingredientRow = Util.KInstantiateUI(instance.
						materialSelectionContainerPrefab, parent, true));
				RefreshIngredientChoices(instance, selectedRecipes, i, ingredientRow);
			}
			// Destroying the old rows will recursively destroy the choices
			for (int i = newRows; i < oldRows; i++) {
				var oldRow = containers[i];
				rbc.Remove(oldRow);
				Util.KDestroyGameObject(oldRow);
			}
			if (oldRows > newRows)
				containers.RemoveRange(newRows, oldRows - newRows);
			// The base game would crash if this was null
			if (selectedRecipe != null)
				instance.target.mostRecentRecipeSelectionByCategory[instance.
					selectedRecipeCategoryID] = selectedRecipe.id;
			parent.SetActive(true);
		}

		/// <summary>
		/// Replaces descriptor effect rows, reusing old rows if possible, with new effects.
		/// </summary>
		/// <param name="items">The effect descriptors to display.</param>
		/// <param name="parent">The parent container for the displayed information.</param>
		/// <param name="instance">The currently active side screen.</param>
		private static void RemoveAndAddRows(IList<DescriptorWithSprite> items,
				GameObject parent, SelectedRecipeQueueScreen instance) {
			var oldRows = ListPool<GameObject, SelectedRecipeQueueScreen>.Allocate();
			var text = CACHED_BUILDER;
			var rows = instance.recipeEffectsDescriptorRows;
			var prefab = instance.recipeElementDescriptorPrefab;
			int n = items.Count, nr = rows.Count;
			bool collapse = true;
			// The descriptors have no GetHashCode or Equals, so direct lookup is
			// unfortunately impossible
			foreach (var pair in rows)
				oldRows.Add(pair.Value);
			rows.Clear();
			for (int i = 0; i < n; i++) {
				var effect = items[i];
				bool isOld = i < nr;
				var go = isOld ? oldRows[i] : Util.KInstantiateUI(prefab, parent, true);
				if (go.TryGetComponent(out HierarchyReferences hr)) {
					var icon = hr.GetReference<Image>("Icon");
					var descriptor = effect.descriptor;
					var sprite = effect.tintedSprite;
					bool hasSprite = sprite != null && sprite.first != null;
					int indent = descriptor.indent, padding = 0;
					// This is quicker than reallocating strings over and over in GetIndented()
					text.Clear();
					icon.gameObject.SetActive(true);
					if (hasSprite) {
						for (int j = 0; j < indent; j++)
							text.Append(Constants.TABSTRING);
						icon.sprite = sprite.first;
						icon.color = sprite.second;
						collapse = true;
					} else {
						icon.sprite = null;
						icon.color = Color.clear;
						if (collapse) {
							// Also hard coded by Klei
							padding = -8;
							collapse = false;
						}
					}
					if (go.TryGetComponent(out VerticalLayoutGroup group))
						group.padding.top = padding;
					if (go.TryGetComponent(out LayoutElement element)) {
						float h = hasSprite ? 32.0f : 0.0f;
						element.minWidth = hasSprite ? 32.0f : 40.0f;
						element.minHeight = h;
						element.preferredHeight = h;
					}
					text.Append(descriptor.text);
					hr.GetReference<LocText>("Label").SetText(text);
					if (!isOld)
						// No need to refresh this again on reused rows
						hr.GetReference<RectTransform>("FilterControls").gameObject.
							SetActive(false);
					hr.GetReference<ToolTip>("Tooltip").SetSimpleTooltip(descriptor.
						tooltipText);
				}
				rows.Add(effect, go);
			}
			// Clean up unused rows
			for (int i = n; i < nr; i++)
				Util.KDestroyGameObject(oldRows[i]);
			oldRows.Recycle();
		}

		/// <summary>
		/// Updates one ingredient in the recipe queue screen.
		/// </summary>
		/// <param name="state">Consolidates state that is reused across all ingredients.</param>
		/// <param name="choice">The selection row to allow a different ingredient to be chosen.</param>
		/// <param name="ingredient">The ingredient to be used.</param>
		/// <param name="index">The index of the recipe in the fabricator list.</param>
		/// <returns>true if there are enough resources for the selected material, or false otherwise.</returns>
		private static bool UpdateIngredient(ref RecipeState state, GameObject choice,
				ComplexRecipe.RecipeElement ingredient, int index) {
			var instance = state.instance;
			var materialTag = ingredient.material;
			string desc = GetIngredientDescription(ingredient, state.inventory,
				out bool hasEnough);
			bool selectedMaterial = instance.selectedMaterialOption[index] == materialTag;
			if (choice.TryGetComponent(out HierarchyReferences hr)) {
				var label = hr.GetReference<LocText>("Label");
				var hover = hr.GetReference<RectTransform>("SelectionHover");
				var icon = hr.GetReference<Image>("Icon");
				var gr = GlobalResources.Instance();
				label.color = hasEnough ? Color.black : INSUFFICIENT_TEXT;
				label.SetText(desc);
				if (selectedMaterial && hover.TryGetComponent(out Image image))
					image.color = hasEnough ? SELECTED_SUFFICIENT : SELECTED_INSUFFICIENT;
				hover.gameObject.SetActive(selectedMaterial);
				// The ingredient queue count might include other recipes
				if (!state.ingredientCounts.TryGetValue(materialTag, out int count))
					count = 0;
				hr.GetReference<LocText>("OrderCountLabel").SetText(count.ToString());
				icon.material = hasEnough ? gr.AnimUIMaterial : gr.AnimMaterialUIDesaturated;
				icon.color = hasEnough ? Color.white : INSUFFICIENT_TINT;
				icon.sprite = Def.GetUISprite(materialTag, "").first;
			}
			if (choice.TryGetComponent(out MultiToggle toggle)) {
				toggle.ChangeState(selectedMaterial ? 1 : 0);
				toggle.onClick += () => {
					// Do not use cached variables here as they will leak memory
					instance.selectedMaterialOption[index] = materialTag;
					instance.RefreshIngredientDescriptors();
					instance.RefreshQueueCountDisplay();
					instance.ownerScreen.RefreshQueueCountDisplayForRecipeCategory(instance.
						selectedRecipeCategoryID, instance.target);
				};
			}
			return selectedMaterial && hasEnough;
		}
		
		/// <summary>
		/// Updates the summary for each ingredient.
		/// </summary>
		/// <param name="hr">References the components to be updated.</param>
		/// <param name="index">The ingredient number in the recipe.</param>
		/// <param name="unknownResources">The resources that were required but not discovered.</param>
		/// <param name="knownCount">The number of required resources already discovered.</param>
		private static void UpdateSummary(HierarchyReferences hr, int index,
				ICollection<Tag> unknownResources, int knownCount) {
			int unknownCount = unknownResources.Count;
			var tooltip = hr.GetReference<ToolTip>("HeaderTooltip");
			var notDiscovered = hr.GetReference<RectTransform>("NoDiscoveredRow");
			var bg = hr.GetReference<Image>("HeaderBG");
			var text = CACHED_BUILDER;
			bool allUndiscovered = knownCount <= 0;
			string undiscovered;
			if (unknownCount > 0) {
				text.Clear();
				foreach (var tag in unknownResources)
					text.Append(Constants.TABBULLETSTRING).Append(TagManager.GetProperName(
						tag)).AppendLine();
				undiscovered = string.Format(STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.
					UNDISCOVERED_INGREDIENTS_IN_CATEGORY, text.ToString().TrimEnd());
			} else
				undiscovered = STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.
					ALL_INGREDIENTS_IN_CATEGORY_DISOVERED;
			tooltip.SetSimpleTooltip(undiscovered);
			notDiscovered.gameObject.SetActive(allUndiscovered);
			if (allUndiscovered && notDiscovered.TryGetComponent(out tooltip))
				tooltip.SetSimpleTooltip(undiscovered);
			bg.color = allUndiscovered ? UNDISCOVERED_COLOR : Color.clear;
			text.Clear().AppendFormat(STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.
				INGREDIENT_CATEGORY, index + 1);
			if (unknownCount > 0)
				text.Append(" <color=#bf5858>(").Append(knownCount).Append("/").
					Append(knownCount + unknownCount).Append(")").Append(
					UIConstants.ColorSuffix);
			hr.GetReference<LocText>("HeaderLabel").SetText(text.ToString());
		}

		/// <summary>
		/// Applied to SelectedRecipeQueueScreen to reuse pooled UI items from the last run.
		/// </summary>
		[HarmonyPatch(typeof(SelectedRecipeQueueScreen), nameof(SelectedRecipeQueueScreen.
			RefreshIngredientDescriptors))]
		internal static class RefreshIngredientDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before RefreshIngredientDescriptors runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(SelectedRecipeQueueScreen __instance) {
				var selected = ListPool<ComplexRecipe, SelectedRecipeQueueScreen>.Allocate();
				if (GetMatchingRecipes(__instance.target, __instance.selectedRecipeCategoryID,
						selected))
					RefreshIngredients(__instance, selected);
				else
					__instance.IngredientsDescriptorPanel.gameObject.SetActive(false);
				selected.Recycle();
				return false;
			}
		}

		/// <summary>
		/// Applied to SelectedRecipeQueueScreen to reuse pooled UI items from the last run.
		/// </summary>
		[HarmonyPatch(typeof(SelectedRecipeQueueScreen), nameof(SelectedRecipeQueueScreen.
			RefreshResultDescriptors))]
		internal static class RefreshResultDescriptors_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before RefreshResultDescriptors runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(SelectedRecipeQueueScreen __instance) {
				var products = ListPool<DescriptorWithSprite, SelectedRecipeQueueScreen>.
					Allocate();
				var recipe = GetFirstRecipe(__instance.target, __instance.
					selectedRecipeCategoryID);
				var parent = __instance.EffectsDescriptorPanel.gameObject;
				// AdditionalEffectsForRecipe is per fabricator
				var extraEffects = __instance.target.AdditionalEffectsForRecipe(recipe);
				int n = extraEffects.Count;
				GetResultDescriptions(recipe, products);
				for (int i = 0; i < n; i++)
					products.Add(new DescriptorWithSprite(extraEffects[i], null));
				if (products.Count > 0) {
					parent.SetActive(true);
					RemoveAndAddRows(products, parent, __instance);
				}
				products.Recycle();
				return false;
			}
		}

		/// <summary>
		/// Reduce argument counts by collecting loop invariant parameters into a single
		/// structure.
		/// </summary>
		private readonly struct RecipeState {
			/// <summary>
			/// Gets the world inventory (avoiding several potential crashes in the base game)
			/// for the specified object.
			/// </summary>
			/// <param name="target">The object to query.</param>
			/// <returns>The world inventory for that object's world, or null if it could not be obtained for any reason.</returns>
			private static WorldInventory GetWorldInventory(Component target) {
				WorldInventory inventory = null;
				if (target != null) {
					int cell = Grid.PosToCell(target.transform.position);
					if (Grid.IsValidCell(cell)) {
						int worldIndex = Grid.WorldIdx[cell];
						if (worldIndex != ClusterManager.INVALID_WORLD_IDX) {
							var world = ClusterManager.Instance.GetWorld(worldIndex);
							if (world != null)
								inventory = world.worldInventory;
						}
					}
				}
				return inventory;
			}

			/// <summary>
			/// The active side screen.
			/// </summary>
			public readonly SelectedRecipeQueueScreen instance;

			/// <summary>
			/// A cached map to simplify looking up the number of times an ingredient is used.
			/// </summary>
			public readonly IDictionary<Tag, int> ingredientCounts;

			/// <summary>
			/// The world inventory for the active fabricator.
			/// </summary>
			public readonly WorldInventory inventory;

			public RecipeState(SelectedRecipeQueueScreen instance,
					IDictionary<Tag, int> ingredientCounts) {
				this.instance = instance;
				this.ingredientCounts = ingredientCounts;
				inventory = GetWorldInventory(instance.target);
			}
			
			/// <summary>
			/// Instead of the inefficient ComplexFabricator.GetIngredientQueueCount, build a
			/// table with the number of queued recipes by ingredient tag.
			/// </summary>
			/// <param name="target">The fabricator to query.</param>
			/// <param name="recipes">The selected recipes.</param>
			public void CountIngredientQueue(IList<ComplexRecipe> recipes) {
				int rc = recipes.Count;
				var counts = instance.target.recipeQueueCounts;
				for (int i = 0; i < rc; i++) {
					var recipe = recipes[i];
					if (counts.TryGetValue(recipe.id, out int count) && count > 0) {
						var ingredients = recipe.ingredients;
						int ic = ingredients.Length;
						for (int j = 0; j < ic; j++) {
							var tag = ingredients[j].material;
							if (!ingredientCounts.TryGetValue(tag, out int total))
								total = 0;
							ingredientCounts[tag] = total + count;
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Applied to ComplexFabricator to return a pooled list instead of allocating a new one.
	/// </summary>
	[HarmonyPatch(typeof(ComplexFabricator), nameof(ComplexFabricator.
		AdditionalEffectsForRecipe))]
	public static class ComplexFabricator_AdditionalEffectsForRecipe_Patch {
		/// <summary>
		/// The cached list to report each time it is used. The metal refinery uses the result
		/// of the base method and appends, so this list will be reused.
		/// </summary>
		private static readonly List<Descriptor> CACHED_DESC = new List<Descriptor>(4);

		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Applied before AdditionalEffectsForRecipe runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref List<Descriptor> __result) {
			var desc = CACHED_DESC;
			desc.Clear();
			__result = desc;
			return false;
		}
	}

	/// <summary>
	/// Updates the complex fabricator side screen much less often.
	/// </summary>
	internal static class ComplexFabricatorUpdater {
		/// <summary>
		/// How often to actually update the side screen in seconds, unscaled.
		/// </summary>
		private const double INTERVAL = 0.2;

		/// <summary>
		/// The color used when ingredients are unavailable.
		/// </summary>
		private static readonly Color UNAVAILABLE = new Color(0.22f, 0.22f, 0.22f, 1f);

		/// <summary>
		/// The next unscaled time when the recipe ingredients will be rechecked.
		/// </summary>
		private static double nextUpdateTime;

		/// <summary>
		/// Checks for sufficient materials to complete the recipe.
		/// </summary>
		/// <param name="inventory">The inventory to search for materials.</param>
		/// <param name="fabricator">The complex fabricator that is creating the recipe.</param>
		/// <param name="recipe">The recipe to look up.</param>
		/// <returns>true if the recipe has enough materials, or false otherwise.</returns>
		private static bool HasAllRecipeRequirements(WorldInventory inventory,
				ComplexFabricator fabricator, ComplexRecipe recipe) {
			bool hasAll = true;
			var ingredients = recipe.ingredients;
			int n = ingredients.Length;
			var bannedTags = fabricator.ForbiddenTags;
			for (int i = 0; i < n && hasAll; i++) {
				var item = ingredients[i];
				hasAll = inventory.GetAmountWithoutTag(item.material, true, bannedTags) +
					fabricator.inStorage.GetAmountAvailable(item.material, bannedTags) +
					fabricator.buildStorage.GetAmountAvailable(item.material, bannedTags) >=
					item.amount;
			}
			return hasAll;
		}

		/// <summary>
		/// Updates the ingredients in complex fabricators.
		/// </summary>
		/// <param name="instance">The side screen to update.</param>
		/// <param name="fabricator">The curently selected complex fabricator.</param>
		/// <param name="inventory">The world inventory to search.</param>
		internal static void UpdateIngredients(ComplexFabricatorSideScreen instance,
				ComplexFabricator fabricator, WorldInventory inventory) {
			foreach (var pair in instance.recipeCategoryToggleMap) {
				var go = pair.Key;
				var recipes = pair.Value;
				int n = recipes.Count;
				if (go.TryGetComponent(out HierarchyReferences refs) && go.TryGetComponent(
						out KToggle toggle) && n > 0) {
					Color color;
					// The base game crashes if the list is empty (which is probably
					// impossible), so it is fine to do nothing if empty
					bool hasAll = false, match = recipes[0].recipeCategoryID == instance.
						selectedRecipeCategory;
					for (int i = 0; i < n && !hasAll; i++)
						hasAll = HasAllRecipeRequirements(inventory, fabricator, recipes[i]);
					if (hasAll) {
						color = Color.black;
						toggle.ActivateFlourish(match, match ? ImageToggleState.State.Active :
							ImageToggleState.State.Inactive);
					} else {
						color = UNAVAILABLE;
						toggle.ActivateFlourish(match, match ? ImageToggleState.State.
							DisabledActive : ImageToggleState.State.Disabled);
					}
					refs.GetReference<LocText>("Label").color = color;
				}
			}
		}

		/// <summary>
		/// Applied to ComplexFabricatorSideScreen to force an update when the fabricator
		/// side screen is first opened.
		/// </summary>
		[HarmonyPatch(typeof(ComplexFabricatorSideScreen), nameof(ComplexFabricatorSideScreen.
			Initialize))]
		public static class Initialize_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				nextUpdateTime = Time.timeAsDouble;
			}
		}

		/// <summary>
		/// Applied to ComplexFabricatorSideScreen to optimze updating the recipe screen.
		/// </summary>
		[HarmonyPatch(typeof(ComplexFabricatorSideScreen), nameof(ComplexFabricatorSideScreen.
			RefreshIngredientAvailabilityVis))]
		public static class RefreshIngredientAvailabilityVis_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

			/// <summary>
			/// Applied before RefreshIngredientAvailabilityVis runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(ComplexFabricatorSideScreen __instance) {
				var target = __instance.targetFab;
				int cell;
				byte worldIndex;
				double now = Time.timeAsDouble;
				if (target != null && now >= nextUpdateTime && Grid.IsValidCell(cell =
						Grid.PosToCell(target.transform.position)) && (worldIndex = Grid.
						WorldIdx[cell]) != ClusterManager.INVALID_WORLD_IDX) {
					var world = ClusterManager.Instance.GetWorld(worldIndex);
					if (world != null)
						UpdateIngredients(__instance, target, world.worldInventory);
					nextUpdateTime = now + INTERVAL;
				}
				return false;
			}
		}
	}
}
