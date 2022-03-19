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
using PeterHan.PLib.Detours;
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
	/// Applied to Grid to pre-emptively update the path grid when Atmo Suit checkpoints
	/// (or their counterparts) update.
	/// </summary>
	[HarmonyPatch(typeof(Grid), nameof(Grid.UpdateSuitMarker))]
	public static class Grid_UpdateSuitMarker_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before UpdateSuitMarker runs.
		/// </summary>
		internal static void Prefix(int cell, Grid.SuitMarker.Flags flags,
				PathFinder.PotentialPath.Flags pathFlags) {
			var fences = NavFences.AllFences;
			if (Grid.TryGetSuitMarkerFlags(cell, out Grid.SuitMarker.Flags oldFlags,
					out PathFinder.PotentialPath.Flags oldPathFlags) && (oldFlags != flags ||
					oldPathFlags != pathFlags) && fences.TryGetValue("MinionNavGrid",
					out NavFences dupeGrid))
				dupeGrid.UpdateSerial(cell);
		}
	}

	/// <summary>
	/// Applied to NavGrid to mark all cells as invalid serials when it is initialized.
	/// </summary>
	[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.InitializeGraph))]
	public static class NavGrid_InitializeGraph_Patch {
		// Workaround for private nav grids + AddNavGrid being inlined
		private static readonly IDetouredField<Pathfinding, List<NavGrid>> NAV_GRID_LIST =
			PDetours.DetourField<Pathfinding, List<NavGrid>>("NavGrids");

		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Cleans up the fences after the game ends to avoid leaking pathfinding data.
		/// </summary>
		internal static void Cleanup() {
			NavFences.AllFences.Clear();
		}

		/// <summary>
		/// Initializes the nav grids when the game starts.
		/// </summary>
		internal static void Init() {
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
		/// <summary>
		/// Transpiles UpdateGraph to clean up allocations and mark cells dirty when required.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var setType = typeof(HashSet<int>);
			var clearMethod = setType.GetMethodSafe(nameof(HashSet<int>.Clear), false);
			var instructions = new List<CodeInstruction>(method);
			var dirtyCellsField = typeof(NavGrid).GetFieldSafe("DirtyCells", false);
			var updateSerials = typeof(NavGrid_UpdateGraph_Patch).GetMethodSafe(nameof(
				NavGrid_UpdateGraph_Patch.UpdateSerials), true, typeof(NavGrid),
				typeof(ISet<int>));
			int n = instructions.Count;
			if (clearMethod == null)
				// Should be unreachable
				PUtil.LogError("What happened to HashSet.Clear?");
			else {
				CodeInstruction instr;
				if (FastTrackOptions.Instance.CachePaths) {
					// Effectively prefix with a call to UpdateSerials
					if (dirtyCellsField != null && updateSerials != null)
						instructions.InsertRange(0, new CodeInstruction[] {
							new CodeInstruction(OpCodes.Ldarg_0),
							new CodeInstruction(OpCodes.Dup),
							new CodeInstruction(OpCodes.Ldfld, dirtyCellsField),
							new CodeInstruction(OpCodes.Call, updateSerials)
						});
					else
						PUtil.LogWarning("Unable to mark cells dirty in NavGrid.UpdateGraph");
				}
				instr = instructions[0];
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
						PUtil.LogDebug("Patched NavGrid.UpdateGraph");
#endif
					}
					instr = next;
				}
			}
			return instructions;
		}

		/// <summary>
		/// Updates the serial numbers of a set of cells.
		/// </summary>
		/// <param name="instance">The nav grid to update.</param>
		/// <param name="dirty">The cells which were changed.</param>
		private static void UpdateSerials(NavGrid instance, ISet<int> dirty) {
			if (dirty.Count > 0 && NavFences.AllFences.TryGetValue(instance.id,
					out NavFences fences))
				fences.UpdateSerial(dirty);
		}
	}

	/// <summary>
	/// Applied to Pathfinding to make debug refresh nav cell update the serial numbers.
	/// </summary>
	[HarmonyPatch(typeof(Pathfinding), nameof(Pathfinding.RefreshNavCell))]
	public static class Pathfinding_RefreshNavCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before RefreshNavCell runs.
		/// </summary>
		internal static void Prefix(int cell) {
			foreach (var fence in NavFences.AllFences)
				fence.Value.UpdateSerial(cell);
		}
	}

	/// <summary>
	/// Applied to Navigator.PathProbeTask to use the path cache first.
	/// </summary>
	[HarmonyPatch(typeof(Navigator.PathProbeTask), nameof(Navigator.PathProbeTask.Run))]
	public static class Navigator_PathProbeTask_Run_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		internal static bool Prefix(Navigator ___navigator, int ___cell) {
			// If nothing has changed since last time, it is a hit!
			var cached = PathCacher.Lookup(___navigator.PathProber);
			bool hit = cached.CheckAndUpdate(___navigator, ___cell);
			Metrics.DebugMetrics.LogHit(hit);
			return !hit;
		}
	}
}
