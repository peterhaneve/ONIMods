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
		/// Tracks specific conditions.
		/// </summary>
		internal static readonly ConcurrentDictionary<string, RatioProfiler> COND =
			new ConcurrentDictionary<string, RatioProfiler>(2, 8);

		/// <summary>
		/// Tracks Klei event triggers.
		/// </summary>
		internal static readonly NameBucketProfiler EVENTS = new NameBucketProfiler();

		/// <summary>
		/// Tracks Unity LateUpdate() methods.
		/// </summary>
		internal static readonly NameBucketProfiler LATE_UPDATE = new NameBucketProfiler();

		/// <summary>
		/// The number of times the path cache hit since the last reset.
		/// </summary>
		internal static readonly RatioProfiler PATH_CACHE = new RatioProfiler();

		/// <summary>
		/// Tracks calls to asychronous path probes.
		/// </summary>
		private static readonly Profiler PATH_PROBES = new Profiler();

		/// <summary>
		/// Tracks Sim and Render buckets.
		/// </summary>
		internal static readonly NameBucketProfiler[] SIMANDRENDER = new NameBucketProfiler[8];

		/// <summary>
		/// Tracks specific methods.
		/// </summary>
		internal static readonly ConcurrentDictionary<string, Profiler> TRACKED =
			new ConcurrentDictionary<string, Profiler>(2, 8);

		/// <summary>
		/// Tracks Unity Update() methods.
		/// </summary>
		internal static readonly NameBucketProfiler UPDATE = new NameBucketProfiler();

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
		/// Logs a profiled condition.
		/// </summary>
		/// <param name="name">The method name that was called.</param>
		/// <param name="condition">Whether the condition was satisfied.</param>
		internal static void LogCondition(string name, bool condition) {
			COND.GetOrAdd(name, NewRatio).Log(condition);
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
		/// Logs a profiled method invocation.
		/// </summary>
		/// <param name="name">The method name that was called.</param>
		/// <param name="ticks">The time it took to run in ticks.</param>
		internal static void LogTracked(string name, long ticks) {
			TRACKED.GetOrAdd(name, NewProfiler).Log(ticks);
		}
		
		/// <summary>
		/// Creates a new profiler to populate the tracking dictionary.
		/// </summary>
		private static Profiler NewProfiler(string _) => new Profiler();

		/// <summary>
		/// Creates a new ratio profile to populate the condition dictionary.
		/// </summary>
		private static RatioProfiler NewRatio(string _) => new RatioProfiler();

		/// <summary>
		/// Resets all metrics.
		/// </summary>
		internal static void Reset() {
			EVENTS.Reset();
			PATH_PROBES.Reset();
			probeWaiting = 0L;
			LATE_UPDATE.Reset();
			UPDATE.Reset();
			foreach (var pair in COND)
				pair.Value.Reset();
			foreach (var pair in TRACKED)
				pair.Value.Reset();
			PATH_CACHE.Reset();
			int n = SIMANDRENDER.Length;
			for (int i = 0; i < n; i++)
				SIMANDRENDER[i].Reset();
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			Reset();
		}

		public void Render1000ms(float dt) {
			if (FastTrackMod.GameRunning) {
				long probeTotal = PATH_PROBES.TimeInMethod, probeSaved = Math.Max(0L,
					probeTotal - probeWaiting.TicksToUS());
				int probeCount = PATH_PROBES.MethodCalls;
				var text = new System.Text.StringBuilder(128);
				// Methods run
				if (TRACKED.Count > 0) {
					text.Append("Methods Run:");
					foreach (var pair in TRACKED)
						text.Append(' ').Append(pair.Key).Append(pair.Value);
					PUtil.LogDebug(text);
				}
				// Conditions tested
				if (COND.Count > 0) {
					text.Clear();
					text.Append("Conditions:");
					foreach (var pair in COND)
						text.Append(' ').Append(pair.Key).Append(pair.Value);
					PUtil.LogDebug(text);
				}
				// Events fired
				PUtil.LogDebug("Events " + EVENTS);
				// Path cache
				PUtil.LogDebug("Path Cache: " + PATH_CACHE);
				// Brain stats
				text.Clear();
				text.Append("Brain Stats:");
				foreach (var pair in BRAIN_LOAD)
					text.Append(' ').Append(pair.Key).Append('[').Append(pair.Value).
						Append(']');
				PUtil.LogDebug(text);
				// Path probes
				PUtil.LogDebug("Path Probes: executed {0:N0}us, saved {1:N0}us ({2:N0}/frame)".
					F(probeTotal, probeSaved, (double)probeSaved / Math.Max(1, probeCount)));
				// Sim, Render, and Update
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
				Reset();
#if DEBUG
				FastTrackProfiler.Log();
#endif
			}
		}

		/// <summary>
		/// Stores brain update statistics.
		/// </summary>
		private readonly struct BrainStats {
			/// <summary>
			/// The elapsed frame time in milliseconds.
			/// </summary>
			private readonly float frameTime;

			/// <summary>
			/// The target frame time in milliseconds.
			/// </summary>
			private readonly float targetFrameTime;

			/// <summary>
			/// The number of probes run per frame.
			/// </summary>
			private readonly int probeCount;

			/// <summary>
			/// The number of cells to probe per frame for each probe.
			/// </summary>
			private readonly int probeSize;

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
