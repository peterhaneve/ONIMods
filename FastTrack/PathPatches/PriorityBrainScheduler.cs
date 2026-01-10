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

using System.Collections.Concurrent;
using System.Collections.Generic;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Updates priority brains in one of three modes.
	/// </summary>
	internal sealed class PriorityBrainScheduler {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static readonly PriorityBrainScheduler Instance = new PriorityBrainScheduler();

		private readonly FastTrackOptions.NextChorePriority priorityMode;

		/// <summary>
		/// The brains already updated, or delayed from updating.
		/// </summary>
		private readonly ISet<Brain> handled;
		
		/// <summary>
		/// The brains awaiting release from the update list.
		/// </summary>
		private readonly ConcurrentQueue<KMonoBehaviour> ready;

		/// <summary>
		/// The brains that will have one round of chore acquisition skipped.
		/// </summary>
		public readonly ISet<Brain> updateFirst;

		public PriorityBrainScheduler() {
			var opts = FastTrackOptions.Instance;
			var pm = opts.ChorePriorityMode;
			handled = new HashSet<Brain>();
			// Disable delay if fast reachability is off
			if (pm == FastTrackOptions.NextChorePriority.Delay && !opts.FastReachability)
				pm = FastTrackOptions.NextChorePriority.Normal;
			priorityMode = pm;
			ready = new ConcurrentQueue<KMonoBehaviour>();
			updateFirst = new HashSet<Brain>();
		}

		/// <summary>
		/// Determines which brains should have priority and run first.
		/// </summary>
		/// <param name="inst">The updater for asynchronous brains like Duplicants.</param>
		/// <param name="brainGroup">The brain group to update.</param>
		private void PopulatePriorityBrains(AsyncBrainGroupUpdater inst,
				BrainScheduler.BrainGroup brainGroup) {
			var pm = priorityMode;
			var prioritize = brainGroup.priorityBrains;
			bool usePriority = brainGroup.AllowPriorityBrains();
			// Critters should always use high priority to reduce ranching "ponder" time
			if (brainGroup.tag != GameTags.DupeBrain)
				pm = FastTrackOptions.NextChorePriority.Higher;
			switch (pm) {
			case FastTrackOptions.NextChorePriority.Delay:
				// Drain the ready queue
				while (ready.TryDequeue(out var prober))
					if (prober != null && prober.TryGetComponent(out Brain brain))
						updateFirst.Remove(brain);
				if (usePriority)
					while (prioritize.Count > 0) {
						var brain = prioritize.Dequeue();
						updateFirst.Add(brain);
						inst.QueueBrain(brain);
					}
				break;
			case FastTrackOptions.NextChorePriority.Normal:
				// Ban priority brains, and always use round robin
				prioritize.Clear();
				break;
			case FastTrackOptions.NextChorePriority.Higher:
			default:
				if (usePriority && prioritize.Count > 0) {
					var brain = prioritize.Dequeue();
					// Execute a priority brain if possible first
					handled.Add(brain);
					inst.QueueBrain(brain);
				}
				break;
			}
		}

		/// <summary>
		/// Signals that a path prober has completed an update and that it can be released for
		/// chores. Safe for any thread.
		/// </summary>
		/// <param name="prober">The path prober which completed an update.</param>
		internal void PathReady(KMonoBehaviour prober) {
			ready.Enqueue(prober);
		}
		
		/// <summary>
		/// Updates a brain group.
		/// </summary>
		/// <param name="inst">The updater for asynchronous brains like Duplicants.</param>
		/// <param name="brainGroup">The brain group to update.</param>
		internal void UpdateBrainGroup(AsyncBrainGroupUpdater inst,
				BrainScheduler.BrainGroup brainGroup) {
			brainGroup.BeginBrainGroupUpdate();
			var brains = brainGroup.brains;
			int n = brains.Count;
			if (n > 0) {
				int index = brainGroup.nextUpdateBrain % n;
				handled.Clear();
				PopulatePriorityBrains(inst, brainGroup);
				for (int i = brainGroup.InitialProbeCount(); i > 0; i--) {
					var brain = brains[index];
					if (handled.Add(brain))
						// Do not run a brain twice
						inst.QueueBrain(brain);
					index = (index + 1) % n;
				}
				brainGroup.nextUpdateBrain = index;
			}
			brainGroup.EndBrainGroupUpdate();
		}
	}
}
