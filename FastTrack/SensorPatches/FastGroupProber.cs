/*
 * Copyright 2024 Peter Han
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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// Handles reachability updates in a better way than MinionGroupProber by performing
	/// updates on background tasks and using a lock-free accessor.
	/// </summary>
	internal sealed class FastGroupProber : IDisposable {
		/// <summary>
		/// Always process at least this many updates per frame.
		/// </summary>
		private const int MIN_PROCESS = 8;

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static FastGroupProber Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void Cleanup() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		internal static void Init() {
			Cleanup();
			Instance = new FastGroupProber();
		}

		#if DEBUG
		internal int[] Cells => cells;
		#endif

		/// <summary>
		/// The partitioner layer used to handle updates.
		/// </summary>
		public ThreadsafePartitionerLayer Mask { get; }

		/// <summary>
		/// The cells which were added (background thread use only).
		/// </summary>
		private readonly IList<int> added;

		/// <summary>
		/// Reference counts for each cell's reachability.
		/// </summary>
		private readonly int[] cells;

		/// <summary>
		/// Set when the prober is destroyed.
		/// </summary>
		private volatile bool destroyed;

		/// <summary>
		/// The cells already marked as dirty this pass (background thread use only).
		/// </summary>
		private readonly ISet<int> alreadyDirty;

		/// <summary>
		/// The cells which were marked dirty during path probes.
		/// </summary>
		private readonly ConcurrentQueue<int> dirtyCells;

		/// <summary>
		/// The temporary list of cells that became dirty this update.
		/// </summary>
		private readonly IList<int> dirtyTemp;
		
		/// <summary>
		/// The cells which were marked dirty during path probes.
		/// </summary>
		private readonly ConcurrentDictionary<object, ReachableCells> probers;

		/// <summary>
		/// The cells which were removed (background thread use only).
		/// </summary>
		private readonly ISet<int> removed;

		/// <summary>
		/// The queue of objects pending destruction (background thread use only).
		/// </summary>
		private readonly Queue<object> toDestroy;

		/// <summary>
		/// The instances which need reachability updated.
		/// </summary>
		private readonly Queue<ReachabilityMonitor.Instance> toDo;

		/// <summary>
		/// Called when there is work to do.
		/// </summary>
		private readonly EventWaitHandle trigger;

		private FastGroupProber() {
			added = new List<int>(256);
			alreadyDirty = new HashSet<int>();
			cells = new int[Grid.CellCount];
			destroyed = false;
			dirtyCells = new ConcurrentQueue<int>();
			dirtyTemp = new List<int>(32);
			Mask = new ThreadsafePartitionerLayer("Path Updates", Grid.WidthInCells, Grid.
				HeightInCells);
			probers = new ConcurrentDictionary<object, ReachableCells>(2, 64);
			removed = new HashSet<int>();
			toDestroy = new Queue<object>(8);
			toDo = new Queue<ReachabilityMonitor.Instance>();
			trigger = new AutoResetEvent(false);
			// Start the task
			var thread = new Thread(ProcessLoop) {
				IsBackground = true, Name = "Group Prober Updater", Priority = ThreadPriority.
				BelowNormal
			};
			Util.ApplyInvariantCultureToThread(thread);
			thread.Start();
		}

		/// <summary>
		/// Creates an entry for the prober if it does not exist.
		/// </summary>
		/// <param name="prober">The prober being updated.</param>
		internal void Allocate(object prober) {
			probers.GetOrAdd(prober, ReachableCells.NewCells);
		}

		public void Dispose() {
			Mask.Dispose();
			toDo.Clear();
			destroyed = true;
			trigger.Set();
		}

		/// <summary>
		/// Enqueues a reachability monitor for updating.
		/// </summary>
		/// <param name="smi">The monitor to update.</param>
		public void Enqueue(ReachabilityMonitor.Instance smi) {
			if (smi != null && smi.master != null)
				toDo.Enqueue(smi);
		}

		/// <summary>
		/// Checks to see if any registered prober can reach this cell.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>true if anything can reach it, or false otherwise.</returns>
		public bool IsReachable(int cell) {
			return cells[cell] > 0;
		}

		/// <summary>
		/// Checks to see if any registered prober can reach this cell from any of the given
		/// offsets.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="offsets">The valid offsets to check.</param>
		/// <returns>true if anything can reach it, or false otherwise.</returns>
		public bool IsReachable(int cell, CellOffset[] offsets) {
			bool found = cells[cell] > 0;
			if (!found && offsets != null) {
				int n = offsets.Length;
				// Avoid allocating an iterator
				for (int i = 0; i < n && !found; i++) {
					int offs = Grid.OffsetCell(cell, offsets[i]);
					if (Grid.IsValidCell(offs) && cells[offs] > 0)
						found = true;
				}
			}
			return found;
		}

		/// <summary>
		/// Marks a prober as being able to reach a set of cells.
		/// </summary>
		/// <param name="prober">The prober being updated.</param>
		/// <param name="reachable">The cells which can be reached.</param>
		/// <param name="isFullQuery">true if the query is a full update (and processes
		/// removed cells), or false otherwise.</param>
		internal void Occupy(object prober, IEnumerable<int> reachable, bool isFullQuery) {
			if (probers.TryGetValue(prober, out var rc))
				// While ugly, the alternative is yet another transpiler on SolidTransferArm...
				rc.Update(reachable, isFullQuery || prober is SolidTransferArm);
			trigger.Set();
		}

		/// <summary>
		/// Goes through all the probers, processing the ones that are dirty.
		/// </summary>
		private void Process() {
			foreach (var pair in probers) {
				int n, cell;
				var rc = pair.Value;
				bool destroy = rc.destroy;
				var oldCells = rc.oldCells;
				var newCells = rc.currentCells;				
				if (destroy) {
					toDestroy.Enqueue(pair.Key);
					n = oldCells.Count;
					// Remove from every cell that it could have reached
					for (int i = 0; i < n; i++) {
						cell = oldCells[i];
						if (Interlocked.Decrement(ref cells[cell]) == 0 && alreadyDirty.Add(
								cell))
							dirtyCells.Enqueue(cell);
					}
				} else
					while (Interlocked.Exchange(ref rc.dirty, 0) != 0) {
						// Decrement cells that it can no longer occupy, increment new ones
						n = oldCells.Count;
						for (int i = 0; i < n; i++)
							// For loop avoids allocations
							removed.Add(oldCells[i]);
						oldCells.Clear();
						// Find new and removed cells
						lock (newCells) {
							foreach (int reachableCell in newCells) {
								if (!removed.Remove(reachableCell))
									// If was not present, add it
									added.Add(reachableCell);
								oldCells.Add(reachableCell);
							}
							newCells.Clear();
						}
						n = added.Count;
						for (int i = 0; i < n; i++) {
							cell = added[i];
							if (Interlocked.Increment(ref cells[cell]) == 1 && alreadyDirty.
									Add(cell))
								dirtyCells.Enqueue(cell);
						}
						// removed has the leftovers
						foreach (int removedCell in removed)
							if (Interlocked.Decrement(ref cells[removedCell]) == 0 &&
									alreadyDirty.Add(removedCell))
								dirtyCells.Enqueue(removedCell);
						added.Clear();
						removed.Clear();
					}
			}
			alreadyDirty.Clear();
			// Remove all pending destroy
			while (toDestroy.Count > 0)
				if (probers.TryRemove(toDestroy.Dequeue(), out var rc))
					rc.Dispose();
		}

		/// <summary>
		/// Run in a background task to continually process group prober updates.
		/// </summary>
		private void ProcessLoop() {
			bool d;
			do {
				bool hit = trigger.WaitOne(FastTrackMod.MAX_TIMEOUT);
				d = destroyed;
				if (!d && hit)
					Process();
			} while (!d);
			// Clean up the object for real
			probers.Clear();
			trigger.Dispose();
		}

		/// <summary>
		/// Removes a prober.
		/// </summary>
		/// <param name="prober">The prober that was destroyed.</param>
		internal void Remove(object prober) {
			if (probers.TryGetValue(prober, out var rc)) {
				rc.Destroy();
				trigger.Set();
			}
		}

		/// <summary>
		/// Drains an appropriate amount of tasks that became reachable or unreachable from
		/// the queue on the foreground thread.
		/// </summary>
		internal void Update() {
			// Trigger partitioner updates on the foreground thread
			while (dirtyCells.TryDequeue(out int cell))
				dirtyTemp.Add(cell);
			if (dirtyTemp.Count > 0) {
				Mask.Trigger(dirtyTemp, this);
				dirtyTemp.Clear();
			}
			int n = toDo.Count;
			if (n > 0 && FastTrackMod.GameRunning) {
				if (n > MIN_PROCESS)
					// Run at least 1/16th of the outstanding
					n = MIN_PROCESS + ((n - MIN_PROCESS + 15) >> 4);
				while (n-- > 0) {
					var smi = toDo.Dequeue();
					var master = smi.master;
					int cell;
					if (master != null && Grid.IsValidCell(cell = Grid.PosToCell(master.
							transform.position)))
						smi.sm.isReachable.Set(IsReachable(cell, master.GetOffsets(cell)),
							smi);
				}
			}
		}

		/// <summary>
		/// Stores the cells that a prober can reach.
		/// </summary>
		private sealed class ReachableCells : IDisposable {
			internal static ReachableCells NewCells(object _) {
				return new ReachableCells();
			}

			/// <summary>
			/// The cells this prober can currently access. Needs to exist as the cells are
			/// a transient parameter that can be destroyed before the cleanup thread gets to
			/// them.
			/// </summary>
			internal readonly ISet<int> currentCells;

			/// <summary>
			/// Set to true when the prober has been destroyed.
			/// </summary>
			internal bool destroy;

			/// <summary>
			/// Set when the object is dirty.
			/// </summary>
			internal volatile int dirty;

			/// <summary>
			/// The cells this prober was able to access.
			/// </summary>
			internal readonly IList<int> oldCells;

			private ReachableCells() {
				currentCells = new HashSet<int>();
				destroy = false;
				dirty = 0;
				oldCells = new List<int>(64);
			}

			public void Destroy() {
				destroy = true;
			}

			public void Dispose() {
				oldCells.Clear();
				currentCells.Clear();
			}

			public override string ToString() {
				return "ReachableCells[reachable={0:D}]".F(oldCells.Count);
			}

			/// <summary>
			/// Updates the accessible cells.
			/// </summary>
			/// <param name="reachableCells">The cells to update.</param>
			/// <param name="isFullQuery">true if the query is a full update (and processes
			/// removed cells), or false otherwise.</param>
			public void Update(IEnumerable<int> reachableCells, bool isFullQuery) {
				if (dirty != 0)
					PUtil.LogWarning("Occupy cells before previous cells finished!");
				lock (currentCells) {
					foreach (int cell in reachableCells)
						currentCells.Add(cell);
				}
				// If the query is complete, send it off for processing
				if (isFullQuery)
					dirty = 1;
			}
		}
	}
}
