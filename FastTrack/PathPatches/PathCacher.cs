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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;

using PathFlags = PathFinder.PotentialPath.Flags;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Caches global pathfind requests, drastically reducing work by avoiding repathing when
	/// nothing has changed.
	/// </summary>
	public sealed class PathCacher {
		private static readonly IDetouredField<Navigator, PathFinderAbilities> ABILITIES =
			PDetours.DetourField<Navigator, PathFinderAbilities>("abilities");

		/// <summary>
		/// Map path cache IDs to path cache values.
		/// </summary>
		private static IDictionary<PathProber, PathCacher> pathCache;

		/// <summary>
		/// PathFinder.InvalidHandle is not even readonly!
		/// </summary>
		public const int InvalidHandle = -1;

		/// <summary>
		/// Avoid leaking the PathProbers when the game ends.
		/// </summary>
		internal static void Cleanup() {
			pathCache.Clear();
		}

		/// <summary>
		/// When a PathProber is destroyed, remove its cached information.
		/// </summary>
		/// <param name="prober">The path prober that was destroyed.</param>
		internal static void Cleanup(PathProber prober) {
			pathCache.Remove(prober);
		}

		/// <summary>
		/// When the game is started, reset the path prober caches.
		/// </summary>
		internal static void Init() {
			pathCache = new Dictionary<PathProber, PathCacher>(128);
		}

		/// <summary>
		/// Looks up the path cache for the given prober.
		/// </summary>
		/// <param name="prober">The path prober to look up.</param>
		/// <returns>The path cache for this path prober's ID.</returns>
		internal static PathCacher Lookup(PathProber prober) {
			if (prober == null)
				throw new ArgumentNullException("prober");
			if (!pathCache.TryGetValue(prober, out PathCacher cache))
				pathCache.Add(prober, cache = new PathCacher());
			return cache;
		}

		/// <summary>
		/// The cell where the navigator was standing when the cache was last valid.
		/// </summary>
		private int cell;

		/// <summary>
		/// The global serial number from NavFences (for the nav type in use) when the cache
		/// was last valid.
		/// </summary>
		private long globalSerial;

		/// <summary>
		/// The flags used when evaluating a path when the cache was last valid.
		/// </summary>
		private PathFlags flags;

		private PathCacher() {
			cell = -1;
			flags = PathFlags.None;
			globalSerial = 0L;
		}

		/// <summary>
		/// Checks to see if this cached path is still valid. If not, the cached parameters are
		/// updated assuming that pathing is recalculated.
		/// </summary>
		/// <param name="navigator">The navigator to use.</param>
		/// <param name="newCell">The starting cell.</param>
		/// <returns>true if cached information can be used, or false otherwise.</returns>
		public bool CheckAndUpdate(Navigator navigator, int newCell) {
			if (navigator == null)
				throw new ArgumentNullException("navigator");
			bool ok = false;
			var newFlags = navigator.flags;
			var navGrid = navigator.NavGrid;
			if (NavFences.AllFences.TryGetValue(navGrid.id, out NavFences fences)) {
				ok = flags == newFlags && cell == newCell && fences.IsPathCurrent(globalSerial,
					ref navigator.path) && PathFinder.ValidatePath(navGrid, ABILITIES.Get(
					navigator), ref navigator.path);
				if (!ok) {
					// Guaranteed out of date
					flags = newFlags;
					globalSerial = fences.CurrentSerial;
				}
				cell = newCell;
			}
			return ok;
		}

		public override bool Equals(object obj) {
			return obj is PathCacher other && other.cell == cell && other.flags == flags &&
				other.globalSerial == globalSerial;
		}

		public override int GetHashCode() {
			return globalSerial.GetHashCode() * 37 + cell;
		}

		public override string ToString() {
			return "PathCacher[cell={0:D},serial={1:D},flags={2}]".F(cell, globalSerial,
				flags);
		}
	}
}
