/*
 * Copyright 2020 Peter Han
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

using Harmony;
using System;
using System.Reflection;

namespace PeterHan.PLib {
	/// <summary>
	/// An attribute placed on an option field for a property used as mod options to define
	/// minimum and maximum acceptable values.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class LimitAttribute : Attribute {
		/// <summary>
		/// Creates a LimitAttribute using an object from another mod.
		/// </summary>
		/// <param name="attr">The attribute from the other mod.</param>
		/// <returns>A LimitAttribute object with the values from that object, where
		/// possible to retrieve; or null if none could be obtained.</returns>
		private static LimitAttribute CreateFrom(object attr) {
			LimitAttribute la = null;
			if (attr.GetType().Name == typeof(LimitAttribute).Name) {
				// Has limit type
				var trAttr = Traverse.Create(attr);
				double min = 0.0, max = 0.0;
				try {
					min = trAttr.GetProperty<double>(nameof(Minimum));
					max = trAttr.GetProperty<double>(nameof(Maximum));
				} catch (Exception e) {
					PUtil.LogExcWarn(e);
				}
				if (min != 0.0 || max != 0.0)
					la = new LimitAttribute(min, max);
			}
			return la;
		}

		/// <summary>
		/// Searches for LimitAttribute attributes on a property.
		/// </summary>
		/// <param name="prop">The property with annotations.</param>
		/// <returns>The Limit attribute if present, or null if none is.</returns>
		internal static LimitAttribute FindFrom(PropertyInfo prop) {
			LimitAttribute fieldLimits = null;
			foreach (var attr in prop.GetCustomAttributes(false))
				if ((fieldLimits = CreateFrom(attr)) != null)
					break;
			return fieldLimits;
		}

		/// <summary>
		/// The maximum value (inclusive).
		/// </summary>
		public double Maximum { get; }

		/// <summary>
		/// The minimum value (inclusive).
		/// </summary>
		public double Minimum { get; }

		public LimitAttribute(double min, double max) {
			Minimum = min.IsNaNOrInfinity() ? 0.0 : min;
			Maximum = (max.IsNaNOrInfinity() || max < min) ? min : max;
		}

		/// <summary>
		/// Clamps the specified value to the range of this Limits object.
		/// </summary>
		/// <param name="value">The value to coerce.</param>
		/// <returns>The nearest value included by these limits to the specified value.</returns>
		public float ClampToRange(float value) {
			return value.InRange((float)Minimum, (float)Maximum);
		}

		/// <summary>
		/// Clamps the specified value to the range of this Limits object.
		/// </summary>
		/// <param name="value">The value to coerce.</param>
		/// <returns>The nearest value included by these limits to the specified value.</returns>
		public int ClampToRange(int value) {
			return value.InRange((int)Minimum, (int)Maximum);
		}

		/// <summary>
		/// Reports whether a value is in the range included in these limits.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <returns>true if it is included in the limits, or false otherwise.</returns>
		public bool InRange(double value) {
			return value >= Minimum && value <= Maximum;
		}

		public override string ToString() {
			return "{0:F2} to {1:F2}".F(Minimum, Maximum);
		}
	}
}