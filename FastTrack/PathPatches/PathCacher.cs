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
		/// The current frame time.
		/// </summary>
		private static double now;

		/// <summary>
		/// The (time, bounding box) of the most recent InvalidateRegion call, used to
		/// skip the redundant per-nav-grid invalidations of a single terrain change.
		/// </summary>
		private static double lastInvalidateTime = double.NaN;
		private static int lastInvalidateMinX, lastInvalidateMinY, lastInvalidateMaxX,
			lastInvalidateMaxY;

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
			bool hit = IsValid(grid) && (!grid.applyOffset || Grid.XYToCell(grid.rootX + grid.
				widthInCells / 2, grid.rootY + grid.heightInCells / 2) == cell);
			if (FastTrackOptions.Instance.Metrics)
				Metrics.DebugMetrics.PATH_CACHE.Log(hit);
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
		/// Invalidates every cached path whose grid region overlaps the given cell
		/// bounding box. Called when terrain changes so affected navigators re-probe
		/// instead of following a stale cached path through a newly-changed cell.
		/// </summary>
		/// <param name="minX">Bounding box minimum X in cell coordinates.</param>
		/// <param name="minY">Bounding box minimum Y in cell coordinates.</param>
		/// <param name="maxX">Bounding box maximum X in cell coordinates.</param>
		/// <param name="maxY">Bounding box maximum Y in cell coordinates.</param>
		internal static void InvalidateRegion(int minX, int minY, int maxX, int maxY) {
			// A single terrain change dirties every nav grid (~12 of them), and each
			// one's UpdateGraph fires this with the same box in the same frame. Skip
			// the redundant cache scans by remembering the last (time, box): only the
			// first scan for a given change does work. Cross-frame changes carry a
			// different time and always run, so this never skips a real invalidation.
			if (now == lastInvalidateTime && minX == lastInvalidateMinX &&
					minY == lastInvalidateMinY && maxX == lastInvalidateMaxX &&
					maxY == lastInvalidateMaxY)
				return;
			lastInvalidateTime = now;
			lastInvalidateMinX = minX;
			lastInvalidateMinY = minY;
			lastInvalidateMaxX = maxX;
			lastInvalidateMaxY = maxY;
			// ponytail: bbox over-invalidation — a grid intersecting the box but not an
			// actual dirty cell still gets dropped, which only costs a lazy re-probe.
			// Upgrade to a per-cell test or spatial index only if a profile shows this
			// scan matters.
			foreach (var pair in pathCache) {
				var grid = pair.Key;
				if (grid == null)
					continue;
				// Bounded probe grids (applyOffset) carry their window position in
				// rootX/rootY (set in PathGrid.BeginUpdate). Full-map grids
				// (!applyOffset) leave rootX/rootY at 0 with width/height spanning the
				// whole map, so the same AABB test always intersects them — which is
				// correct: a full-map path can be affected by any terrain change.
				int gx = grid.rootX, gy = grid.rootY;
				// AABB overlap: grid covers [gx, gx+width) x [gy, gy+height).
				if (gx <= maxX && gx + grid.widthInCells > minX && gy <= maxY &&
						gy + grid.heightInCells > minY)
					pathCache.TryRemove(grid, out _);
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
