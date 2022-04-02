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
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Applied to ChoreDriver.States to reduce the tracking of the activity reports to
	/// Sim1000 vs Sim200.
	/// </summary>
	[HarmonyPatch(typeof(ChoreDriver.States), nameof(ChoreDriver.States.InitializeStates))]
	public static class ChoreDriver_States_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ReduceColonyTracking;

		/// <summary>
		/// Transpiles InitializeStates to convert the 200ms to 1000ms. Note that postfixing
		/// and swapping is not enough, Update mutates the buckets for the singleton state
		/// machine updater, CLAY PLEASE.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			return PPatchTools.ReplaceConstant(method, (int)UpdateRate.SIM_200ms, (int)
				UpdateRate.SIM_1000ms, true);
		}
	}

	/// <summary>
	/// Applied to NavGrid to replace unnecessary allocations with a Clear call.
	/// </summary>
	[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.UpdateGraph), new Type[0])]
	public static class NavGrid_UpdateGraph_Patch {
		/// <summary>
		/// Transpiles UpdateGraph to clean up allocations and mark cells dirty when required.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var setType = typeof(HashSet<int>);
			var clearMethod = setType.GetMethodSafe(nameof(HashSet<int>.Clear), false);
			var instructions = new List<CodeInstruction>(method);
			int n = instructions.Count;
			if (clearMethod == null)
				// Should be unreachable
				PUtil.LogError("What happened to HashSet.Clear?");
			else {
				var instr = instructions[0];
				for (int i = 0; i < n - 1; i++) {
					var next = instructions[i + 1];
					if (instr.opcode == OpCodes.Newobj && (instr.operand as MethodBase)?.
							DeclaringType == setType && next.opcode == OpCodes.Stfld) {
						// Change "newobj" to "ldfld"
						instr.opcode = OpCodes.Ldfld;
						instr.operand = next.operand;
						// Change "stfld" to "callvirt"
						next.opcode = OpCodes.Callvirt;
						next.operand = clearMethod;
#if DEBUG
						PUtil.LogDebug("Patched NavGrid.UpdateGraph at {0:D}".F(i));
#endif
					}
					instr = next;
				}
			}
			return instructions;
		}
	}

	/// <summary>
	/// Applied to Navigator to force invalid paths if AdvancePath detects that the path needs
	/// to be updated.
	/// </summary>
	[HarmonyPatch(typeof(Navigator), nameof(Navigator.AdvancePath))]
	public static class Navigator_AdvancePath_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.CachePaths || options.DisableAchievements != FastTrackOptions.
				AchievementDisable.Never;
		}

		/// <summary>
		/// Updates the distance traveled only if the achievements are unlocked.
		/// </summary>
		/// <param name="instance">The particle to check.</param>
		/// <param name="dt">The time elapsed since the last check.</param>
		private static void AchievementUpdate(Navigator instance) {
			if (GamePatches.AchievementDisablePatches.TrackAchievements()) {
				var distanceByType = instance.distanceTravelledByNavType;
				var nt = instance.CurrentNavType;
				int dist = distanceByType[nt];
				if (dist < int.MaxValue)
					dist++;
				distanceByType[nt] = dist;
			}
		}

		/// <summary>
		/// Sets the cached path to invalid.
		/// </summary>
		/// <param name="instance">The navigator to invalidate.</param>
		private static void ForceInvalid(Navigator instance) {
			if (instance != null)
				PathCacher.Lookup(instance.PathProber).MarkInvalid();
		}

		/// <summary>
		/// Transpiles AdvancePath to trigger an invalidation if the path is updated.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = typeof(PathFinder).GetMethodSafe(nameof(PathFinder.UpdatePath), true,
				PPatchTools.AnyArguments);
			var forceInvalid = typeof(Navigator_AdvancePath_Patch).GetMethodSafe(nameof(
				ForceInvalid), true, typeof(Navigator));
			var replacement = typeof(Navigator_AdvancePath_Patch).GetMethodSafe(nameof(
				AchievementUpdate), true, typeof(Navigator));
			var start = typeof(Navigator).GetFieldSafe(nameof(Navigator.
				distanceTravelledByNavType), false);
			// Compiler generated name
			var end = typeof(Dictionary<NavType, int>).GetMethodSafe("set_Item", false,
				typeof(NavType), typeof(int));
			var options = FastTrackOptions.Instance;
			bool cache = options.CachePaths, achieve = options.DisableAchievements !=
				FastTrackOptions.AchievementDisable.Never;
			int state = 0;
			if (start != null && end != null && target != null && forceInvalid != null)
				foreach (var instr in instructions) {
					if (achieve) {
						if (state == 0 && instr.Is(OpCodes.Ldfld, start)) {
							instr.opcode = OpCodes.Pop;
							instr.operand = null;
							yield return instr;
							state = 1;
						} else if (state == 1 && instr.Is(OpCodes.Callvirt, end)) {
							state = 2;
							yield return new CodeInstruction(OpCodes.Ldarg_0);
							instr.opcode = OpCodes.Call;
							instr.operand = replacement;
#if DEBUG
							PUtil.LogDebug("Patched Navigator.AdvancePath [Achievement]");
#endif
						}
					}
					if (state != 1) {
						if (cache && target != null && forceInvalid != null && instr.Is(
								OpCodes.Call, target)) {
							// Load "this"
							yield return new CodeInstruction(OpCodes.Ldarg_0);
							yield return new CodeInstruction(OpCodes.Call, forceInvalid);
#if DEBUG
							PUtil.LogDebug("Patched Navigator.AdvancePath [Cache]");
#endif
						}
						yield return instr;
					}
				}
			else
				foreach (var instr in instructions)
					yield return instr;
			if (state != 2 && achieve)
				PUtil.LogWarning("Unable to patch Navigator.AdvancePath");
		}
	}

	/// <summary>
	/// Applied to Navigator.PathProbeTask to use the path cache first.
	/// </summary>
	[HarmonyPatch(typeof(Navigator.PathProbeTask), nameof(Navigator.PathProbeTask.Run))]
	public static class Navigator_PathProbeTask_Run_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before Run runs.
		/// </summary>
		internal static bool Prefix(Navigator ___navigator) {
			// If nothing has changed since last time, it is a hit!
			var cached = PathCacher.Lookup(___navigator.PathProber);
			bool hit = cached.CheckAndMarkValid();
			Metrics.DebugMetrics.LogHit(hit);
			return !hit;
		}
	}

	/// <summary>
	/// Applied to Navigator to force update the path cache (even if "clean") every 4000ms.
	/// </summary>
	[HarmonyPatch(typeof(Navigator), nameof(Navigator.Sim4000ms))]
	public static class Navigator_Sim4000ms_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.CachePaths || options.AsyncPathProbe;
		}

		/// <summary>
		/// Applied before Sim4000ms runs.
		/// </summary>
		internal static bool Prefix(Navigator __instance) {
			var options = FastTrackOptions.Instance;
			if (__instance != null && options.CachePaths)
				PathCacher.Lookup(__instance.PathProber).MarkInvalid();
			return !options.AsyncPathProbe;
		}
	}

	/// <summary>
	/// Applied to Navigator to force update the path cache once when the navigator is forced
	/// to stop moving. This can happen due to falls, entombment...
	/// </summary>
	[HarmonyPatch(typeof(Navigator), nameof(Navigator.Stop))]
	public static class Navigator_Stop_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied after Stop runs.
		/// </summary>
		internal static void Postfix(Navigator __instance, bool arrived_at_destination) {
			if (__instance != null && !arrived_at_destination)
				PathCacher.Lookup(__instance.PathProber).MarkInvalid();
		}
	}

	/// <summary>
	/// Applied to PathProber to remove the instance from the cache when it is destroyed.
	/// </summary>
	[HarmonyPatch(typeof(PathProber), nameof(PathProber.OnCleanUp))]
	public static class PathProber_OnCleanUp_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before OnCleanUp runs.
		/// </summary>
		internal static void Prefix(PathProber __instance) {
			if (__instance != null)
				PathCacher.Cleanup(__instance);
		}
	}
}
