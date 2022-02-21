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

using HarmonyLib;
using PeterHan.FastTrack.Metrics;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Applied to BrainScheduler.BrainGroup to move the path probe updates to a fully
	/// asychronous task.
	/// </summary>
	[HarmonyPatch]
	public static class BrainScheduler_BrainGroup_AsyncPathProbe_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AsyncPathProbe;

		internal static MethodBase TargetMethod() {
			// Private type with private method
			var targetType = typeof(BrainScheduler).GetNestedType("BrainGroup",
				PPatchTools.BASE_FLAGS | BindingFlags.Instance);
			return targetType?.GetMethodSafe("AsyncPathProbe", false);
		}

		/// <summary>
		/// Transpiles AsyncPathProbe to use our job manager instead.
		/// </summary>
		internal static IEnumerable<CodeInstruction> Transpiler(
				IEnumerable<CodeInstruction> instructions) {
			var workItemType = typeof(IWorkItemCollection);
			return PPatchTools.ReplaceMethodCall(instructions, typeof(GlobalJobManager).
				GetMethodSafe(nameof(GlobalJobManager.Run), true, workItemType),
				typeof(PathProbeJobManager).GetMethodSafe(nameof(PathProbeJobManager.RunAsync),
				true, workItemType));
		}
	}

	/// <summary>
	/// A separate instance of GlobalJobManager just for async path probes that are not run
	/// on the foreground thread.
	/// </summary>
	public sealed class PathProbeJobManager : KMonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static PathProbeJobManager Instance { get; private set; }

		/// <summary>
		/// Runs the work items but does not wait for them to finish.
		/// </summary>
		/// <param name="workItems">The work items to run.</param>
		public static void RunAsync(IWorkItemCollection workItems) {
			Instance?.StartJob(workItems);
		}

		/// <summary>
		/// Cached value of FastTrackOptions.Instance.AsyncPathProbes.
		/// </summary>
		private bool debug;

		/// <summary>
		/// The job manager to use for executing path probes.
		/// </summary>
		private AsyncJobManager jobManager;

		/// <summary>
		/// Destroys the job manager.
		/// </summary>
		private void Dispose() {
			jobManager?.Dispose();
		}

		protected override void OnCleanUp() {
			Dispose();
			Instance = null;
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			if (Instance != null)
				Instance.Dispose();
			Instance = this;
			debug = FastTrackOptions.Instance.AsyncPathProbe;
			jobManager = new AsyncJobManager();
		}

		/// <summary>
		/// Starts a new job.
		/// </summary>
		/// <param name="workItems">The job to start.</param>
		private void StartJob(IWorkItemCollection workItems) {
			jobManager?.Start(workItems);
		}

		/// <summary>
		/// Avoids stacking up queues by waiting for the async path probe. Game updates almost
		/// all Sim and Render handlers (including BrainScheduler) in a LateUpdate call
		/// (not Update, KLEI PLEASE), so we let it spill over into the next frame and just
		/// hold up the next LateUpdate with a regular Update.
		/// </summary>
		public void Update() {
			if (jobManager != null) {
				if (debug) {
					var now = Stopwatch.StartNew();
					jobManager.Wait();
					DebugMetrics.LogPathProbe(now.ElapsedTicks, jobManager.LastRunTime);
				} else
					jobManager.Wait();
			}
		}
	}
}
