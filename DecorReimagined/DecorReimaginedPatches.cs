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

using Database;
using HarmonyLib;
using Klei.AI;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using ReimaginationTeam.Reimagination;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Patches which will be applied via annotations for Decor Reimagined.
	/// </summary>
	public sealed class DecorReimaginedPatches : KMod.UserMod2 {
		/// <summary>
		/// The achievement name of the bugged initial And It Feels Like Home achievement.
		/// </summary>
		private const string ACHIEVE_NAME = "AndItFeelsLikeHome";

		private static ChoreType SleepChoreType;

		/// <summary>
		/// The options for Decor Reimagined.
		/// </summary>
		internal static DecorReimaginedOptions Options { get; private set; }

		/// <summary>
		/// Applies the new decor levels.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void ApplyDecorEffects() {
			DecorTuning.InitEffects();
			new PColonyAchievement(ACHIEVE_NAME) {
				Name = DecorReimaginedStrings.FEELSLIKEHOME_NAME,
				Description = DecorReimaginedStrings.FEELSLIKEHOME_DESC.text.F(DecorTuning.
					NUM_DECOR_FOR_ACHIEVEMENT),
				Requirements = new List<ColonyAchievementRequirement>() {
					// Specified number of +decor items on one cell
					new NumDecorPositives(DecorTuning.NUM_DECOR_FOR_ACHIEVEMENT)
				},
				Icon = "art_underground"
			}.AddAchievement();
			SleepChoreType = Db.Get().ChoreTypes.Sleep;
			PUtil.LogDebug("Initialized decor effects");
		}

		/// <summary>
		/// Counts the number of growing, wild plants in a room.
		/// </summary>
		/// <param name="room">The room to check.</param>
		/// <returns>The number of wild, living (not stifled/dead) plants in that room.</returns>
		private static int CountValidPlants(Room room) {
			int wildPlants = 0;
			if (room != null)
				foreach (var plant in room.cavity.plants)
					// Plant must not be wilted
					if (plant != null && ((plant.TryGetComponent(out ReceptacleMonitor farm) &&
							!farm.Replanted) || plant.TryGetComponent(
							out BasicForagePlantPlanted _)) && (!plant.TryGetComponent(
							out WiltCondition wilting) || !wilting.IsWilting()))
						wildPlants++;
			return wildPlants;
		}

		/// <summary>
		/// Checks to see if the recreation building is actually working. Disabled buildings
		/// are not functional.
		/// </summary>
		/// <param name="building">The building to check.</param>
		/// <returns>true if it is a working rec building, or false otherwise.</returns>
		private static bool IsWorkingRecBuilding(KPrefabID building) {
			return building != null && building.HasTag(RoomConstraints.ConstraintTags.
				RecBuilding) && (!building.TryGetComponent(out Operational operational) ||
				operational.IsFunctional);
		}
		
		/// <summary>
		/// Patches the park and nature reserve to require living plants.
		/// </summary>
		private static void PatchParks() {
			RoomConstraints.WILDPLANT = new RoomConstraints.Constraint(null, room =>
				CountValidPlants(room) >= 2, 1, STRINGS.ROOMS.CRITERIA.WILDPLANT.NAME, STRINGS.
				ROOMS.CRITERIA.WILDPLANT.DESCRIPTION);
			RoomConstraints.WILDPLANTS = new RoomConstraints.Constraint(null, room =>
					CountValidPlants(room) >= 4, 1, STRINGS.ROOMS.CRITERIA.WILDPLANTS.NAME,
				STRINGS.ROOMS.CRITERIA.WILDPLANTS.DESCRIPTION);
		}

		/// <summary>
		/// Patches the recreation buildings constraint (for great hall and rec room) to
		/// require that the building actually be functional. It can be disabled by automation,
		/// but that cannot be excluded without also excluding buildings that are not in use.
		/// 
		/// Broken, disabled, entombed, and unplugged buildings are not functional.
		/// </summary>
		private static void PatchRecBuildings() {
			RoomConstraints.REC_BUILDING = new RoomConstraints.Constraint(IsWorkingRecBuilding,
				null, 1, STRINGS.ROOMS.CRITERIA.REC_BUILDING.NAME, STRINGS.ROOMS.CRITERIA.
				REC_BUILDING.DESCRIPTION);
		}

		/// <summary>
		/// Cleans up the decor manager on close.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void DestroyDecor() {
			PUtil.LogDebug("Destroying DecorCellManager");
			DecorCellManager.DestroyInstance();
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			LocString.CreateLocStringKeys(typeof(DecorReimaginedStrings.UI));
			PUtil.InitLibrary();
			new PLocalization().Register();
			Options = new DecorReimaginedOptions();
			ImaginationLoader.Instance.Register(typeof(DecorReimaginedPatches));
			new POptions().RegisterOptions(this, typeof(DecorReimaginedOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(DecorReimaginedPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			PatchParks();
			PatchRecBuildings();
		}

		/// <summary>
		/// Sets up the decor manager on start.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void SetupDecor() {
			DecorCellManager.CreateInstance();
			//ImaginationLoader.Instance.IsFinalDestination();
			PUtil.LogDebug("Created DecorCellManager");
		}

		/// <summary>
		/// Updates the decor levels of decor monitors to add the low tiers of decor.
		/// </summary>
		[PLibPatch(RunAt.AfterModsLoad, typeof(DecorMonitor.Instance), "", typeof(
			IStateMachineTarget))]
		internal static void UpdateDecorLevels_Postfix(List<KeyValuePair<float,
				string>> ___effectLookup) {
			if (___effectLookup != null) {
				___effectLookup.Clear();
				foreach (var decorLevel in DecorTuning.DECOR_LEVELS)
					___effectLookup.Add(new KeyValuePair<float, string>(decorLevel.MinDecor,
						decorLevel.ID));
#if DEBUG
				PUtil.LogDebug("Updated decor levels");
#endif
			}
		}

		/// <summary>
		/// Applied to Artable to strip off the "Incomplete" modifier when the artable is
		/// completed.
		/// </summary>
		[HarmonyPatch(typeof(Artable), "OnCompleteWork")]
		public static class Artable_OnCompleteWork_Patch {
			/// <summary>
			/// Applied before OnCompleteWork runs.
			/// </summary>
			internal static void Prefix(Artable __instance) {
				// Remove the decor bonus (SetStage adds it back)
				var attr = __instance.GetAttributes().Get(Db.Get().BuildingAttributes.Decor);
				attr?.Modifiers.RemoveAll(modifier => modifier.Description ==
					"Art Quality");
			}
		}

		/// <summary>
		/// Applied to AtmoSuitConfig to patch the atmo suit to look ugly.
		/// </summary>
		[HarmonyPatch(typeof(AtmoSuitConfig), nameof(AtmoSuitConfig.CreateEquipmentDef))]
		public static class AtmoSuitConfig_CreateEquipmentDef_Patch {
			/// <summary>
			/// Applied after CreateEquipmentDef runs.
			/// </summary>
			internal static void Postfix(EquipmentDef __result) {
				if (__result != null && Options != null) {
					PUtil.LogDebug("Atmo Suit: {0:D}".F(Options.AtmoSuitDecor));
					DecorTuning.TuneSuits(Options, __result);
				}
			}
		}

		/// <summary>
		/// Applied to DecorMonitor.Instance to hide decor while sleeping.
		/// </summary>
		[HarmonyPatch(typeof(DecorMonitor.Instance), nameof(DecorMonitor.Instance.Update))]
		public static class DecorMonitor_Instance_Update_Patch {
			/// <summary>
			/// The slew speed of displayed decor.
			/// </summary>
			private const float SLEW = 4.166666667f;

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static bool Prefix(DecorMonitor.Instance __instance, float dt,
					AmountInstance ___amount, AttributeModifier ___modifier,
					ref float ___cycleTotalDecor) {
				bool cont = true;
				var go = __instance.gameObject;
				// If no chore driver, allow stock implementation
				if (go != null && go.TryGetComponent(out ChoreDriver driver)) {
					var chore = driver.GetCurrentChore();
					cont = false;
					// Slew to half decor if sleeping
					float decorAtCell = GameUtil.GetDecorAtCell(Grid.PosToCell(__instance));
					if (chore != null && chore.choreType == SleepChoreType)
						decorAtCell *= DecorTuning.DECOR_FRACTION_SLEEP;
					___cycleTotalDecor += decorAtCell * dt;
					// Constants are the same as the base game
					float value = 0.0f, curDecor = ___amount.value;
					if (Mathf.Abs(decorAtCell - curDecor) > 0.5f) {
						if (decorAtCell > curDecor)
							value = 3.0f * SLEW;
						else if (decorAtCell < curDecor)
							value = -SLEW;
					} else
						___amount.value = decorAtCell;
					___modifier.SetValue(value);
				}
				return cont;
			}
		}

		/// <summary>
		/// Applied to DecorProvider to properly attribute decor sources.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), nameof(DecorProvider.GetDecorForCell))]
		public static class DecorProvider_GetDecorForCell_Patch {
			/// <summary>
			/// Applied before GetDecorForCell runs.
			/// </summary>
			internal static bool Prefix(DecorProvider __instance, int cell, out float __result)
			{
				bool cont = true;
				var inst = DecorCellManager.Instance;
				if (inst != null) {
					__result = inst.GetDecorProvided(cell, __instance);
					cont = false;
				} else
					__result = 0.0f;
				return cont;
			}
		}

		/// <summary>
		/// Applied to DecorProvider to refresh it when operational status changes.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), "OnPrefabInit")]
		public static class DecorProvider_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(DecorProvider __instance) {
				__instance.gameObject.AddOrGet<DecorSplatNew>();
			}
		}

		/// <summary>
		/// Applied to DecorProvider to properly handle broken/disabled building decor.
		/// </summary>
		[HarmonyPatch(typeof(DecorProvider), nameof(DecorProvider.Refresh))]
		public static class DecorProvider_Refresh_Patch {
			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix(DecorProvider __instance) {
				bool cont = true;
				if (__instance != null && __instance.TryGetComponent(out DecorSplatNew splat))
				{
					// Replace it
					cont = false;
					splat.RefreshDecor();
				}
				return cont;
			}
		}

		/// <summary>
		/// Applied to adjust sculpture art decor levels.
		/// </summary>
		[HarmonyPatch(typeof(IceSculptureConfig), nameof(IceSculptureConfig.
			DoPostConfigureComplete))]
		public static class IceSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				try {
					Options?.ApplyToSculpture(go);
				} catch (MemberAccessException e) {
					PUtil.LogWarning("Unable to patch Ice Sculptures:");
					PUtil.LogExcWarn(e);
				}
			}
		}

		/// <summary>
		/// Applied to JetSuitConfig to patch the jet suit to look ugly.
		/// </summary>
		[HarmonyPatch(typeof(JetSuitConfig), nameof(JetSuitConfig.CreateEquipmentDef))]
		public static class JetSuitConfig_CreateEquipmentDef_Patch {
			/// <summary>
			/// Applied after CreateEquipmentDef runs.
			/// </summary>
			internal static void Postfix(EquipmentDef __result) {
				if (__result != null && Options != null) {
					PUtil.LogDebug("Jet Suit: {0:D}".F(Options.AtmoSuitDecor));
					DecorTuning.TuneSuits(Options, __result);
				}
			}
		}

		/// <summary>
		/// Applied to LegacyModMain to alter building decor.
		/// </summary>
		[HarmonyPatch(typeof(LegacyModMain), "LoadBuildings")]
		public static class LegacyModMain_LoadBuildings_Patch {
			/// <summary>
			/// Applied after LoadBuildings runs.
			/// </summary>
			internal static void Postfix() {
				// Settings need to be read at this time
				Options = POptions.ReadSettings<DecorReimaginedOptions>() ??
					new DecorReimaginedOptions();
				PUtil.LogDebug("DecorReimaginedOptions settings: Hard Mode = {0}".F(Options.
					HardMode));
				PUtil.LogDebug("Loading decor database");
				DecorTuning.ApplyDatabase(Options);
			}
		}

		/// <summary>
		/// Applied to adjust sculpture art decor levels.
		/// </summary>
		[HarmonyPatch(typeof(MarbleSculptureConfig), nameof(MarbleSculptureConfig.
			DoPostConfigureComplete))]
		public static class MarbleSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				try {
					Options?.ApplyToSculpture(go);
				} catch (MemberAccessException e) {
					PUtil.LogWarning("Unable to patch Marble Sculptures:");
					PUtil.LogExcWarn(e);
				}
			}
		}

		/// <summary>
		/// Applied to adjust sculpture art decor levels.
		/// </summary>
		[HarmonyPatch(typeof(MetalSculptureConfig), nameof(MetalSculptureConfig.
			DoPostConfigureComplete))]
		public static class MetalSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				try {
					Options?.ApplyToSculpture(go);
				} catch (MemberAccessException e) {
					PUtil.LogWarning("Unable to patch Metal Sculptures:");
					PUtil.LogExcWarn(e);
				}
			}
		}

		/// <summary>
		/// Applied to Operational to update room status.
		/// </summary>
		[HarmonyPatch(typeof(Operational), "UpdateFunctional")]
		public static class Operational_UpdateFunctional_Patch {
			/// <summary>
			/// Applied before UpdateFunctional runs.
			/// </summary>
			internal static void Prefix(Operational __instance, ref bool __state) {
				__state = __instance.IsFunctional;
			}

			/// <summary>
			/// Applied after UpdateFunctional runs.
			/// </summary>
			internal static void Postfix(Operational __instance, bool __state) {
				var obj = __instance.gameObject;
				if (obj != null && obj.HasTag(RoomConstraints.ConstraintTags.RecBuilding) &&
						__instance.IsFunctional != __state)
					// Update rooms if rec buildings break down or get disabled, but only
					// if the status actually changed
					Game.Instance.roomProber.SolidChangedEvent(Grid.PosToCell(obj), true);
			}
		}

		/// <summary>
		/// Applied to adjust sculpture art decor levels.
		/// </summary>
		[HarmonyPatch(typeof(SculptureConfig), nameof(SculptureConfig.
			DoPostConfigureComplete))]
		public static class SculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				try {
					Options?.ApplyToSculpture(go);
				} catch (MemberAccessException e) {
					PUtil.LogWarning("Unable to patch Large Sculptures:");
					PUtil.LogExcWarn(e);
				}
			}
		}

		/// <summary>
		/// Applied to adjust sculpture art decor levels.
		/// </summary>
		[HarmonyPatch(typeof(SmallSculptureConfig), nameof(SmallSculptureConfig.
			DoPostConfigureComplete))]
		public static class SmallSculptureConfig_DoPostConfigureComplete_Patch {
			/// <summary>
			/// Applied after DoPostConfigureComplete runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				try {
					Options?.ApplyToSculpture(go);
				} catch (MemberAccessException e) {
					PUtil.LogWarning("Unable to patch Small Sculptures:");
					PUtil.LogExcWarn(e);
				}
			}
		}

		/// <summary>
		/// Applied to UglyCryChore.States to make ugly criers more ugly!
		/// </summary>
		[HarmonyPatch(typeof(UglyCryChore.States), nameof(UglyCryChore.States.
			InitializeStates))]
		public static class UglyCryChore_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(Effect ___uglyCryingEffect) {
				string decorID = Db.Get().Attributes.Decor.Id;
				int uglyDecor = Math.Min(0, Options?.UglyCrierDecor ?? -30);
				foreach (var modifier in ___uglyCryingEffect.SelfModifiers)
					if (modifier.AttributeId == decorID) {
#if DEBUG
						PUtil.LogDebug("Ugly Crier: {0:D}".F());
#endif
						modifier.SetValue(uglyDecor);
						break;
					}
			}
		}

		/// <summary>
		/// Applied to WiltCondition to re-evaluate Park and Nature Reserve status when a plant
		/// is no longer dead.
		/// </summary>
		[HarmonyPatch(typeof(WiltCondition), "DoRecover")]
		public static class WiltCondition_DoRecover_Patch {
			/// <summary>
			/// Applied after DoRecover runs.
			/// </summary>
			internal static void Postfix(WiltCondition __instance) {
				var obj = __instance.gameObject;
				var prober = Game.Instance.roomProber;
				if (obj != null && prober != null) {
					var room = prober.GetRoomOfGameObject(obj)?.roomType;
					var types = Db.Get().RoomTypes;
					// Only need to re-evaluate, if the room is a miscellaneous room
					// or a park may be upgraded to a natural reserve.
					if (room == types.Neutral || room == types.Park)
						// Update that room
						Game.Instance.roomProber.SolidChangedEvent(Grid.PosToCell(obj), true);
				}
			}
		}

		/// <summary>
		/// Applied to WiltCondition to re-evaluate Park and Nature Reserve status when a plant
		/// becomes dead.
		/// </summary>
		[HarmonyPatch(typeof(WiltCondition), "DoWilt")]
		public static class WiltCondition_DoWilt_Patch {
			/// <summary>
			/// Applied after DoWilt runs.
			/// </summary>
			internal static void Postfix(WiltCondition __instance) {
				var obj = __instance.gameObject;
				var prober = Game.Instance.roomProber;
				if (obj != null && prober != null) {
					var room = prober.GetRoomOfGameObject(obj)?.roomType;
					// Only need to re-evaluate, if the room is a park or a nature reserve
					var types = Db.Get().RoomTypes;
					if (room == types.Park || room == types.NatureReserve)
						// Update that room
						prober.SolidChangedEvent(Grid.PosToCell(obj), true);
				}
			}
		}
	}
}
