/*
 * Copyright 2026 Peter Han
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
using System.Collections.Concurrent;
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
	/// Applied to Navigator to detect when a cache hit occurs and avoid releasing the result.
	/// </summary>
	/// <summary>
	/// Patches TickFrame to null-guard PathFinderAbilities.RecycleClone on WorkResult's
	/// abilitiesInstance field, which Aquatic added. Execute sets it; FT skips Execute on
	/// cache hits, so abilitiesInstance is null — guard prevents the NPE.
	/// </summary>
	[HarmonyPatch(typeof(AsyncPathProber.Manager), "TickFrame")]
	public static class TickFrame_AbilitiesInstance_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.CachePaths || (options.FastReachability && options.
				ChorePriorityMode == FastTrackOptions.NextChorePriority.Delay);
		}

		// ponytail: null-safe wrapper so skipped-Execute cache hits don't crash TickFrame
		internal static void SafeRecycleClone(PathFinderAbilities abilities) {
			abilities?.RecycleClone();
		}

		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var abilitiesField = typeof(AsyncPathProber.WorkResult).GetFieldSafe(
				"abilitiesInstance", false);
			var recycleMethod = typeof(PathFinderAbilities).GetMethodSafe("RecycleClone", false);
			var safeMethod = typeof(TickFrame_AbilitiesInstance_Patch).GetMethodSafe(
				nameof(SafeRecycleClone), true, typeof(PathFinderAbilities));
			if (abilitiesField == null || recycleMethod == null || safeMethod == null) {
				PUtil.LogWarning("FastTrack: TickFrame null-guard fields not found");
				return instructions;
			}
			var instList = new List<CodeInstruction>(instructions);
			for (int i = 0; i < instList.Count - 1; i++) {
				// Pattern: ldfld abilitiesInstance → callvirt RecycleClone
				if (instList[i].opcode == OpCodes.Ldfld &&
						instList[i].operand is System.Reflection.FieldInfo fi &&
						fi == abilitiesField &&
						instList[i + 1].opcode == OpCodes.Callvirt &&
						instList[i + 1].operand is System.Reflection.MethodInfo mi &&
						mi == recycleMethod) {
					instList[i + 1] = new CodeInstruction(OpCodes.Call, safeMethod);
					break;
				}
			}
			return instList;
		}
	}

	[HarmonyPatch(typeof(Navigator), nameof(Navigator.TakeResult))]
	public static class Navigator_TakeResult_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied before TakeResult runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref AsyncPathProber.WorkResult result,
				ref PathGrid __result) {
			if (WorkOrder_Execute_Patch.CacheHitNavigators.TryRemove(result.navigator,
					out _)) {
				// Cache hit: release the new grid that TickFrame allocated but we don't need.
				// The navigator's existing grid stays valid, so return null (no swap).
				var manager = AsyncPathProber.Instance;
				var newGrid = result.pathGrid;
				if (manager != null && newGrid != null)
					manager.gridPool[newGrid.AllocatedClassification].Release(newGrid);
				// __result stays null — navigator keeps its current PathGrid
				return false;
			}
			return true;
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
	/// Applied to AsyncPathProber.WorkOrder.Execute to handle delayed priority mode and the
	/// path cache.
	/// </summary>
	[HarmonyPatch(typeof(AsyncPathProber.WorkOrder), nameof(AsyncPathProber.WorkOrder.Execute))]
	public static class WorkOrder_Execute_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.CachePaths || (options.FastReachability && options.
				ChorePriorityMode == FastTrackOptions.NextChorePriority.Delay);
		}

		// ponytail: Aquatic's TickFrame reads result.pathGrid after Execute returns, so we
		// cannot null it to signal a cache hit. Execute runs on a worker thread; TakeResult
		// runs on the main thread (inside TickFrame) — [ThreadStatic] doesn't cross that
		// boundary. Use a ConcurrentDictionary keyed by Navigator for cross-thread signaling.
		internal static readonly ConcurrentDictionary<Navigator, bool> CacheHitNavigators =
			new ConcurrentDictionary<Navigator, bool>();

		/// <summary>
		/// Applied before Execute runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ref AsyncPathProber.WorkOrder __instance, out bool __state,
				ref AsyncPathProber.WorkResult result) {
			// Path cache hits should skip the method
			var options = FastTrackOptions.Instance;
			var nav = __instance.navigator;
			PathGrid originalGrid = nav.PathGrid;
			bool miss = !options.CachePaths || !PathCacher.CheckCache(originalGrid,
				__instance.originCell);
			var reachables = result.reachableCells;
			if (!miss) {
				// Do not bump the serial number, the path grid is still accurate using the old
				// one
				if (__instance.computeReachables && reachables != null) {
					reachables.Clear();
					reachables.AddRange(nav.occupiedCells);
					reachables.Sort();
					// No changes
					result.noLongerReachableCells.Clear();
					result.newlyReachableCells.Clear();
				}
				// Signal TakeResult (main thread) to release the new grid instead of installing it
				CacheHitNavigators.TryAdd(nav, true);
				// Manually release for chores
				if (options.FastReachability && options.ChorePriorityMode ==
						FastTrackOptions.NextChorePriority.Delay)
					PriorityBrainScheduler.Instance.PathReady(nav);
			}
			__state = miss;
			return miss;
		}

		/// <summary>
		/// Applied after Execute runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(Navigator ___navigator, bool __state) {
			var options = FastTrackOptions.Instance;
			if (__state && options.FastReachability && options.ChorePriorityMode ==
					FastTrackOptions.NextChorePriority.Delay)
				// Bump the brain out of the the waiting list
				PriorityBrainScheduler.Instance.PathReady(___navigator);
		}
	}

	/// <summary>
	/// Applied to PathProber.Run to handle delayed priority mode and the path cache.
	/// </summary>
	[HarmonyPatch(typeof(PathProber), nameof(PathProber.Run), typeof(Navigator),
		typeof(List<int>))]
	public static class PathProber_RunSync_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.CachePaths || (options.FastReachability && options.
				ChorePriorityMode == FastTrackOptions.NextChorePriority.Delay);
		}
		
		/// <summary>
		/// Applied before Run runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(out bool __state, Navigator navigator,
				List<int> found_cells) {
			// Path cache hits should skip the method
			var options = FastTrackOptions.Instance;
			bool miss = !options.CachePaths || !PathCacher.CheckCache(navigator.PathGrid,
				navigator.cachedCell);
			if (!miss) {
				if (found_cells != null && navigator.reportOccupation) {
					found_cells.Clear();
					found_cells.AddRange(navigator.occupiedCells);
				}
				// Manually release for chores
				if (options.FastReachability && options.ChorePriorityMode ==
						FastTrackOptions.NextChorePriority.Delay)
					PriorityBrainScheduler.Instance.PathReady(navigator);
			}
			__state = miss;
			return miss;
		}

		/// <summary>
		/// Applied after Run runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(Navigator navigator, bool __state) {
			var options = FastTrackOptions.Instance;
			if (__state && options.FastReachability && options.ChorePriorityMode ==
					FastTrackOptions.NextChorePriority.Delay)
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
	public static class PathProber_RunAsync_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Ends a path grid update and marks it as valid.
		/// </summary>
		/// <param name="grid">The path grid to update.</param>
		private static void EndUpdate(PathGrid grid) {
			grid.freshlyOccupiedCells = null;
			PathCacher.SetValid(grid, true);
		}

		/// <summary>
		/// Transpiles Run to use a slightly altered EndUpdate method instead.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var target = typeof(PathGrid).GetMethodSafe(nameof(PathGrid.EndUpdate), false);
			var replacement = typeof(PathProber_RunAsync_Patch).GetMethodSafe(nameof(
				EndUpdate), true, typeof(PathGrid));
			bool patched = false;
			foreach (var instr in instructions) {
				if (target != null && replacement != null && instr.opcode == OpCodes.
						Callvirt && instr.operand is MethodBase info && info == target) {
					instr.opcode = OpCodes.Call;
					instr.operand = replacement;
#if DEBUG
					PUtil.LogDebug("Patched PathProber.Run [E]");
#endif
					patched = true;
				}
				yield return instr;
			}
			if (!patched)
				PUtil.LogWarning("Unable to patch PathProber.Run [E]");
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

	/// <summary>
	/// Applied to NavGrid to invalidate cached paths whose region overlaps cells
	/// that just changed. The base game refreshes the nav grid for dirty cells
	/// here, but FastTrack's path cache has no terrain-change signal of its own, so
	/// an idle navigator (typically a swim-nav critter that does not constantly
	/// re-path the way a chore-driven Duplicant does) can follow a stale cached
	/// path into a newly-placed tile and get stuck. Same bug class as the
	/// SuitMarker fix above.
	/// </summary>
	[HarmonyPatch(typeof(NavGrid), nameof(NavGrid.UpdateGraph), new[] {
		typeof(List<int>) })]
	public static class NavGrid_UpdateGraph_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CachePaths;

		/// <summary>
		/// Applied after UpdateGraph runs to drop caches over the changed region.
		/// The dirty cells are already expanded by the nav update range upstream, so
		/// a navigator adjacent to a change is covered.
		///
		/// __0 is the first original argument (the dirty cell list) by position.
		/// Harmony injects original parameters by NAME; receiving it positionally
		/// avoids a silent no-op if the publicized assembly's parameter name ever
		/// differs from the source name.
		/// </summary>
		internal static void Postfix(List<int> __0) {
			var dirtyCells = __0;
			int n;
			if (dirtyCells == null || (n = dirtyCells.Count) < 1)
				return;
			int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue,
				maxY = int.MinValue;
			for (int i = 0; i < n; i++) {
				int cell = dirtyCells[i];
				// Guard: an invalid (-1) or out-of-range cell would corrupt the bbox.
				// Grid.CellToXY is pure modulo (no throw), so a bad cell silently
				// stretches the box to cover the whole map and invalidates the entire
				// cache — defeating the optimization. Skip anything not a real cell.
				if (!Grid.IsValidCell(cell))
					continue;
				Grid.CellToXY(cell, out int x, out int y);
				if (x < minX)
					minX = x;
				if (x > maxX)
					maxX = x;
				if (y < minY)
					minY = y;
				if (y > maxY)
					maxY = y;
			}
			// No valid cell updated the bounds — nothing to invalidate (avoids
			// passing an inverted box).
			if (minX > maxX)
				return;
			PathCacher.InvalidateRegion(minX, minY, maxX, maxY);
		}
	}
}
