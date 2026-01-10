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

namespace PeterHan.FastTrack.SensorPatches {
	/// <summary>
	/// Handles reachability updates by masking them to only areas where nav grid cells change.
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

		/// <summary>
		/// The partitioner layer used to handle updates.
		/// </summary>
		public ThreadsafePartitionerLayer Mask { get; }
		
		/// <summary>
		/// The cells which were marked dirty during path probes.
		/// </summary>
		private readonly ConcurrentQueue<int> dirtyCells;
		
		/// <summary>
		/// The temporary list of cells that became dirty this update.
		/// </summary>
		private readonly IList<int> dirtyTemp;
		
		/// <summary>
		/// The instances which need reachability updated.
		/// </summary>
		private readonly Queue<ReachabilityMonitor.Instance> toDo;

		private FastGroupProber() {
			dirtyCells = new ConcurrentQueue<int>();
			dirtyTemp = new List<int>(32);
			Mask = new ThreadsafePartitionerLayer("Path Updates", Grid.WidthInCells, Grid.
				HeightInCells);
			toDo = new Queue<ReachabilityMonitor.Instance>();
		}

		/// <summary>
		/// Adds a cell that became reachable or unreachable by all Duplicants.
		/// </summary>
		/// <param name="cell">The cell to change</param>
		public void AddDirtyCell(int cell) {
			dirtyCells.Enqueue(cell);
		}

		public void Dispose() {
			Mask.Dispose();
			toDo.Clear();
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
			var gp = MinionGroupProber.Get();
			if (n > 0 && FastTrackMod.GameRunning && gp != null) {
				if (n > MIN_PROCESS)
					// Run at least 1/16th of the outstanding
					n = MIN_PROCESS + ((n - MIN_PROCESS + 15) >> 4);
				while (n-- > 0) {
					var smi = toDo.Dequeue();
					var master = smi.master;
					int cell;
					if (master != null && Grid.IsValidCell(cell = Grid.PosToCell(master.
							transform.position)))
						smi.sm.isReachable.Set(gp.IsReachable(cell, master.GetOffsets(cell)),
							smi);
				}
			}
		}
	}
}
