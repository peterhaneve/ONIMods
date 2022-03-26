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
using System.Threading;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Caches global pathfind requests, drastically reducing work by avoiding repathing when
	/// nothing has changed.
	/// </summary>
	public sealed class PathCacher {
		/// <summary>
		/// Map path cache IDs to path cache values.
		/// </summary>
		private static ConcurrentDictionary<PathProber, PathCacher> pathCache;

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
			pathCache = new ConcurrentDictionary<PathProber, PathCacher>(4, 128);
		}

		/// <summary>
		/// Looks up the path cache for the given prober.
		/// </summary>
		/// <param name="prober">The path prober to look up.</param>
		/// <returns>The path cache for this path prober's ID.</returns>
		internal static PathCacher Lookup(PathProber prober) {
			if (prober == null)
				throw new ArgumentNullException("prober");
			return pathCache.GetOrAdd(prober, NewCacher);
		}

		/// <summary>
		/// Generates a new PathCacher.
		/// </summary>
		private static PathCacher NewCacher(PathProber _) => new PathCacher();

		/// <summary>
		/// Set to true if the path was forced invalid.
		/// </summary>
		private volatile int force;

		private PathCacher() {
			// Start out dirty
			force = 1;
		}

		/// <summary>
		/// Checks to see if this cached path is still valid. If not, the cached parameters are
		/// updated assuming that pathing is recalculated.
		/// </summary>
		/// <returns>true if cached information can be used, or false otherwise.</returns>
		public bool CheckAndMarkValid() {
			return Interlocked.Exchange(ref force, 0) == 0;
		}

		/// <summary>
		/// Marks the cached path as invalid.
		/// </summary>
		public void MarkInvalid() {
			force = 1;
		}

		/// <summary>
		/// Marks the cached path as valid.
		/// </summary>
		public void MarkValid() {
			force = 0;
		}
	}
}
