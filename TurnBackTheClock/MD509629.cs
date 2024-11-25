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
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// Patches for MD-509629: Fast Friends.
	/// </summary>
	internal static class MD509629 {
		/// <summary>
		/// Applied to BaseSquirrelConfig to make Pip unable to eat Thimble Reed.
		/// </summary>
		[HarmonyPatch(typeof(BaseSquirrelConfig), nameof(BaseSquirrelConfig.BasicDiet))]
		public static class BaseSquirrelConfig_BasicDiet_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableCreatures;

			internal static void Postfix(Diet.Info[] __result) {
				var oldInfo = __result[0];
				__result[0] = new Diet.Info(new HashSet<Tag> {
					"ForestTree"
				}, oldInfo.producedElement, oldInfo.caloriesPerKg, oldInfo.
					producedConversionRate, null, oldInfo.diseasePerKgProduced, oldInfo.
					produceSolidTile, oldInfo.foodType, oldInfo.emmitDiseaseOnCell,
					oldInfo.eatAnims);
			}
		}
		
		/// <summary>
		/// Applied to ClothingAlterationStationConfig to disable it when MD-509629 buildings
		/// are turned off.
		/// </summary>
		[HarmonyPatch(typeof(ClothingAlterationStationConfig), nameof(IBuildingConfig.
			CreateBuildingDef))]
		public static class ClothingAlterationStationConfig_CreateBuildingDef_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableBuildings;

			internal static void Postfix(BuildingDef __result) {
				__result.Deprecated = true;
			}
		}

		/// <summary>
		/// Applied to CrabConfig to make Pokeshell unable to morph (thus disabling the Oakshell
		/// and Sanishell).
		/// </summary>
		[HarmonyPatch(typeof(CrabConfig), nameof(CrabConfig.CreatePrefab))]
		public static class CrabConfig_CreatePrefab_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableCreatures;

			internal static void Prefix() {
				var chances = CrabTuning.EGG_CHANCES_BASE;
				chances.Clear();
				chances.Add(new FertilityMonitor.BreedingChance {
					egg = CrabConfig.EGG_ID.ToTag(), weight = 1.00f
				});
			}
		}
		
		/// <summary>
		/// Applied to GourmetCookingStationConfig to remove the Curried Beans recipe.
		/// BUG: recipe overwrites the spicy tofu recipe
		/// </summary>
		[HarmonyPatch(typeof(GourmetCookingStationConfig), "ConfigureRecipes")]
		public static class GourmetCookingStationConfig_CreatePrefab_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableFood;

			internal static void Postfix() {
				var recipes = ComplexRecipeManager.Get().recipes;
				recipes.RemoveAll((recipe) => recipe.id.Contains(GourmetCookingStationConfig.
					ID) && recipe.id.EndsWith(CurryConfig.ID));
			}
		}
		
		/// <summary>
		/// Applied to Immigration to remove care packages of Primo Garb.
		/// </summary>
		[HarmonyPatch]
		public static class Immigration_ConfigureCarePackages_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableBuildings;

			internal static IEnumerable<System.Reflection.MethodBase> TargetMethods() {
				yield return typeof(Immigration).GetMethodSafe("ConfigureBaseGameCarePackages",
					false, PPatchTools.AnyArguments);
				yield return typeof(Immigration).GetMethodSafe(
					"ConfigureMultiWorldCarePackages", false, PPatchTools.AnyArguments);
			}

			internal static void Postfix(ref CarePackageInfo[] ___carePackages) {
				var packages = ___carePackages;
				if (packages != null) {
					int n = packages.Length;
					var newPackages = new List<CarePackageInfo>(n);
					for (int i = 0; i < n; i++) {
						var package = packages[i];
						if (package != null && package.id != CustomClothingConfig.ID)
							newPackages.Add(package);
					}
					___carePackages = newPackages.ToArray();
				}
			}
		}

		/// <summary>
		/// Applied to ModifierSet to make Pokeshells, Shove Voles and Pips unable to morph
		/// (thus disabling the Oakshell, Sanishell, Cuddle Pip, and Delecta Vole).
		/// </summary>
		[HarmonyPatch(typeof(ModifierSet), nameof(ModifierSet.CreateFertilityModifier))]
		public static class ModifierSet_CreateFertilityModifier_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableCreatures;

			internal static bool Prefix(Tag targetTag) {
				string tagName = targetTag.Name;
				return tagName != MoleConfig.EGG_ID && tagName != SquirrelConfig.EGG_ID &&
					tagName != CrabConfig.EGG_ID;
			}
		}

		/// <summary>
		/// Applied to MoleConfig to make Shove Vole unable to morph (thus disabling the Delecta
		/// Vole).
		/// </summary>
		[HarmonyPatch(typeof(MoleConfig), nameof(MoleConfig.CreatePrefab))]
		public static class MoleConfig_CreatePrefab_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableCreatures;

			internal static void Prefix() {
				var chances = MoleTuning.EGG_CHANCES_BASE;
				chances.Clear();
				chances.Add(new FertilityMonitor.BreedingChance {
					egg = MoleConfig.EGG_ID.ToTag(), weight = 1.00f
				});
			}
		}

		/// <summary>
		/// Applied to RoomTypes to disable the Kitchen when MD-525812 buildings are
		/// turned off.
		/// </summary>
		[HarmonyPatch(typeof(Database.Personalities), MethodType.Constructor, new Type[0])]
		public static class Personalities_Constructor_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableDuplicants;

			/// <summary>
			/// Removes a Duplicant personality.
			/// </summary>
			/// <param name="instance">The personalities to alter.</param>
			/// <param name="name">The name of the Duplicant to remove.</param>
			private static void RemoveDuplicant(Database.Personalities instance,
					string name) {
				// The base game uses the incorrect uppercase version as well
				var dupe = instance.TryGet(name.ToUpper());
				if (dupe != null)
					instance.Remove(dupe);
			}

			internal static void Postfix(Database.Personalities __instance) {
				RemoveDuplicant(__instance, STRINGS.DUPLICANTS.PERSONALITIES.AMARI.NAME);
				RemoveDuplicant(__instance, STRINGS.DUPLICANTS.PERSONALITIES.PEI.NAME);
				RemoveDuplicant(__instance, STRINGS.DUPLICANTS.PERSONALITIES.QUINN.NAME);
				RemoveDuplicant(__instance, STRINGS.DUPLICANTS.PERSONALITIES.STEVE.NAME);
			}
		}

		/// <summary>
		/// Applied to SquirrelConfig to make Pip unable to morph (thus disabling the Cuddle
		/// Pip :cry: ).
		/// </summary>
		[HarmonyPatch(typeof(SquirrelConfig), nameof(SquirrelConfig.CreatePrefab))]
		public static class SquirrelConfig_CreatePrefab_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD509629_DisableCreatures;

			internal static void Prefix() {
				var chances = SquirrelTuning.EGG_CHANCES_BASE;
				chances.Clear();
				chances.Add(new FertilityMonitor.BreedingChance {
					egg = SquirrelConfig.EGG_ID.ToTag(), weight = 1.00f
				});
			}
		}
	}
}
