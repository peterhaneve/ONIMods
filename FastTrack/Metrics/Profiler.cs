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
	/// Shared by all profilers to allow a quick universal reset.
	/// </summary>
	public interface IProfiler {
		void Reset();
	}

	/// <summary>
	/// Profiles calls of a particular method.
	/// </summary>
	public class Profiler : IProfiler {
		public int MethodCalls => methodCalls;

		public long TimeInMethod => timeInMethod.TicksToUS();

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
			DebugMetrics.Accumulate(ref timeInMethod, time);
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
	public sealed class NameBucketProfiler : Profiler {
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

		/// <summary>
		/// Wraps sim bucket results and allows sorting by time.
		/// </summary>
		private sealed class SimBucketResults : IComparable<SimBucketResults> {
			/// <summary>
			/// The class name in this bucket.
			/// </summary>
			public string ClassName;

			/// <summary>
			/// The time taken in ticks.
			/// </summary>
			public long Time;

			public SimBucketResults(long time, string className) {
				Time = time.TicksToUS();
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

	/// <summary>
	/// Monitors ratios, like hit rates.
	/// </summary>
	public class RatioProfiler : IProfiler {
		/// <summary>
		/// The number of times the condition was true since the last reset.
		/// </summary>
		private volatile int hits;

		/// <summary>
		/// The number of times the condition was checked since the last reset.
		/// </summary>
		private volatile int total;

		public RatioProfiler() {
			hits = 0;
			total = 0;
		}

		/// <summary>
		/// Logs a condition check.
		/// </summary>
		/// <param name="condition">Whether the condition was satisfied.</param>
		public void Log(bool condition) {
			Interlocked.Increment(ref total);
			if (condition)
				Interlocked.Increment(ref hits);
		}

		public void Reset() {
			hits = 0;
			total = 0;
		}

		public override string ToString() {
			int h = hits, t = total;
			return "[{0:D}/{1:D}]({2:F1}%)".F(h, t, h * 100.0 / Math.Max(1, total));
		}
	}
}
