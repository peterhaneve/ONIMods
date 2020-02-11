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
using PeterHan.MoreAchievements.Criteria;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// Patches which will be applied via annotations for One Giant Leap.
	/// </summary>
	public static class MoreAchievementsPatches {
		/// <summary>
		/// The base path to the embedded images.
		/// </summary>
		private const string BASE_PATH = "PeterHan.MoreAchievements.images.";

		/// <summary>
		/// Adds all colony achievements for this mod.
		/// </summary>
		private static void AddAllAchievements() {
			int added = 0;
			Achievements.InitAchievements();
			foreach (var aDesc in Achievements.AllAchievements) {
				var achieve = aDesc.GetColonyAchievement();
				string icon = achieve.icon;
				PUtil.AddColonyAchievement(achieve);
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
		/// Loads a sprite and adds it to the master sprite list.
		/// </summary>
		/// <param name="sprite">The sprite to load.</param>
		private static void LoadAndAddSprite(string sprite) {
			try {
				Assets.Sprites.Add(sprite, PUtil.LoadSprite(BASE_PATH + sprite + ".png",
					log: false));
			} catch (System.ArgumentException) {
				PUtil.LogWarning("Unable to load image " + sprite + "!");
			}
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			PLocalization.Register();
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
		/// Applied to Butcherable to count dying critters if they die at a young age.
		/// </summary>
		[HarmonyPatch(typeof(Butcherable), "OnButcherComplete")]
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
					if (!natural)
						Game.Instance?.GetComponent<AchievementStateComponent>()?.
							OnCritterKilled();
				}
			}
		}

		/// <summary>
		/// Applied to Db to add colony achievements.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				AddAllAchievements();
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
				if (___isDigComplete)
					Game.Instance.Trigger(DigNTiles.DigComplete, __instance);
			}
		}

		/// <summary>
		/// Applied to Game to add our achievement state tracker to it on game start.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnSpawn")]
		public static class Game_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Game __instance) {
				var obj = __instance.gameObject;
				if (obj != null)
					obj.AddOrGet<AchievementStateComponent>();
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
				Game.Instance?.GetComponent<AchievementStateComponent>()?.
					OnGeneShuffleComplete();
			}
		}
	}
}
