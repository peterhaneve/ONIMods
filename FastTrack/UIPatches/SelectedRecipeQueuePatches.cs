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

		private static string RECIPE_REQUIREMENT_UNAVAILABLE;

		/// <summary>
		/// Gets the ingredients required for the recipe.
		/// </summary>
		/// <param name="recipe">The recipe to describe.</param>
		/// <param name="target">The fabricator requesting the recipe.</param>
		/// <param name="result">The location where the ingredients will be stored.</param>
		private static void GetIngredientDescriptions(ComplexRecipe recipe,
				KMonoBehaviour target, ICollection<DescriptorWithSprite> result) {
			var ingredients = recipe.ingredients;
			int n = ingredients.Length;
			var inventory = target.GetMyWorld().worldInventory;
			string template = RECIPE_REQUIREMENT;
			var text = CACHED_BUILDER;
			for (int i = 0; i < n; i++) {
				var item = ingredients[i];
				var material = item.material;
				var prefab = Assets.GetPrefab(material);
				float available = inventory.GetAmount(material, true);
				string name = prefab.GetProperName(), amountText = GameUtil.GetFormattedByTag(
					material, item.amount), availableText = GameUtil.GetFormattedByTag(material,
					available);
				bool hasEnough = available >= item.amount;
				if (template == null) {
					text.Clear();
					if (!hasEnough)
						text.Append(UIConstants.ColorPrefixRed);
					text.Append(name).Append(": ").Append(amountText).Append(" / ").Append(
						availableText);
					if (!hasEnough)
						text.Append(UIConstants.ColorSuffix);
				} else
					text.Clear().Append(hasEnough ? RECIPE_REQUIREMENT :
						RECIPE_REQUIREMENT_UNAVAILABLE).Replace("{0}", prefab.GetProperName()).
						Replace("{1}", amountText).Replace("{2}", availableText);
				string description = text.ToString();
				result.Add(new DescriptorWithSprite(new Descriptor(description, description,
					Descriptor.DescriptorType.Requirement), Def.GetUISprite(material),
					prefab.TryGetComponent(out MutantPlant _)));
			}
			if (recipe.consumedHEP > 0 && target.TryGetComponent(
					out HighEnergyParticleStorage component)) {
				string consumed = recipe.consumedHEP.ToString();
				text.Clear().Append(consumed).Append(" / ");
				component.Particles.ToRyuHardString(text, 1);
				string requirements = HEP_FORMAT + text;
				// The concatenated strings had to be created anyways, so manipulating the
				// string builder again does not save memory
				result.Add(new DescriptorWithSprite(new Descriptor(requirements,
					requirements, Descriptor.DescriptorType.Requirement), RADBOLT_ICON));
			}
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
			string template = RECIPE_PRODUCT;
			var text = CACHED_BUILDER;
			if (recipe.producedHEP > 0) {
				text.Clear().Append(HEP_TOOLTIP).Append(recipe.producedHEP.ToString());
				string description = text.ToString();
				result.Add(new DescriptorWithSprite(new Descriptor(description, description,
					Descriptor.DescriptorType.Requirement), RADBOLT_ICON));
			}
			var desc = ListPool<Descriptor, SelectedRecipeQueueScreen>.Allocate();
			for (int i = 0; i < n; i++) {
				var item = results[i];
				var material = item.material;
				var prefab = Assets.GetPrefab(material);
				string amountText = GameUtil.GetFormattedByTag(material, item.amount),
					name = TagManager.GetProperName(material), description, facade = item.
					facadeID;
				var element = ElementLoader.GetElement(material);
				string product = string.IsNullOrWhiteSpace(facade) ? name : TagManager.
					GetProperName(facade);
				desc.Clear();
				if (template == null)
					description = text.Clear().Append(product).Append(": ").Append(amountText).
						ToString();
				else
					description = text.Clear().Append(template).Replace("{0}", product).
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
			string req = STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPERQUIREMENT;
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
			if (req == "{0}: {1} / {2}") {
				RECIPE_REQUIREMENT = null;
				RECIPE_REQUIREMENT_UNAVAILABLE = null;
			} else {
				RECIPE_REQUIREMENT = req;
				RECIPE_REQUIREMENT_UNAVAILABLE = UIConstants.ColorPrefixRed +
					RECIPE_REQUIREMENT + UIConstants.ColorSuffix;
			}
		}

		/// <summary>
		/// Replaces descriptor effect rows, reusing old rows if possible, with new effects.
		/// </summary>
		/// <param name="items">The effect descriptors to display.</param>
		/// <param name="parent">The parent container for the displayed information.</param>
		/// <param name="prefab">The object to be cloned if additional descriptors are required.</param>
		/// <param name="rows">The existing descriptor rows to reuse.</param>
		private static void RemoveAndAddRows(IList<DescriptorWithSprite> items,
				GameObject parent, GameObject prefab,
				IDictionary<DescriptorWithSprite, GameObject> rows) {
			var oldRows = ListPool<GameObject, SelectedRecipeQueueScreen>.Allocate();
			var text = CACHED_BUILDER;
			int n = items.Count, nr = rows.Count;
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
					int indent = descriptor.indent;
					// This is quicker than reallocating strings over and over in GetIndented()
					text.Clear();
					for (int j = 0; j < indent; j++)
						text.Append(Constants.TABSTRING);
					text.Append(descriptor.text);
					hr.GetReference<LocText>("Label").SetText(text);
					if (sprite == null) {
						icon.sprite = null;
						icon.color = Color.white;
					} else {
						icon.sprite = sprite.first;
						icon.color = sprite.second;
					}
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
				var ingredients = ListPool<DescriptorWithSprite, SelectedRecipeQueueScreen>.
					Allocate();
				var recipe = __instance.selectedRecipe;
				var parent = __instance.IngredientsDescriptorPanel.gameObject;
				GetIngredientDescriptions(recipe, __instance.target, ingredients);
				parent.SetActive(true);
				RemoveAndAddRows(ingredients, parent, __instance.recipeElementDescriptorPrefab,
					__instance.recipeIngredientDescriptorRows);
				ingredients.Recycle();
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
				var recipe = __instance.selectedRecipe;
				var parent = __instance.EffectsDescriptorPanel.gameObject;
				// AdditionalEffectsForRecipe is per fabricator
				var extraEffects = __instance.target.AdditionalEffectsForRecipe(recipe);
				int n = extraEffects.Count;
				GetResultDescriptions(recipe, products);
				for (int i = 0; i < n; i++)
					products.Add(new DescriptorWithSprite(extraEffects[i], null));
				if (products.Count > 0) {
					parent.SetActive(true);
					RemoveAndAddRows(products, parent, __instance.
						recipeElementDescriptorPrefab, __instance.recipeEffectsDescriptorRows);
				}
				products.Recycle();
				return false;
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
			foreach (var pair in instance.recipeMap) {
				var go = pair.Key;
				var recipe = pair.Value;
				if (go.TryGetComponent(out HierarchyReferences refs) && go.TryGetComponent(
					out KToggle toggle)) {
					Color color;
					bool hasAll = HasAllRecipeRequirements(inventory, fabricator, recipe),
						match = instance.selectedRecipe == recipe;
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
