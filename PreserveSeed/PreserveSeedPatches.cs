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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using PeterHan.PLib.UI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using KMod;

namespace PeterHan.PreserveSeed {
	/// <summary>
	/// Patches which will be applied via annotations for Preserve Random Seed.
	/// </summary>
	public sealed class PreserveSeedPatches : UserMod2 {
		private static readonly IDetouredField<Immigration, int> GET_SPAWN_IDX = PDetours.
			DetourField<Immigration, int>("spawnIdx");

		/// <summary>
		/// Applied to multiple methods to switch out the random calls for something a little
		/// less random.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit(Harmony harmony) {
			var useSharedRandom = new HarmonyMethod(typeof(PreserveSeedPatches),
				nameof(TranspileRandom));
			harmony.PatchTranspile(typeof(CharacterContainer), "GetIdleAnim", useSharedRandom);
			harmony.PatchTranspile(typeof(CharacterSelectionController),
				"InitializeContainers", useSharedRandom);
			harmony.PatchTranspile(typeof(CryoTank), "DropContents", useSharedRandom);
			harmony.PatchTranspile(typeof(MinionStartingStats), "GenerateStats",
				useSharedRandom);
		}

		public override void OnLoad(Harmony harmony) {
			PreserveSeedOptions.InitInstance();
			SharedRandom.Reset();
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			LocString.CreateLocStringKeys(typeof(PreserveSeedStrings.UI));
			new PLocalization().Register();
			new PPatchManager(harmony).RegisterPatchClass(typeof(PreserveSeedPatches));
			new POptions().RegisterOptions(this, typeof(PreserveSeedOptions));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		// No restart required
		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			PreserveSeedOptions.InitInstance();
		}

		/// <summary>
		/// Transpiles these methods to replace UnityEngine.Random calls with the Random
		/// initialized on the shared seed.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		internal static IEnumerable<CodeInstruction> TranspileRandom(
				IEnumerable<CodeInstruction> method) {
			return PPatchTools.ReplaceMethodCallSafe(method,
				typeof(UnityEngine.Random).GetMethodSafe(nameof(UnityEngine.Random.Range),
					true, typeof(int), typeof(int)),
				typeof(SharedRandom).GetMethodSafe(nameof(SharedRandom.GetRange), true,
					typeof(int), typeof(int)));
		}

		/// <summary>
		/// Updates the next spawn time of the pod.
		/// </summary>
		/// <param name="immigration">The Immigration instance to modify.</param>
		/// <param name="spawnIndex">The number of items printed so far.</param>
		private static void UpdateSpawnTime(Immigration immigration, int spawnIndex) {
			var spawnInterval = immigration.spawnInterval;
			int maxIndex = spawnInterval.Length - 1;
			// The first print occurs a little earlier than the others (2.5 cycles instead of
			// 3) so leave it unchanged
			immigration.timeBeforeSpawn = spawnIndex <= maxIndex ? spawnInterval[spawnIndex] :
				PreserveSeedOptions.Instance.RechargeNormal * Constants.SECONDS_PER_CYCLE;
		}

		/// <summary>
		/// Applied to CryoTank to make the Duplicant that comes out the same each load.
		/// </summary>
		[HarmonyPatch(typeof(CryoTank), nameof(CryoTank.DropContents))]
		public static class CryoTank_DropContents_Patch {
			/// <summary>
			/// Applied before DropContents runs.
			/// </summary>
			internal static void Prefix(CryoTank __instance) {
				if (__instance != null)
					SharedRandom.SetForWorld(__instance.GetMyWorldId());
			}

			/// <summary>
			/// Applied after DropContents runs.
			/// </summary>
			internal static void Postfix() {
				SharedRandom.Reset();
			}
		}

		/// <summary>
		/// Applied to Database.Personalities to control the random personality handed out.
		/// </summary>
		[HarmonyPatch(typeof(Personalities), nameof(Personalities.GetRandom))]
		public static class Database_Personalities_GetRandom_Patch {
			/// <summary>
			/// Applied after GetRandom runs.
			/// </summary>
			internal static void Postfix(Personalities __instance, bool onlyEnabledMinions,
					bool onlyStartingMinions, ref Personality __result) {
				if (SharedRandom.UseSharedRandom) {
					var results = __instance.GetAll(onlyEnabledMinions, onlyStartingMinions);
					int n = results.Count;
					if (n > 0)
						__result = results[SharedRandom.GetRange(0, n)];
				}
			}
		}

		/// <summary>
		/// Applied to ImmigrantScreen to reset the main random seed whenever the Printing
		/// Pod screen is opened.
		/// </summary>
		[HarmonyPatch(typeof(ImmigrantScreen), "Initialize")]
		public static class ImmigrantScreen_Initialize_Patch {
			/// <summary>
			/// Applied before Initialize runs.
			/// </summary>
			internal static void Prefix() {
				var inst = Immigration.Instance;
				if (inst != null && PreserveSeedOptions.Instance.PreservePodSeed)
					SharedRandom.SetSeed(GET_SPAWN_IDX.Get(inst));
			}

			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				var inst = Game.Instance;
				// Need to wait 2 frames for the dupes to spawn before restoring seed
				if (inst != null && PreserveSeedOptions.Instance.PreservePodSeed)
					inst.StartCoroutine(RestoreRandomSeed());
			}

			/// <summary>
			/// Restores the random seed to using random values (for spawn Duplicant and so
			/// forth) after the printing pod has been generated, wait 2 frames to make sure.
			/// </summary>
			private static IEnumerator RestoreRandomSeed() {
				yield return null;
				yield return null;
				SharedRandom.Reset();
			}
		}

		/// <summary>
		/// Applied to ImmigrantScreen to add a nice tooltip showing how long it will take if
		/// all options are rejected.
		/// </summary>
		[HarmonyPatch(typeof(ImmigrantScreen), "OnSpawn")]
		public static class ImmigrantScreen_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(KButton ___rejectButton) {
				if (___rejectButton != null)
					PUIElements.SetToolTip(___rejectButton.gameObject, string.Format(
						PreserveSeedStrings.UI.TOOLTIPS.PRESERVESEED.REJECTTOOLTIP,
						PreserveSeedOptions.Instance.RechargeReject));
			}
		}

		/// <summary>
		/// Applied to Immigration to update the recharge time.
		/// </summary>
		[HarmonyPatch(typeof(Immigration), "OnPrefabInit")]
		public static class Immigration_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(Immigration __instance, int ___spawnIdx) {
				UpdateSpawnTime(__instance, ___spawnIdx);
#if DEBUG
				PUtil.LogDebug("Vanilla printing intervals: " + string.Join(",", __instance.
					spawnInterval));
#endif
			}
		}

		/// <summary>
		/// Applied to Immigration to use a consistent seed for the care package to select.
		/// </summary>
		[HarmonyPatch(typeof(Immigration), nameof(Immigration.RandomCarePackage))]
		public static class Immigration_RandomCarePackage_Patch {
			/// <summary>
			/// Applied after RandomCarePackage runs.
			/// </summary>
			internal static void Postfix(ref CarePackageInfo __result,
					CarePackageInfo[] ___carePackages) {
				if (SharedRandom.UseSharedRandom) {
					var viable = ListPool<CarePackageInfo, Immigration>.Allocate();
					int n = ___carePackages.Length;
					for (int i = 0; i < n; i++) {
						var candidate = ___carePackages[i];
						if (candidate.requirement == null || candidate.requirement())
							viable.Add(candidate);
					}
					n = viable.Count;
					if (n > 0)
						__result = viable[SharedRandom.GetRange(0, n)];
					else
						PUtil.LogWarning("No care packages are available!");
					viable.Recycle();
				}
			}
		}

		/// <summary>
		/// Applied to LonelyMinionHouse.Instance to make Jorge's stats the same for a given
		/// asteroid seed.
		/// </summary>
		[HarmonyPatch(typeof(LonelyMinionHouse.Instance), "SpawnMinion")]
		public static class LonelyMinionHouse_Instance_SpawnMinion_Patch {
			/// <summary>
			/// Applied before SpawnMinion runs.
			/// </summary>
			internal static void Prefix(LonelyMinionHouse.Instance __instance) {
				var go = __instance.gameObject;
				if (go != null)
					SharedRandom.SetForWorld(go.GetMyWorldId());
			}

			/// <summary>
			/// Applied after SpawnMinion runs.
			/// </summary>
			internal static void Postfix() {
				// Still should use random Duplicants for sandbox spawner and so forth
				SharedRandom.Reset();
			}
		}
		
		/// <summary>
		/// Applied to MinionStartingStats to switch out the random calls for something a
		/// little less random.
		/// </summary>
		[HarmonyPatch]
		public static class MinionStartingStats_GenerateAptitudesAndAttributes_Patch {
			internal static IEnumerable<MethodBase> TargetMethods() {
				yield return typeof(MinionStartingStats).GetMethodSafe("GenerateAptitudes",
					false, PPatchTools.AnyArguments);
				yield return typeof(MinionStartingStats).GetMethodSafe("GenerateAttributes",
					false, PPatchTools.AnyArguments);
			}

			/// <summary>
			/// Transpiles these methods to replace Shuffle calls with the Random
			/// initialized on the shared seed.
			/// </summary>
			[HarmonyPriority(Priority.LowerThanNormal)]
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				// Shuffle has 2 overloads, but cannot bind by concrete parameter type as the
				// method is generic
				MethodInfo target = null;
				ParameterInfo[] parameters;
				System.Type paramType;
				foreach (var candidate in typeof(Util).GetMethods(BindingFlags.Static |
						PPatchTools.BASE_FLAGS))
					if (candidate.Name == nameof(Util.Shuffle) && (parameters = candidate.
							GetParameters()).Length == 1 && (paramType = parameters[0].
							ParameterType).IsGenericType && paramType.
							GetGenericTypeDefinition() == typeof(IList<>)) {
						target = candidate.MakeGenericMethod(typeof(SkillGroup));
						break;
					}
				var replacement = typeof(SharedRandom).GetMethod(nameof(SharedRandom.
					ShuffleSeeded), BindingFlags.Static | PPatchTools.BASE_FLAGS)?.
					MakeGenericMethod(typeof(SkillGroup));
				var map = new Dictionary<MethodInfo, MethodInfo> {
					{
						typeof(UnityEngine.Random).GetMethodSafe(nameof(UnityEngine.Random.
							Range), true, typeof(int), typeof(int)),
						typeof(SharedRandom).GetMethodSafe(nameof(SharedRandom.
							GetRange), true, typeof(int), typeof(int))
					}
				};
				if (target != null && replacement != null)
					map.Add(target, replacement);
				else
					PUtil.LogWarning("Unable to replace IList.Shuffle");
				return PPatchTools.ReplaceMethodCallSafe(method, map);
			}
		}

		/// <summary>
		/// Applied to MinionStartingStats to switch out the random calls for something a
		/// little less random.
		/// </summary>
		[HarmonyPatch(typeof(MinionStartingStats), "GenerateTraits")]
		public static class MinionStartingStats_GenerateTraits_Patch {
			/// <summary>
			/// Transpiles GenerateTraits to replace KRandom calls with the Random initialized
			/// on the shared seed.
			/// </summary>
			[HarmonyPriority(Priority.LowerThanNormal)]
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				var target = typeof(KRandom).GetConstructor(BindingFlags.Instance |
					PPatchTools.BASE_FLAGS, null, System.Type.EmptyTypes, null);
				var replacement = typeof(KRandom).GetConstructor(BindingFlags.Instance |
					PPatchTools.BASE_FLAGS, null, new[] { typeof(int) }, null);
				var nextSeed = typeof(SharedRandom).GetMethodSafe(nameof(SharedRandom.
					GetNextSeed), true);
				bool replaced = false;
				foreach (var instruction in method) {
					if (instruction.opcode == OpCodes.Newobj && instruction.operand is
							ConstructorInfo operand && operand == target &&
							replacement != null) {
						yield return new CodeInstruction(OpCodes.Call, nextSeed);
						instruction.operand = replacement;
						replaced = true;
					}
					yield return instruction;
				}
				if (!replaced)
					PUtil.LogWarning("Unable to patch MinionStartingStats.GenerateTraits");
			}
		}

		/// <summary>
		/// Applied to Telepad to change the recharge time when all items are rejected.
		/// </summary>
		[HarmonyPatch(typeof(Telepad), nameof(Telepad.RejectAll))]
		public static class Telepad_RejectAll_Patch {
			/// <summary>
			/// Applied after RejectAll runs.
			/// </summary>
			internal static void Postfix() {
				Immigration.Instance.timeBeforeSpawn = PreserveSeedOptions.Instance.
					RechargeReject * Constants.SECONDS_PER_CYCLE;
			}
		}
	}
}
