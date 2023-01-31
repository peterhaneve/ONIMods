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
		/// Map path cache IDs to path cache values.
		/// </summary>
		private static ConcurrentDictionary<PathProber, double> pathCache;

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
			pathCache.TryRemove(prober, out _);
		}

		/// <summary>
		/// When the game is started, reset the path prober caches.
		/// </summary>
		internal static void Init() {
			if (pathCache == null)
				pathCache = new ConcurrentDictionary<PathProber, double>(4, 128);
			else
				pathCache.Clear();
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
					pathCache.TryRemove(nav.PathProber, out _);
			}
		}

		/// <summary>
		/// Checks to see if the prober's cache is valid.
		/// </summary>
		/// <param name="prober">The path prober to look up.</param>
		/// <returns>true if the cache is valid for this ID, or false otherwise.</returns>
		internal static bool IsValid(PathProber prober) {
			if (prober == null)
				throw new ArgumentNullException(nameof(prober));
			return pathCache.TryGetValue(prober, out double expires) && now < expires;
		}

		/// <summary>
		/// Sets a prober as valid or invalid.
		/// </summary>
		/// <param name="prober">The path prober to look up.</param>
		/// <param name="valid">true if the prober is valid, or false if it is invalid.</param>
		internal static void SetValid(PathProber prober, bool valid) {
			if (prober == null)
				throw new ArgumentNullException(nameof(prober));
			if (valid)
				pathCache[prober] = now + INVALIDATE_TIME;
			else
				pathCache.TryRemove(prober, out _);
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
