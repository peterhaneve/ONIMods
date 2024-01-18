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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PeterHan.NotEnoughTags {
	/// <summary>
	/// Manages the extended tag bits (uppermost 32 bits) of TagBits to allow storing a much
	/// larger number of different tags.
	/// </summary>
	public sealed class ExtendedTagBits {
		/// <summary>
		/// An empty bit set.
		/// </summary>
		private static readonly BitSet EMPTY = new BitSet();

		/// <summary>
		/// The only instance of this class.
		/// </summary>
		public static ExtendedTagBits Instance { get; } = new ExtendedTagBits();

		/// <summary>
		/// The number of reserved indexes for single tags.
		/// </summary>
		public const int INITIAL_TAG_BITS = 1024;

		/// <summary>
		/// The number of indexes which can be efficiently still used by the base game.
		/// Calculated as [number of tag fields]*64 - 32 = 288.
		/// </summary>
		public const int VANILLA_LIMIT = 288;

		/// <summary>
		/// Maps indexes to their tag.
		/// </summary>
		private readonly List<Tag> inverseTagTable;

		/// <summary>
		/// Map tag bits back to values.
		/// </summary>
		private readonly IDictionary<BitSet, int> inverseTagBits;

		/// <summary>
		/// The more flexible way to store tagbits!
		/// </summary>
		private readonly IDictionary<int, BitSet> tagBits;

		/// <summary>
		/// A counter for tag IDs.
		/// </summary>
		private int tagID;

		/// <summary>
		/// Maps tags to their index.
		/// </summary>
		private readonly IDictionary<Tag, int> tagTable;

		private ExtendedTagBits() {
			// This need not be thread safe, because TagBits itself is not thread safe
			inverseTagBits = new Dictionary<BitSet, int>(INITIAL_TAG_BITS * 4);
			tagBits = new Dictionary<int, BitSet>(INITIAL_TAG_BITS * 4);
			tagID = INITIAL_TAG_BITS;

			// Fetch these through reflection
			inverseTagTable = TagBits.inverseTagTable;
			tagTable = TagBits.tagTable;
			if (inverseTagTable == null || tagTable == null)
				throw new InvalidOperationException("Tag tables are not initialized!");
		}

		/// <summary>
		/// Gets the ID to use for tag bits that have the specified ID cleared.
		/// </summary>
		/// <param name="id">The existing ID.</param>
		/// <param name="extIndex">The tag index to clear.</param>
		/// <returns>A new or reused ID with that tag bit clear.</returns>
		public int GetIDWithTagClear(int id, int extIndex) {
			if (id < INITIAL_TAG_BITS)
				// Cleared a tag bit in the initial 1024
				id = (id == extIndex + 1) ? 0 : id;
			else {
				var bits = BitSetPool.Allocate();
				bits.SetTo(GetTagBits(id));
				bits.Set(extIndex, false);
				id = GetIDWithBits(bits);
				BitSetPool.Recycle(bits);
			}
			return id;
		}

		/// <summary>
		/// Gets the ID to use for tag bits that have the specified ID set.
		/// </summary>
		/// <param name="id">The existing ID.</param>
		/// <param name="extIndex">The tag index to set.</param>
		/// <returns>A new or reused ID with that tag bit set.</returns>
		public int GetIDWithTagSet(int id, int extIndex) {
			if ((extIndex >= INITIAL_TAG_BITS || id != 0) && id != extIndex + 1) {
				var bits = BitSetPool.Allocate();
				bits.SetTo(GetTagBits(id));
				bits.Set(extIndex, true);
				id = GetIDWithBits(bits);
				BitSetPool.Recycle(bits);
			} else
				// All bits are clear, use the optimized route to avoid allocating
				id = extIndex + 1;
			return id;
		}

		/// <summary>
		/// Gets the tag ID with the specified bits set.
		/// </summary>
		/// <param name="bits">The bits to be set.</param>
		/// <returns>The tag ID with those bits set (allocating if needed)</returns>
		public int GetIDWithBits(BitSet bits) {
			if (!inverseTagBits.TryGetValue(bits, out int id)) {
				id = NextID();
				inverseTagBits[bits] = id;
				tagBits[id] = bits;
			}
			return id;
		}

		/// <summary>
		/// Gets the actual bits set in an extended tag index.
		/// 
		/// Do not modify the return value!
		/// </summary>
		/// <param name="id">The ID, with 0 being no bits set and so forth.</param>
		/// <returns>The bits set at that index.</returns>
		public BitSet GetTagBits(int id) {
			if (id <= 0 || !tagBits.TryGetValue(id, out BitSet bits))
				bits = EMPTY;
			return bits;
		}

		/// <summary>
		/// Gets the tag which matches the specified tag bit index.
		/// </summary>
		/// <param name="index">The index of the tag. Works for both stock and extended tags.</param>
		/// <returns>The tag for that index.</returns>
		public Tag GetTagForIndex(int index) {
			return (index >= inverseTagTable.Count) ? Tag.Invalid : inverseTagTable[index];
		}

		/// <summary>
		/// Gets the index for a tag, adding it to the table if needed.
		/// </summary>
		/// <param name="tag">The tag to check.</param>
		/// <returns>The index of that tag. Allocates a new one if needed.</returns>
		internal int ManifestFlagIndex(Tag tag) {
			int invCount;
			if (!tagTable.TryGetValue(tag, out int count)) {
				count = tagTable.Count;
				tagTable.Add(tag, count);
				inverseTagTable.Add(tag);
				invCount = inverseTagTable.Count;
				if (invCount != count + 1)
					// Duplicate tags maybe?
					PUtil.LogError("Tag tables are out of sync: Expected {0:D}, got {1:D}".F(
						count + 1, invCount));
				int extIndex = count - VANILLA_LIMIT;
				if (extIndex >= 0) {
					// If in the initial tagbits
					var identity = new BitSet(++extIndex);
					identity.Set(extIndex - 1, true);
					inverseTagBits.Add(identity, extIndex);
					tagBits.Add(extIndex, identity);
					if (extIndex >= INITIAL_TAG_BITS) {
						// Wow we are really over the limit
						var text = new StringBuilder("No more tags! Performance may be poor:",
							128);
						int printed = 0;
						text.AppendLine();
						foreach (var pair in tagTable) {
							text.Append(pair.Key.ToString());
							text.Append(", ");
							if (++printed % 64 == 0)
								text.AppendLine();
						}
						PUtil.LogWarning(text.ToString());
					}
				}
#if DEBUG
				PUtil.LogDebug("Assigned tag {0} the index {1:D}".F(tag, count));
#endif
			}
			return count;
		}

		/// <summary>
		/// Gets the next available unique index for multiple cardinality tag bits.
		/// </summary>
		/// <returns>The next available tag index.</returns>
		internal int NextID() {
			// Returns the incremented value (old value +1)
			return Interlocked.Increment(ref tagID);
		}
	}
}
