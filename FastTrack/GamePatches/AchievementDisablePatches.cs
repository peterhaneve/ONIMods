/*
 * Copyright 2023 Peter Han
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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Groups patches to handle achievement tracking disable in debug/sandbox.
	/// </summary>
	public static class AchievementDisablePatches {
		/// <summary>
		/// Forces achievements to be enabled in sandbox for compatibility with other mods.
		/// </summary>
		internal static bool forceEnableAchievements;

		/// <summary>
		/// Checks to see if achievements should be tracked.
		/// </summary>
		/// <returns>true if they should be tracked, or false otherwise.</returns>
		internal static bool TrackAchievements() {
			return forceEnableAchievements || (FastTrackOptions.Instance.DisableAchievements ==
				FastTrackOptions.AchievementDisable.SandboxDebug &&
				!SaveGame.Instance.sandboxEnabled && !DebugHandler.InstantBuildMode &&
				!Game.Instance.debugWasUsed);
		}

		/// <summary>
		/// Applied to ColonyAchievementTracker to disable explicit achievement checks.
		/// </summary>
		[HarmonyPatch]
		internal static class CheckAchievements_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.DisableAchievements !=
				FastTrackOptions.AchievementDisable.Never;

			internal static IEnumerable<MethodBase> TargetMethods() {
				var type = typeof(ColonyAchievementTracker);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.
					CheckAchievements), false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.
					LogAnalyzedSeed), false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.
					LogCritterTamed), false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.LogFetchChore),
					false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.LogSuitChore),
					false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.OnNewDay),
					false, PPatchTools.AnyArguments);
				yield return type.GetMethodSafe(nameof(ColonyAchievementTracker.
					UpgradeTamedCritterAchievements), false, PPatchTools.AnyArguments);
				yield return typeof(RationTracker).GetMethodSafe(nameof(RationTracker.
					RegisterRationsConsumed), false, PPatchTools.AnyArguments);
				yield return typeof(Vent).GetMethodSafe(nameof(Vent.UpdateVentedMass), false,
					PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Applied before these methods run.
			/// </summary>
			internal static bool Prefix() {
				return TrackAchievements();
			}
		}

		/// <summary>
		/// Applied to ColonyAchievementTracker to speed up the slow RenderEveryTick method
		/// which linearly iterates a dictionary by index (!!)
		/// </summary>
		[HarmonyPatch(typeof(ColonyAchievementTracker), nameof(ColonyAchievementTracker.
			RenderEveryTick))]
		internal static class RenderEveryTick_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix(ColonyAchievementTracker __instance) {
				var mode = FastTrackOptions.Instance.DisableAchievements;
				if (mode != FastTrackOptions.AchievementDisable.Always && (mode ==
						FastTrackOptions.AchievementDisable.Never || TrackAchievements())) {
					var allAchievements = Db.Get().ColonyAchievements.resources;
					var achievements = __instance.achievements;
					// Update one per frame
					int index = __instance.updatingAchievement;
					if (index >= achievements.Count || index >= allAchievements.Count)
						index = 0;
					string key = allAchievements[index].Id;
					__instance.updatingAchievement = index + 1;
					// If achievement has not already failed or succeeded
					if (achievements.TryGetValue(key, out var status) && !status.success &&
							!status.failed) {
						status.UpdateAchievement();
						if (status.success && !status.failed) {
							ColonyAchievementTracker.UnlockPlatformAchievement(key);
							__instance.completedAchievementsToDisplay.Add(key);
							__instance.TriggerNewAchievementCompleted(key);
							RetireColonyUtility.SaveColonySummaryData();
						}
					}
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to HighEnergyParticle to stop tracking radbolt travel distance if
		/// achievements are off.
		/// </summary>
		[HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.MovingUpdate))]
		internal static class MovingUpdate_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return !options.RadiationOpts && options.DisableAchievements !=
					FastTrackOptions.AchievementDisable.Never;
			}

			/// <summary>
			/// Updates the distance traveled only if the achievements are unlocked.
			/// </summary>
			/// <param name="instance">The particle to check.</param>
			/// <param name="dt">The time elapsed since the last check.</param>
			private static void AchievementUpdate(HighEnergyParticle instance, float dt) {
				var inst = SaveGame.Instance;
				if (TrackAchievements() && inst != null && inst.TryGetComponent(
						out ColonyAchievementTracker cat))
					cat.radBoltTravelDistance += instance.speed * dt;
			}

			/// <summary>
			/// Transpiles MovingUpdate to cut out a series of instructions and replace it
			/// with a conditional check and add.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var replacement = typeof(MovingUpdate_Patch).GetMethodSafe(nameof(
					AchievementUpdate), true, typeof(HighEnergyParticle), typeof(float));
				var start = typeof(SaveGame).GetFieldSafe(nameof(SaveGame.Instance), true);
				var end = typeof(ColonyAchievementTracker).GetFieldSafe(nameof(
					ColonyAchievementTracker.radBoltTravelDistance), false);
				int state = 0;
				if (start != null && end != null && replacement != null)
					foreach (var instr in instructions) {
						if (state == 0 && instr.Is(OpCodes.Ldsfld, start))
							state = 1;
						else if (state == 1 && instr.Is(OpCodes.Stfld, end)) {
							state = 2;
							yield return new CodeInstruction(OpCodes.Ldarg_0);
							yield return new CodeInstruction(OpCodes.Ldarg_1);
							instr.opcode = OpCodes.Call;
							instr.operand = replacement;
#if DEBUG
							PUtil.LogDebug("Patched HighEnergyParticle.MovingUpdate");
#endif
						}
						if (state != 1)
							yield return instr;
					}
				else
					foreach (var instr in instructions)
						yield return instr;
				if (state != 2)
					PUtil.LogWarning("Unable to patch HighEnergyParticle.MovingUpdate");
			}
		}

		/// <summary>
		/// Applied to MinionBrain to turn off biome tracking on every update.
		/// </summary>
		[HarmonyPatch(typeof(MinionBrain), nameof(MinionBrain.UpdateBrain))]
		internal static class UpdateBrain_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.DisableAchievements !=
				FastTrackOptions.AchievementDisable.Never;

			/// <summary>
			/// Wraps Game.Instance to prevent checking for the surface or oil if achievements
			/// are off.
			/// </summary>
			private static Game CheckAchievements(Game instance) {
				return TrackAchievements() ? instance : null;
			}

			/// <summary>
			/// Transpiles Update to set the first Game.Instance to null, which exits the
			/// method before the achievements are checked.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var target = typeof(Game).GetPropertySafe<Game>(nameof(Game.Instance), true)?.
					GetGetMethod(true);
				var insertion = typeof(UpdateBrain_Patch).GetMethodSafe(nameof(
					CheckAchievements), true, typeof(Game));
				bool patched = false;
				foreach (var instr in instructions) {
					yield return instr;
					if (!patched && target != null && instr.Is(OpCodes.Call, target) &&
							insertion != null) {
						patched = true;
						yield return new CodeInstruction(OpCodes.Call, insertion);
#if DEBUG
						PUtil.LogDebug("Patched MinionBrain.UpdateBrain");
#endif
					}
				}
				if (!patched)
					PUtil.LogWarning("Unable to patch Game.Instance");
			}
		}
	}
}
