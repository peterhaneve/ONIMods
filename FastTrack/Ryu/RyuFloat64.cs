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
using System.Globalization;
using System.Text;

namespace Ryu {
	/// <summary>
	/// The Ryu representation of a 64-bit float.
	/// </summary>
	internal struct RyuFloat64 : IEquatable<RyuFloat64> {
		internal const int DOUBLE_BIAS = 1023;

		internal const int DOUBLE_EXPONENT_BITS = 11;

		internal const int DOUBLE_MANTISSA_BITS = 52;

		private const int DOUBLE_POW5_BITCOUNT = 125;

		private const int DOUBLE_POW5_INV_BITCOUNT = 125;

		internal const uint EXPONENT_MASK = (1U << DOUBLE_EXPONENT_BITS) - 1U;

		private const ulong SIGN_MASK = 1UL << (DOUBLE_MANTISSA_BITS + DOUBLE_EXPONENT_BITS);

		private const ulong MANTISSA_MASK = (1UL << DOUBLE_MANTISSA_BITS) - 1UL;

		/// <summary>
		/// Decode a double into its sign, mantissa, and exponent.
		/// </summary>
		internal static bool Decode(double value, out ulong mantissa, out uint exponent) {
			ulong bits = RyuUtils.double_to_bits(value);
			mantissa = bits & MANTISSA_MASK;
			exponent = (uint)(bits >> DOUBLE_MANTISSA_BITS) & EXPONENT_MASK;
			return (bits & SIGN_MASK) != 0U;
		}

		internal readonly int exponent;

		internal readonly ulong mantissa;

		internal readonly bool sign;

		public RyuFloat64(bool sign, ulong ieeeMantissa, uint ieeeExponent) {
			// Subtract 2 more so that the bounds computation has 2 additional bits
			const int DOUBLE_EXP_DIFF_P2 = DOUBLE_BIAS + DOUBLE_MANTISSA_BITS + 2;
			int exponent;
			ulong mantissa;
			if (ieeeExponent == 0) {
				exponent = 1 - DOUBLE_EXP_DIFF_P2;
				mantissa = ieeeMantissa;
			} else {
				exponent = (int)ieeeExponent - DOUBLE_EXP_DIFF_P2;
				mantissa = (1UL << DOUBLE_MANTISSA_BITS) | ieeeMantissa;
			}

			// Step 2: Determine the interval of valid decimal representations
			bool even = (mantissa & 1UL) == 0UL, acceptBounds = even, mmShift =
				ieeeMantissa != 0U || ieeeExponent <= 1U;
			ulong mv = mantissa << 2;

			// Step 3: Convert to a decimal power base using 128-bit arithmetic
			ulong vr, vm, vp;
			int e10;
			bool vmTrailingZeroes = false, vrTrailingZeroes = false;
			if (exponent >= 0) {
				// Tried special-casing q == 0, but there was no effect on performance
				// This expression is slightly faster than max(0, log10Pow2(e2) - 1)
				int q = RyuUtils.Log10Pow2(exponent);
				if (exponent > 3) q--;
				int k = DOUBLE_POW5_INV_BITCOUNT + RyuUtils.Pow5Bits(q) - 1, i = -exponent +
					q + k;
				ulong a = RyuTables.double_computeInvPow5(q, out ulong b);
				e10 = q;
				vr = RyuUtils.MulShiftAll(mantissa, a, b, i, out vp, out vm, mmShift);
				if (q <= 21U) {
					// This should use q <= 22, but I think 21 is also safe. Smaller values
					// may still be safe, but it's more difficult to reason about them.
					// Only one of mp, mv, and mm can be a multiple of 5, if any
					uint mvMod5 = (uint)mv % 5U;
					if (mvMod5 == 0U)
						vrTrailingZeroes = RyuUtils.IsMultipleOf5Power(mv, q);
					else if (acceptBounds)
						// Same as min(e2 + (~mm & 1), pow5Factor(mm)) >= q
						// <=> e2 + (~mm & 1) >= q && pow5Factor(mm) >= q
						// <=> true && pow5Factor(mm) >= q, since e2 >= q
						vmTrailingZeroes = RyuUtils.IsMultipleOf5Power(mv - 1UL - (mmShift ?
							1UL : 0UL), q);
					else
						// Same as min(e2 + 1, pow5Factor(mp)) >= q
						vp -= RyuUtils.IsMultipleOf5Power(mv + 2UL, q) ? 1UL : 0UL;
				}
			} else {
				// This expression is slightly faster than max(0, log10Pow5(-e2) - 1)
				int q = RyuUtils.Log10Pow5(-exponent);
				if (-exponent > 1) q--;
				int i = -exponent - q, k = RyuUtils.Pow5Bits(i) - DOUBLE_POW5_BITCOUNT,
					j = q - k;
				ulong a = RyuTables.double_computePow5(i, out ulong b);
				e10 = q + exponent;
				vr = RyuUtils.MulShiftAll(mantissa, a, b, j, out vp, out vm, mmShift);
				if (q <= 1U) {
					// {vr,vp,vm} is trailing zeros if {mv,mp,mm} has at least q trailing 0
					// bits; mv = 4 * m2, so it always has at least two trailing 0 bits
					vrTrailingZeroes = true;
					if (acceptBounds)
						// mm = mv - 1 - mmShift, so it has 1 trailing 0 bit iff mmShift == 1
						vmTrailingZeroes = mmShift;
					else
						// mp = mv + 2, so it always has at least one trailing 0 bit
						--vp;
				} else if (q < 63U)
					// We want to know if the fUL product has at least q trailing zeros
					// We need to compute min(p2(mv), p5(mv) - e2) >= q
					// <=> p2(mv) >= q && p5(mv) - e2 >= q
					// <=> p2(mv) >= q (because -e2 >= q)
					vrTrailingZeroes = RyuUtils.IsMultipleOf2Power(mv, q);
			}

			// Step 4: Find the shortest decimal representation in the interval of valid
			// representations
			int removed = 0;
			uint removedDigit = 0U;
			ulong output, p10, m10;
			// On average, we remove ~2 digits
			if (vmTrailingZeroes || vrTrailingZeroes) {
				// General case, which happens rarely (~0.7%)
				while ((p10 = vp / 10UL) > (m10 = vm.DivMod(10U, out uint vmRem))) {
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
					while (((uint)vm - 10U * (uint)(m10 = vm / 10UL)) == 0U) {
						if (removedDigit != 0U)
							vrTrailingZeroes = false;
						vr = vr.DivMod(10U, out removedDigit);
						vp /= 10UL;
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
				// Specialized for the common case (~99.3%); percentages below are relative to
				// this
				bool roundUp = false;
				if (RyuUtils.DivCompare(ref vp, ref vm, 100UL)) {
					// Optimization: remove two digits at a time (~86.2%)
					vr = vr.DivMod(100U, out uint round100);
					roundUp = round100 >= 50U;
					removed += 2;
				}
				// Loop iterations below (approximately), without optimization above:
				// 0: 0.03%, 1: 13.8%, 2: 70.6%, 3: 14.0%, 4: 1.40%, 5: 0.14%, 6+: 0.02%
				// Loop iterations below (approximately), with optimization above:
				// 0: 70.6%, 1: 27.8%, 2: 1.40%, 3: 0.14%, 4+: 0.02%
				while (RyuUtils.DivCompare(ref vp, ref vm, 10UL)) {
					vr = vr.DivMod(10U, out uint vrRem);
					roundUp = vrRem >= 5U;
					removed++;
				}
				// We need to take vr + 1 if vr is outside bounds or we need to round up
				output = vr;
				if (vr == vm || roundUp)
					output++;
			}

			this.sign = sign;
			this.exponent = e10 + removed;
			this.mantissa = output;
		}

		public override bool Equals(object obj) {
			return obj is RyuFloat64 other && Equals(other);
		}

		public bool Equals(RyuFloat64 other) {
			return sign == other.sign && mantissa == other.mantissa && exponent ==
				other.exponent;
		}

		public override int GetHashCode() {
			return exponent ^ mantissa.GetHashCode();
		}

		/// <summary>
		/// Convert a 64-bit Ryu floating point number to a roundtrip notation string.
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
			ulong mantissa = this.mantissa;
			uint mantissaShort;
			int olength = RyuUtils.DecimalLength17(mantissa), start = result.Length, index =
				olength + start;
			result.Length = index + 1;
			// Print the decimal digits: group of 8
			if ((mantissa >> 32) != 0U) {
				// We prefer 32-bit operations, even on 64-bit platforms.
				// We have at most 17 digits, and uint can store 9 digits.
				// If output doesn't fit into uint, we cut off 8 digits,
				// so the rest will fit into uint
				ulong q = mantissa / 100000000UL;
				mantissaShort = (uint)mantissa - 100000000U * (uint)q;
				uint o10000 = mantissaShort / 10000U;
				uint c0 = mantissaShort - 10000U * o10000, d0 = o10000 % 10000U;
				uint c1 = c0 / 100U, d1 = d0 / 100U;

				mantissa = q;
				index = result.WriteDigits(index, c0 - 100U * c1);
				index = result.WriteDigits(index, c1);
				index = result.WriteDigits(index, d0 - 100U * d1);
				index = result.WriteDigits(index, d1);
			}
			mantissaShort = (uint)mantissa;
			index = RyuUtils.PrintGroups42(result, ref mantissaShort, index);
			// Group of 1
			if (mantissaShort >= 10U) {
				string digits = RyuTables.DIGIT_TABLE[mantissaShort];
				// We can't use memcpy here: the decimal dot goes between these two digits
				result[index--] = digits[1];
				result[start] = digits[0];
			} else
				result[start] = mantissaShort.DigitToChar();

			// Print decimal point if needed
			if (olength > 1) {
				result[start + 1] = info.NumberDecimalSeparator[0];
				index += olength;
			}
			result.Length = index;

			RyuUtils.AppendExponent(result, exponent + olength - 1, options, info);
		}

		public override string ToString() {
			var sb = new StringBuilder(64);
			ToRoundtripString(sb, RyuFormatOptions.RoundtripMode);
			return sb.ToString();
		}
	}
}
