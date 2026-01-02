/*
 * Copyright 2025 Peter Han
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
using System.Reflection.Emit;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Applied to ChoreDriver.States to reduce the tracking of the activity reports to
	/// Sim1000 vs Sim200.
	/// </summary>
	[HarmonyPatch(typeof(ChoreDriver.States), nameof(ChoreDriver.States.InitializeStates))]
	public static class ChoreDriver_States_InitializeStates_Patch1 {
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
	/// Applied to ChoreDriver.States to invalidate the path cache when a chore completes if
	/// delaying chore calculation is requested.
	/// </summary>
	[HarmonyPatch(typeof(ChoreDriver.States), nameof(ChoreDriver.States.InitializeStates))]
	public static class ChoreDriver_States_InitializeStates_Patch2 {
		internal static bool Prepare() {
			var opts = FastTrackOptions.Instance;
			return opts.PickupOpts && opts.ChorePriorityMode == FastTrackOptions.
				NextChorePriority.Delay;
		}

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(ChoreDriver.States __instance) {
			__instance.haschore.Exit(smi => {
				var consumer = smi.choreConsumer;
				if (consumer != null && !consumer.consumerState.hasSolidTransferArm) {
					var nav = consumer.navigator;
					if (nav != null)
						PathCacher.SetValid(nav.PathGrid, false);
				}
			});
		}
	}

	/// <summary>
	/// Applied to MoveToLocationTool to flush the path cache upon activating the tool,
	/// which can help when players are trying to save Duplicants at the last second.
	/// </summary>
	[HarmonyPatch(typeof(MoveToLocationTool), nameof(MoveToLocationTool.Activate),
		typeof(Navigator))]
	public static class MoveToLocationTool_Activate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before Activate runs.
		/// </summary>
		internal static void Prefix(Navigator navigator) {
			if (navigator != null)
				PathCacher.SetValid(navigator.PathGrid, false);
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
				PathCacher.SetValid(instance.PathGrid, false);
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
						if (cache && instr.Is(OpCodes.Call, target)) {
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
				PathCacher.SetValid(__instance.PathGrid, false);
		}
	}

	/// <summary>
	/// Applied to PathGrid to add a lock around a race condition in BeginUpdate where rootX
	/// and rootY could be accessed while being updated.
	/// </summary>
	[HarmonyPatch(typeof(PathGrid), nameof(PathGrid.BeginUpdate))]
	public static class PathGrid_BeginUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AsyncPathProbe;

		/// <summary>
		/// Applied before BeginUpdate runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(PathGrid __instance, ushort new_serial_no, int root_cell,
				List<int> found_cells_list) {
			lock (__instance.Cells) {
				__instance.freshlyOccupiedCells = found_cells_list;
				if (__instance.applyOffset) {
					Grid.CellToXY(root_cell, out int rootX, out int rootY);
					__instance.rootX = rootX - (__instance.widthInCells >> 1);
					__instance.rootY = rootY - (__instance.heightInCells >> 1);
				}
				__instance.serialNo = new_serial_no;
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to PathGrid to add a lock and stop a potential racy access in OffsetCell.
	/// </summary>
	[HarmonyPatch(typeof(PathGrid), nameof(PathGrid.OffsetCell))]
	public static class PathGrid_OffsetCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AsyncPathProbe;

		/// <summary>
		/// Applied before OffsetCell runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(PathGrid __instance, int cell, ref int __result) {
			int newCell = cell;
			if (__instance.applyOffset) {
				int w = __instance.widthInCells, h = __instance.heightInCells;
				Grid.CellToXY(cell, out int x, out int y);
				lock (__instance.Cells) {
					int rx = __instance.rootX, ry = __instance.rootY;
					newCell = (x < rx || x >= rx + w || y < ry || y >= ry + h) ? -1 :
						(y - ry) * w + (x - rx);
				}
			}
			__result = newCell;
			return false;
		}
	}
	
	/// <summary>
	/// Applied to AsyncPathProber.Workorder.Execute to handle delayed priority mode.
	/// </summary>
	[HarmonyPatch(typeof(AsyncPathProber.WorkOrder), nameof(AsyncPathProber.WorkOrder.Execute))]
	public static class WorkOrder_Execute_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.FastReachability && options.ChorePriorityMode ==
				FastTrackOptions.NextChorePriority.Delay;
		}

		/// <summary>
		/// Applied after Execute runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(ref AsyncPathProber.WorkOrder __instance) {
			// Bump the brain out of the the waiting list
			PriorityBrainScheduler.Instance.PathReady(__instance.navigator);
		}
	}

	/// <summary>
	/// Applied to PathProber.Run to handle delayed priority mode.
	/// </summary>
	[HarmonyPatch(typeof(PathProber), nameof(PathProber.Run), typeof(Navigator),
		typeof(List<int>))]
	public static class PathProber_RunSync_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.FastReachability && options.ChorePriorityMode ==
				FastTrackOptions.NextChorePriority.Delay;
		}

		/// <summary>
		/// Applied after Run runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(Navigator navigator) {
			// Bump the brain out of the the waiting list
			PriorityBrainScheduler.Instance.PathReady(navigator);
		}
	}

	/// <summary>
	/// Applied to PathProber.Run to set full path probes as the source of truth for
	/// group probing and update the path cache when it finishes.
	/// </summary>
	[HarmonyPatch(typeof(PathProber), nameof(PathProber.Run), typeof(int), typeof(
		PathFinderAbilities), typeof(NavGrid), typeof(NavType), typeof(PathGrid),
		typeof(ushort), typeof(PathFinder.PotentialScratchPad), typeof(PathFinder.
		PotentialList), typeof(PathFinder.PotentialPath.Flags), typeof(List<int>))]
	public static class PathProber_Run_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.FastReachability || options.CachePaths;
		}

		/// <summary>
		/// Checks to see if the path cache is clean.
		/// </summary>
		/// <param name="grid">The grid that is querying.</param>
		/// <param name="cell">The root cell that will be used for updates.</param>
		/// <returns>true if the cache is clean, or false if it needs to run.</returns>
		private static bool CheckCache(PathGrid grid, int cell) {
			// If nothing has changed since last time, it is a hit!
			bool hit = PathCacher.IsValid(grid) && (!grid.applyOffset || Grid.XYToCell(
				grid.rootX + grid.widthInCells / 2, grid.rootY + grid.heightInCells / 2) ==
				cell);
			if (FastTrackOptions.Instance.Metrics)
				Metrics.DebugMetrics.PATH_CACHE.Log(hit);
			return hit;
		}

		/// <summary>
		/// Ends a path grid update and marks it as valid
		/// </summary>
		/// <param name="grid">The path grid to update.</param>
		private static void EndUpdate(PathGrid grid) {
			grid.freshlyOccupiedCells = null;
			if (FastTrackOptions.Instance.CachePaths)
				PathCacher.SetValid(grid, true);
		}

		/// <summary>
		/// Transpiles Run to use a slightly altered EndUpdate method instead.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var target = typeof(PathGrid).GetMethodSafe(nameof(PathGrid.EndUpdate), false);
			var replacement = typeof(PathProber_Run_Patch).GetMethodSafe(nameof(
				EndUpdate), true, typeof(PathGrid));
			var checker = typeof(PathProber_Run_Patch).GetMethodSafe(nameof(
				CheckCache), true, typeof(PathGrid), typeof(int));
			bool patched = false;
			var end = generator.DefineLabel();
			// Sadly streaming is not possible with the "label last RET"
			var method = new List<CodeInstruction>(instructions);
			if (FastTrackOptions.Instance.CachePaths) {
				if (checker != null) {
					method.InsertRange(0, new[] {
						new CodeInstruction(OpCodes.Ldarg_S, 4),
						// Argument #1 is "root_cell"
						new CodeInstruction(OpCodes.Ldarg_0),
						new CodeInstruction(OpCodes.Call, checker),
						new CodeInstruction(OpCodes.Brtrue_S, end)
					});
#if DEBUG
					PUtil.LogDebug("Patched PathProber.Run [C]");
#endif
				} else
					PUtil.LogWarning("Unable to patch PathProber.Run [C]");
			}
			if (target != null && replacement != null) {
				int n = method.Count;
				for (int i = 0; i < n && !patched; i++) {
					var instr = method[i];
					if (instr.Is(OpCodes.Callvirt, target)) {
						instr.opcode = OpCodes.Call;
						instr.operand = replacement;
#if DEBUG
						PUtil.LogDebug("Patched PathProber.Run [E]");
#endif
						patched = true;
					}
				}
			}
			// Label the last RET
			for (int i = method.Count - 1; i > 0; i--) {
				var instr = method[i];
				if (instr.opcode == OpCodes.Ret) {
					// Add the label
					var labels = instr.labels;
					if (labels == null)
						instr.labels = labels = new List<Label>(2);
					labels.Add(end);
					break;
				}
			}
			if (!patched)
				PUtil.LogWarning("Unable to patch PathProber.Run [E]");
			return method;
		}
	}

	/// <summary>
	/// Applied to Navigator to remove the path grid from the cache when it is destroyed.
	/// </summary>
	[HarmonyPatch(typeof(Navigator), nameof(Navigator.OnCleanUp))]
	public static class Navigator_OnCleanUp_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before OnCleanUp runs.
		/// </summary>
		internal static void Prefix(Navigator __instance) {
			if (__instance != null)
				PathCacher.Cleanup(__instance.PathGrid);
		}
	}

	/// <summary>
	/// Applied to SuitMarker to clear all Duplicant path caches if an important flag changes.
	/// Fixes a base game bug where turning checkpoints on or off (or toggling vacancy) does
	/// not invalidate current paths.
	/// </summary>
	[HarmonyPatch(typeof(SuitMarker), nameof(SuitMarker.UpdateGridFlag))]
	public static class SuitMarker_UpdateGridFlag_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before UpdateGridFlag runs.
		/// </summary>
		internal static void Prefix(SuitMarker __instance, bool state,
				Grid.SuitMarker.Flags flag) {
			if (((__instance.gridFlags & flag) == 0) == state && flag != Grid.SuitMarker.Flags.
					Rotated && FastTrackMod.GameRunning)
				// Just to be safe
				PathCacher.InvalidateAllDuplicants();
		}
	}
}
