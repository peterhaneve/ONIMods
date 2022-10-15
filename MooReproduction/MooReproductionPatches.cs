/*
 * Copyright 2022 Peter Han
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
using Klei.AI;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;
using UnityEngine;

using HappyState = HappinessMonitor.HappyState;
using IdleAnimCallback = IdleStates.Def.IdleAnimCallback;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// Patches which will be applied via annotations for Moo Reproduction.
	/// </summary>
	public sealed class MooReproductionPatches : KMod.UserMod2 {
		/// <summary>
		/// A tag with the Gassy Moo's ID.
		/// </summary>
		private static readonly Tag MOO_TAG = new Tag(MooConfig.ID);

		/// <summary>
		/// The happy state, which has its effect modified depending on the source critter.
		/// </summary>
		private static readonly IDetouredField<HappinessMonitor, HappyState>
			HAPPY_TAME_STATE = PDetours.DetourField<HappinessMonitor, HappyState>("happy");

		/// <summary>
		/// The happy state, which has its effect modified depending on the source critter.
		/// </summary>
		private static readonly IDetouredField<HappinessMonitor, Effect>
			HAPPY_TAME_EFFECT = PDetours.DetourField<HappinessMonitor, Effect>("happyTameEffect");

		/// <summary>
		/// The Gassy Moo's custom idle animation.
		/// </summary>
		private static readonly IdleAnimCallback MOO_IDLE_ANIM = typeof(BaseMooConfig).
			Detour<IdleAnimCallback>("CustomIdleAnim");

		/// <summary>
		/// Disables Mooteor showers if the option is set.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			if (MooReproductionOptions.Instance.DisableMooMeteors)
				Db.Get().GameplaySeasons.GassyMooteorShowers.numEventsToStartEachPeriod = 0;
		}

		/// <summary>
		/// Adds the additional Moo chores required for growth. Growing up and giving birth
		/// chores are required.
		/// </summary>
		/// <param name="prefab">The prefab to add chores.</param>
		/// <param name="baby">true for babies (which cannot be ranched) or false for adults.</param>
		internal static void UpdateMooChores(GameObject prefab, bool baby) {
			var cc = prefab.GetComponent<ChoreConsumer>();
			var newChoreTable = new ChoreTable.Builder().
				Add(new DeathStates.Def()).
				Add(new AnimInterruptStates.Def()).
				Add(new BaggedStates.Def()).
				Add(new StunnedStates.Def()).
				Add(new DebugGoToStates.Def()).
				Add(new DrowningStates.Def()).
				Add(new MooGrowUpStates.Def()).
				PushInterruptGroup().
				Add(new CreatureSleepStates.Def()).
				Add(new FixedCaptureStates.Def()).
				Add(new RanchedStates.Def(), !baby).
				Add(new GiveBirthStates.Def()).
				Add(new EatStates.Def()).
				Add(new PlayAnimsStates.Def(GameTags.Creatures.Poop, false, "poop", STRINGS.CREATURES.STATUSITEMS.EXPELLING_GAS.NAME, STRINGS.CREATURES.STATUSITEMS.EXPELLING_GAS.TOOLTIP)).
				Add(new MoveToLureStates.Def()).
				PopInterruptGroup().
				Add(new IdleStates.Def {
					customIdleAnim = MOO_IDLE_ANIM
				});
			if (cc != null)
				cc.choreTable = newChoreTable.CreateTable();
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			LocString.CreateLocStringKeys(typeof(MooReproductionStrings.CREATURES));
			LocString.CreateLocStringKeys(typeof(MooReproductionStrings.UI));
			new PLocalization().Register();
			new PPatchManager(harmony).RegisterPatchClass(typeof(MooReproductionPatches));
			new POptions().RegisterOptions(this, typeof(MooReproductionOptions));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to AnimInterruptMonitor.Instance to avoid playing the nonexistant
		/// growing up animation on Gassy Moo babies.
		/// </summary>
		[HarmonyPatch(typeof(AnimInterruptMonitor.Instance), nameof(AnimInterruptMonitor.
			Instance.PlayAnim))]
		public static class AnimInterruptMonitor_Instance_PlayAnim_Patch {
			public static readonly HashedString GROWUP_ANIM = "growup_pst";

			/// <summary>
			/// Applied before PlayAnim runs.
			/// </summary>
			internal static bool Prefix(AnimInterruptMonitor.Instance __instance,
					HashedString anim) {
				var go = __instance.gameObject;
				return !anim.IsValid || anim != GROWUP_ANIM || go == null || go.PrefabID() !=
					MOO_TAG;
			}
		}

		/// <summary>
		/// Applied to HappinessMonitor to adjust the happy egg multiplier (default 10x -> 16
		/// eggs vs 1.6) on Gassy Moos to 5x (8 births).
		/// </summary>
		[HarmonyPatch(typeof(HappinessMonitor), nameof(HappinessMonitor.InitializeStates))]
		public static class HappinessMonitor_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(HappinessMonitor __instance) {
				var newEffect = new Effect("Happy", STRINGS.CREATURES.MODIFIERS.HAPPY.
					NAME, STRINGS.CREATURES.MODIFIERS.HAPPY.TOOLTIP, 0f, true, false, false);
				newEffect.Add(new AttributeModifier(Db.Get().Amounts.Fertility.
					deltaAttribute.Id, 4f, STRINGS.CREATURES.MODIFIERS.HAPPY.NAME, true));
				if (__instance != null) {
					var happyState = HAPPY_TAME_STATE.Get(__instance);
					var happyTameEffect = HAPPY_TAME_EFFECT.Get(__instance);
					if (happyState?.tame != null && happyTameEffect != null) {
						// Remove default effect, and toggle a specific effect
						var tame = happyState.tame;
						tame.ClearEnterActions();
						tame.ClearExitActions();
						tame.ToggleEffect((smi) => {
							var obj = smi.master.gameObject;
							return (obj != null && obj.PrefabID() == MOO_TAG) ? newEffect :
								happyTameEffect;
						});
					}
				}
			}
		}

		/// <summary>
		/// Applied to MooConfig to add a reproduction monitor to Gassy Moos.
		/// </summary>
		[HarmonyPatch(typeof(MooConfig), nameof(MooConfig.CreatePrefab))]
		public static class MooConfig_CreatePrefab_Patch {
			/// <summary>
			/// Applied after CreatePrefab runs.
			/// </summary>
			internal static void Postfix(GameObject __result) {
				var prefabID = __result.GetComponent<KPrefabID>();
				// ExtendEntityToFertileCreature requires an egg prefab
				var fm = __result.AddOrGetDef<LiveFertilityMonitor.Def>();
				fm.initialBreedingWeights = new List<FertilityMonitor.BreedingChance>() {
					new FertilityMonitor.BreedingChance() {
						egg = BabyMooConfig.ID_TAG,
						weight = 1.0f
					}
				};
				// Reduce to 2kg meat for adult
				var butcherable = __result.GetComponent<Butcherable>();
				if (butcherable != null)
					butcherable.SetDrops(new string[] { MeatConfig.ID, MeatConfig.ID });
				// Hardcoded in CreateMoo, 6/10ths of the max age
				fm.baseFertileCycles = 45.0f;
				prefabID.prefabSpawnFn += (inst) => {
					// Needs to be changed for vanilla
					DiscoveredResources.Instance.Discover(BabyMooConfig.ID_TAG,
						DiscoveredResources.GetCategoryForTags(prefabID.Tags));
				};
				UpdateMooChores(__result, false);
			}
		}
	}
}
