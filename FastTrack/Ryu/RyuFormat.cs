/*
 * Copyright 2026 Peter Han
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
 * 
 * This is a substantial modification of https://github.com/ulfjack/ryu, which is available
 * under the Boost Software License, Version 1.0 (https://www.boost.org/LICENSE_1_0.txt).
 */

using System;
using System.Text;

namespace Ryu {
	/// <summary>
	/// Format floats faster with no allocs!
	/// </summary>
	public static class RyuFormat {
		/// <summary>
		/// Converts a double precision floating point number to a string.
		/// </summary>
		/// <param name="result">The location where the result will be placed.</param>
		/// <param name="value">The value to format.</param>
		/// <param name="precision">The precision to output when formatting.</param>
		/// <param name="options">The format mode to use.</param>
		/// <param name="provider">The provider used to determine how the value will be
		/// formatted.</param>
		public static void ToString(StringBuilder result, double value, int precision,
				RyuFormatOptions options = RyuFormatOptions.RoundtripMode,
				IFormatProvider provider = null) {
			var info = RyuUtils.GetFormatInfo(provider);
			// Step 1: Decode the floating-point number, and unify normalized and subnormal
			// cases
			bool ieeeSign = RyuFloat64.Decode(value, out ulong ieeeMantissa,
				out uint ieeeExponent);
			bool mZero = ieeeMantissa == 0UL;

			// Case distinction; exit early for the easy cases
			if (ieeeExponent == RyuFloat64.EXPONENT_MASK)
				RyuUtils.GenerateSpecial(result, ieeeSign, !mZero, info);
			else if (mZero && ieeeExponent == 0UL)
				RyuUtils.GenerateZero(result, ieeeSign, precision, options, info);
			else if ((options & RyuFormatOptions.RoundtripMode) != 0)
				new RyuFloat64(ieeeSign, ieeeMantissa, ieeeExponent).ToRoundtripString(
					result, options, info);
			else {
				if (ieeeSign)
					result.Append(info.NegativeSign);
				if ((options & RyuFormatOptions.ExponentialMode) != 0)
					Float64ToString.ToExponentialString(result, ieeeMantissa, ieeeExponent,
						precision, options, info);
				else
					Float64ToString.ToFixedString(result, ieeeMantissa, ieeeExponent,
						precision, options, info);
			}
		}

		/// <summary>
		/// Converts a single precision floating point number to a string.
		/// </summary>
		/// <param name="result">The location where the result will be placed.</param>
		/// <param name="value">The value to format.</param>
		/// <param name="precision">The precision to output when formatting.</param>
		/// <param name="options">The format mode to use.</param>
		/// <param name="provider">The provider used to determine how the value will be
		/// formatted.</param>
		public static void ToString(StringBuilder result, float value, int precision,
				RyuFormatOptions options = RyuFormatOptions.RoundtripMode,
				IFormatProvider provider = null) {
			var info = RyuUtils.GetFormatInfo(provider);
			// Step 1: Decode the floating-point number, and unify normalized and subnormal
			// cases
			bool ieeeSign = RyuFloat32.Decode(value, out uint ieeeMantissa,
				out uint ieeeExponent);

			// Case distinction; exit early for the easy cases
			bool mZero = ieeeMantissa == 0UL;
			if (ieeeExponent == RyuFloat32.EXPONENT_MASK)
				RyuUtils.GenerateSpecial(result, ieeeSign, !mZero, info);
			else if (mZero && ieeeExponent == 0UL)
				RyuUtils.GenerateZero(result, ieeeSign, precision, options, info);
			else if ((options & RyuFormatOptions.RoundtripMode) != 0)
				new RyuFloat32(ieeeSign, ieeeMantissa, ieeeExponent).ToRoundtripString(result,
					options, info);
		}
	}

	/// <summary>
	/// Controls the options for fast float formatting.
	/// </summary>
	[Flags]
	public enum RyuFormatOptions {
		ExponentialMode = 1,
		FixedMode = 2,
		RoundtripMode = 4,
		AutoMode = ExponentialMode | FixedMode,
		ModeMask = ExponentialMode | FixedMode | RoundtripMode,
		CompatibleExponent = 8,
		SoftPrecision = 16,
		ThousandsSeparators = 32
	}
}
