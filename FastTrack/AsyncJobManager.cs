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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PeterHan.FastTrack {
	/// <summary>
	/// A version of JobManager that can be non-blocking.
	/// </summary>
	public sealed class AsyncJobManager : IDisposable {
		/// <summary>
		/// The total number of Stopwatch ticks (not us, ms, or ns!) that elapsed from the
		/// first job in the queue until queue emptying.
		/// </summary>
		public long LastRunTime { get; private set; }

		/// <summary>
		/// The number of worker threads still finishing a task.
		/// </summary>
		private volatile int activeThreads;

		/// <summary>
		/// Cached reference to the head of workQueue.
		/// </summary>
		private IWorkItemCollection currentJob;

		/// <summary>
		/// Used to prevent multiple disposes.
		/// </summary>
		private volatile bool isDisposed;

		/// <summary>
		/// Records when the last job began.
		/// </summary>
		private Stopwatch lastStartTime;

		/// <summary>
		/// The index of the next not yet started work item.
		/// </summary>
		private volatile int nextWorkIndex;

		/// <summary>
		/// Signaled when work completes.
		/// </summary>
		private readonly ManualResetEvent onComplete;

		/// <summary>
		/// The semaphore signaled to release the workers.
		/// </summary>
		private readonly Semaphore semaphore;

		/// <summary>
		/// The worker threads used to perform tasks.
		/// </summary>
		private readonly WorkerThread[] threads;

		/// <summary>
		/// The queue of jobs waiting to be started.
		/// </summary>
		private readonly Queue<IWorkItemCollection> workQueue;

		public AsyncJobManager() {
			int n = CPUBudget.coreCount;
			if (n < 1)
				// Ensure at least one thread is created
				n = 1;
			activeThreads = 0;
			currentJob = null;
			isDisposed = false;
			LastRunTime = 0L;
			lastStartTime = null;
			nextWorkIndex = -1;
			onComplete = new ManualResetEvent(false);
			semaphore = new Semaphore(0, n);
			threads = new WorkerThread[n];
			// Should only be 2 tasks - dupes and critters
			workQueue = new Queue<IWorkItemCollection>();
			for (int i = 0; i < n; i++)
				threads[i] = new WorkerThread(this, "FastTrackWorker{0}".F(i));
		}

		/// <summary>
		/// Advances to the next task in the queue.
		/// </summary>
		/// <param name="toStart">The job that will be started.</param>
		private void AdvanceNext(IWorkItemCollection toStart) {
			int n = threads.Length;
			nextWorkIndex = -1;
			activeThreads = n;
			currentJob = toStart;
			// Not sure if this matters, borrowed from Klei code
			Thread.MemoryBarrier();
			semaphore.Release(n);
		}

		public void Dispose() {
			if (!isDisposed) {
				currentJob = null;
				isDisposed = true;
				lastStartTime = null;
				semaphore.Release(threads.Length);
				// Clear work queue
				lock (workQueue) {
					workQueue.Clear();
				}
				onComplete.Set();
			}
		}

		/// <summary>
		/// Called by workers to dequeue and execute a work item.
		/// </summary>
		internal bool DoNextWorkItem() {
			int index = Interlocked.Increment(ref nextWorkIndex);
			bool executed = false;
			if (currentJob != null && index >= 0 && index < currentJob.Count) {
				currentJob.InternalDoWorkItem(index);
				executed = true;
			}
			return executed;
		}

		/// <summary>
		/// Called by workers when the job queue is empty.
		/// </summary>
		internal void ReportInactive() {
			if (Interlocked.Decrement(ref activeThreads) <= 0) {
				IWorkItemCollection next = null;
				lock (workQueue) {
					// Remove the old head, and check for a new one
					int n = workQueue.Count;
					if (n > 0)
						workQueue.Dequeue();
					if (n > 1)
						next = workQueue.Peek();
				}
				if (next != null)
					AdvanceNext(next);
				else {
					currentJob = null;
					if (lastStartTime != null) {
						// Measure the runtime
						LastRunTime = lastStartTime.ElapsedTicks;
						lastStartTime = null;
					}
					onComplete.Set();
				}
			}
		}

		/// <summary>
		/// Starts executing the list of work items in the background. Returns immediately
		/// after execution begins; use Wait to monitor the status.
		/// </summary>
		/// <param name="workItems">The work items to run in parallel.</param>
		public void Start(IWorkItemCollection workItems) {
			int n = threads.Length;
			bool starting = false;
			if (isDisposed)
				throw new ObjectDisposedException(nameof(AsyncJobManager));
			lock (workQueue) {
				starting = workQueue.Count == 0;
				workQueue.Enqueue(workItems);
			}
			if (starting) {
				lastStartTime = Stopwatch.StartNew();
				AdvanceNext(workItems);
			}
		}

		/// <summary>
		/// Waits for all current work to complete.
		/// </summary>
		/// <param name="timeout">The maximum time to wait in milliseconds, or Timeout.Infinite
		/// to wait indefinitely.</param>
		/// <returns>true if the tasks are complete, or false otherwise.</returns>
		public bool Wait(int timeout = Timeout.Infinite) {
			bool done = true;
			int n;
			if (isDisposed)
				throw new ObjectDisposedException(nameof(AsyncJobManager));
			lock (workQueue) {
				n = workQueue.Count;
			}
			// If the queue is empty, do not bother waiting
			if (n > 0) {
				done = onComplete.WaitOne(timeout);
				if (done) {
					onComplete.Reset();
					foreach (var thread in threads)
						thread.PrintExceptions();
				}
			}
			return done;
		}

		/// <summary>
		/// A worker thread used to execute jobs in parallel.
		/// </summary>
		private sealed class WorkerThread {
			/// <summary>
			/// The errors that occurred during execution.
			/// </summary>
			private readonly IList<Exception> errors;

			/// <summary>
			/// The parent job manager of this worker.
			/// </summary>
			private readonly AsyncJobManager parent;

			internal WorkerThread(AsyncJobManager parent, string name) {
				errors = new List<Exception>(4);
				this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
				var thread = new Thread(Run, 131072) {
					// Klei uses AboveNormal
					Priority = ThreadPriority.Normal, Name = name
				};
				Util.ApplyInvariantCultureToThread(thread);
				thread.Start();
			}

			/// <summary>
			/// Prints the errors that occurred during execution and clears the errors.
			/// </summary>
			internal void PrintExceptions() {
				foreach (var error in errors)
					Debug.LogException(error);
				errors.Clear();
			}

			/// <summary>
			/// Runs the thread body.
			/// </summary>
			private void Run() {
				bool disposed = false;
				while (!disposed) {
					parent.semaphore.WaitOne(Timeout.Infinite);
					try {
						while (!(disposed = parent.isDisposed) && parent.DoNextWorkItem());
					} catch (Exception e) {
						errors.Add(e);
					}
					if (!disposed)
						parent.ReportInactive();
				}
			}
		}
	}
}
