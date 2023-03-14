/*
 * Copyright 2023 Peter Han
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
using System.Collections.Generic;

namespace PeterHan.PLib.Core {
	/// <summary>
	/// A class similar to Queue<typeparamref name="T"/> that allows efficient access to its
	/// items in ascending order.
	/// </summary>
	public class PriorityQueue<T> where T : IComparable<T> {
		/// <summary>
		/// Returns the index of the specified item's first child. Its second child index is
		/// that index plus one.
		/// </summary>
		/// <param name="index">The item index.</param>
		/// <returns>The index of its first child.</returns>
		private static int ChildIndex(int index) {
			return 2 * index + 1;
		}

		/// <summary>
		/// Returns the index of the specified item's parent.
		/// </summary>
		/// <param name="index">The item index.</param>
		/// <returns>The index of its parent.</returns>
		private static int ParentIndex(int index) {
			return (index - 1) / 2;
		}

		/// <summary>
		/// The number of elements in this queue.
		/// </summary>
		public int Count => heap.Count;

		/// <summary>
		/// The heap where the items are stored.
		/// </summary>
		private readonly IList<T> heap;

		/// <summary>
		/// Creates a new PriorityQueue&lt;<typeparamref name="T"/>&gt; with the default
		/// initial capacity.
		/// </summary>
		public PriorityQueue() : this(32) { }

		/// <summary>
		/// Creates a new PriorityQueue&lt;<typeparamref name="T"/>&gt; with the specified
		/// initial capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of this queue.</param>
		public PriorityQueue(int capacity) {
			if (capacity < 1)
				throw new ArgumentException("capacity > 0");
			heap = new List<T>(Math.Max(capacity, 8));
		}

		/// <summary>
		/// Removes all objects from this PriorityQueue&lt;<typeparamref name="T"/>&gt;.
		/// </summary>
		public void Clear() {
			heap.Clear();
		}

		/// <summary>
		/// Returns whether the specified key is present in this priority queue. This operation
		/// is fairly slow, use with caution.
		/// </summary>
		/// <param name="key">The key to check.</param>
		/// <returns>true if it exists in this priority queue, or false otherwise.</returns>
		public bool Contains(T key) {
			return heap.Contains(key);
		}

		/// <summary>
		/// Removes and returns the smallest object in the
		/// PriorityQueue&lt;<typeparamref name="T"/>&gt;.
		/// 
		/// If multiple objects are the smallest object, an unspecified one is returned.
		/// </summary>
		/// <returns>The object that is removed from this PriorityQueue.</returns>
		/// <exception cref="InvalidOperationException">If this queue is empty.</exception>
		public T Dequeue() {
			int index = 0, length = heap.Count, childIndex;
			if (length == 0)
				throw new InvalidOperationException("Queue is empty");
			T top = heap[0];
			// Put the last element as the new head
			heap[0] = heap[--length];
			heap.RemoveAt(length);
			while ((childIndex = ChildIndex(index)) < length) {
				T first = heap[index], child = heap[childIndex];
				if (childIndex < length - 1) {
					var rightChild = heap[childIndex + 1];
					// Select the smallest child
					if (child.CompareTo(rightChild) > 0) {
						childIndex++;
						child = rightChild;
					}
				}
				if (first.CompareTo(child) < 0)
					break;
				heap[childIndex] = first;
				heap[index] = child;
				index = childIndex;
			}
			return top;
		}

		/// <summary>
		/// Adds an object to the PriorityQueue&lt;<typeparamref name="T"/>&gt;.
		/// </summary>
		/// <param name="item">The object to add to this PriorityQueue.</param>
		/// <exception cref="ArgumentNullException">If item is null.</exception>
		public void Enqueue(T item) {
			if (item == null)
				throw new ArgumentNullException(nameof(item));
			int index = heap.Count;
			heap.Add(item);
			while (index > 0) {
				int parentIndex = ParentIndex(index);
				T first = heap[index], parent = heap[parentIndex];
				if (first.CompareTo(parent) > 0)
					break;
				heap[parentIndex] = first;
				heap[index] = parent;
				index = parentIndex;
			}
		}

		/// <summary>
		/// Returns the smallest object in the PriorityQueue&lt;<typeparamref name="T"/>&gt;
		/// without removing it.
		/// 
		/// If multiple objects are the smallest object, an unspecified one is returned.
		/// </summary>
		/// <returns>The smallest object in this PriorityQueue.</returns>
		/// <exception cref="InvalidOperationException">If this queue is empty.</exception>
		public T Peek() {
			if (Count == 0)
				throw new InvalidOperationException("Queue is empty");
			return heap[0];
		}

		public override string ToString() {
			return heap.ToString();
		}
	}

	/// <summary>
	/// A priority queue that includes a paired value.
	/// </summary>
	/// <typeparam name="K">The type to use for the sorting in the PriorityQueue.</typeparam>
	/// <typeparam name="V">The type to include as extra data.</typeparam>
	public sealed class PriorityDictionary<K, V> : PriorityQueue<PriorityDictionary<K, V>.
			PriorityQueuePair> where K : IComparable<K> {
		/// <summary>
		/// Creates a new PriorityDictionary&lt;<typeparamref name="K"/>,
		/// <typeparamref name="V"/>&gt; with the default initial capacity.
		/// </summary>
		public PriorityDictionary() : base() { }

		/// <summary>
		/// Creates a new PriorityDictionary&lt;<typeparamref name="K"/>,
		/// <typeparamref name="V"/>&gt; with the specified initial capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity of this dictionary.</param>
		public PriorityDictionary(int capacity) : base(capacity) { }

		/// <summary>
		/// Removes and returns the smallest object in the
		/// PriorityDictionary&lt;<typeparamref name="K"/>, <typeparamref name="V"/>&gt;.
		/// 
		/// If multiple objects are the smallest object, an unspecified one is returned.
		/// </summary>
		/// <param name="key">The key of the object removed.</param>
		/// <param name="value">The value of the object removed.</param>
		/// <exception cref="InvalidOperationException">If this dictionary is empty.</exception>
		public void Dequeue(out K key, out V value) {
			var pair = Dequeue();
			key = pair.Key;
			value = pair.Value;
		}

		/// <summary>
		/// Adds an object to the PriorityDictionary&lt;<typeparamref name="K"/>,
		/// <typeparamref name="V"/>&gt;.
		/// </summary>
		/// <param name="item">The object to add to this PriorityDictionary.</param>
		/// <exception cref="ArgumentNullException">If item is null.</exception>
		public void Enqueue(K key, V value) {
			Enqueue(new PriorityQueuePair(key, value));
		}

		/// <summary>
		/// Returns the smallest object in the PriorityDictionary&lt;<typeparamref name="K"/>,
		/// <typeparamref name="V"/>&gt; without removing it.
		/// 
		/// If multiple objects are the smallest object, an unspecified one is returned.
		/// </summary>
		/// <param name="key">The key of the smallest object.</param>
		/// <param name="value">The value of the smallest object.</param>
		/// <exception cref="InvalidOperationException">If this dictionary is empty.</exception>
		public void Peek(out K key, out V value) {
			var pair = Peek();
			key = pair.Key;
			value = pair.Value;
		}

		/// <summary>
		/// Stores a value with the key that is used for comparison.
		/// </summary>
		public sealed class PriorityQueuePair : IComparable<PriorityQueuePair> {
			/// <summary>
			/// Retrieves the key of this QueueItem.
			/// </summary>
			public K Key { get; }

			/// <summary>
			/// Retrieves the value of this QueueItem.
			/// </summary>
			public V Value { get; }

			/// <summary>
			/// Creates a new priority queue pair.
			/// </summary>
			/// <param name="key">The item key.</param>
			/// <param name="value">The item value.</param>
			public PriorityQueuePair(K key, V value) {
				if (key == null)
					throw new ArgumentNullException(nameof(key));
				Key = key;
				Value = value;
			}

			public int CompareTo(PriorityQueuePair other) {
				if (other == null)
					throw new ArgumentNullException(nameof(other));
				return Key.CompareTo(other.Key);
			}

			public override bool Equals(object obj) {
				return obj is PriorityQueuePair other && other.Key.Equals(Key);
			}

			public override int GetHashCode() {
				return Key.GetHashCode();
			}

			public override string ToString() {
				return "PriorityQueueItem[key=" + Key + ",value=" + Value + "]";
			}
		}
	}
}
