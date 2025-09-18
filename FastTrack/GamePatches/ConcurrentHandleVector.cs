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
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A faster and thread safe version of KCompactedVector.
	/// </summary>
	public sealed class ConcurrentHandleVector<T> : ICollection<T> {
		/// <summary>
		/// Allows modification of the base collection.
		/// </summary>
		public IDictionary<int, T> BackingDictionary => lookup;

		public int Count => lookup.Count;

		public bool IsReadOnly => false;

		public T this[HandleVector<int>.Handle handle] {
			get => GetData(handle);
			set => SetData(handle, value);
		}

		/// <summary>
		/// Stores the required values by handle.
		/// </summary>
		private readonly ConcurrentDictionary<int, T> lookup;

		/// <summary>
		/// The next index to be handed out.
		/// </summary>
		private volatile int nextIndex;

		public ConcurrentHandleVector() : this(64) { }

		/// <summary>
		/// Creates a new ConcurrentHandleVector with the specified initial capacity.
		/// </summary>
		/// <param name="size">The initial capacity of the handle vector.</param>
		public ConcurrentHandleVector(int size) {
			lookup = new ConcurrentDictionary<int, T>(2, Math.Max(size, 8));
			nextIndex = 0;
		}

		/// <summary>
		/// Stores a new value into the handle vector and returns a reference to its index.
		/// </summary>
		/// <param name="value">The value to add.</param>
		/// <returns>A handle referring to the value's location.</returns>
		public HandleVector<int>.Handle Allocate(T value) {
			// Ensure that 0 is never used as an index
			int index = (Interlocked.Increment(ref nextIndex) & 0x7FFFFFFF) + 1;
			if (!lookup.TryAdd(index, value))
				throw new InvalidOperationException("Handle vector overflow! Element {1} already exists at {0:D}".
					F(index, lookup[index]));
			return new HandleVector<int>.Handle() {
				_index = index
			};
		}

		/// <summary>
		/// Adds an item to the handle vector.
		/// </summary>
		/// <param name="item">The value to add.</param>
		public void Add(T item) {
			Allocate(item);
		}

		/// <summary>
		/// Removes all data from the handle vector.
		/// </summary>
		public void Clear() {
			lookup.Clear();
		}

		/// <summary>
		/// Searches for a value in the handle vector.
		/// 
		/// Warning: Slow! Exists mostly to fulfill ICollection&lt;T&gt;!
		/// </summary>
		/// <param name="item">The item to look up.</param>
		/// <returns>true if the item exists, or false if it was not found.</returns>
		public bool Contains(T item) {
			return lookup.Values.Contains(item);
		}

		/// <summary>
		/// Copies all data in this handle vector to an array.
		/// </summary>
		/// <param name="array">The destination for the data.</param>
		/// <param name="arrayIndex">The offset within the destination where copying will begin.</param>
		public void CopyTo(T[] array, int arrayIndex) {
			lookup.Values.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes a value from the handle vector.
		/// </summary>
		/// <param name="handle">The reference handle to the value's location.</param>
		/// <returns>The invalid handle, for compatibility with KCV.</returns>
		public HandleVector<int>.Handle Free(HandleVector<int>.Handle handle) {
			int index = handle._index;
			if (index != 0 && !lookup.TryRemove(index, out _)) {
#if DEBUG
				PUtil.LogWarning("Removing handle {0:D}, but did not exist".F(index));
#endif
			}
			return HandleVector<int>.Handle.InvalidHandle;
		}

		/// <summary>
		/// Gets the value at the specified handle index.
		/// </summary>
		/// <param name="handle">The reference handle to the value's location.</param>
		/// <returns>The value, or the default value if the handle is invalid (as opposed to a crash!)</returns>
		public T GetData(HandleVector<int>.Handle handle) {
			int index = handle._index;
			if (index == 0 || !lookup.TryGetValue(index, out var data))
				data = default;
			return data;
		}

		public IEnumerator<T> GetEnumerator() {
			return lookup.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public bool Remove(T item) {
			bool removed = false;
			// This is a best effort method, it may go very poorly
			if (item == null) {
				// Look for null
				foreach (var pair in lookup)
					if (pair.Value == null && (removed = lookup.TryRemove(pair.Key, out _)))
						break;
			} else {
				var ec = EqualityComparer<T>.Default;
				foreach (var pair in lookup)
					if (ec.Equals(item, pair.Value) && (removed = lookup.TryRemove(pair.Key,
							out _)))
						break;
			}
			return removed;
		}

		/// <summary>
		/// Sets the value at the specified handle index.
		/// </summary>
		/// <param name="handle">The reference handle to the value's location.</param>
		/// <param name="value">The new value to be placed at that location.</param>
		public void SetData(HandleVector<int>.Handle handle, T value) {
			int index = handle._index;
			if (index != 0)
				lookup[index] = value;
		}

		/// <summary>
		/// Sets the value at the specified handle index only if the specified value is the
		/// current value. The check is performed atomically.
		/// </summary>
		/// <param name="handle">The reference handle to the value's location.</param>
		/// <param name="value">The new value to be placed at that location.</param>
		/// <param name="requiredValue">The value that must be already present for the change to succeed.</param>
		/// <returns>true if the value was updated, or false if the current value did not match the required value.</returns>
		public bool SetDataIf(HandleVector<int>.Handle handle, T value, T requiredValue) {
			int index = handle._index;
			return index != 0 && lookup.TryUpdate(index, value, requiredValue);
		}
	}
}
