/*
 * Copyright 2024 Peter Han
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

namespace PeterHan.FastTrack {
	/// <summary>
	/// Stores the minimum and maximum values seen.
	/// </summary>
	public struct MinMax {
		public float Delta => max - min;

		/// <summary>
		/// Whether any values have been seen at all.
		/// </summary>
		public bool has;

		/// <summary>
		/// The maximum value seen.
		/// </summary>
		public float max;

		/// <summary>
		/// The minimum value seen.
		/// </summary>
		public float min;

		/// <summary>
		/// Creates a new min/max tracker.
		/// </summary>
		/// <param name="iv"></param>
		public MinMax(float iv) {
			has = false;
			min = iv;
			max = iv;
		}

		/// <summary>
		/// Adds a value to the tracker.
		/// </summary>
		/// <param name="value">The value to add.</param>
		public void Add(float value) {
			bool had = has;
			if (!had || value < min)
				min = value;
			if (!had || value > max)
				max = value;
			has = true;
		}

		/// <summary>
		/// Adds a range of values to the tracker.
		/// </summary>
		/// <param name="vMin">The minimum value</param>
		/// <param name="vMax">The maximum value</param>
		public void Add(float vMin, float vMax) {
			bool had = has;
			if (!had || vMin < min)
				min = vMin;
			if (!had || vMax > max)
				max = vMax;
			has = true;
		}
	}
}
