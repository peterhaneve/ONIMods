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
	[SkipSaveFileSerialization]
	public sealed class AsyncJobManager : KMonoBehaviour, IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static AsyncJobManager Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The number of worker threads still finishing a task.
		/// </summary>
		private volatile int activeThreads;

		/// <summary>
		/// Cached reference to the head of workQueue.
		/// </summary>
		private IWork currentJob;

		/// <summary>
		/// Used to prevent multiple disposes.
		/// </summary>
		private bool isDisposed;

		/// <summary>
		/// The index of the next not yet started work item.
		/// </summary>
		private volatile int nextWorkIndex;

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
		private readonly Queue<IWork> workQueue;

		internal AsyncJobManager() {
			int n = CPUBudget.coreCount;
			if (n < 1)
				// Ensure at least one thread is created
				n = 1;
			activeThreads = 0;
			currentJob = null;
			isDisposed = false;
			Instance = this;
			nextWorkIndex = -1;
			semaphore = new Semaphore(0, n);
			threads = new WorkerThread[n];
			workQueue = new Queue<IWork>();
			for (int i = 0; i < n; i++)
				threads[i] = new WorkerThread(this, "FastTrackWorker{0}".F(i));
		}

		/// <summary>
		/// Advances to the next task in the queue.
		/// </summary>
		/// <param name="toStart">The job that will be started.</param>
		private void AdvanceNext(IWork toStart) {
			int n = threads.Length;
			nextWorkIndex = -1;
			activeThreads = n;
			currentJob = toStart;
			// Not sure if this matters, borrowed from Klei code
			Thread.MemoryBarrier();
			toStart.TriggerStart();
			semaphore.Release(n);
		}

		public void Dispose() {
			if (!isDisposed) {
				currentJob = null;
				isDisposed = true;
				semaphore.Release(threads.Length);
				// Clear work queue
				lock (workQueue) {
					workQueue.Clear();
				}
			}
		}

		/// <summary>
		/// Called by workers to dequeue and execute a work item.
		/// </summary>
		internal bool DoNextWorkItem() {
			int index = Interlocked.Increment(ref nextWorkIndex);
			IWorkItemCollection items;
			bool executed = false;
			if (currentJob != null && index >= 0 && index < (items = currentJob.Jobs).Count) {
				items.InternalDoWorkItem(index);
				executed = true;
			}
			return executed;
		}

		protected override void OnCleanUp() {
			Dispose();
			base.OnCleanUp();
		}

		/// <summary>
		/// Called by workers when the job queue is empty.
		/// </summary>
		internal void ReportInactive() {
			if (Interlocked.Decrement(ref activeThreads) <= 0) {
				IWork next = null;
				currentJob?.TriggerComplete();
				lock (workQueue) {
					// Remove the old head, and check for a new one
					int n = workQueue.Count;
					if (n > 0)
						workQueue.Dequeue();
					if (n > 1)
						next = workQueue.Peek();
					currentJob = null;
				}
				if (next != null)
					AdvanceNext(next);
				else
					foreach (var thread in threads)
						thread.PrintExceptions();
			}
		}

		/// <summary>
		/// Starts executing the list of work items in the background. Returns immediately
		/// after execution begins; use Wait to monitor the status.
		/// </summary>
		/// <param name="workItems">The work items to run in parallel.</param>
		public void Run(IWork workItems) {
			bool starting = false;
			if (workItems == null)
				throw new ArgumentNullException(nameof(workItems));
			if (isDisposed)
				throw new ObjectDisposedException(nameof(AsyncJobManager));
			lock (workQueue) {
				starting = workQueue.Count == 0;
				workQueue.Enqueue(workItems);
			}
			if (starting)
				AdvanceNext(workItems);
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

		/// <summary>
		/// An interface implemented to handle a collection of related tasks.
		/// </summary>
		public interface IWork {
			/// <summary>
			/// The jobs to run.
			/// </summary>
			IWorkItemCollection Jobs { get; }

			/// <summary>
			/// Called by AsyncJobManager when the work item collection completes.
			/// </summary>
			void TriggerComplete();

			/// <summary>
			/// Called by AsyncJobManager when the work item collection is started.
			/// </summary>
			void TriggerStart();
		}
	}
}
