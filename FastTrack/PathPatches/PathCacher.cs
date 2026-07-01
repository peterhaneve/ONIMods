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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Caches global pathfind requests, drastically reducing work by avoiding repathing when
	/// nothing has changed.
	/// </summary>
	public static class PathCacher {
		/// <summary>
		/// The number of scaled in-game seconds that will pass before the path cache is
		/// automatically invalidated for an entity.
		/// </summary>
		public const double INVALIDATE_TIME = 6.0;

		/// <summary>
		/// Above this many dirty cells in one InvalidateCells call, a genuine map-wide
		/// event is in progress (e.g. a large cave-in or flood) where most cached grids
		/// would overlap a dirty cell anyway. Per-cell membership testing against every
		/// cached grid is wasted work at that scale, so fall back to wiping the whole
		/// cache instead.
		/// NOTE: NavGrid.UpdateGraph expands each physical dirty tile by the nav update
		/// range BEFORE this runs, so the count here is post-expansion (one dug tile can
		/// become dozens of cells). 8192 keeps ordinary multi-tile digs/floods on the
		/// precise membership path (where the win is) and only falls back to a full wipe
		/// for genuinely map-wide events that would invalidate nearly everything anyway.
		/// The per-cell scan is cheap (bounded, early-break), so erring high is safe.
		/// </summary>
		private const int INVALIDATE_ALL_THRESHOLD = 8192;

		/// <summary>
		/// The current frame time.
		/// </summary>
		private static double now;

		/// <summary>
		/// The frame time of the most recent InvalidateCells call, used to reset the
		/// same-frame dedup set below when a new frame starts.
		/// </summary>
		private static double lastInvalidateTime = double.NaN;

		/// <summary>
		/// Cells already tested for membership against every cached grid this frame. A
		/// single terrain change dirties every nav grid (~16 of them) and each one's
		/// UpdateGraph fires InvalidateCells in the same frame with a dirty-cell list
		/// that differs only by that grid's nav-update expansion range, so the lists
		/// nest around the same root changed cells. Skipping cells already proven (this
		/// frame) to have been checked against the whole pathCache avoids rescanning the
		/// dictionary for them on every repeat call. This is a cell-level dedup rather
		/// than the bounding-box containment check InvalidateRegion used to do, because
		/// once invalidation is precise (per-cell membership) rather than a bbox-overlap
		/// over-approximation, a bbox-containment skip is no longer sound: a grid could
		/// sit inside the earlier (larger) box without containing any of the earlier
		/// call's actual dirty cells, yet contain one of the current call's. Tracking the
		/// literal cell set sidesteps that.
		/// </summary>
		private static readonly HashSet<int> cellsInvalidatedThisFrame = new HashSet<int>();

		/// <summary>
		/// Reusable scratch list of this call's dirty cells not already covered by
		/// cellsInvalidatedThisFrame, to avoid an allocation per call.
		/// </summary>
		private static readonly List<int> newDirtyCells = new List<int>();

		/// <summary>
		/// Map path cache IDs to path cache values.
		/// </summary>
		private static ConcurrentDictionary<PathGrid, double> pathCache;
		
		/// <summary>
		/// Checks to see if the path cache is clean.
		/// </summary>
		/// <param name="grid">The grid that is querying.</param>
		/// <param name="cell">The root cell that will be used for updates.</param>
		/// <returns>true if the cache is clean, or false if it needs to run.</returns>
		internal static bool CheckCache(PathGrid grid, int cell) {
			// If nothing has changed since last time, it is a hit!
			bool valid = IsValid(grid);
			bool hit = valid && (!grid.applyOffset || Grid.XYToCell(grid.rootX + grid.
				widthInCells / 2, grid.rootY + grid.heightInCells / 2) == cell);
			if (FastTrackOptions.Instance.Metrics) {
				Metrics.DebugMetrics.PATH_CACHE.Log(hit);
				if (!hit) {
					// Of the misses: stale entry (!IsValid) vs valid-but-navigator-moved.
					Metrics.DebugMetrics.PATH_CACHE_MISS_INVALID.Log(!valid);
					// Of the stale (!IsValid) misses: expired (still in cache, TTL lapsed ->
					// TTL is the lever) vs absent (removed by invalidation or never cached).
					if (!valid)
						Metrics.DebugMetrics.PATH_CACHE_MISS_EXPIRED.Log(
							pathCache.TryGetValue(grid, out _));
				}
			}
			return hit;
		}

		/// <summary>
		/// Avoid leaking the PathGrids when the game ends.
		/// </summary>
		internal static void Cleanup() {
			pathCache.Clear();
		}

		/// <summary>
		/// When a PathGrid is destroyed, remove its cached information.
		/// </summary>
		/// <param name="grid">The path prober that was destroyed.</param>
		internal static void Cleanup(PathGrid grid) {
			if (grid != null)
				pathCache.TryRemove(grid, out _);
		}

		/// <summary>
		/// When the game is started, reset the path prober caches.
		/// </summary>
		internal static void Init() {
			if (pathCache == null)
				pathCache = new ConcurrentDictionary<PathGrid, double>(4, 128);
			else
				pathCache.Clear();
			lastInvalidateTime = double.NaN;
			cellsInvalidatedThisFrame.Clear();
		}
		
		/// <summary>
		/// Sets all Duplicant paths to invalid.
		/// </summary>
		internal static void InvalidateAllDuplicants() {
			var ids = Components.LiveMinionIdentities;
			int n = ids.Count;
			for (int i = 0; i < n; i++) {
				var id = ids[i];
				Navigator nav;
				// navigator is initialized in a Sim1000...
				if (id != null && (nav = id.navigator) != null)
					pathCache.TryRemove(nav.PathGrid, out _);
			}
		}

		/// <summary>
		/// Invalidates every cached path whose grid window actually contains one of the
		/// given dirty cells. Called when terrain changes so affected navigators
		/// re-probe instead of following a stale cached path through a newly-changed
		/// cell, without dropping unrelated grids that merely happen to overlap the
		/// union bounding box of a scattered dirty-cell list (the old InvalidateRegion
		/// behavior, which collapsed the path cache hit rate under map-wide churn).
		/// </summary>
		/// <param name="dirtyCells">The cells that changed this nav-update cycle, as
		/// passed to NavGrid.UpdateGraph.</param>
		internal static void InvalidateCells(List<int> dirtyCells) {
			int n;
			if (dirtyCells == null || (n = dirtyCells.Count) < 1)
				return;
			if (n > INVALIDATE_ALL_THRESHOLD) {
				// Genuine map-wide event (large cave-in/flood/etc.) — at this scale most
				// cached grids legitimately overlap a dirty cell, so per-cell membership
				// testing against every grid is wasted work. Wipe everything, matching
				// the conservative (never under-invalidate) behavior of the old bbox
				// scan for this case.
				pathCache.Clear();
				return;
			}
			// Same-frame dedup: skip cells already proven (this frame) to have been
			// checked against every cached grid. See cellsInvalidatedThisFrame's doc
			// comment for why this must track the literal cell set rather than a
			// bounding box now that invalidation is precise.
			if (now != lastInvalidateTime) {
				lastInvalidateTime = now;
				cellsInvalidatedThisFrame.Clear();
			}
			newDirtyCells.Clear();
			bool anyValid = false;
			for (int i = 0; i < n; i++) {
				int cell = dirtyCells[i];
				// Guard: an invalid (-1) or out-of-range cell would corrupt CellToXY
				// below. Mirrors the validity check the old Postfix bbox scan used.
				if (!Grid.IsValidCell(cell))
					continue;
				anyValid = true;
				if (cellsInvalidatedThisFrame.Add(cell))
					newDirtyCells.Add(cell);
			}
			// No valid cell at all this call — nothing to invalidate.
			if (!anyValid)
				return;
			// Every valid cell this call was already tested against pathCache earlier
			// this frame (by an overlapping/superset dirty-cell list from another nav
			// grid's UpdateGraph) — every grid relevant to those cells is already gone.
			if (newDirtyCells.Count < 1)
				return;
			int newCount = newDirtyCells.Count;
			foreach (var pair in pathCache) {
				var grid = pair.Key;
				if (grid == null)
					continue;
				// Full-map grids (!applyOffset) have no bounded window — any terrain
				// change can affect a full-map path, so invalidate unconditionally.
				// Bounded probe grids (applyOffset) carry their window position in
				// rootX/rootY/widthInCells/heightInCells (set in PathGrid.BeginUpdate);
				// invalidate iff at least one new dirty cell falls inside that window.
				if (!grid.applyOffset)
					pathCache.TryRemove(grid, out _);
				else {
					int gx = grid.rootX, gy = grid.rootY, gw = grid.widthInCells,
						gh = grid.heightInCells;
					for (int i = 0; i < newCount; i++) {
						Grid.CellToXY(newDirtyCells[i], out int cx, out int cy);
						if (PathCacheGeometry.CellInWindow(cx, cy, gx, gy, gw, gh)) {
							pathCache.TryRemove(grid, out _);
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Checks to see if the grid's cache is valid.
		/// </summary>
		/// <param name="grid">The path grid to look up.</param>
		/// <returns>true if the cache is valid for this ID, or false otherwise.</returns>
		internal static bool IsValid(PathGrid grid) {
			if (grid == null)
				throw new ArgumentNullException(nameof(grid));
			return pathCache.TryGetValue(grid, out double expires) && now < expires;
		}

		/// <summary>
		/// Sets a grid as valid or invalid.
		/// </summary>
		/// <param name="grid">The path grid to look up.</param>
		/// <param name="valid">true if the grid is valid, or false if it is invalid.</param>
		internal static void SetValid(PathGrid grid, bool valid) {
			if (grid == null)
				throw new ArgumentNullException(nameof(grid));
			if (valid)
				pathCache[grid] = now + INVALIDATE_TIME;
			else
				pathCache.TryRemove(grid, out _);
		}

		/// <summary>
		/// Updates the current time.
		/// </summary>
		/// <param name="time">The current scaled game time.</param>
		internal static void UpdateTime(double time) {
			now = time;
		}
	}
}
