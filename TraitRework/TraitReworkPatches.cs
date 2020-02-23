/*
 * Copyright 2020 Peter Han
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

using Harmony;
using Klei.AI;
using PeterHan.PLib;
using ReimaginationTeam.Reimagination;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.TraitRework {
	/// <summary>
	/// Patches which will be applied via annotations for Trait Rework.
	/// </summary>
	public static class TraitReworkPatches {
		/// <summary>
		/// The modifier applied when eating in a lit area.
		/// </summary>
		internal static AttributeModifier EAT_LIT_MODIFIER;

		/// <summary>
		/// The minimum version where "lit workspace" eating was fixed in the stock game.
		/// </summary>
		private const uint VERSION_DISABLE_LITEATING = 379337U;

		/// <summary>
		/// Adds a short trait description for the embark screen.
		/// </summary>
		/// <param name="traitID">The trait ID to add.</param>
		/// <param name="shortDesc">The description to display.</param>
		/// <param name="tooltip">The tooltip to display on that description.</param>
		private static void AddShortDesc(string traitID, string shortDesc, string tooltip) {
			string key = "STRINGS.DUPLICANTS.TRAITS." + traitID.ToUpperInvariant() +
				".SHORT_DESC";
			Strings.Add(key, shortDesc);
			Strings.Add(key + "_TOOLTIP", tooltip);
		}

		/// <summary>
		/// Adjusts trait strings.
		/// </summary>
		private static void InitStrings() {
			// Maximum HP description is in a different category than the embark screen looks
			Strings.Add("STRINGS.DUPLICANTS.ATTRIBUTES.HITPOINTSMAX.NAME", STRINGS.DUPLICANTS.
				STATS.HITPOINTS.NAME);
			Strings.Add("STRINGS.DUPLICANTS.ATTRIBUTES.HITPOINTSMAX.TOOLTIP", STRINGS.
				DUPLICANTS.STATS.HITPOINTS.TOOLTIP);
			// Short descriptions
			AddShortDesc("CantCook", TraitStrings.CANTCOOK_SHORTDESC, TraitStrings.
				NOFOOD_SHORTTOOLTIP);
			AddShortDesc("Narcolepsy", TraitStrings.NARCOLEPSY_SHORTDESC, TraitStrings.
				NARCOLEPSY_TOOLTIP);
			AddShortDesc("ScaredyCat", TraitStrings.SCAREDYCAT_SHORTDESC, TraitStrings.
				NOFOOD_SHORTTOOLTIP);
			// Trait description updates, only public fields desired anyways
			var traitsClass = typeof(STRINGS.DUPLICANTS.TRAITS);
			foreach (var field in typeof(TraitStrings.TraitDescriptions).GetFields())
				if (field.FieldType == typeof(LocString)) {
					string name = field.Name;
					// This is done before TRAITS tuning class is initialized
					var traitClass = traitsClass.GetNestedType(name);
					try {
						if (traitClass != null && (field.GetValue(null) is LocString initValue))
							Traverse.Create(traitClass).SetField("DESC", initValue);
					} catch (FieldAccessException e) {
						// Should be unreachable, only public fields
						PUtil.LogExcWarn(e);
					} catch (TargetException e) {
						PUtil.LogExcWarn(e);
					}
				}
		}

		public static void OnLoad() {
			// Defined in Edible
			const float BASE_EAT_RATE = 50000.0f;
			ImaginationLoader.Init(typeof(TraitReworkPatches));
			InitStrings();
			// Create modifier for "Eating in lit area"
			EAT_LIT_MODIFIER = new AttributeModifier("CaloriesDelta", BASE_EAT_RATE * (1.0f /
				(1.0f - TraitTuning.EAT_SPEED_BUFF) - 1.0f), TraitStrings.EATING_LIT);
		}

		/// <summary>
		/// Applied to ConsumableConsumer to ban meat and fish from Pacifist Duplicants,
		/// and the Gas Range foods from Gastrophobic Duplicants.
		/// </summary>
		[HarmonyPatch(typeof(ConsumableConsumer), "OnPrefabInit")]
		public static class ConsumableConsumer_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(ConsumableConsumer __instance) {
				TraitReworkUtils.ApplyBannedFoods(__instance);
			}
		}

		/// <summary>
		/// Applied to ConsumableConsumer to ban meat and fish from Pacifist Duplicants,
		/// and the Gas Range foods from Gastrophobic Duplicants.
		/// </summary>
		[HarmonyPatch(typeof(ConsumableConsumer), "SetPermitted")]
		public static class ConsumableConsumer_SetPermitted_Patch {
			/// <summary>
			/// Applied after SetPermitted runs.
			/// </summary>
			internal static void Postfix(ConsumableConsumer __instance) {
				if (TraitReworkUtils.ApplyBannedFoods(__instance))
					__instance.consumableRulesChanged.Signal();
			}
		}

		/// <summary>
		/// Applied to ConsumerManager to ban newly discovered consumables which cannot be
		/// eaten by some Duplicants.
		/// </summary>
		[HarmonyPatch(typeof(ConsumerManager), "OnSpawn")]
		public static class ConsumerManager_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(ConsumerManager __instance) {
				__instance.OnDiscover += TraitReworkUtils.ApplyAllBannedFoods;
			}
		}

		/// <summary>
		/// Applied to Edible to remove the lit eating modifier on eating end.
		/// </summary>
		[HarmonyPatch(typeof(Edible), "OnStopWork")]
		public static class Edible_OnStopWork_Patch {
			/// <summary>
			/// Controls whether this patch is implemented.
			/// </summary>
			internal static bool Prepare() {
				return PUtil.GameVersion < VERSION_DISABLE_LITEATING;
			}

			/// <summary>
			/// Applied after OnStopWork runs.
			/// </summary>
			internal static void Postfix(Worker worker) {
				if (EAT_LIT_MODIFIER != null)
					worker.GetAttributes()?.Remove(EAT_LIT_MODIFIER);
			}
		}

		/// <summary>
		/// Applied to Edible to fix the loss of calories when eating in the light.
		/// </summary>
		[HarmonyPatch(typeof(Edible), "OnWorkTick")]
		public static class Edible_OnWorkTick_Patch {
			/// <summary>
			/// Controls whether this patch is implemented.
			/// </summary>
			internal static bool Prepare() {
				return PUtil.GameVersion < VERSION_DISABLE_LITEATING;
			}

			/// <summary>
			/// Applied after OnWorkTick runs.
			/// </summary>
			internal static void Postfix(Worker worker) {
				if (worker != null)
					TraitReworkUtils.UpdateLitEatingModifier(worker);
			}
		}

		/// <summary>
		/// Applied to EntityModifierSet to alter/add traits on load.
		/// </summary>
		[HarmonyPatch(typeof(EntityModifierSet), "Initialize")]
		public static class EntityModifierSet_Initialize_Patch {
			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix(EntityModifierSet __instance) {
				TraitReworkUtils.FixTraits(__instance);
				PUtil.LogDebug("Updated traits");
				// Add "Disturbed" trait
				var disturbedEffect = new Effect(TraitTuning.DISTURBED_EFFECT, TraitStrings.
					DISTURBED_NAME, TraitStrings.DISTURBED_DESC, 200.0f, true, true, true);
				disturbedEffect.Add(new AttributeModifier("StressDelta", 0.033333333f,
					TraitStrings.DISTURBED_NAME));
				__instance.effects.Add(disturbedEffect);
			}
		}

		/// <summary>
		/// Applied to Flatulence to only fart if the Duplicant is not holding breath and to
		/// change the amount.
		/// </summary>
		[HarmonyPatch(typeof(Flatulence), "Emit")]
		public static class Flatulence_Emit_Patch {
			/// <summary>
			/// Applied before Emit runs.
			/// </summary>
			internal static bool Prefix(Flatulence __instance) {
				var obj = __instance.gameObject;
				bool cont = true;
				var equipper = obj.GetComponent<SuitEquipper>();
				// Always fart if wearing a suit
				if (equipper == null || !equipper.IsWearingAirtightSuit()) {
					cont = !obj.HasTag(GameTags.NoOxygen);
#if DEBUG
					if (!cont)
						PUtil.LogDebug("Suppressing fart for {0} - not breathing".F(obj.name));
#endif
				}
				return cont;
			}

			/// <summary>
			/// Transpiles Emit to change the amount emitted (grr consts)
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceConstant(method, 0.1f, TraitTuning.FART_AMOUNT,
					true);
			}
		}

		/// <summary>
		/// Applied to MinionIdentity to properly apply banned food preferences to Duplicants
		/// who arrive via the Printing Pod or Spawner in sandbox mode.
		/// </summary>
		[HarmonyPatch(typeof(MinionIdentity), "OnSpawn")]
		public static class MinionIdentity_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(MinionIdentity __instance) {
				var cc = __instance.gameObject.GetComponentSafe<ConsumableConsumer>();
				if (cc != null)
					TraitReworkUtils.ApplyBannedFoods(cc);
			}
		}

		/// <summary>
		/// Applied to Narcolepsy.States to add new states for preventing sleeping while
		/// carrying an item.
		/// </summary>
		[HarmonyPatch(typeof(Narcolepsy.States), "InitializeStates")]
		public static class Narcolepsy_States_InitializeStates_Patch {
			/// <summary>
			/// Retrieves the interval for the next sleep, using the same method as the base
			/// game.
			/// </summary>
			/// <param name="min">The minimum interval.</param>
			/// <param name="max">The maximum interval.</param>
			/// <returns>A random number between those two intervals.</returns>
			private static float GetNewInterval(float min, float max) {
				return UnityEngine.Random.Range(min, max);
			}

			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(Narcolepsy.States __instance) {
				waitForSleep = __instance.CreateState(nameof(waitForSleep));
				// Transition from idle to waitForSleep
				var idle = __instance.idle;
				idle.ClearEnterActions();
				idle.Enter("ScheduleNextSleep", (smi) => {
					float when = GetNewInterval(TUNING.TRAITS.NARCOLEPSY_INTERVAL_MIN,
						TUNING.TRAITS.NARCOLEPSY_INTERVAL_MAX);
					smi.ScheduleGoTo(when, waitForSleep);
#if DEBUG
					PUtil.LogDebug("Narcolepsy: check for sleep in {0:F1}s".F(when));
#endif
				});
				// Transition from waitForSleep to sleepy
				waitForSleep.Transition(__instance.sleepy, (smi) => {
					var obj = smi.gameObject;
					bool holding = false;
					if (obj != null) {
						holding = !(obj.GetComponent<Storage>()?.IsEmpty() ?? false);
#if DEBUG
						if (holding)
							PUtil.LogDebug("{0} is holding object, defer narcolepsy".F(obj.name));
#endif
					}
					return obj == null || !holding;
				}, UpdateRate.SIM_1000ms);
			}

			/// <summary>
			/// The state used when waiting to drop an object before sleeping.
			/// </summary>
			internal static GameStateMachine<Narcolepsy.States, Narcolepsy.StatesInstance,
				Narcolepsy, object>.State waitForSleep;
		}

		/// <summary>
		/// Applied to ToolMenu to update Duplicant food bans on save load.
		/// </summary>
		[HarmonyPatch(typeof(ToolMenu), "OnSpawn")]
		public static class ToolMenu_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// 
			/// This is not the right place to create it formally, but all Duplicants have been
			/// spawned in at this time, so the banned foods can be applied properly.
			/// </summary>
			internal static void Postfix() {
				TraitReworkUtils.ApplyAllBannedFoods(null);
			}
		}

		/// <summary>
		/// Applied to SleepChoreMonitor to prevent Narcoleptic duplicants from getting
		/// Sore Back.
		/// </summary>
		[HarmonyPatch(typeof(SleepChoreMonitor.Instance), "CreateFloorLocator")]
		public static class SleepChoreMonitor_Instance_CreateFloorLocator_Patch {
			/// <summary>
			/// Applied after CreateFloorLocator runs.
			/// </summary>
			internal static void Postfix(SleepChoreMonitor.Instance __instance,
					GameObject __result) {
				TraitReworkUtils.RemoveSoreBack(__instance, __result);
			}
		}

		/// <summary>
		/// Applied to SleepChoreMonitor to prevent Narcoleptic duplicants from getting
		/// Sore Back.
		/// </summary>
		[HarmonyPatch(typeof(SleepChoreMonitor.Instance), "CreatePassedOutLocator")]
		public static class SleepChoreMonitor_Instance_CreatePassedOutLocator_Patch {
			/// <summary>
			/// Applied after CreatePassedOutLocator runs.
			/// </summary>
			internal static void Postfix(SleepChoreMonitor.Instance __instance,
					GameObject __result) {
				TraitReworkUtils.RemoveSoreBack(__instance, __result);
			}
		}

		/// <summary>
		/// Applied to Snorer.StatesInstance to stress woken Duplicants who hear a snorer.
		/// </summary>
		[HarmonyPatch(typeof(Snorer.StatesInstance), "StartSnoreBGEffect")]
		public static class Snorer_StatesInstance_StartSnoreBGEffect_Patch {
			/// <summary>
			/// Applied after StartSnoreBGEffect runs.
			/// </summary>
			internal static void Postfix(Snorer.StatesInstance __instance) {
				TraitReworkUtils.DisturbInRange(__instance.gameObject, 3.0f);
			}
		}
	}
}
