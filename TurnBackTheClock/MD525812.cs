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
using Klei.CustomSettings;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using UnityEngine;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.TurnBackTheClock {
	/// <summary>
	/// Patches for MD-525812: Sweet Dreams.
	/// </summary>
	internal static class MD525812 {
		/// <summary>
		/// Runs after the Db is initialized.
		/// </summary>
		/// <param name="harmony">The Harmony instance to patch.</param>
		internal static void AfterDbInit(Harmony harmony) {
			if (TurnBackTheClockOptions.Instance.MD525812_DisableBuildings) {
				harmony.Patch(typeof(SpiceGrinderConfig), nameof(IBuildingConfig.
					CreateBuildingDef), postfix: new HarmonyMethod(typeof(MD525812),
					nameof(DisableSpiceGrinder)));
				harmony.Patch(typeof(SpiceGrinderConfig), nameof(IBuildingConfig.
					DoPostConfigureComplete), postfix: new HarmonyMethod(typeof(MD525812),
					nameof(FixSpiceGrinderRoom)));
			}
		}

		/// <summary>
		/// Applied to SpiceGrinderConfig to disable it when MD-525812 buildings are turned
		/// off.
		/// </summary>
		private static void DisableSpiceGrinder(BuildingDef __result) {
			__result.Deprecated = true;
		}

		/// <summary>
		/// Applied to SpiceGrinderConfig to remove references to the kitchen room.
		/// </summary>
		private static void FixSpiceGrinderRoom(GameObject go) {
			if (go.TryGetComponent(out RoomTracker tracker))
				Object.Destroy(tracker);
		}

		// No plug slugs in vanilla, duh
		#if false
		/// <summary>
		/// Adds drowning chores to the creature chore table, after stunned states.
		/// </summary>
		/// <param name="instance">The chore table to modify.</param>
		/// <param name="def">The def to be added.</param>
		/// <param name="condition">The condition to add the def.</param>
		/// <param name="forcePriority">The priority to use, or -1 to use the default.</param>
		/// <returns>The chore table for call chaining.</returns>
		private static ChoreTable.Builder AddDrownStates(ChoreTable.Builder instance,
				StateMachine.BaseDef def, bool condition, int forcePriority) {
			if (def is DebugGoToStates.Def)
				instance.Add(new DrowningStates.Def());
			return instance.Add(def, condition, forcePriority);
		}

		/// <summary>
		/// Applied to BaseStaterpillarConfig to add drowning chores to Plug Slugs.
		/// </summary>
		[HarmonyPatch(typeof(BaseStaterpillarConfig), nameof(BaseStaterpillarConfig.
			BaseStaterpillar))]
		public static class BaseStaterpillarConfig_BaseStaterpillar_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_SlugsDrown;

			internal static TranspiledMethod Transpiler(TranspiledMethod method) {
				return PPatchTools.ReplaceMethodCallSafe(method, typeof(ChoreTable.Builder).
					GetMethodSafe(nameof(ChoreTable.Builder.Add), false, typeof(StateMachine.
					BaseDef), typeof(bool), typeof(int)), typeof(MD525812).GetMethodSafe(
					nameof(AddDrownStates), true, PPatchTools.AnyArguments));
			}
		}

		/// <summary>
		/// Applied to BaseStaterpillarConfig to remove refined metal from Plug Slug diets.
		/// </summary>
		[HarmonyPatch(typeof(BaseStaterpillarConfig), nameof(BaseStaterpillarConfig.
			RefinedMetalDiet))]
		public static class BaseStaterpillarConfig_RefinedMetalDiet_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_SlugDiet;

			internal static void Postfix(List<Diet.Info> __result) {
				__result.Clear();
			}
		}
		#endif

		/// <summary>
		/// Applied to CustomGameSettings to always disable story traits.
		/// </summary>
		[HarmonyPatch(typeof(CustomGameSettings), nameof(CustomGameSettings.AddStorySettingConfig))]
		public static class CustomGameSettings_AddStorySettingConfig_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_DisableStoryTraits;

			internal static void Postfix(IDictionary<string, string> ___currentStoryLevelsBySetting,
					SettingConfig config) {
				if (___currentStoryLevelsBySetting != null)
					___currentStoryLevelsBySetting[config.id] = CustomGameSettings.
						STORY_DISABLED_LEVEL;
			}
		}

		/// <summary>
		/// Applied to CustomGameSettings to always disable story traits.
		/// </summary>
		[HarmonyPatch(typeof(CustomGameSettings), nameof(CustomGameSettings.SetStorySetting),
			typeof(SettingConfig), typeof(bool))]
		public static class CustomGameSettings_SetStorySetting_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_DisableStoryTraits;

			internal static void Prefix(ref bool value) {
				value = false;
			}
		}

		#if false
		/// <summary>
		/// Applied to ModifierSet to make Plug Slugs unable to morph (thus disabling
		/// the smog and sponge variants).
		/// </summary>
		[HarmonyPatch(typeof(ModifierSet), nameof(ModifierSet.CreateFertilityModifier))]
		public static class ModifierSet_CreateFertilityModifier_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_SlugsDrown;

			internal static bool Prefix(Tag targetTag) {
				string tagName = targetTag.Name;
				return tagName != StaterpillarGasConfig.EGG_ID && tagName !=
					StaterpillarLiquidConfig.EGG_ID;
			}
		}
		#endif

		/// <summary>
		/// Applied to RoomTypes to disable the Kitchen when MD-525812 buildings are
		/// turned off.
		/// </summary>
		[HarmonyPatch(typeof(Database.RoomTypes), MethodType.Constructor,
			typeof(ResourceSet))]
		public static class RoomTypes_Constructor_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_DisableBuildings;

			internal static void Postfix(Database.RoomTypes __instance) {
				__instance.Remove(__instance.Kitchen);
			}
		}

		#if false
		/// <summary>
		/// Applied to StaterpillarConfig to make Plug Slugs unable to morph (thus disabling
		/// the smog and sponge variants).
		/// </summary>
		[HarmonyPatch(typeof(StaterpillarConfig), nameof(StaterpillarConfig.CreatePrefab))]
		public static class StaterpillarConfig_CreatePrefab_Patch {
			internal static bool Prepare() => TurnBackTheClockOptions.Instance.
				MD525812_SlugsDrown;

			internal static void Prefix() {
				var chances = StaterpillarTuning.EGG_CHANCES_BASE;
				chances.Clear();
				chances.Add(new FertilityMonitor.BreedingChance {
					egg = StaterpillarConfig.EGG_ID.ToTag(), weight = 1.00f
				});
			}
		}
		#endif
	}
}
