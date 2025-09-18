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

using System;
using System.Collections.Generic;
using System.Threading;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack {
	/// <summary>
	/// A thread-safe layer akin to ScenePartitionerLayer; there is no central manager like
	/// GameScenePartitioner, instances must be managed separately.
	/// </summary>
	public sealed class ThreadsafePartitionerLayer : IDisposable {
		/// <summary>
		/// Entries are grouped in blocks of this size.
		/// </summary>
		public const int NODE_SIZE = 16;

		/// <summary>
		/// The layer's friendly name.
		/// </summary>
		public readonly string name;

		/// <summary>
		/// The next entry ID.
		/// </summary>
		private volatile int nextID;

		/// <summary>
		/// The grid height in nodes.
		/// </summary>
		private readonly int nodeHeight;

		/// <summary>
		/// The nodes used for partitioning.
		/// </summary>
		private readonly HashSet<ThreadsafePartitionerEntry>[,] nodes;

		/// <summary>
		/// The grid width in nodes.
		/// </summary>
		private readonly int nodeWidth;

		/// <summary>
		/// The event triggered when any changes occur in the layer.
		/// </summary>
		public Action<int, object> OnEvent;

		public ThreadsafePartitionerLayer(string name, int width, int height) {
			if (width <= 0)
				throw new ArgumentOutOfRangeException(nameof(width));
			if (height <= 0)
				throw new ArgumentOutOfRangeException(nameof(height));
			this.name = name ?? "Layer";
			nextID = 0;
			nodeHeight = (height + NODE_SIZE - 1) / NODE_SIZE;
			nodeWidth = (width + NODE_SIZE - 1) / NODE_SIZE;
			nodes = new HashSet<ThreadsafePartitionerEntry>[nodeWidth, nodeHeight];
			for (int i = 0; i < nodeWidth; i++)
				for (int j = 0; j < nodeHeight; j++)
					// There are not that many, and they are pretty small when empty
					nodes[i, j] = new HashSet<ThreadsafePartitionerEntry>();
		}
		
		/// <summary>
		/// Adds a new scene partitioner entry. The extents will have a height and width of 1.
		/// </summary>
		/// <param name="x">The entry X coordinate.</param>
		/// <param name="y">The entry Y coordinate.</param>
		/// <param name="data">The data to associate with the entry.</param>
		/// <param name="onEvent">The callback to run when the entry is gathered.</param>
		/// <returns>The partitioner entry.</returns>
		public ThreadsafePartitionerEntry Add(int x, int y, object data,
				Action<object> onEvent) {
			var extents = new Extents(x, y, 1, 1);
			var entry = new ThreadsafePartitionerEntry(this, ref extents, Interlocked.
				Increment(ref nextID), data, onEvent);
			Update(entry);
			return entry;
		}

		/// <summary>
		/// Adds a new scene partitioner entry.
		/// </summary>
		/// <param name="extents">The extents occupied by the entry.</param>
		/// <param name="data">The data to associate with the entry.</param>
		/// <param name="onEvent">The callback to run when the entry is gathered.</param>
		/// <returns>The partitioner entry.</returns>
		public ThreadsafePartitionerEntry Add(Extents extents, object data,
				Action<object> onEvent) {
			var entry = new ThreadsafePartitionerEntry(this, ref extents, Interlocked.
				Increment(ref nextID), data, onEvent);
			Update(entry);
			return entry;
		}
		
		/// <summary>
		/// Limits the index to valid node X indexes.
		/// </summary>
		/// <param name="x">The X coordinate.</param>
		/// <returns>An X index inside the valid range.</returns>
		private int ClampNodeX(int x) {
			if (x < 0) x = 0;
			if (x >= nodeWidth) x = nodeWidth - 1;
			return x;
		}
		
		/// <summary>
		/// Limits the index to valid node Y indexes.
		/// </summary>
		/// <param name="y">The Y coordinate.</param>
		/// <returns>A Y index inside the valid range.</returns>
		private int ClampNodeY(int y) {
			if (y < 0) y = 0;
			if (y >= nodeHeight) y = nodeHeight - 1;
			return y;
		}

		public void Dispose() {
			OnEvent = null;
			for (int i = 0; i < nodeWidth; i++)
				for (int j = 0; j < nodeHeight; j++) {
					var set = nodes[i, j];
					lock (set) {
						set.Clear();
					}
				}
		}

		public override bool Equals(object obj) {
			return obj is ThreadsafePartitionerLayer other && other.name == name;
		}
		
		public override int GetHashCode() {
			return name.GetHashCode();
		}

		/// <summary>
		/// Gathers all of the entries which are exactly inside the given extents.
		/// </summary>
		/// <param name="entries">The location where the gathered entries will be stored.</param>
		/// <param name="extents">The region to search.</param>
		public void Gather(ICollection<ThreadsafePartitionerEntry> entries, Extents extents) {
			Gather(entries, extents.x, extents.y, extents.width, extents.height);
		}

		/// <summary>
		/// Gathers all of the entries which are exactly inside the given extents.
		/// </summary>
		/// <param name="entries">The location where the gathered entries will be stored.</param>
		/// <param name="x">The region X coordinate.</param>
		/// <param name="y">The region Y coordinate.</param>
		/// <param name="width">The width of the region.</param>
		/// <param name="height">The height of the region.</param>
		public void Gather(ICollection<ThreadsafePartitionerEntry> entries, int x, int y,
				int width, int height) {
			var nodeExtents = GetNodeExtents(x, y, width, height);
			int nx = nodeExtents.x, nh = nodeExtents.height, maxX = x + width - 1,
				maxY = y + height - 1, nsy = nodeExtents.y;
			for (int dx = nodeExtents.width; dx > 0; dx--) {
				int ny = nsy;
				for (int dy = nh; dy > 0; dy--) {
					var set = nodes[nx, ny];
					lock (set) {
						foreach (var entry in set) {
							ref var extents = ref entry.extents;
							int ex = extents.x, ey = extents.y;
							if (maxX >= ex && x <= ex + extents.width - 1 && maxY >= ey && y <=
									ey + extents.height - 1)
								entries.Add(entry);
						}
					}
					ny++;
				}
				nx++;
			}
		}
		
		/// <summary>
		/// Converts coordinates to node extents. The node extents are conservative and will
		/// always include the coordinates if the coordinates are valid.
		/// </summary>
		/// <param name="x">The region X coordinate.</param>
		/// <param name="y">The region Y coordinate.</param>
		/// <param name="width">The width of the region.</param>
		/// <param name="height">The height of the region.</param>
		/// <returns>The coordinates as node indexes.</returns>
		private Extents GetNodeExtents(int x, int y, int width, int height) {
			int nx = ClampNodeX(x / NODE_SIZE);
			int ny = ClampNodeY(y / NODE_SIZE);
			return new Extents(nx, ny, ClampNodeX((x + width - 1) / NODE_SIZE) - nx + 1,
				ClampNodeY((y + height - 1) / NODE_SIZE) - ny + 1);
		}

		/// <summary>
		/// Removes a partitioner entry.
		/// </summary>
		/// <param name="entry">The entry to remove.</param>
		internal void Remove(ThreadsafePartitionerEntry entry) {
			ref var ee = ref entry.extents;
			var extents = GetNodeExtents(ee.x, ee.y, ee.width, ee.height);
			int x = extents.x, height = extents.height, ey = extents.y;
			for (int dx = extents.width; dx > 0; dx--) {
				int y = ey;
				for (int dy = height; dy > 0; dy--) {
					var set = nodes[x, y];
					lock (set) {
						set.Remove(entry);
					}
					y++;
				}
				x++;
			}
		}

		public override string ToString() {
			return name;
		}
		
		/// <summary>
		/// Triggers an event on the specified cells.
		/// </summary>
		/// <param name="cells">The cell locations (will be converted with Grid.CellToXY) to trigger.</param>
		/// <param name="data">The data to pass the event.</param>
		public void Trigger(IEnumerable<int> cells, object data) {
			var evt = OnEvent;
			var uniqueEntries = HashSetPool<ThreadsafePartitionerEntry,
				ThreadsafePartitionerLayer>.Allocate();
			foreach (int cell in cells)
				if (Grid.IsValidCell(cell)) {
					evt?.Invoke(cell, data);
					Grid.CellToXY(cell, out int x, out int y);
					Gather(uniqueEntries, x, y, 1, 1);
				}
			foreach (var entry in uniqueEntries)
				entry.OnEvent?.Invoke(data);
			uniqueEntries.Recycle();
		}

		/// <summary>
		/// Triggers an event on the specified cell.
		/// </summary>
		/// <param name="cell">The cell location (will be converted with Grid.CellToXY) to trigger.</param>
		/// <param name="data">The data to pass the event.</param>
		public void Trigger(int cell, object data) {
			if (Grid.IsValidCell(cell)) {
				Grid.CellToXY(cell, out int x, out int y);
				var uniqueEntries = HashSetPool<ThreadsafePartitionerEntry,
					ThreadsafePartitionerLayer>.Allocate();
				OnEvent?.Invoke(cell, data);
				Gather(uniqueEntries, x, y, 1, 1);
				foreach (var entry in uniqueEntries)
					entry.OnEvent?.Invoke(data);
				uniqueEntries.Recycle();
			}
		}

		/// <summary>
		/// Triggers an event on all cells in the given rectangle.
		/// </summary>
		/// <param name="x">The region X coordinate.</param>
		/// <param name="y">The region Y coordinate.</param>
		/// <param name="width">The width of the region.</param>
		/// <param name="height">The height of the region.</param>
		/// <param name="data">The data to pass the event.</param>
		public void Trigger(int x, int y, int width, int height, object data) {
			int cx = x;
			var evt = OnEvent;
			var uniqueEntries = HashSetPool<ThreadsafePartitionerEntry,
				ThreadsafePartitionerLayer>.Allocate();
			for (int dx = width; dx > 0; dx--) {
				int cy = y;
				for (int dy = height; dy > 0; dy--) {
					int cell = Grid.XYToCell(cx, cy);
					if (Grid.IsValidCell(cell))
						evt?.Invoke(cell, data);
					cy++;
				}
				cx++;
			}
			Gather(uniqueEntries, x, y, width, height);
			foreach (var entry in uniqueEntries)
				entry.OnEvent?.Invoke(data);
			uniqueEntries.Recycle();
		}

		/// <summary>
		/// Adds a partitioner entry.
		/// </summary>
		/// <param name="entry">The entry to re-add.</param>
		internal void Update(ThreadsafePartitionerEntry entry) {
			ref var ee = ref entry.extents;
			var extents = GetNodeExtents(ee.x, ee.y, ee.width, ee.height);
			int x = extents.x, height = extents.height, ey = extents.y;
			for (int dx = extents.width; dx > 0; dx--) {
				int y = ey;
				for (int dy = height; dy > 0; dy--) {
					var set = nodes[x, y];
					lock (set) {
						set.Add(entry);
					}
					y++;
				}
				x++;
			}
		}
	}

	/// <summary>
	/// A refernce to an entry used in the threadsafe scene partitioner.
	/// </summary>
	public sealed class ThreadsafePartitionerEntry : IEquatable<ThreadsafePartitionerEntry>,
			IDisposable {
		/// <summary>
		/// The data stored in this partitioner entry.
		/// </summary>
		public object data;

		/// <summary>
		/// The extents of this object.
		/// </summary>
		internal Extents extents;

		/// <summary>
		/// The unique ID of this entry.
		/// </summary>
		internal readonly int id;

		/// <summary>
		/// Called when a request to execute all entries in a given area is given.
		/// </summary>
		public readonly Action<object> OnEvent;

		/// <summary>
		/// The partitioner which owns this entry.
		/// </summary>
		private readonly ThreadsafePartitionerLayer partitioner;

		internal ThreadsafePartitionerEntry(ThreadsafePartitionerLayer partitioner,
				ref Extents extents, int id, object data, Action<object> onEvent) {
			this.data = data;
			this.extents = extents;
			this.id = id;
			this.partitioner = partitioner ?? throw new ArgumentNullException(
				nameof(partitioner));
			OnEvent = onEvent;
		}

		public void Dispose() {
			partitioner.Remove(this);
		}

		public override bool Equals(object obj) {
			return obj is ThreadsafePartitionerEntry other && other.id == id;
		}

		public bool Equals(ThreadsafePartitionerEntry other) {
			return other != null && other.id == id;
		}

		public override int GetHashCode() {
			return id;
		}

		/// <summary>
		/// Reports a move of this entry to another location.
		/// </summary>
		/// <param name="x">The new X coordinate.</param>
		/// <param name="y">The new Y coordinate.</param>
		public void UpdatePosition(int x, int y) {
			partitioner.Remove(this);
			extents.x = x;
			extents.y = y;
			extents.height = 1;
			extents.width = 1;
			partitioner.Update(this);
		}

		/// <summary>
		/// Reports a move of this entry to another location.
		/// </summary>
		/// <param name="newExtents">The new extents of this entry.</param>
		public void UpdatePosition(Extents newExtents) {
			partitioner.Remove(this);
			extents = newExtents;
			partitioner.Update(this);
		}

		public override string ToString() {
			return "ScenePartitionerEntry[layer={0},x={1:D},y={2:D},width={3:D},height={4:D}]".
				F(partitioner.name, extents.x, extents.y, extents.width, extents.height);
		}
	}
}
