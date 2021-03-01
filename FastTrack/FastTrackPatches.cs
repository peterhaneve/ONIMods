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

using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<Harmony.CodeInstruction>;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Patches which will be applied via annotations for FastTrack.
	/// </summary>
	public static class FastTrackPatches {
		// Workaround for private nav grids + AddNavGrid being inlined
		private static readonly IDetouredField<Pathfinding, List<NavGrid>> NAV_GRID_LIST =
			PDetours.DetourField<Pathfinding, List<NavGrid>>("NavGrids");

		/// <summary>
		/// Initializes the nav grids on game start, since Pathfinding.AddNavGrid gets inlined.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void InitNavGrids() {
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

		public static void OnLoad() {
			PUtil.InitLibrary();
			PUtil.RegisterPatchClass(typeof(FastTrackPatches));
		}

		/// <summary>
		/// Applied to multiple methods in Grid to pre-emptively update the path grid when
		/// access control is modified.
		/// </summary>
		[HarmonyPatch]
		public static class Grid_Restrictions_Patch {
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
		/// Applied to NavGrid to mark all cells as invalid serials when it is initialized.
		/// </summary>
		[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.InitializeGraph))]
		public static class NavGrid_InitializeGraph_Patch {
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
			internal static bool Prefix(Navigator ___navigator, int ___cell) {
				// If nothing has changed since last time, it is a hit!
				var cached = PathCacher.Lookup(___navigator.PathProber);
				bool hit;
				if (hit = cached.CheckAndUpdate(___navigator, ___cell))
					Interlocked.Increment(ref hits);
				Interlocked.Increment(ref total);
				return !hit;
			}
		}

#if true
		private static volatile int total, hits;

		internal sealed class PathCacheDebugger : KMonoBehaviour, IRender1000ms {
			protected override void OnPrefabInit() {
				base.OnPrefabInit();
				total = 0;
				hits = 0;
			}

			public void Render1000ms(float dt) {
				PUtil.LogDebug("PathProber: {0:D}/{1:D} hits, {2:F1}%".F(hits, total,
					hits * 100.0f / Math.Max(1, total)));
				total = 0;
				hits = 0;
			}
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void GetHitRate() {
			Game.Instance.gameObject.AddOrGet<PathCacheDebugger>();
		}
#endif
	}
}
