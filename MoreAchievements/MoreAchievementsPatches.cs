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

using HarmonyLib;
using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using System.Collections.Generic;
using UnityEngine;

using AchieveDict = System.Collections.Generic.IDictionary<string, ColonyAchievementStatus>;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Patches which will be applied via annotations for One Giant Leap.
	/// </summary>
	public sealed class MoreAchievementsPatches : KMod.UserMod2 {
		/// <summary>
		/// The base path to the embedded images.
		/// </summary>
		private const string BASE_PATH = "PeterHan.MoreAchievements.images.";

		/// <summary>
		/// The current options used for the mod.
		/// </summary>
		internal static MoreAchievementsOptions Options { get; private set; }

		/// <summary>
		/// The tag used when a Duplicant is incapacitated due to scalding.
		/// </summary>
		internal static Tag ScaldedTag { get; private set; }

		/// <summary>
		/// Adds all colony achievements for this mod.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AddAllAchievements() {
			int added = 0;
			ScaldedTag = TagManager.Create("Scalded", AchievementStrings.SCALDED);
			Achievements.InitAchievements();
			foreach (var aDesc in Achievements.AllAchievements) {
				var achieve = aDesc.GetColonyAchievement();
				string icon = achieve.icon;
				PDatabaseUtils.AddColonyAchievement(achieve);
				// Load image if necessary
				if (Assets.GetSprite(icon) == null) {
					LoadAndAddSprite(icon);
					LoadAndAddSprite(icon + "_locked");
					LoadAndAddSprite(icon + "_unlocked");
				}
				added++;
			}
			PUtil.LogDebug("Added {0:D} achievements".F(added));
		}

		/// <summary>
		/// Checks to see if the object that just got melted is a POI object.
		/// 
		/// Destroys the object before returning.
		/// </summary>
		/// <param name="obj">The object that just melted, before the melt actually completes.</param>
		private static void CheckAndDestroy(GameObject obj) {
			if (obj != null) {
				if (Achievements.POI_PROPS.Contains(obj.PrefabID().Name))
					AchievementStateComponent.Trigger(AchievementStrings.WATCHTHEWORLDBURN.ID);
				Util.KDestroyGameObject(obj);
			}
		}

		/// <summary>
		/// Checks the remaining breath of the Duplicant when they reach oxygen.
		/// </summary>
		/// <param name="smi">The state machine instance controlling breath recovery.</param>
		private static void CheckBreath(RecoverBreathChore.StatesInstance smi) {
			var dupe = smi.sm.recoverer.Get(smi);
			if (dupe != null) {
				// How long until that dupe would have suffocated?
				var instance = Db.Get().Amounts.Breath.Lookup(dupe);
				var breathSMI = dupe.GetSMI<SuffocationMonitor.Instance>();
				if (instance != null && breathSMI != null) {
					float delta = instance.GetDelta();
#if DEBUG
					PUtil.LogDebug("Reached air with {0:F2} left".F(instance.value));
#endif
					if (delta != 0.0f && instance.value / Mathf.Abs(delta) <
							AchievementStrings.FINALBREATH.THRESHOLD)
						AchievementStateComponent.Trigger(AchievementStrings.FINALBREATH.
							ID);
				}
			}
		}

		/// <summary>
		/// Applied to Game to clean up the achievement tracker on close.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void Cleanup() {
			AchievementStateComponent.DestroyInstance();
		}

		/// <summary>
		/// Loads a sprite and adds it to the master sprite list.
		/// </summary>
		/// <param name="sprite">The sprite to load.</param>
		private static void LoadAndAddSprite(string sprite) {
			try {
				Assets.Sprites.Add(sprite, PUIUtils.LoadSprite(BASE_PATH + sprite + ".png",
					log: false));
			} catch (System.ArgumentException) {
				PUtil.LogWarning("Unable to load image " + sprite + "!");
			}
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PLocalization().Register();
			Options = new MoreAchievementsOptions();
			new PPatchManager(harmony).RegisterPatchClass(typeof(MoreAchievementsPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			// Set hidden achievements
			var acAPI = new MoreAchievementsAPI();
			acAPI.AddAchievementInformation(AchievementStrings.HAVEIWONYET.ID, "", true);
			acAPI.AddAchievementInformation(AchievementStrings.ALLTHEDUPLICANTS.ID, "", true);
			acAPI.AddAchievementInformation(AchievementStrings.ISEEWHATYOUDIDTHERE.ID, "",
				true);
		}

		/// <summary>
		/// Applied to BuildingComplete to update the maximum building temperature seen.
		/// </summary>
		[HarmonyPatch(typeof(BuildingComplete), "OnSetTemperature")]
		public static class BuildingComplete_OnSetTemperature_Patch {
			/// <summary>
			/// Applied after OnSetTemperature runs.
			/// </summary>
			internal static void Postfix(float temperature) {
				AchievementStateComponent.UpdateMaxKelvin(temperature);
			}
		}

		/// <summary>
		/// Applied to BuildingComplete to update the maximum building temperature seen.
		/// </summary>
		[HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
		public static class BuildingComplete_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(PrimaryElement ___primaryElement) {
				if (___primaryElement != null)
					AchievementStateComponent.UpdateMaxKelvin(___primaryElement.Temperature);
			}
		}

		/// <summary>
		/// Applied to BuildingHP to check for damage from overloading wires.
		/// </summary>
		[HarmonyPatch(typeof(BuildingHP), "OnDoBuildingDamage")]
		public static class BuildingHP_DoDamage_Patch {
			/// <summary>
			/// Applied after DoDamage runs.
			/// </summary>
			internal static void Postfix(BuildingHP __instance, object data) {
				Wire wire;
				WireUtilityNetworkLink bridge;
				if (__instance != null && data is BuildingHP.DamageSourceInfo source &&
						(source.source == STRINGS.BUILDINGS.DAMAGESOURCES.CIRCUIT_OVERLOADED ||
						source.takeDamageEffect == SpawnFXHashes.BuildingSpark)) {
					var obj = __instance.gameObject;
#if DEBUG
					PUtil.LogDebug("Wire overloaded: " + obj.name);
#endif
					if ((wire = obj.GetComponentSafe<Wire>()) != null)
						// Wire is overloading
						AchievementStateComponent.OnOverload(wire.GetMaxWattageRating());
					else if ((bridge = obj.GetComponentSafe<WireUtilityNetworkLink>()) != null)
						// Wire bridge is overloading
						AchievementStateComponent.OnOverload(bridge.GetMaxWattageRating());
				}
			}
		}

		/// <summary>
		/// Applied to Butcherable to count dying critters if they die at a young age.
		/// </summary>
		[HarmonyPatch(typeof(Butcherable), nameof(Butcherable.OnButcherComplete))]
		public static class Butcherable_OnButcherComplete_Patch {
			/// <summary>
			/// Applied after OnButcherComplete runs.
			/// </summary>
			internal static void Postfix(Butcherable __instance) {
				var obj = __instance.gameObject;
				bool natural = false;
				if (obj != null) {
					var smi = obj.GetSMI<AgeMonitor.Instance>();
					if (smi != null)
						natural = smi.CyclesUntilDeath < (1.0f / Constants.SECONDS_PER_CYCLE);
#if DEBUG
					PUtil.LogDebug("Critter died: " + (natural ? "old age" : "prematurely"));
#endif
					if (!natural)
						AchievementStateComponent.OnCritterKilled();
				}
			}
		}

		/// <summary>
		/// Applied to Clinic to check Duplicant HP when they enter.
		/// </summary>
		[HarmonyPatch(typeof(Clinic), "OnStartWork")]
		public static class Clinic_OnStartWork_Patch {
			/// <summary>
			/// Applied after OnStartWork runs.
			/// </summary>
			internal static void Postfix(Clinic __instance, Worker worker) {
				var building = __instance.gameObject.GetComponentSafe<Building>();
				var hp = Db.Get().Amounts.HitPoints.Lookup(worker);
#if DEBUG
				if (hp != null)
					PUtil.LogDebug("Reached clinic with {0:F2} left".F(hp.value));
#endif
				if (building != null && building.Def.PrefabID == MedicalCotConfig.ID && hp !=
						null && hp.value <= AchievementStrings.SAVINGMEEP.THRESHOLD)
					AchievementStateComponent.Trigger(AchievementStrings.SAVINGMEEP.ID);
			}
		}

		/// <summary>
		/// Applied to DeathMonitor.Instance to track Duplicant deaths.
		/// </summary>
		[HarmonyPatch(typeof(DeathMonitor.Instance), nameof(DeathMonitor.Instance.Kill))]
		public static class DeathMonitor_Instance_Kill_Patch {
			/// <summary>
			/// Applied after Kill runs.
			/// </summary>
			internal static void Postfix(DeathMonitor.Instance __instance, Death death) {
#if DEBUG
				PUtil.LogDebug("Duplicant died: " + death?.Id);
#endif
				if (__instance.IsDuplicant)
					AchievementStateComponent.OnDeath(death);
			}
		}

		/// <summary>
		/// Applied to Diggable to chalk one up when a dig errand completes.
		/// </summary>
		[HarmonyPatch(typeof(Diggable), "OnSolidChanged")]
		public static class Diggable_OnSolidChanged_Patch {
			/// <summary>
			/// Applied after OnSolidChanged runs.
			/// </summary>
			internal static void Postfix(bool ___isDigComplete, Diggable __instance) {
				if (___isDigComplete) {
#if DEBUG
					PUtil.LogDebug("Tile dug: " + Grid.PosToCell(__instance));
#endif
					Game.Instance?.Trigger(DigNTiles.DigComplete, __instance);
				}
			}
		}

		/// <summary>
		/// Applied to DiscoveredResources to grant an achievement upon discovering items.
		/// </summary>
		[HarmonyPatch(typeof(DiscoveredResources), nameof(DiscoveredResources.Discover))]
		public static class DiscoveredResources_Discover_Patch {
			/// <summary>
			/// Applied after Discover runs.
			/// </summary>
			internal static void Postfix(Tag tag) {
				var neutronium = ElementLoader.FindElementByHash(SimHashes.Unobtanium);
				if (neutronium != null && tag.Equals(neutronium.tag))
					// I See What You Did There
					AchievementStateComponent.Trigger(AchievementStrings.ISEEWHATYOUDIDTHERE.
						ID);
			}
		}

		/// <summary>
		/// Applied to Game to add our achievement state tracker to it on game start.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(Game __instance) {
				// Reload options
				var newOptions = POptions.ReadSettings<MoreAchievementsOptions>();
				if (newOptions != null)
					Options = newOptions;
				__instance.gameObject.AddOrGet<AchievementStateComponent>();
			}
		}

		/// <summary>
		/// Applied to GeneShuffer to count towards the achievement on each vacillator use.
		/// </summary>
		[HarmonyPatch(typeof(GeneShuffler), "ApplyRandomTrait")]
		public static class GeneShuffler_ApplyRandomTrait_Patch {
			/// <summary>
			/// Applied after ApplyRandomTrait runs.
			/// </summary>
			internal static void Postfix() {
#if DEBUG
				PUtil.LogDebug("Neural Vacillator used");
#endif
				AchievementStateComponent.OnGeneShuffleComplete();
			}
		}

		/// <summary>
		/// Applied to Health to properly track deaths due to scalding.
		/// </summary>
		[HarmonyPatch(typeof(Health), nameof(Health.Incapacitate))]
		public static class Health_Incapacitate_Patch {
			/// <summary>
			/// Applied after Incapacitate runs.
			/// </summary>
			internal static void Postfix(Health __instance) {
				KSelectable target;
				if (ScaldedTag != null && (target = __instance.GetComponent<KSelectable>()) !=
						null && target.HasStatusItem(Db.Get().CreatureStatusItems.Scalding))
					__instance.GetComponent<KPrefabID>()?.AddTag(ScaldedTag);
			}
		}

		/// <summary>
		/// Applied to Health to remove scalding tags when a Duplicant recovers.
		/// </summary>
		[HarmonyPatch(typeof(Health), nameof(Health.OnHealthChanged))]
		public static class Health_OnHealthChanged_Patch {
			/// <summary>
			/// Applied before OnHealthChanged runs.
			/// </summary>
			internal static void Prefix(Health __instance) {
				if (__instance.State != Health.HealthState.Invincible && __instance.hitPoints >
						0.0f)
					__instance.GetComponent<KPrefabID>()?.RemoveTag(ScaldedTag);
			}
		}

		/// <summary>
		/// Applied to Health to remove scalding tags when a Duplicant recovers.
		/// </summary>
		[HarmonyPatch(typeof(Health), "Recover")]
		public static class Health_Recover_Patch {
			/// <summary>
			/// Applied after Recover runs.
			/// </summary>
			internal static void Postfix(Health __instance) {
				__instance.GetComponent<KPrefabID>()?.RemoveTag(ScaldedTag);
			}
		}

		/// <summary>
		/// Applied to IncapacitationMonitor to properly attribute scalding deaths.
		/// </summary>
		[HarmonyPatch(typeof(IncapacitationMonitor.Instance), nameof(IncapacitationMonitor.
			Instance.GetCauseOfIncapacitation))]
		public static class IncapacitationMonitor_Instance_GetCauseOfIncapacitation_Patch {
			/// <summary>
			/// Applied after GetCauseOfIncapacitation runs.
			/// </summary>
			internal static void Postfix(IncapacitationMonitor.Instance __instance,
					ref Death __result) {
				var id = __instance.GetComponent<KPrefabID>();
				if (id != null && id.HasTag(ScaldedTag))
					__result = Db.Get().Deaths.Overheating;
			}
		}

		/// <summary>
		/// Applied to OxygenBreather to register for events on each Duplicant for oxygen.
		/// </summary>
		[HarmonyPatch(typeof(RecoverBreathChore.States), nameof(RecoverBreathChore.States.
			InitializeStates))]
		public static class RecoverBreathChore_States_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(RecoverBreathChore.States __instance) {
				__instance.recover.Enter(CheckBreath);
			}
		}

		/// <summary>
		/// Applied to SimTemperatureTransfer to check for POIs melting down.
		/// </summary>
		[HarmonyPatch(typeof(SimTemperatureTransfer), nameof(SimTemperatureTransfer.
			DoOreMeltTransition))]
		public static class SimTemperatureTransfer_DoOreMeltTransition_Patch {
			/// <summary>
			/// Transpiles DoOreMeltTransition to flag destruction of POI objects.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(Util).GetMethodSafe(
					nameof(Util.KDestroyGameObject), true, typeof(GameObject)),
					typeof(MoreAchievementsPatches).GetMethodSafe(nameof(CheckAndDestroy),
					true, typeof(GameObject)));
			}
		}

		/// <summary>
		/// Applied to Spacecraft to track space destination visits when a spacecraft launches.
		/// </summary>
		[HarmonyPatch(typeof(Spacecraft), nameof(Spacecraft.BeginMission))]
		public static class Spacecraft_BeginMission_Patch {
			/// <summary>
			/// Applied after BeginMission runs.
			/// </summary>
			internal static void Postfix(SpaceDestination destination) {
				if (destination != null)
					AchievementStateComponent.OnVisit(destination.id);
			}
		}

		/// <summary>
		/// Applied to Spacecraft to track space destination visits when a spacecraft lands.
		/// </summary>
		[HarmonyPatch(typeof(Spacecraft), nameof(Spacecraft.CompleteMission))]
		public static class Spacecraft_CompleteMission_Patch {
			/// <summary>
			/// Applied after CompleteMission runs.
			/// </summary>
			internal static void Postfix(Spacecraft __instance) {
				var instance = SpacecraftManager.instance;
				SpaceDestination destination;
				if ((destination = instance.GetSpacecraftDestination(__instance.id)) != null)
					AchievementStateComponent.OnVisit(destination.id);
			}
		}
	}
}
