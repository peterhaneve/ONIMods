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
using System.Collections.Concurrent;
using System.Threading;

namespace PeterHan.FastTrack.Metrics {
	/// <summary>
	/// Logs extra debug metrics every real-time second to the game log.
	/// </summary>
	internal sealed class DebugMetrics : KMonoBehaviour, IRender1000ms {
		/// <summary>
		/// In a thread safe, lockless manner, adds a value to a 64-bit accumulator.
		/// </summary>
		/// <param name="accumulator">The location where the total will be stored.</param>
		/// <param name="add">The value to add.</param>
		public static void Accumulate(ref long accumulator, long add) {
			long oldValue, newValue;
			do {
				oldValue = Interlocked.Read(ref accumulator);
				newValue = oldValue + add;
			} while (Interlocked.CompareExchange(ref accumulator, newValue, oldValue) !=
				oldValue);
		}

		/// <summary>
		/// Monitors Brain load balancing.
		/// </summary>
		private static readonly ConcurrentDictionary<string, BrainStats> BRAIN_LOAD =
			new ConcurrentDictionary<string, BrainStats>(2, 8);

		/// <summary>
		/// Tracks Unity LateUpdate() methods.
		/// </summary>
		internal static readonly NameBucketProfiler LATE_UPDATE = new NameBucketProfiler();

		/// <summary>
		/// Tracks calls to asychronous path probes.
		/// </summary>
		private static readonly Profiler PATH_PROBES = new Profiler();

		/// <summary>
		/// Tracks Unity OnRenderImage() and related methods.
		/// </summary>
		internal static readonly NameBucketProfiler RENDER_IMAGE = new NameBucketProfiler();

		/// <summary>
		/// Tracks calls to sensor updates.
		/// </summary>
		internal static readonly Profiler SENSORS = new Profiler();

		/// <summary>
		/// Tracks Sim and Render buckets.
		/// </summary>
		internal static readonly NameBucketProfiler[] SIMANDRENDER = new NameBucketProfiler[8];

		/// <summary>
		/// Tracks a specific method.
		/// </summary>
		internal static readonly Profiler[] TRACKED = new Profiler[] {
			new Profiler(), new Profiler()
		};

		/// <summary>
		/// Tracks Unity Update() methods.
		/// </summary>
		internal static readonly NameBucketProfiler UPDATE = new NameBucketProfiler();

		/// <summary>
		/// The number of times the path cache hit since the last reset.
		/// </summary>
		private static volatile int cacheHits;

		/// <summary>
		/// The number of times the path cache was queried since the last reset.
		/// </summary>
		private static volatile int cacheTotal;

		/// <summary>
		/// The time spent waiting for async path probes to complete (while synchronously
		/// blocked).
		/// </summary>
		private static long probeWaiting;

		// Initializes all profiler buckets
		static DebugMetrics() {
			int n = SIMANDRENDER.Length;
			for (int i = 0; i < n; i++)
				SIMANDRENDER[i] = new NameBucketProfiler();
		}

		/// <summary>
		/// Logs brain load balancing stats.
		/// </summary>
		/// <param name="name">The brain group name.</param>
		/// <param name="actual">The actual frame time in s.</param>
		/// <param name="target">The target frame time in s.</param>
		/// <param name="count">The number of probes run in the frame.</param>
		/// <param name="size">The number of cells to probe.</param>
		internal static void LogBrainBalance(string name, float actual, float target,
				int count, int size) {
			BRAIN_LOAD[name] = new BrainStats(actual, target, count, size);
		}

		/// <summary>
		/// Logs a pathfinding cache hit or miss.
		/// </summary>
		/// <param name="hit">true for a cache hit, or false for a miss.</param>
		internal static void LogHit(bool hit) {
			Interlocked.Increment(ref cacheTotal);
			if (hit)
				Interlocked.Increment(ref cacheHits);
		}

		/// <summary>
		/// Logs how long path probing took in microseconds.
		/// </summary>
		/// <param name="wait">How long the foreground thread was waiting for path probing.</param>
		/// <param name="run">How long the total asynchronous path probe ran.</param>
		internal static void LogPathProbe(long wait, long run) {
			Accumulate(ref probeWaiting, wait);
			PATH_PROBES.Log(run);
		}

		/// <summary>
		/// Resets the async path probing metrics.
		/// </summary>
		internal static void ResetAsyncPath() {
			PATH_PROBES.Reset();
			probeWaiting = 0L;
		}

		/// <summary>
		/// Resets the method call metrics.
		/// </summary>
		internal static void ResetMethodHits() {
			int n = TRACKED.Length;
			LATE_UPDATE.Reset();
			RENDER_IMAGE.Reset();
			SENSORS.Reset();
			UPDATE.Reset();
			for (int i = 0; i < n; i++)
				TRACKED[i].Reset();
		}

		/// <summary>
		/// Resets the path cache metrics.
		/// </summary>
		internal static void ResetPathCache() {
			cacheTotal = 0;
			cacheHits = 0;
		}

		/// <summary>
		/// Resets metrics about sim and render callbacks.
		/// </summary>
		internal static void ResetSimAndRenderStats() {
			int n = SIMANDRENDER.Length;
			for (int i = 0; i < n; i++)
				SIMANDRENDER[i].Reset();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			ResetAsyncPath();
			ResetMethodHits();
			ResetPathCache();
			ResetSimAndRenderStats();
		}

		public void Render1000ms(float dt) {
			if (FastTrackMod.GameRunning) {
				long probeTotal = PATH_PROBES.TimeInMethod, probeSaved = Math.Max(0L,
					probeTotal - probeWaiting.TicksToUS());
				int probeCount = PATH_PROBES.MethodCalls;
				var brainStats = new System.Text.StringBuilder(128);
				PUtil.LogDebug("Methods Run: {0}(s), {1}(t), {2}(t)".F(SENSORS, TRACKED[0],
					TRACKED[1]));
				PUtil.LogDebug("Path Cache: {0:D}/{1:D} hits, {2:F1}%".F(cacheHits, cacheTotal,
					cacheHits * 100.0f / Math.Max(1, cacheTotal)));
				ResetPathCache();
				brainStats.Append("Brain Stats:");
				foreach (var pair in BRAIN_LOAD)
					brainStats.Append(' ').Append(pair.Key).Append('[').Append(pair.Value).
						Append(']');
				PUtil.LogDebug(brainStats);
				PUtil.LogDebug("Path Probes: executed {0:N0}us, saved {1:N0}us ({2:N0}/frame)".
					F(probeTotal, probeSaved, (double)probeSaved / Math.Max(1, probeCount)));
				PUtil.LogDebug("Sim/Render: *r{0}\n200r{1}\n1000r{2}\n*s{3}\n33s{4}\n200s{5}\n1000s{6}\n4000s{7}".F(
					SIMANDRENDER[(int)UpdateRate.RENDER_EVERY_TICK],
					SIMANDRENDER[(int)UpdateRate.RENDER_200ms],
					SIMANDRENDER[(int)UpdateRate.RENDER_1000ms],
					SIMANDRENDER[(int)UpdateRate.SIM_EVERY_TICK],
					SIMANDRENDER[(int)UpdateRate.SIM_33ms],
					SIMANDRENDER[(int)UpdateRate.SIM_200ms],
					SIMANDRENDER[(int)UpdateRate.SIM_1000ms],
					SIMANDRENDER[(int)UpdateRate.SIM_4000ms]));
				PUtil.LogDebug("Update: {0}\nLateUpdate: {1}".F(UPDATE, LATE_UPDATE));
			}
			ResetAsyncPath();
			ResetMethodHits();
			ResetSimAndRenderStats();
		}

		/// <summary>
		/// Stores brain update statistics.
		/// </summary>
		private struct BrainStats {
			/// <summary>
			/// The elapsed frame time in milliseconds.
			/// </summary>
			public float frameTime;

			/// <summary>
			/// The target frame time in milliseconds.
			/// </summary>
			public float targetFrameTime;

			/// <summary>
			/// The number of probes run per frame.
			/// </summary>
			public int probeCount;

			/// <summary>
			/// The number of cells to probe per frame for each probe.
			/// </summary>
			public int probeSize;

			public BrainStats(float frameTime, float targetFrameTime, int probeCount,
					int probeSize) {
				this.frameTime = frameTime;
				this.targetFrameTime = targetFrameTime;
				this.probeCount = probeCount;
				this.probeSize = probeSize;
			}

			public override string ToString() {
				return "{0:F3}ms/{1:F3}ms,{2:D}x{3:D}".F(frameTime, targetFrameTime,
					probeCount, probeSize);
			}
		}
	}
}
