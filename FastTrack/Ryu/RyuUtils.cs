/*
 * Copyright 2025 Peter Han
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
	/// Shared utilities for Ryu number formatting.
	/// </summary>
	internal static class RyuUtils {
		internal const ulong UINT32_BITS = uint.MaxValue;

		/// <summary>
		/// The character for Arabic numeral zero. Some functions assume the other 9 are laid
		/// out in order after this one.
		/// </summary>
		internal const char ZERO = '0';

		#region Float32
		/// <summary>
		/// Returns the number of decimal digits in v, which must not contain more than 9
		/// digits.
		/// </summary>
		internal static int DecimalLength9(uint value) {
			// Function precondition: v is not a 10-digit number.
			// (f2s: 9 digits are sufficient for round-tripping.)
			// (d2fixed: We print 9-digit blocks.)
#if DEBUG
			if (value >= 1000000000)
				throw new ArgumentOutOfRangeException(nameof(value), value, "value too large");
#endif
			if (value >= 100000000) { return 9; }
			if (value >= 10000000) { return 8; }
			if (value >= 1000000) { return 7; }
			if (value >= 100000) { return 6; }
			if (value >= 10000) { return 5; }
			if (value >= 1000) { return 4; }
			if (value >= 100) { return 3; }
			if (value >= 10) { return 2; }
			return 1;
		}

		/// <summary>
		/// Divides the 32-bit number by another and also outputs the modulus as a 32-bit int.
		/// </summary>
		internal static uint DivMod(this uint digits, uint divisor, out uint remainder) {
			uint digits10 = digits / divisor;
			remainder = digits - divisor * digits10;
			return digits10;
		}

		/// <summary>
		/// Divides two 32-bit numbers by ten, and reports whether the first is greater after
		/// dividing. Only updates the references if the condition is true.
		/// </summary>
		internal static bool DivCompare10(ref uint p, ref uint m) {
			uint pd = p / 10U, md = m / 10U;
			bool result = pd > md;
			if (result) {
				p = pd;
				m = md;
			}
			return result;
		}

		/// <summary>
		/// Returns true if value is divisible by 2^p.
		/// </summary>
		/// <returns></returns>
		internal static bool IsMultipleOf2Power(uint value, int p) {
			return (value & ((1U << p) - 1U)) == 0U;
		}

		/// <summary>
		/// Returns true if the value is divisible by 5^p.
		/// </summary>
		internal static bool IsMultipleOf5Power(uint value, int p) {
			return Pow5Factor(value) >= p;
		}

		/// <summary>
		/// It seems to be slightly faster to avoid uint128_t here, although the
		/// generated code for uint128_t looks slightly nicer.
		/// </summary>
		internal static uint MulShift(uint m, ulong factor, int shift) {
#if DEBUG
			if (shift <= 32)
				throw new ArgumentOutOfRangeException(nameof(shift), shift, "shift too small");
#endif
			ulong bits0 = m * (factor & UINT32_BITS), bits1 = m * (factor >> 32);
			ulong sum = (bits0 >> 32) + bits1, shiftedSum = sum >> (shift - 32);
#if DEBUG
			if (shiftedSum > uint.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(shiftedSum));
#endif
			return (uint)shiftedSum;
		}

		private static int Pow5Factor(uint value) {
			int count = 0;
			while (true) {
#if DEBUG
				if (value == 0U)
					throw new ArgumentException("value is 0");
#endif
				uint q = value / 5U, r = value - 5U * q;
				if (r != 0U)
					break;
				value = q;
				++count;
			}
			return count;
		}
		#endregion

		#region Float64
		/// <summary>
		/// Returns the number of decimal digits in v, which must not contain more than 17
		/// digits.
		/// </summary>
		internal static int DecimalLength17(ulong value) {
			// This is slightly faster than a loop.
			// The average output length is 16.38 digits, so we check high-to-low.
			// Function precondition: v is not an 18, 19, or 20-digit number.
			// (17 digits are sufficient for round-tripping.)
#if DEBUG
			if (value >= 100000000000000000UL)
				throw new ArgumentOutOfRangeException(nameof(value), value, "value too large");
#endif
			if (value >= 10000000000000000UL) { return 17; }
			if (value >= 1000000000000000UL) { return 16; }
			if (value >= 100000000000000UL) { return 15; }
			if (value >= 10000000000000UL) { return 14; }
			if (value >= 1000000000000UL) { return 13; }
			if (value >= 100000000000UL) { return 12; }
			if (value >= 10000000000UL) { return 11; }
			if (value >= 1000000000UL) { return 10; }
			if (value >= 100000000UL) { return 9; }
			if (value >= 10000000UL) { return 8; }
			if (value >= 1000000UL) { return 7; }
			if (value >= 100000UL) { return 6; }
			if (value >= 10000UL) { return 5; }
			if (value >= 1000UL) { return 4; }
			if (value >= 100UL) { return 3; }
			if (value >= 10UL) { return 2; }
			return 1;
		}

		/// <summary>
		/// Divides two 64-bit numbers by another, and reports whether the first is greater
		/// after dividing. Only updates the references if the condition is true.
		/// </summary>
		internal static bool DivCompare(ref ulong p, ref ulong m, ulong divisor) {
			ulong pd = p / divisor, md = m / divisor;
			bool result = pd > md;
			if (result) {
				p = pd;
				m = md;
			}
			return result;
		}

		/// <summary>
		/// Divides the 64-bit number by another and also outputs the modulus as a 32-bit int.
		/// </summary>
		internal static ulong DivMod(this ulong digits, uint divisor, out uint remainder) {
			ulong digits10 = digits / divisor;
			remainder = (uint)digits - divisor * (uint)digits10;
			return digits10;
		}

		// Returns true if value is divisible by 2^p.
		internal static bool IsMultipleOf2Power(ulong value, int p) {
#if DEBUG
			if (value == 0UL)
				throw new ArgumentException("value is 0");
			if (p >= 64)
				throw new ArgumentOutOfRangeException(nameof(p), p, "p is too large");
#endif
			return (value & ((1UL << p) - 1UL)) == 0UL;
		}

		/// <summary>
		/// Returns true if value is divisible by 5^p.
		/// </summary>
		internal static bool IsMultipleOf5Power(ulong value, int p) {
			return Pow5Factor(value) >= p;
		}

		private static uint Mod1E9(ulong x) => (uint)(x % 1000000000UL);

		/// <summary>
		/// We need a 64x128-bit multiplication and a subsequent 128-bit shift.
		/// Multiplication:
		///   The 64-bit factor is variable and passed in, the 128-bit factor comes
		///   from a lookup table. We know that the 64-bit factor only has 55
		///   significant bits (i.e., the 9 topmost bits are zeros). The 128-bit
		///   factor only has 124 significant bits (i.e., the 4 topmost bits are
		///   zeros).
		/// Shift:
		///   In principle, the multiplication result requires 55 + 124 = 179 bits to
		///   represent. However, we then shift this value to the right by j, which is
		///   at least j >= 115, so the result is guaranteed to fit into 179 - 115 = 64
		///   bits. This means that we only need the topmost 64 significant bits of
		///   the 64x128-bit multiplication.
		/// </summary>
		internal static ulong MulShift(ulong m, ulong mulA, ulong mulB, int j) {
			// m is maximum 55 bits
			ulong low1 = UMul128(m, mulB, out ulong high1);
			UMul128(m, mulA, out ulong high0);
			ulong sum = high0 + low1;
			if (sum < high0)
				// overflow into high1
				++high1;
			return ShiftRight128(sum, high1, j - 64);
		}

		/// <summary>
		/// This is faster without a 64x64->128-bit multiplication.
		/// </summary>
		internal static ulong MulShiftAll(ulong m, ulong mulA, ulong mulB, int j, out ulong vp,
				out ulong vm, bool mmShift) {
			m <<= 1;
			// m is maximum 55 bits
			ulong lo = UMul128(m, mulA, out ulong tmp);
			ulong mid = tmp + UMul128(m, mulB, out ulong hi);
			// overflow into hi
			hi += (mid < tmp) ? 1UL : 0UL;

			ulong lo2 = lo + mulA;
			ulong mid2 = mid + mulB + ((lo2 < lo) ? 1UL : 0UL);
			ulong hi2 = hi + ((mid2 < mid) ? 1UL : 0UL);
			vp = ShiftRight128(mid2, hi2, j - 65);

			if (mmShift) {
				ulong lo3 = lo - mulA;
				ulong mid3 = mid - mulB - ((lo3 > lo) ? 1UL : 0UL);
				ulong hi3 = hi - ((mid3 > mid) ? 1UL : 0UL);
				vm = ShiftRight128(mid3, hi3, j - 65);
			} else {
				ulong lo3 = lo + lo;
				ulong mid3 = mid + mid + ((lo3 < lo) ? 1UL : 0UL);
				ulong hi3 = hi + hi + ((mid3 < mid) ? 1UL : 0UL);
				ulong lo4 = lo3 - mulA;
				ulong mid4 = mid3 - mulB - ((lo4 > lo3) ? 1UL : 0UL);
				ulong hi4 = hi3 - ((mid4 > mid3) ? 1UL : 0UL);
				vm = ShiftRight128(mid4, hi4, j - 64);
			}

			return ShiftRight128(mid, hi, j - 65);
		}

		// = floor(mul * m / (1 << j)) mod 1E9
		internal static uint MulShiftMod1E9(ulong m, ulong mulA, ulong mulB, ulong mulC,
				int j) {
			// 0 and 64
			ulong low0 = UMul128(m, mulA, out ulong high0);
			// 64 and 128
			ulong low1 = UMul128(m, mulB, out ulong high1);
			// 128 and 192
			ulong low2 = UMul128(m, mulC, out ulong high2);
			// 64
			ulong s0high = low1 + high0;
			// 128
			ulong s1low = low2 + high1;
			if (s0high < low1) s1low++;
			// high1 + c1 cannot overflow, so compare against low2
			// 192
			ulong s1high = high2;
			if (s1low < low2) s1high++;
#if DEBUG
			if (j < 128)
				throw new ArgumentOutOfRangeException(nameof(j), j, "j too low");
			if (j > 180)
				throw new ArgumentOutOfRangeException(nameof(j), j, "j too high");
#endif
			if (j < 160) {
				ulong r1 = Mod1E9(((ulong)Mod1E9(s1high) << 32) | (s1low >> 32));
				return Mod1E9(((r1 << 32) | (s1low & UINT32_BITS)) >> (j - 128));
			} else
				return Mod1E9(((Mod1E9(s1high) << 32) | (s1low >> 32)) >> (j - 160));
		}

		internal static uint MulShiftMod1E9(uint m, uint mulA, uint mulB, uint mulC, int j) {
			ulong a0 = (ulong)m * mulA, a1 = (ulong)m * mulB, a2 = (ulong)m * mulC;
			// s = a0 + (a1 >> 32) + (a2 >> 64)
			// 0
			ulong s0low = a0 + (a1 << 32), s0high = (a1 >> 32) + a2;
			if (s0low < a0) s0high++;
			if (j < 64)
				return Mod1E9(((s0high << 32) | (s0low >> 32)) >> (j - 32));
			else
				return Mod1E9(s0high >> (j - 64));
		}

		private static int Pow5Factor(ulong value) {
			int count = 0;
			while (true) {
#if DEBUG
				if (value == 0U)
					throw new ArgumentException("value is 0");
#endif
				ulong q = value / 5UL;
				uint r = (uint)value - 5U * (uint)q;
				if (r != 0U)
					break;
				value = q;
				++count;
			}
			return count;
		}

		internal static ulong ShiftRight128(ulong lo, ulong hi, int dist) {
			// No need to handle the case dist >= 64 here (see above)
#if DEBUG
			if (dist <= 0U || dist >= 64U)
				throw new ArgumentOutOfRangeException(nameof(dist), dist, "dist out of range");
#endif
			return (hi << (64 - dist)) | (lo >> dist);
		}

		/// <summary>
		/// Multiplies two 64-bit numbers into a 128-bit number, returning the low 64 bits
		/// and using the out parameter for the upper 64 bits.
		/// </summary>
		internal static ulong UMul128(ulong a, ulong b, out ulong productHigh) {
			ulong aLo = a & UINT32_BITS, aHi = a >> 32, bLo = b & UINT32_BITS, bHi = b >> 32;

			ulong b00 = aLo * bLo, b01 = aLo * bHi, b10 = aHi * bLo, b11 = aHi * bHi;
			ulong b00Low = b00 & UINT32_BITS, b00High = b00 >> 32;
			ulong mid1 = b10 + b00High;
			ulong mid1Low = mid1 & UINT32_BITS, mid1High = mid1 >> 32;
			ulong mid2 = b01 + mid1Low;
			ulong mid2Low = mid2 & UINT32_BITS, mid2High = mid2 >> 32;

			productHigh = b11 + mid1High + mid2High;
			return (mid2Low << 32) | b00Low;
		}
		#endregion

		#region Math
		internal static int IndexForExponent(int e) => (e + 15) >> 4;

		internal static uint LastDigit(ref uint digits, int iterations) {
			uint lastDigit = 0U, iter = digits;
			for (int i = iterations; i > 0; i--) {
				uint iter10 = iter / 10U;
				lastDigit = iter - 10U * iter10;
				iter = iter10;
			}
			digits = iter;
			return lastDigit;
		}

		internal static int LengthForIndex(int idx) => (Log10Pow2(idx << 4) + 25) / 9;

		/// <summary>
		/// Returns e == 0 ? 1 : [log_2(5^e)]; requires 0 <= e <= 3528.
		/// </summary>
		internal static int Log2Pow5(int e) {
			// This approximation works up to the point that the multiplication overflows at
			// e = 3529. If the multiplication were done in 64 bits, it would fail at 5^4004
			// which is just greater than 2^9297.
#if DEBUG
			if (e < 0 || e > 3528)
				throw new ArgumentOutOfRangeException(nameof(e), e, "Overflow");
#endif
			return (int)(((uint)e * 1217359U) >> 19);
		}

		/// <summary>
		/// Returns floor(log_10(2^e)); requires 0 <= e <= 1650.
		/// </summary>
		internal static int Log10Pow2(int e) {
			// The first value this approximation fails for is 2^1651 which is just greater
			// than 10^297.
#if DEBUG
			if (e < 0 || e > 1650)
				throw new ArgumentOutOfRangeException(nameof(e), e, "Overflow");
#endif
			return (e * 78913) >> 18;
		}

		/// <summary>
		/// Returns floor(log_10(5^e)); requires 0 <= e <= 2620.
		/// </summary>
		internal static int Log10Pow5(int e) {
			// The first value this approximation fails for is 5^2621 which is just greater
			// than 10^1832.
#if DEBUG
			if (e < 0 || e > 2620)
				throw new ArgumentOutOfRangeException(nameof(e), e, "Overflow");
#endif
			return (e * 732923) >> 20;
		}

		/// <summary>
		/// Returns e == 0 ? 1 : ceil(log_2(5^e)); requires 0 <= e <= 3528.
		/// </summary>
		internal static int Pow5Bits(int e) {
			// This approximation works up to the point that the multiplication overflows at
			// e = 3529. If the multiplication were done in 64 bits, it would fail at 5^4004
			// which is just greater than 2^9297.
#if DEBUG
			if (e > 3528)
				throw new ArgumentOutOfRangeException(nameof(e), e, "Overflow");
#endif
			return (int)((((uint)e * 1217359U) >> 19) + 1U);
		}
		#endregion

		#region String Operations
		/// <summary>
		/// Adds thousands separators to the fixed point number. It uses separators per
		/// hardcoded 3 digits because accessing NumberGroupSizes clones it (!)
		/// 
		/// Must be invoked before the decimal point is added.
		/// </summary>
		internal static void AddThousands(StringBuilder result, int start,
				NumberFormatInfo info) {
			const int GROUP_SIZE = 3;
			string ts = info.NumberGroupSeparator;
			int len = result.Length, i, perSep = ts.Length, needed, j;
			// Negative sign (if any) is not counted in the mantissa length
			needed = len - start;
			if (result[start] == info.NegativeSign[0])
				needed--;
			needed = (needed - 1) / GROUP_SIZE * perSep;
			if (needed > 0) {
				// Allocate more space, and then move-up
				j = len + needed;
				result.Length = j;
				needed = 0;
				i = len - 1;
				while (i >= start && i < --j) {
					result[j] = result[i--];
					// If GROUP_SIZE characters have been moved, insert a sep
					needed++;
					if (needed == GROUP_SIZE) {
						j -= perSep;
						for (int k = 0; k < perSep; k++)
							result[j + k] = ts[k];
						needed = 0;
					}
				}
			}
		}

		/// <summary>
		/// Appends zeroes to the number.
		/// </summary>
		internal static void Append0(StringBuilder result, int digits) {
			for (int i = digits; i > 0; i--)
				result.Append(ZERO);
		}

		/// <summary>
		/// Convert `digits` to decimal and append the last 9 decimal digits to result.
		/// If `digits` contains additional digits, then those are silently ignored.
		/// </summary>
		internal static void Append9Digits(StringBuilder result, uint digits) {
			if (digits == 0U)
				Append0(result, 9);
			else {
				int index = result.Length + 9;
				result.Length = index--;
				for (int i = 0; i < 2; i++) {
					uint d10000 = digits / 10000U, c0 = digits - 10000 * d10000;
					uint c1 = c0 / 100U;
					digits = d10000;
					index = result.WriteDigits(index, c0 - 100U * c1);
					index = result.WriteDigits(index, c1);
				}
				result[index] = digits.DigitToChar();
			}
		}

		/// <summary>
		/// Convert `digits` to decimal and write the last `count` decimal digits to result.
		/// If `digits` contains additional digits, then those are silently ignored.
		/// </summary>
		internal static int AppendCDigits(StringBuilder result, uint digits, int count) {
			int i = 0, index = result.Length + count;
			result.Length = index--;
			for (; i < count - 1; i += 2) {
				uint d100 = digits / 100U, c = digits - 100U * d100;
				digits = d100;
				index = result.WriteDigits(index, c);
			}
			// Generate the last digit if count is odd
			if (i < count)
				result[index--] = (digits % 10U).DigitToChar();
			return index;
		}

		/// <summary>
		/// Convert `digits` to a sequence of decimal digits. Print the first digit, followed
		/// by a decimal separator followed by the remaining digits. The caller has to
		/// guarantee that:
		///   10^(olength-1) <= digits < 10^olength
		/// e.g., by passing `olength` as `decimalLength9(digits)`
		/// </summary>
		internal static void AppendDDigits(StringBuilder result, uint digits, int count,
				bool printDecimalPoint, NumberFormatInfo info) {
			if (printDecimalPoint) {
				char dp = info.NumberDecimalSeparator[0];
				int index = result.Length + count;
				result.Length = index--;
				index = PrintGroups42(result, ref digits, index);
				if (digits >= 10U) {
					string tbl = RyuTables.DIGIT_TABLE[digits];
					result[index--] = tbl[1];
					result[index--] = dp;
					result[index--] = tbl[0];
				} else {
					result[index--] = dp;
					result[index--] = digits.DigitToChar();
				}
			} else
				result.Append(digits.DigitToChar());
		}

		/// <summary>
		/// Convert `digits` to a sequence of decimal digits. Appends the digits to the result.
		/// The caller has to guarantee that:
		///   10^(olength-1) <= digits < 10^olength
		/// e.g., by passing `olength` as `decimalLength9(digits)`
		/// </summary>
		internal static int AppendNDigits(StringBuilder result, int count, uint digits) {
			int index = result.Length + count;
			result.Length = index--;
			index = PrintGroups42(result, ref digits, index);
			if (digits >= 10U)
				index = result.WriteDigits(index, digits);
			else
				result[index--] = digits.DigitToChar();
			return index;
		}

		/// <summary>
		/// Appends the exponent for exponential and roundtrip notation.
		/// </summary>
		internal static void AppendExponent(StringBuilder result, int exponent,
				RyuFormatOptions options, NumberFormatInfo info) {
			bool compat = (options & RyuFormatOptions.CompatibleExponent) != 0;
#if DEBUG
			if (exponent < -9999 || exponent > 9999)
				throw new ArgumentOutOfRangeException(nameof(exponent), exponent,
					"4 digit exponent limit");
#endif
			result.Append('E');
			if (exponent < 0) {
				result.Append(info.NegativeSign);
				exponent = -exponent;
			} else if (compat || (options & RyuFormatOptions.ExponentialMode) != 0)
				result.Append(info.PositiveSign);

			if (exponent >= 100) {
				int e100 = exponent / 100;
				if (e100 >= 10)
					result.Append(RyuTables.DIGIT_TABLE[e100]);
				else
					result.Append(e100.DigitToChar());
				result.Append(RyuTables.DIGIT_TABLE[exponent - 100 * e100]);
			} else {
				if (compat)
					result.Append(ZERO);
				if (exponent >= 10)
					result.Append(RyuTables.DIGIT_TABLE[exponent]);
				else {
					if (compat)
						result.Append(ZERO);
					result.Append(exponent.DigitToChar());
				}
			}
		}

		/// <summary>
		/// Converts a single digit to a its character representation.
		/// </summary>
		internal static char DigitToChar(this int digit) => (char)(ZERO + digit);

		internal static char DigitToChar(this uint digit) => (char)(ZERO + digit);

		/// <summary>
		/// Generates special strings for infinity and NaN.
		/// </summary>
		internal static void GenerateSpecial(StringBuilder result, bool sign, bool mantissa,
				NumberFormatInfo info) {
			if (mantissa)
				result.Append(info.NaNSymbol);
			else
				result.Append(sign ? info.NegativeInfinitySymbol : info.
					PositiveInfinitySymbol);
		}

		/// <summary>
		/// Generates the output for positive (and negative) zero.
		/// </summary>
		internal static void GenerateZero(StringBuilder result, bool sign, int precision,
				RyuFormatOptions options, NumberFormatInfo info) {
			bool compat = (options & RyuFormatOptions.CompatibleExponent) != 0, hard =
				precision > 0 && (options & RyuFormatOptions.SoftPrecision) == 0;
			if ((options & RyuFormatOptions.RoundtripMode) != 0) {
				// Roundtrip mode just needs +0 or -0
				if (sign)
					result.Append("-");
				if (compat)
					result.Append("0E+000");
				else
					result.Append("0E0");
			} else if ((options & RyuFormatOptions.ExponentialMode) != 0) {
				// Exponential mode
				result.Append(ZERO);
				if (hard) {
					result.Append(info.NumberDecimalSeparator);
					Append0(result, precision);
				}
				if (compat)
					result.Append("E+000");
				else
					result.Append("E0");
			} else {
				// Fixed mode
				result.Append(ZERO);
				if (hard) {
					result.Append(info.NumberDecimalSeparator);
					Append0(result, precision);
				}
			}
		}

		internal static NumberFormatInfo GetFormatInfo(IFormatProvider provider) {
			NumberFormatInfo result;
			if (provider != null && provider.GetFormat(typeof(NumberFormatInfo)) is
					NumberFormatInfo info)
				result = info;
			else
				result = CultureInfo.CurrentCulture.NumberFormat;
			return result;
		}

		/// <summary>
		/// Prints groups of four digits to the output buffer building backwards.
		/// </summary>
		internal static int PrintGroups42(StringBuilder result, ref uint digits, int index) {
			uint output = digits;
			// Groups of 4
			while (output >= 10000U) {
				uint o10000 = output / 10000U, c0 = output - 10000U * o10000, c1 = c0 / 100U;
				output = o10000;
				index = WriteDigits(result, index, c0 - 100U * c1);
				index = WriteDigits(result, index, c1);
			}
			// Group of 2
			if (output >= 100U) {
				uint o100 = output / 100U;
				index = WriteDigits(result, index, output - 100U * o100);
				output = o100;
			}
			digits = output;
			return index;
		}

		/// <summary>
		/// Rounds the result up if necessary. Returns true if the number was rounded up one
		/// whole unit, or false otherwise.
		/// </summary>
		internal static bool RoundResult(StringBuilder result, int start, int roundFlag,
				out int decimalIndex, NumberFormatInfo info) {
			// '.' cannot be located at index 0
			int index = result.Length, dIndex = 0;
			bool roundedUnits = false;
			char c, dp = info.NumberDecimalSeparator[0];
			while (true) {
				if (--index < start) {
					roundedUnits = true;
					break;
				}
				c = result[index];
				if (c == dp)
					dIndex = index;
				else if (c < '0' || c > '9') {
					roundedUnits = true;
					break;
				} else if (c == '9') {
					result[index] = ZERO;
					roundFlag = 1;
				} else {
					if (roundFlag != 2 || c % 2 != 0)
						result[index] = ++c;
					break;
				}
			}
			if (roundedUnits)
				result[index + 1] = '1';
			decimalIndex = dIndex;
			return roundedUnits;
		}

		/// <summary>
		/// Trims off all the trailing zeroes after the decimal. Used to implement soft
		/// precision (like the custom # specifier).
		/// </summary>
		internal static void SoftenResult(StringBuilder result, NumberFormatInfo info) {
			int start = result.Length - 1, index = start;
			char dp = info.NumberDecimalSeparator[0];
			while (index > 0) {
				char c = result[index];
				if (c == dp) {
					index--;
					break;
				} else if (c != ZERO)
					break;
				index--;
			}
			if (index != start)
				result.Length = index + 1;
		}

		/// <summary>
		/// Writes two digits from 00-99 to the string building backwards.
		/// </summary>
		internal static int WriteDigits(this StringBuilder builder, int index, uint entry) {
			string tbl = RyuTables.DIGIT_TABLE[entry];
			builder[index--] = tbl[1];
			builder[index--] = tbl[0];
			return index;
		}
		#endregion

		#region Bit Conversions
		internal static unsafe uint float_to_bits(float f) {
			return *(uint*)&f;
		}

		internal static unsafe ulong double_to_bits(double d) {
			return *(ulong*)&d;
		}
		#endregion
	}
}
