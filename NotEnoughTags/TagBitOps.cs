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

using PeterHan.PLib.Detours;

namespace PeterHan.NotEnoughTags {
	/// <summary>
	/// Operations performed on tags.
	/// </summary>
	internal static class TagBitOps {
		/// <summary>
		/// The maximum number of bits to set for a complemented (NOT) tag bits.
		/// </summary>
		private const int MAX_BITS = 2048;

		/// <summary>
		/// The upper 32 bits of a ulong.
		/// </summary>
		private const ulong UPPER_MASK = 0xFFFFFFFF00000000UL;

		// Allows accessing the fifth/seventh bit of TagBits without an instance.
		internal static readonly IDetouredField<object, ulong> FIFTH_BIT = typeof(TagBits).
			DetourStructField<ulong>("bits7");

		/// <summary>
		/// Ands two sets of tag bits and replaces Tag Bits A with the result A & B.
		/// </summary>
		/// <param name="lhs">Tag Bits A.</param>
		/// <param name="rhs">Tag Bits B.</param>
		internal static void And(ref TagBits lhs, ref TagBits rhs) {
			TagBits bits = lhs;
			ulong hibits = FIFTH_BIT.Get(lhs);
			bits.And(ref rhs);
			// Box into a type to allow the fields to be changed
			object localBits = bits;
			FIFTH_BIT.Set(localBits, TranspileAnd(hibits, FIFTH_BIT.Get(rhs)));
			lhs = (TagBits)localBits;
		}

		/// <summary>
		/// Gets the least significant 32 bits of tag bits 5/7.
		/// </summary>
		/// <param name="value">The upper 64 tag bits.</param>
		/// <returns>The tag bits value as an integer.</returns>
		internal static ulong GetLowerBits(ulong value) {
			return value & 0xFFFFFFFFUL;
		}

		/// <summary>
		/// Gets the most significant 32 bits of tag bits 5/7, shifted into the rightmost 32
		/// bits.
		/// </summary>
		/// <param name="value">The upper 64 tag bits.</param>
		/// <returns>The tag bits value as an integer.</returns>
		internal static int GetUpperBits(ulong value) {
			return (int)(value >> 32);
		}

		/// <summary>
		/// Replaces (bitsA & bitsB) == bitsB for extended tag bits.
		/// </summary>
		/// <param name="bitsA">The most significant bits of Tag A.</param>
		/// <param name="bitsB">The most significant bits of Tag B.</param>
		/// <returns>true if tag A has all of the tags of tag B, or false otherwise.</returns>
		internal static bool HasAll(ulong bitsA, ulong bitsB) {
			ulong lbB = GetLowerBits(bitsB);
			bool has = false;
			// Has all lower tags?
			if ((GetLowerBits(bitsA) & lbB) == lbB) {
				int ubA = GetUpperBits(bitsA), ubB = GetUpperBits(bitsB);
				if (ubB == 0 || ubA == ubB)
					// Fairly common case when not out of tags
					has = true;
				else if (ubA != 0) {
					// If there are extended bits in B, but not in A, then automatically false
					var inst = ExtendedTagBits.Instance;
					has = inst.GetTagBits(ubA).HasAll(inst.GetTagBits(ubB));
				}
			}
			return has;
		}

		/// <summary>
		/// Replaces (bitsA & bitsB) != 0 for extended tag bits. Also includes the previous
		/// terms of the comparison.
		/// </summary>
		/// <param name="before">The value of the OR statement for previous terms.</param>
		/// <param name="bitsA">The most significant bits of Tag A.</param>
		/// <param name="bitsB">The most significant bits of Tag B.</param>
		/// <returns>true if tag A has any of the tags of tag B, or false otherwise.</returns>
		internal static bool HasAny(ulong before, ulong bitsA, ulong bitsB) {
			bool has = before != 0UL || (GetLowerBits(bitsA) & GetLowerBits(bitsB)) != 0UL;
			if (!has) {
				// Has all lower tags?
				int ubA = GetUpperBits(bitsA), ubB = GetUpperBits(bitsB);
				if (ubA == 0 && ubB == 0)
					// 0 does not have any of 0
					has = false;
				else if (ubA == ubB)
					has = true;
				else {
					var inst = ExtendedTagBits.Instance;
					has = inst.GetTagBits(ubA).HasAny(inst.GetTagBits(ubB));
				}
			}
			return has;
		}

		/// <summary>
		/// Checks to see if tag bits 5/7 has extended bits set.
		/// </summary>
		/// <param name="value">The value to check.</param>
		/// <returns>true if it needs resolution in the extended tag bits, or false if it is
		/// a simple vanilla value.</returns>
		internal static bool HasUpperBits(ulong value) {
			return (value & UPPER_MASK) != 0UL;
		}

		/// <summary>
		/// Complements a set of tag bits.
		/// </summary>
		/// <param name="bits">The bits to complement.</param>
		/// <returns>The complement of those bits.</returns>
		internal static TagBits Not(TagBits bits) {
			bits.Complement();
			// Box into a type to allow the fields to be changed
			object localBits = bits;
			ulong hibits = FIFTH_BIT.Get(localBits);
			FIFTH_BIT.Set(localBits, GetLowerBits(hibits) | NotHighBits(hibits));
			return (TagBits)localBits;
		}

		private static ulong NotHighBits(ulong bits) {
			int ubA = GetUpperBits(bits);
			var inst = ExtendedTagBits.Instance;
			var notSet = new BitSet(inst.GetTagBits(ubA));
			notSet.Not(MAX_BITS);
			return ((ulong)inst.GetIDWithBits(notSet) << 32);
		}

		/// <summary>
		/// Ands two sets of tag bits and replaces Tag Bits A with the result A | B.
		/// </summary>
		/// <param name="lhs">Tag Bits A.</param>
		/// <param name="rhs">Tag Bits B.</param>
		internal static void Or(ref TagBits lhs, ref TagBits rhs) {
			TagBits bits = lhs;
			ulong hibits = FIFTH_BIT.Get(lhs);
			bits.Or(ref rhs);
			// Box into a type to allow the fields to be changed
			object localBits = bits;
			FIFTH_BIT.Set(localBits, TranspileOr(hibits, FIFTH_BIT.Get(rhs)));
			lhs = (TagBits)localBits;
		}

		/// <summary>
		/// Ands the two bit fields, respecting the upper 32 bits as an ID.
		/// </summary>
		/// <param name="bitsA">The most significant bits of Tag A.</param>
		/// <param name="bitsB">The most significant bits of Tag B.</param>
		/// <returns>The most significant bits of Tag A AND Tag B.</returns>
		internal static ulong TranspileAnd(ulong bitsA, ulong bitsB) {
			ulong result = GetLowerBits(bitsA) & GetLowerBits(bitsB);
			int ubA = GetUpperBits(bitsA), ubB = GetUpperBits(bitsB), ubResult;
			if (ubA == 0 || ubB == 0)
				ubResult = 0;
			else {
				// Both nonzero
				var inst = ExtendedTagBits.Instance;
				var bitSetA = new BitSet(inst.GetTagBits(ubA));
				bitSetA.And(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
			}
			return result | ((ulong)ubResult << 32);
		}

		/// <summary>
		/// Nots the bit field, respecting the upper 32 bits as an ID.
		/// </summary>
		/// <param name="bits">The most significant bits of the tag.</param>
		/// <returns>The most significant bits inverted.</returns>
		internal static ulong TranspileNot(ulong bits) {
			return GetLowerBits(~bits) | NotHighBits(bits);
		}

		/// <summary>
		/// Ors the two bit fields, respecting the upper 32 bits as an ID.
		/// </summary>
		/// <param name="bitsA">The most significant bits of Tag A.</param>
		/// <param name="bitsB">The most significant bits of Tag B.</param>
		/// <returns>The most significant bits of Tag A OR Tag B.</returns>
		internal static ulong TranspileOr(ulong bitsA, ulong bitsB) {
			ulong result = GetLowerBits(bitsA) | GetLowerBits(bitsB);
			int ubA = GetUpperBits(bitsA), ubB = GetUpperBits(bitsB), ubResult;
			if (ubA == 0)
				ubResult = ubB;
			else if (ubB == 0)
				ubResult = ubA;
			else {
				// Both nonzero
				var inst = ExtendedTagBits.Instance;
				var bitSetA = new BitSet(inst.GetTagBits(ubA));
				bitSetA.Or(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
			}
			return result | ((ulong)ubResult << 32);
		}

		/// <summary>
		/// Xors the two bit fields, respecting the upper 32 bits as an ID.
		/// </summary>
		/// <param name="bitsA">The most significant bits of Tag A.</param>
		/// <param name="bitsB">The most significant bits of Tag B.</param>
		/// <returns>The most significant bits of Tag A XOR Tag B.</returns>
		internal static ulong TranspileXor(ulong bitsA, ulong bitsB) {
			ulong result = GetLowerBits(bitsA) ^ GetLowerBits(bitsB);
			int ubA = GetUpperBits(bitsA), ubB = GetUpperBits(bitsB), ubResult;
			if (ubA == 0)
				ubResult = ubB;
			else if (ubB == 0)
				ubResult = ubA;
			else {
				// Both nonzero
				var inst = ExtendedTagBits.Instance;
				var bitSetA = new BitSet(inst.GetTagBits(ubA));
				bitSetA.Xor(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
			}
			return result | ((ulong)ubResult << 32);
		}
	}
}
