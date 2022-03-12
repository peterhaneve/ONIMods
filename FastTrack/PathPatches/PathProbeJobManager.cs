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
using System.Diagnostics;
using System.Threading;

namespace PeterHan.FastTrack.PathPatches {
	/// <summary>
	/// Manages async path probe jobs that run while LateUpdate is processing.
	/// </summary>
	public sealed class PathProbeJobManager : KMonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static PathProbeJobManager Instance { get; private set; }

		/// <summary>
		/// Globally locks accesses to CPUBudget to avoid races.
		/// </summary>
		private static readonly object CPU_BUDGET_LOCK = new object();

		/// <summary>
		/// Runs the work items but does not wait for them to finish.
		/// </summary>
		/// <param name="workItems">The work items to run.</param>
		public static void RunAsync(IWorkItemCollection workItems) {
			Instance?.StartJob(workItems);
		}

		/// <summary>
		/// Marks the CPULoad object that will be tracked by the next RunAsync call.
		/// </summary>
		/// <param name="group">The CPULoad object to use for tracking.</param>
		public static void SetCPUBudget(ICPULoad group) {
			var inst = Instance;
			if (inst != null)
				inst.budget = group;
		}

		/// <summary>
		/// The CPU budget against which the next task will be charged.
		/// </summary>
		private ICPULoad budget;

		/// <summary>
		/// Tracks the job count currently outstanding for path probes.
		/// </summary>
		private volatile int jobCount;

		/// <summary>
		/// The event to trigger when path probing is done.
		/// </summary>
		private readonly ManualResetEvent onPathDone;

		/// <summary>
		/// Set to true if any path probes were started since the last update.
		/// </summary>
		private bool running;

		/// <summary>
		/// The total runtime this frame.
		/// </summary>
		private long totalRuntime;

		internal PathProbeJobManager() {
			budget = null;
			onPathDone = new ManualResetEvent(false);
			running = false;
			totalRuntime = 0L;
		}

		protected override void OnCleanUp() {
			onPathDone.Dispose();
			budget = null;
			Instance = null;
			base.OnCleanUp();
		}

		/// <summary>
		/// Called when one pathfinding job completes. When all do, the event is triggered.
		/// </summary>
		private void OnPathComplete(AsyncPathWork job) {
			if (Interlocked.Decrement(ref jobCount) <= 0)
				onPathDone.Set();
			Interlocked.Add(ref totalRuntime, job.runtime);
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Instance = this;
		}

		/// <summary>
		/// Starts a new job.
		/// </summary>
		/// <param name="workItems">The job to start.</param>
		private void StartJob(IWorkItemCollection workItems) {
			var jobManager = AsyncJobManager.Instance;
			if (jobManager != null) {
				if (Interlocked.Increment(ref jobCount) <= 1)
					onPathDone.Reset();
				jobManager.Run(new AsyncPathWork(workItems, budget));
				running = true;
			}
		}

		/// <summary>
		/// Avoids stacking up queues by waiting for the async path probe. Game updates almost
		/// all handlers that use pathfinding (including BrainScheduler) in a LateUpdate call,
		/// so we let it spill over into the next frame and just hold up the next LateUpdate
		/// with a regular Update (if necessary).
		/// </summary>
		public void Update() {
			var jobManager = AsyncJobManager.Instance;
			if (jobManager != null && running) {
				var now = Stopwatch.StartNew();
				onPathDone.WaitOne(Timeout.Infinite);
				Metrics.DebugMetrics.LogPathProbe(now.ElapsedTicks, totalRuntime);
				jobCount = 0;
				totalRuntime = 0L;
				running = false;
			}
		}

		/// <summary>
		/// A job wrapping the base game path probe job set from BrainScheduler.
		/// </summary>
		private sealed class AsyncPathWork : AsyncJobManager.IWork {
			/// <summary>
			/// Allows the base game's "CPU load" balancer to work properly.
			/// </summary>
			internal readonly ICPULoad budget;

			/// <summary>
			/// The runtime of this job in Stopwatch ticks.
			/// </summary>
			internal long runtime;

			/// <summary>
			/// Tracks how long the probe took for our purposes.
			/// </summary>
			private readonly Stopwatch time;

			public IWorkItemCollection Jobs { get; }

			internal AsyncPathWork(IWorkItemCollection gameJobs, ICPULoad budget) {
				Jobs = gameJobs ?? throw new ArgumentNullException(nameof(gameJobs));
				this.budget = budget;
				runtime = 1L;
				time = new Stopwatch();
			}

			public void TriggerComplete() {
				runtime = time.ElapsedTicks;
				if (budget != null && FastTrackPatches.GameRunning)
					lock (CPU_BUDGET_LOCK) {
						CPUBudget.End(budget);
					}
				Instance?.OnPathComplete(this);
			}

			public void TriggerStart() {
				if (budget != null && FastTrackPatches.GameRunning)
					lock (CPU_BUDGET_LOCK) {
						CPUBudget.Start(budget);
					}
				time.Restart();
			}
		}
	}
}
