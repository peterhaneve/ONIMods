/*
 * Copyright 2020 Peter Han
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

using PeterHan.PLib;
using System;
using System.Collections.Generic;

using PathFlags = PathFinder.PotentialPath.Flags;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Caches global pathfind requests, drastically reducing work by avoiding repathing when
	/// nothing has changed.
	/// </summary>
	public sealed class PathCacher {
		/// <summary>
		/// Map path cache IDs to path cache values.
		/// </summary>
		private static IDictionary<PathProber, PathCacher> pathCache;

		/// <summary>
		/// PathFinder.InvalidHandle is not even readonly!
		/// </summary>
		public const int InvalidHandle = -1;

		/// <summary>
		/// Checks to see if the navigator can reciprocally go back and forth between a cell
		/// and another cell, checking only if they are immediately reachable.
		/// </summary>
		/// <param name="fromCell">The cell where the navigator is currently located.</param>
		/// <param name="toCell">The potential destination cell.</param>
		/// <param name="navGrid">The navigation grid to use for lookups.</param>
		/// <param name="startNavType">The navigation type to use.</param>
		/// <param name="abilities">The current navigator abilities.</param>
		/// <param name="newFlags">The flags available for this path.</param>
		/// <returns>true if navigation can be performed, both ways, or false otherwise.</returns>
		private static bool CanNavigateReciprocal(int fromCell, int toCell, NavGrid navGrid,
				NavType startNavType, PathFinderAbilities abilities, PathFlags flags) {
			var grid = PathFinder.PathGrid;
			bool ok = false;
			// Find a link from this cell to the target cell
			var link = FindLinkToCell(fromCell, toCell, navGrid, startNavType);
			if (link.link != InvalidHandle) {
				var endNavType = link.endNavType;
				var pp = new PathFinder.PotentialPath(toCell, endNavType, flags);
				int uwCost = grid.GetCell(toCell, endNavType, out _).underwaterCost;
				// Can navigate there, and has a link back?
				if (abilities.TraversePath(ref pp, fromCell, startNavType, link.cost, link.
						transitionId, uwCost) && (link = FindLinkToCell(toCell, fromCell,
						navGrid, endNavType)).link != InvalidHandle) {
					pp.cell = fromCell;
					pp.navType = startNavType;
					uwCost = grid.GetCell(fromCell, startNavType, out _).underwaterCost;
					ok = abilities.TraversePath(ref pp, toCell, endNavType, link.cost,
						link.transitionId, uwCost);
				}
			}
			return ok;
		}

		/// <summary>
		/// Finds a link between two cells on the nav grid.
		/// </summary>
		/// <param name="fromCell">The initial cell.</param>
		/// <param name="toCell">The destination cell.</param>
		/// <param name="navGrid">The grid to search.</param>
		/// <param name="navType">The required starting navigation type.</param>
		/// <returns>The matching link, or a link with an invalid destination if no matches
		/// are found.</returns>
		private static NavGrid.Link FindLinkToCell(int fromCell, int toCell, NavGrid navGrid,
				NavType navType) {
			NavGrid.Link link;
			var links = navGrid.Links;
			int lpc = navGrid.maxLinksPerCell, index = fromCell * lpc, end = index + lpc;
			do {
				link = links[index];
				int dest = link.link;
				if (dest == InvalidHandle || (dest == toCell && link.startNavType ==
					navType)) break;
				index++;
			} while (index < end);
			return link;
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
		/// <param name="navGrid">The navigation grid to use.</param>
		/// <param name="newCell">The starting cell.</param>
		/// <param name="navType">The navigation type currently in use.</param>
		/// <param name="abilities">The path finder's current abilities.</param>
		/// <param name="newFlags">The path finder's current flags.</param>
		/// <returns>true if cached information can be used, or false otherwise.</returns>
		public bool CheckAndUpdate(NavGrid navGrid, int newCell, NavType navType,
				PathFinderAbilities abilities, PathFlags newFlags) {
			if (navGrid == null)
				throw new ArgumentNullException("navGrid");
			if (abilities == null)
				throw new ArgumentNullException("abilities");
			bool ok = false;
			if (NavFences.AllFences.TryGetValue(navGrid.id, out NavFences fences)) {
				long serial = fences.CurrentSerial;
				if (serial == globalSerial && flags == newFlags)
					ok = (cell == newCell) || CanNavigateReciprocal(cell, newCell, navGrid,
						navType, abilities, newFlags);
				else {
					// Guaranteed out of date
					flags = newFlags;
					globalSerial = serial;
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
