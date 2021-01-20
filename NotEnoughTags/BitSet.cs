/*
 * Copyright 2021 Peter Han
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

using System;
using System.Text;

namespace PeterHan.NotEnoughTags {
	/// <summary>
	/// A version of System.Collections.BitArray with a GetHashCode that works.
	/// </summary>
	public sealed class BitSet {
		/// <summary>
		/// The number of bits in a ulong.
		/// </summary>
		public const int ULONG_BITS = 64;

		public static bool operator ==(BitSet a, BitSet b) {
			return (a is null && b is null) || (!(a is null) && a.Equals(b));
		}

		public static bool operator !=(BitSet a, BitSet b) {
			return (!(a is null) || !(b is null)) && (a is null || !a.Equals(b));
		}

		/// <summary>
		/// The number of bit locations allocated in this bit set. The bit set automatically
		/// expands if locations past Capacity - 1 are set. This does not guarantee that any
		/// bits near that index are actually set.
		/// </summary>
		public int Capacity { get; private set; }

		/// <summary>
		/// The raw bits backing this bit set.
		/// </summary>
		private ulong[] bits;

		public BitSet() : this(ULONG_BITS) { }

		public BitSet(int capacity) {
			if (capacity < 1)
				throw new ArgumentOutOfRangeException("capacity");
			capacity = Math.Max(ULONG_BITS, capacity);
			bits = new ulong[(capacity + ULONG_BITS - 1) >> 6];
			Capacity = capacity;
		}

		public BitSet(BitSet other) {
			if (other == null)
				throw new ArgumentNullException("other");
			Capacity = other.Capacity;
			int n = other.bits.Length;
			bits = new ulong[n];
			Array.Copy(other.bits, bits, n);
		}

		/// <summary>
		/// Sets all bits which are set in this BitSet and the other BitSet.
		/// </summary>
		/// <param name="other">The bit set to and.</param>
		public void And(BitSet other) {
			if (other == null)
				throw new ArgumentNullException("other");
			ulong[] oBits = other.bits;
			int n = Math.Min(bits.Length, oBits.Length);
			for (int i = 0; i < n; i++)
				bits[i] &= oBits[i];
		}

		/// <summary>
		/// Sets all bits in this bit set to 0.
		/// </summary>
		public void Clear() {
			int n = bits.Length;
			for (int i = 0; i < n; i++)
				bits[i] = 0UL;
		}

		/// <summary>
		/// Ensures that there is space for the specified number of bits.
		/// </summary>
		/// <param name="capacity">The capacity to set.</param>
		private void EnsureCapacity(int capacity) {
			if (Capacity < capacity) {
				int required = (capacity + ULONG_BITS - 1) >> 6, have;
				if ((have = bits.Length) < required) {
					// Embiggen the array
					var newBits = new ulong[required];
					if (bits != null)
						Array.Copy(bits, newBits, have);
					bits = newBits;
				}
				Capacity = capacity;
			}
		}

		public override bool Equals(object obj) {
			bool eql = false;
			if (obj is BitSet other) {
				ulong[] oBits = other.bits;
				int n = Math.Min(bits.Length, oBits.Length);
				eql = true;
				for (int i = 0; i < n && eql; i++)
					if (bits[i] != oBits[i])
						eql = false;
			}
			return eql;
		}

		/// <summary>
		/// Gets the bit at the specified index.
		/// </summary>
		/// <param name="index">The index to query.</param>
		/// <returns>The bit value at that index.</returns>
		public bool Get(int index) {
			if (index < 0)
				throw new ArgumentOutOfRangeException("index");
			bool value = false;
			if (index < Capacity)
				value = (bits[index >> 6] & (1UL << (index & 0x3F))) != 0UL;
			return value;
		}

		/// <summary>
		/// Checks to see if the intersection between this bit set and another is equal to that
		/// bit set (if the two sets have all bits in common).
		/// 
		/// A non-allocating method of writing new BitSet(this).And(other).Equals(other).
		/// </summary>
		/// <param name="other">The other bit set.</param>
		/// <returns>true if the intersection is equal to the other, or false otherwise.</returns>
		public bool HasAll(BitSet other) {
			ulong[] oBits = other.bits;
			int oLen = oBits.Length, n = Math.Min(bits.Length, oLen);
			for (int i = 0; i < n; i++) {
				ulong b = oBits[i];
				if ((bits[i] & b) != b)
					return false;
			}
			for (int i = n; i < oLen; i++)
				if (oBits[i] != 0UL)
					// 0 And nonzero cannot be nonzero
					return false;
			return true;
		}

		/// <summary>
		/// Checks to see if the intersection between this bit set and another has any set
		/// bits (if the two sets have any bits in common).
		/// 
		/// A non-allocating method of writing new BitSet(this).And(other).IsEmpty().
		/// </summary>
		/// <param name="other">The other bit set.</param>
		/// <returns>true if the intersection has any set bits, or false otherwise.</returns>
		public bool HasAny(BitSet other) {
			ulong[] oBits = other.bits;
			int n = Math.Min(bits.Length, oBits.Length);
			for (int i = 0; i < n; i++)
				if ((bits[i] & oBits[i]) != 0UL)
					return true;
			return false;
		}

		public override int GetHashCode() {
			int hashCode = 17;
			int n = bits.Length;
			for (int i = 0; i < n; i++) {
				ulong value = bits[i];
				hashCode = hashCode * 23 + (int)(value >> 32);
				hashCode = hashCode * 23 + (int)value;
			}
			return hashCode;
		}

		/// <summary>
		/// Checks to see if any bits are set in this bit set.
		/// </summary>
		/// <returns>true if any bit is set, or false otherwise</returns>
		public bool IsEmpty() {
			int n = bits.Length;
			for (int i = 0; i < n; i++)
				if (bits[i] != 0)
					return false;
			return true;
		}

		/// <summary>
		/// Inverts all bits in this bit set below the specified index.
		/// </summary>
		/// <param name="maxIndex">The maximum index to set, exclusive.</param>
		public void Not(int maxIndex) {
			if (maxIndex < 1)
				throw new ArgumentOutOfRangeException("maxIndex");
			EnsureCapacity(maxIndex);
			int n = maxIndex >> 6, left = maxIndex & 0x3F;
			// Flip all bits wholesale that are in indexes less than maxIndex
			for (int i = 0; i < n; i++)
				bits[i] = ~bits[i];
			if (left > 0) {
				ulong mask = 0UL;
				// Flip indexes in the last element
				for (int i = 0; i < left; i++)
					mask |= 1UL << i;
				bits[n] ^= mask;
			}
		}

		/// <summary>
		/// Sets all bits which are set in either this BitSet or the other BitSet.
		/// </summary>
		/// <param name="other">The bit set to or.</param>
		public void Or(BitSet other) {
			if (other == null)
				throw new ArgumentNullException("other");
			ulong[] oBits = other.bits;
			int n = oBits.Length;
			EnsureCapacity(other.Capacity);
			for (int i = 0; i < n; i++)
				bits[i] |= oBits[i];
		}

		/// <summary>
		/// Sets the bit at the specified index.
		/// </summary>
		/// <param name="index">The index to modify.</param>
		/// <param name="value">The new bit value to place at that index.</param>
		public void Set(int index, bool value) {
			if (index < 0)
				throw new ArgumentOutOfRangeException("index");
			EnsureCapacity(index + 1);
			int element = index >> 6, bit = index & 0x3F;
			ulong rmw = bits[element];
			if (value)
				rmw |= 1UL << bit;
			else
				rmw &= ~(1UL << bit);
			bits[element] = rmw;
		}

		/// <summary>
		/// Clears this bit set and then sets its bits to exactly match the set bits in the
		/// specified bit set.
		/// </summary>
		/// <param name="other">The bit set to copy.</param>
		public void SetTo(BitSet other) {
			if (other == null)
				throw new ArgumentNullException("other");
			ulong[] oBits = other.bits;
			int n = oBits.Length;
			EnsureCapacity(other.Capacity);
			Array.Copy(oBits, bits, n);
		}

		public override string ToString() {
			var result = new StringBuilder("BitSet[");
			bool first = true;
			// List out set indices
			for (int i = 0; i < Capacity; i++)
				if (Get(i)) {
					if (!first)
						result.Append(", ");
					result.Append(i);
					first = false;
				}
			result.Append("]");
			return result.ToString();
		}

		/// <summary>
		/// Sets all bits which are set in either this BitSet or the other BitSet but not both.
		/// </summary>
		/// <param name="other">The bit set to xor.</param>
		public void Xor(BitSet other) {
			if (other == null)
				throw new ArgumentNullException("other");
			ulong[] oBits = other.bits;
			int n = oBits.Length;
			EnsureCapacity(other.Capacity);
			for (int i = 0; i < n; i++)
				bits[i] ^= oBits[i];
		}
	}
}
