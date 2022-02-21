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
using KMod;
using PeterHan.FastTrack.Metrics;
using PeterHan.FastTrack.SensorPatches;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Patches which will be applied via annotations for FastTrack.
	/// </summary>
	public sealed class FastTrackPatches : KMod.UserMod2 {
		// Workaround for private nav grids + AddNavGrid being inlined
		private static readonly IDetouredField<Pathfinding, List<NavGrid>> NAV_GRID_LIST =
			PDetours.DetourField<Pathfinding, List<NavGrid>>("NavGrids");

		/// <summary>
		/// Caches the value of the debug flag.
		/// </summary>
		private static bool debug = false;

		/// <summary>
		/// Initializes the nav grids on game start, since Pathfinding.AddNavGrid gets inlined.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			var inst = Game.Instance;
			var options = FastTrackOptions.Instance;
			if (options.CachePaths) {
				var fences = NavFences.AllFences;
				PathCacher.Init();
				foreach (var nav_grid in NAV_GRID_LIST.Get(Pathfinding.Instance)) {
					string id = nav_grid.id;
#if DEBUG
					PUtil.LogDebug("Add nav grid: {0}".F(id));
#endif
					if (fences.TryGetValue(id, out NavFences current))
						current.Reset();
					else
						fences.Add(id, new NavFences());
				}
			}
			// Slices updates to Duplicant sensors
			if (inst != null) {
				var go = inst.gameObject;
				if (options.SensorOpts || options.PickupOpts)
					go.AddOrGet<SensorWrapperUpdater>();
				if (options.AsyncPathProbe)
					go.AddOrGet<PathProbeJobManager>();
				// If debugging is on, start logging
				if (debug)
					go.AddOrGet<DebugMetrics>();
			}
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			base.OnAllModsLoaded(harmony, mods);
			// Manual patch in the rewritten FetchManager.UpdatePickups only if Efficient
			// Supply is not enabled
			if (FastTrackOptions.Instance.FastUpdatePickups) {
				if (PPatchTools.GetTypeSafe("PeterHan.EfficientFetch.EfficientFetchManager") ==
						null) {
					harmony.Patch(typeof(FetchManager.FetchablesByPrefabId),
						nameof(FetchManager.FetchablesByPrefabId.UpdatePickups),
						prefix: new HarmonyMethod(typeof(FetchManagerFastUpdate),
						nameof(FetchManagerFastUpdate.Prefix)));
					PUtil.LogDebug("Patched FetchManager for fast pickup updates");
				} else
					PUtil.LogWarning("Disabling fast pickup updates: Efficient Supply active");
			}
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(FastTrackOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(FastTrackPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			debug = FastTrackOptions.Instance.Metrics;
		}

		/// <summary>
		/// Applied to multiple methods in Grid to pre-emptively update the path grid when
		/// access control is modified.
		/// </summary>
		[HarmonyPatch]
		public static class Grid_Restrictions_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

			internal static IEnumerable<MethodBase> TargetMethods() {
				var targetType = typeof(Grid);
				return new List<MethodBase>(4) {
					targetType.GetMethodSafe(nameof(Grid.ClearRestriction), true, typeof(int),
						typeof(int)),
					targetType.GetMethodSafe(nameof(Grid.RegisterRestriction), true,
						typeof(int), typeof(Grid.Restriction.Orientation)),
					targetType.GetMethodSafe(nameof(Grid.SetRestriction), true, typeof(int),
						typeof(int), typeof(Grid.Restriction.Directions)),
					targetType.GetMethodSafe(nameof(Grid.UnregisterRestriction), true,
						typeof(int))
				};
			}

			/// <summary>
			/// Applied after each of the target methods run.
			/// </summary>
			internal static void Postfix(int cell) {
				var fences = NavFences.AllFences;
				// Access control only affects dupes and (DLC) rovers
				if (fences.TryGetValue("MinionNavGrid", out NavFences dupeGrid))
					dupeGrid.UpdateSerial(cell);
				if (fences.TryGetValue("RobotNavGrid", out NavFences roverGrid))
					roverGrid.UpdateSerial(cell);
			}
		}

		/// <summary>
		/// Applied to LoopingSoundManager to reduce sound updates to every other frame.
		/// </summary>
		[HarmonyPatch(typeof(LoopingSoundManager), nameof(LoopingSoundManager.
			RenderEveryTick))]
		public static class LoopingSoundManager_RenderEveryTick_Patch {
			/// <summary>
			/// Whether sounds were updated last frame.
			/// </summary>
			internal static bool updated;

			internal static bool Prepare() => FastTrackOptions.Instance.ReduceSoundUpdates;

			/// <summary>
			/// Applied before RenderEveryTick runs.
			/// </summary>
			internal static bool Prefix() {
				bool wasUpdated = updated;
				updated = !updated;
				return wasUpdated;
			}
		}

		/// <summary>
		/// Applied to MinionConfig to add an instance of SensorWrapper.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.OnSpawn))]
		public static class MinionConfig_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				var opts = FastTrackOptions.Instance;
				if (opts.SensorOpts || opts.PickupOpts)
					go.AddOrGet<SensorWrapper>();
			}
		}

		/// <summary>
		/// Applied to NavGrid to mark all cells as invalid serials when it is initialized.
		/// </summary>
		[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.InitializeGraph))]
		public static class NavGrid_InitializeGraph_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

			/// <summary>
			/// Applied before InitializeGraph runs.
			/// </summary>
			internal static void Postfix(NavGrid __instance) {
				if (NavFences.AllFences.TryGetValue(__instance.id, out NavFences fences))
					fences.UpdateSerial();
			}
		}

		/// <summary>
		/// Applied to NavGrid to replace unnecessary allocations with a Clear call.
		/// </summary>
		[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.UpdateGraph), new Type[0])]
		public static class NavGrid_UpdateGraph_Patch {
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
						}
						instr = next;
					}
				}
				return instructions;
			}
		}

		/// <summary>
		/// Applied to NavGrid to track serial numbers.
		/// </summary>
		[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.UpdateGraph), typeof(HashSet<int>))]
		public static class NavGrid_UpdateGraph2_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

			/// <summary>
			/// Applied after UpdateGraph runs.
			/// </summary>
			internal static void Postfix(NavGrid __instance, HashSet<int> dirty_nav_cells) {
				if (dirty_nav_cells.Count > 0 && NavFences.AllFences.TryGetValue(__instance.id,
						out NavFences fences))
					fences.UpdateSerial(dirty_nav_cells);
			}
		}

		/// <summary>
		/// Applied to Navigator.PathProbeTask to estimate the hitrate.
		/// </summary>
		[HarmonyPatch(typeof(Navigator.PathProbeTask), nameof(Navigator.PathProbeTask.Run))]
		public static class Navigator_PathProbeTask_Run_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

			internal static bool Prefix(Navigator ___navigator, int ___cell) {
				// If nothing has changed since last time, it is a hit!
				var cached = PathCacher.Lookup(___navigator.PathProber);
				bool hit = cached.CheckAndUpdate(___navigator, ___cell);
				if (debug)
					DebugMetrics.LogHit(hit);
				return !hit;
			}
		}
	}
}
