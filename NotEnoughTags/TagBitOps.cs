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

using System.Collections.Concurrent;

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

		/// <summary>
		/// Ands two sets of tag bits and replaces Tag Bits A with the result A & B.
		/// </summary>
		/// <param name="lhs">Tag Bits A.</param>
		/// <param name="rhs">Tag Bits B.</param>
		internal static void And(ref TagBits lhs, ref TagBits rhs) {
			ulong hibits = lhs.bits8;
			lhs.And(ref rhs);
			lhs.bits8 = TranspileAnd(hibits, rhs.bits8);
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
			ulong hibits = bits.bits8;
			bits.Complement();
			bits.bits8 = GetLowerBits(bits.bits8) | NotHighBits(hibits);
			return bits;
		}

		private static ulong NotHighBits(ulong bits) {
			int ubA = GetUpperBits(bits);
			var inst = ExtendedTagBits.Instance;
			var notSet = BitSetPool.Allocate();
			notSet.SetTo(inst.GetTagBits(ubA));
			notSet.Not(MAX_BITS);
			int id = inst.GetIDWithBits(notSet);
			BitSetPool.Recycle(notSet);
			return (ulong)id << 32;
		}

		/// <summary>
		/// Ands two sets of tag bits and replaces Tag Bits A with the result A | B.
		/// </summary>
		/// <param name="lhs">Tag Bits A.</param>
		/// <param name="rhs">Tag Bits B.</param>
		internal static void Or(ref TagBits lhs, ref TagBits rhs) {
			ulong hibits = lhs.bits8;
			lhs.Or(ref rhs);
			// Box into a type to allow the fields to be changed
			lhs.bits8 = TranspileOr(hibits, rhs.bits8);
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
				var bitSetA = BitSetPool.Allocate();
				bitSetA.SetTo(inst.GetTagBits(ubA));
				bitSetA.And(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
				BitSetPool.Recycle(bitSetA);
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
				var bitSetA = BitSetPool.Allocate();
				bitSetA.SetTo(inst.GetTagBits(ubA));
				bitSetA.Or(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
				BitSetPool.Recycle(bitSetA);
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
				var bitSetA = BitSetPool.Allocate();
				bitSetA.SetTo(inst.GetTagBits(ubA));
				bitSetA.Xor(inst.GetTagBits(ubB));
				ubResult = inst.GetIDWithBits(bitSetA);
				BitSetPool.Recycle(bitSetA);
			}
			return result | ((ulong)ubResult << 32);
		}
	}

	/// <summary>
	/// A threadsafe pool that stores bit sets to avoid allocating memory.
	/// </summary>
	public static class BitSetPool {
		/// <summary>
		/// The initial size of pooled bit sets from this class.
		/// </summary>
		public const int PRESIZE = 512;

		/// <summary>
		/// Stores pooled instances of BitSet.
		/// </summary>
		private static readonly ConcurrentStack<BitSet> POOL = new ConcurrentStack<BitSet>();

		/// <summary>
		/// Retrieves a bit set from the pool, or allocates a new one if none are available.
		/// </summary>
		/// <returns>The pooled bit set.</returns>
		public static BitSet Allocate() {
			if (!POOL.TryPop(out BitSet set))
				set = new BitSet(PRESIZE);
			return set;
		}

		/// <summary>
		/// Returns a bit set to the pool.
		/// </summary>
		/// <param name="set">The bit set to clear and recycle.</param>
		public static void Recycle(BitSet set) {
			set.Clear();
			POOL.Push(set);
		}
	}
}
