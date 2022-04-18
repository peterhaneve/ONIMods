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

using Unity.Profiling;

namespace PeterHan.FastTrack.Metrics {
#if DEBUG
	/// <summary>
	/// Profiles memory usage to find out what is causing GCs.
	/// </summary>
	internal static class FastTrackProfiler {
		private static ProfilerRecorder managedReserved;

		private static ProfilerRecorder managedUsed;

		private static ProfilerRecorder totalUsed;

		private static ProfilerRecorder systemUsed;

		private static long lastManagedUsed;

		/// <summary>
		/// Starts the profiler.
		/// </summary>
		public static void Begin() {
			managedReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
				"GC Reserved Memory");
			managedUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
				"GC Used Memory");
			totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
				"Total Used Memory");
			systemUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
				"System Used Memory");
			lastManagedUsed = 0L;
		}

		/// <summary>
		/// Stops the profiler.
		/// </summary>
		public static void End() {
			managedReserved.Dispose();
			managedUsed.Dispose();
			totalUsed.Dispose();
			systemUsed.Dispose();
			lastManagedUsed = 0L;
		}

		/// <summary>
		/// Logs data from the profiler.
		/// </summary>
		public static void Log() {
			if (totalUsed.Valid)
				Debug.Log("Total Reserved KB: " + (totalUsed.LastValue >> 10));
			if (managedReserved.Valid && managedUsed.Valid) {
				long used = managedUsed.LastValue;
				long perSecond = used - lastManagedUsed;
				Debug.LogFormat("Managed Heap KB: {0:D}/{1:D} ({2:D}/s)", used >> 10,
					managedReserved.LastValue >> 10, perSecond >> 10);
				lastManagedUsed = used;
			}
			if (systemUsed.Valid)
				Debug.Log("System Used KB: " + (systemUsed.LastValue >> 10));
		}
	}
#endif
}
