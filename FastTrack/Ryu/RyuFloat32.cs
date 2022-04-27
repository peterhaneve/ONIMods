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
 * 
 * This is a substantial modification of https://github.com/ulfjack/ryu, which is available
 * under the Boost Software License, Version 1.0 (https://www.boost.org/LICENSE_1_0.txt).
 */

using System;
using System.Globalization;
using System.Text;

namespace Ryu {
	/// <summary>
	/// The Ryu representation of a 32-bit float.
	/// </summary>
	internal struct RyuFloat32 : IEquatable<RyuFloat32> {
		internal const int FLOAT_BIAS = 127;

		internal const int FLOAT_EXPONENT_BITS = 8;

		internal const int FLOAT_MANTISSA_BITS = 23;

		private const int FLOAT_POW5_BITCOUNT = 61;

		private const int FLOAT_POW5_INV_BITCOUNT = 59;

		internal const uint EXPONENT_MASK = (1U << FLOAT_EXPONENT_BITS) - 1U;

		private const uint SIGN_MASK = 1U << (FLOAT_MANTISSA_BITS + FLOAT_EXPONENT_BITS);

		private const uint MANTISSA_MASK = (1U << FLOAT_MANTISSA_BITS) - 1U;

		/// <summary>
		/// Decode a float into its sign, mantissa, and exponent.
		/// </summary>
		internal static bool Decode(float value, out uint mantissa, out uint exponent) {
			uint bits = RyuUtils.float_to_bits(value);
			mantissa = bits & MANTISSA_MASK;
			exponent = (bits >> FLOAT_MANTISSA_BITS) & EXPONENT_MASK;
			return (bits & SIGN_MASK) != 0U;
		}

		internal readonly int exponent;

		internal readonly uint mantissa;

		internal readonly bool sign;

		/// <summary>
		/// Creates a Ryu float from an IEEE 32 bit float.
		/// </summary>
		public RyuFloat32(bool fSign, uint ieeeMantissa, uint ieeeExponent) {
			const int FLOAT_EXP_DIFF_P2 = FLOAT_BIAS + FLOAT_MANTISSA_BITS + 2;
			int exponent;
			uint mantissa;
			if (ieeeExponent == 0U) {
				// Subtract 2 so that the bounds computation has 2 additional bits
				exponent = 1 - FLOAT_EXP_DIFF_P2;
				mantissa = ieeeMantissa;
			} else {
				exponent = (int)ieeeExponent - FLOAT_EXP_DIFF_P2;
				mantissa = (1U << FLOAT_MANTISSA_BITS) | ieeeMantissa;
			}
			bool even = (mantissa & 1U) == 0U, acceptBounds = even, mmShift =
				ieeeMantissa != 0 || ieeeExponent <= 1;

			// Step 2: Determine the interval of valid decimal representations
			uint mv = mantissa << 2, mp = mv + 2U, mm = mv - 1U - (mmShift ? 1U : 0U);

			// Step 3: Convert to a decimal power base using 64-bit arithmetic
			uint vr, vp, vm;
			int e10;
			bool vmTrailingZeroes = false, vrTrailingZeroes = false;
			uint removedDigit = 0U;
			if (exponent >= 0) {
				int q = RyuUtils.Log10Pow2(exponent), k = FLOAT_POW5_INV_BITCOUNT + RyuUtils.
					Pow5Bits(q) - 1, i = -exponent + q + k;
				e10 = q;
				vr = RyuTables.MulPow5InvDivPow2(mv, q, i);
				vp = RyuTables.MulPow5InvDivPow2(mp, q, i);
				vm = RyuTables.MulPow5InvDivPow2(mm, q, i);
				if (q != 0U && (vp - 1U) / 10U <= vm / 10U) {
					// We need to know one removed digit even if we are not going to loop
					// below. We could use q = X - 1 above, except that would require 33 bits
					// for the result, and we've found that 32-bit arithmetic is faster even on
					// 64-bit machines
					int l = FLOAT_POW5_INV_BITCOUNT + RyuUtils.Pow5Bits(q - 1) - 1;
					removedDigit = RyuTables.MulPow5InvDivPow2(mv, q - 1, -exponent +
						q - 1 + l) % 10U;
				}
				if (q <= 9U) {
					// The largest power of 5 that fits in 24 bits is 5^10, but q <= 9 seems to
					// be safe as well. Only one of mp, mv, and mm can be a multiple of 5, if
					// any
					if (mv % 5U == 0U)
						vrTrailingZeroes = RyuUtils.IsMultipleOf5Power(mv, q);
					else if (acceptBounds)
						vmTrailingZeroes = RyuUtils.IsMultipleOf5Power(mm, q);
					else if (RyuUtils.IsMultipleOf5Power(mp, q))
						vp -= 1U;
				}
			} else {
				int q = RyuUtils.Log10Pow5(-exponent), i = -exponent - q, k = RyuUtils.
					Pow5Bits(i) - FLOAT_POW5_BITCOUNT, j = q - k;
				e10 = q + exponent;
				vr = RyuTables.MulPow5DivPow2(mv, i, j);
				vp = RyuTables.MulPow5DivPow2(mp, i, j);
				vm = RyuTables.MulPow5DivPow2(mm, i, j);
				if (q != 0U && (vp - 1U) / 10U <= vm / 10U) {
					j = q - 1 - (RyuUtils.Pow5Bits(i + 1) - FLOAT_POW5_BITCOUNT);
					removedDigit = RyuTables.MulPow5DivPow2(mv, i + 1, j) % 10U;
				}
				if (q <= 1U) {
					// {vr,vp,vm} is trailing zeros if {mv,mp,mm} has at least q trailing 0
					// bits. mv = 4 * m2, so it always has at least two trailing 0 bits
					vrTrailingZeroes = true;
					if (acceptBounds)
						// mm = mv - 1 - mmShift, so it has 1 trailing 0 bit iff mmShift == 1
						vmTrailingZeroes = mmShift;
					else
						// mp = mv + 2, so it always has at least one trailing 0 bit
						--vp;
				} else if (q < 31U)
					vrTrailingZeroes = RyuUtils.IsMultipleOf2Power(mv, q - 1);
			}

			// Step 4: Find the shortest decimal representation in the interval of valid
			// representations
			int removed = 0;
			uint output, m10, p10;
			if (vmTrailingZeroes || vrTrailingZeroes) {
				// General case, which happens rarely (~4.0%)
				while ((p10 = vp / 10U) > (m10 = vm.DivMod(10U, out uint vmRem))) {
					if (vmRem != 0U)
						vmTrailingZeroes = false;
					if (removedDigit != 0U)
						vrTrailingZeroes = false;
					vr = vr.DivMod(10U, out removedDigit);
					vp = p10;
					vm = m10;
					removed++;
				}
				if (vmTrailingZeroes)
					while ((vm - 10U * (m10 = vm / 10U)) == 0U) {
						if (removedDigit != 0U)
							vrTrailingZeroes = false;
						vr = vr.DivMod(10U, out removedDigit);
						vp /= 10U;
						vm = m10;
						removed++;
					}
				if (vrTrailingZeroes && removedDigit == 5U && vr % 2U == 0U)
					// Round even if the exact number is .....50..0
					removedDigit = 4U;
				// We need to take vr + 1 if vr is outside bounds or we need to round up
				output = vr;
				if ((vr == vm && (!acceptBounds || !vmTrailingZeroes)) || removedDigit >= 5U)
					output++;
			} else {
				// Specialized for the common case (~96.0%). Percentages below are relative to
				// this. Loop iterations below (approximately):
				// 0: 13.6%, 1: 70.7%, 2: 14.1%, 3: 1.39%, 4: 0.14%, 5+: 0.01%
				while (RyuUtils.DivCompare10(ref vp, ref vm)) {
					vr = vr.DivMod(10U, out removedDigit);
					removed++;
				}
				// We need to take vr + 1 if vr is outside bounds or we need to round up
				output = vr;
				if (vr == vm || removedDigit >= 5)
					output++;
			}
			sign = fSign;
			this.mantissa = output;
			this.exponent = e10 + removed;
		}

		public override bool Equals(object obj) {
			return obj is RyuFloat32 other && Equals(other);
		}

		public bool Equals(RyuFloat32 other) {
			return sign == other.sign && mantissa == other.mantissa && exponent ==
				other.exponent;
		}

		public override int GetHashCode() {
			return exponent ^ (int)mantissa;
		}

		/// <summary>
		/// Convert a 32-bit Ryu floating point number to a roundtrip notation string.
		/// </summary>
		public void ToRoundtripString(StringBuilder result, RyuFormatOptions options,
				NumberFormatInfo info = null) {
			// Step 5: Print the decimal representation
			if (info == null)
				info = CultureInfo.CurrentCulture.NumberFormat;
			if (sign)
				result.Append(info.NegativeSign);
#if DEBUG
			if (info.NumberDecimalSeparator.Length > 1)
				throw new ArgumentException("Requires a single character decimal point");
#endif

			// Print the decimal digits
			uint mantissa = this.mantissa;
			int olength = RyuUtils.DecimalLength9(mantissa), start = result.Length, index =
				olength + start;
			result.Length = index + 1;
			index = RyuUtils.PrintGroups42(result, ref mantissa, index);
			// Group of 1
			if (mantissa >= 10U) {
				string digits = RyuTables.DIGIT_TABLE[mantissa];
				// We can't use memcpy here: the decimal dot goes between these two digits
				result[index--] = digits[1];
				result[start] = digits[0];
			} else
				result[start] = mantissa.DigitToChar();

			// Print decimal point if needed
			if (olength > 1) {
				result[start + 1] = info.NumberDecimalSeparator[0];
				index += olength;
			}
			result.Length = index;

			RyuUtils.AppendExponent(result, exponent + olength - 1, options, info);
		}

		public override string ToString() {
			var sb = new StringBuilder(32);
			ToRoundtripString(sb, RyuFormatOptions.RoundtripMode);
			return sb.ToString();
		}
	}
}
