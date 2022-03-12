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
using System.Diagnostics;
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
		/// Gets the elapsed time in microseconds.
		/// </summary>
		/// <param name="ticks">The time elapsed in stopwatch ticks.</param>
		/// <returns>The elapsed time in microseconds.</returns>
		public static long TicksToUS(long ticks) {
			return ticks * 1000000L / Stopwatch.Frequency;
		}

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
			if (FastTrackPatches.GameRunning) {
				long probeTotal = PATH_PROBES.TimeInMethod, probeSaved = Math.Max(0L,
					probeTotal - TicksToUS(probeWaiting));
				int probeCount = PATH_PROBES.MethodCalls;
				PUtil.LogDebug("Methods Run: {0}(s), {1}(t), {2}(t)".F(SENSORS, TRACKED[0],
					TRACKED[1]));
				PUtil.LogDebug("Path Cache: {0:D}/{1:D} hits, {2:F1}%".F(cacheHits, cacheTotal,
					cacheHits * 100.0f / Math.Max(1, cacheTotal)));
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
			ResetPathCache();
			ResetSimAndRenderStats();
		}

		/// <summary>
		/// Profiles calls of a particular method.
		/// </summary>
		internal class Profiler {
			public int MethodCalls => methodCalls;

			public long TimeInMethod => TicksToUS(timeInMethod);

			/// <summary>
			/// The number of times the method was called since the last reset.
			/// </summary>
			private volatile int methodCalls;

			/// <summary>
			/// The time in ticks spent in the method since the last reset.
			/// </summary>
			private long timeInMethod;

			public Profiler() {
				methodCalls = 0;
				timeInMethod = 0L;
			}

			/// <summary>
			/// Logs a method invocation.
			/// </summary>
			/// <param name="time">The time it took the method to run in microseconds.</param>
			public void Log(long time) {
				Interlocked.Increment(ref methodCalls);
				Accumulate(ref timeInMethod, time);
			}

			/// <summary>
			/// Resets the method call and time counts.
			/// </summary>
			public virtual void Reset() {
				methodCalls = 0;
				timeInMethod = 0L;
			}

			public override string ToString() {
				long t = TimeInMethod;
				return "[{0:D}/{1:N0}us|1/{2:N0}us]".F(methodCalls, t, (methodCalls > 0) ?
					((double)t / methodCalls) : 0.0);
			}
		}

		/// <summary>
		/// Profiles calls to Sim and Render methods by the class name used.
		/// </summary>
		internal sealed class NameBucketProfiler : Profiler {
			/// <summary>
			/// The minimum time in us/1000ms to be displayed. 10000us/1000ms is 1%.
			/// </summary>
			public const long MIN_TIME = 1000;

			/// <summary>
			/// Categorizes the time taken by the class name updated.
			/// </summary>
			private readonly ConcurrentDictionary<string, long> timeByClassName;

			public NameBucketProfiler() {
				timeByClassName = new ConcurrentDictionary<string, long>(2, 32);
			}

			/// <summary>
			/// Logs time taken by a particular sim/update class.
			/// </summary>
			/// <param name="className">The class which was just run.</param>
			/// <param name="time">The time it took in ticks.</param>
			public void AddSlice(string className, long time) {
				timeByClassName.AddOrUpdate(className, time, (_, oldTotal) => oldTotal + time);
				Log(time);
			}

			/// <summary>
			/// Gets the time taken by a particular class name in this update bucket.
			/// </summary>
			/// <param name="updaterClass">The class that used SimXXms or RenderXXms.</param>
			/// <returns>The total time it took since the last reset in ticks.</returns>
			public long GetTimeByClassName(string updaterClass) {
				if (!timeByClassName.TryGetValue(updaterClass, out long time))
					time = 0L;
				return time;
			}

			public override void Reset() {
				base.Reset();
				timeByClassName.Clear();
			}

			public override string ToString() {
				var byTime = ListPool<SimBucketResults, NameBucketProfiler>.Allocate();
				var header = new System.Text.StringBuilder(base.ToString());
				int n = timeByClassName.Count;
				foreach (var pair in timeByClassName)
					byTime.Add(new SimBucketResults(pair.Value, pair.Key));
				byTime.Sort();
				foreach (var entry in byTime) {
					if (entry.Time < MIN_TIME) break;
					header.AppendLine();
					header.Append(' ');
					header.Append(entry.ToString());
					n--;
				}
				byTime.Recycle();
				if (n > 0) {
					header.AppendLine();
					header.AppendFormat("  and {0:D} more...", n);
				}
				return header.ToString();
			}
		}

		/// <summary>
		/// Wraps sim bucket results and allows sorting by time.
		/// </summary>
		private struct SimBucketResults : IComparable<SimBucketResults> {
			/// <summary>
			/// The class name in this bucket.
			/// </summary>
			public string ClassName;

			/// <summary>
			/// The time taken in ticks.
			/// </summary>
			public long Time;
			
			public SimBucketResults(long time, string className) {
				Time = TicksToUS(time);
				ClassName = className;
			}

			public int CompareTo(SimBucketResults other) {
				return -Time.CompareTo(other.Time);
			}

			public override bool Equals(object obj) {
				return obj is SimBucketResults other && Time == other.Time && ClassName ==
					other.ClassName;
			}

			public override int GetHashCode() {
				return ClassName.GetHashCode();
			}

			public override string ToString() {
				return "{0}: {1:N0}us".F(ClassName, Time);
			}
		}
	}
}
