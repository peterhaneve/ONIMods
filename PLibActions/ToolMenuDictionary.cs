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
 */

using System;
using System.Collections;
using System.Collections.Generic;

using TS = ToolParameterMenu.ToggleState;

namespace PeterHan.PLib.Actions {
	/// <summary>
	/// Wraps the new Klei ToggleData[] class in a dictionary-like wrapper for
	/// compatibility.
	/// </summary>
	internal sealed class ToolMenuDictionary : IReadOnlyDictionary<string, TS>,
			IDictionary<string, TS>, IEnumerable {
		public TS this[string key] {
			get {
				// This correctly will throw KeyNotFoundException when needed
				return backing[key].State;
			}
			set {
				throw new NotImplementedException("Collection is read-only");
			}
		}

		public ICollection<string> Keys => backing.Keys;
		
		IEnumerable<string> IReadOnlyDictionary<string, TS>.Keys => backing.Keys;

		public ICollection<TS> Values => new ValueCollection(backing);
		
		IEnumerable<TS> IReadOnlyDictionary<string, TS>.Values => new ValueCollection(backing);

		public int Count => backing.Count;

		public bool IsReadOnly => true;

		/// <summary>
		/// Stores the tool toggle data by name.
		/// </summary>
		private readonly IDictionary<string, PToggleData> backing;

		internal ToolMenuDictionary(PToggleDataCollection data) {
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			int n = data.ToolCount;
			backing = new Dictionary<string, PToggleData>(n);
			for (int i = 0; i < n; i++) {
				var value = data[i];
				backing[value.Name] = value;
			}
		}

		public void Add(string key, TS value) {
			throw new NotImplementedException("Collection is read-only");
		}

		public void Add(KeyValuePair<string, TS> item) {
			throw new NotImplementedException("Collection is read-only");
		}

		public void Clear() {
			throw new NotImplementedException("Collection is read-only");
		}

		public bool Contains(KeyValuePair<string, TS> item) {
			// The key must exist first, then check the value
			var key = item.Key;
			return key != null && backing.TryGetValue(key, out var td) && td.State ==
				item.Value;
		}

		public bool ContainsKey(string key) {
			return backing.ContainsKey(key);
		}

		public void CopyTo(KeyValuePair<string, TS>[] array,
				int arrayIndex) {
			if (array == null)
				throw new ArgumentNullException(nameof(array));
			if (arrayIndex < 0)
				throw new ArgumentOutOfRangeException(nameof(arrayIndex),
					"Array index is negative");
			int length = backing.Count;
			if (arrayIndex + length > array.Length)
				// Yes the parameters are in the other order, thanks C#
				throw new ArgumentException("Array is too short", nameof(array));
			foreach (var pair in backing)
				array[arrayIndex++] = new KeyValuePair<string, TS>(pair.Key, pair.Value.State);
		}

		public IEnumerator<KeyValuePair<string, TS>> GetEnumerator() {
			return new ToolEnumerator(backing);
		}

		public bool Remove(string key) {
			throw new NotImplementedException("Collection is read-only");
		}

		public bool Remove(KeyValuePair<string, TS> item) {
			throw new NotImplementedException("Collection is read-only");
		}

		public bool TryGetValue(string key, out TS value) {
			bool result = backing.TryGetValue(key, out var td);
			value = result ? td.State : TS.On;
			return result;
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		/// <summary>
		/// Wraps the values in a read-only value collection.
		/// </summary>
		private sealed class ValueCollection : IReadOnlyCollection<TS>, ICollection<TS> {
			public int Count => throw new NotImplementedException();

			public bool IsReadOnly => throw new NotImplementedException();

			/// <summary>
			/// Stores the tool toggle data by name.
			/// </summary>
			private readonly IDictionary<string, PToggleData> backing;

			internal ValueCollection(IDictionary<string, PToggleData> backing) {
				this.backing = backing ?? throw new ArgumentNullException(nameof(backing));
			}

			public void Add(TS item) {
				throw new NotImplementedException("Collection is read-only");
			}

			public void Clear() {
				throw new NotImplementedException("Collection is read-only");
			}

			public bool Contains(TS item) {
				bool result = false;
				foreach (var pair in backing)
					if (item == pair.Value.State) {
						result = true;
						break;
					}
				return result;
			}

			public void CopyTo(TS[] array, int arrayIndex) {
				if (array == null)
				throw new ArgumentNullException(nameof(array));
				if (arrayIndex < 0)
					throw new ArgumentOutOfRangeException(nameof(arrayIndex),
						"Array index is negative");
				int length = backing.Count;
				if (arrayIndex + length > array.Length)
					// Yes the parameters are in the other order, thanks C#
					throw new ArgumentException("Array is too short", nameof(array));
				foreach (var pair in backing)
					array[arrayIndex++] = pair.Value.State;
			}

			public IEnumerator<TS> GetEnumerator() {
				return new ToolStateEnumerator(backing);
			}

			public bool Remove(TS item) {
				throw new NotImplementedException("Collection is read-only");
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}
		}
		
		/// <summary>
		/// Enumerates the collection by tool name.
		/// </summary>
		private sealed class ToolEnumerator : IEnumerator<KeyValuePair<string, TS>> {
			public KeyValuePair<string, TS> Current {
				get {
					if (disposed)
						throw new ObjectDisposedException("backing");
					var result = backing.Current;
					return new KeyValuePair<string, TS>(result.Key, result.Value.State);
				}
			}

			object IEnumerator.Current => Current;
			
			/// <summary>
			/// Stores the tool toggle data by name.
			/// </summary>
			private readonly IEnumerator<KeyValuePair<string, PToggleData>> backing;

			private bool disposed;

			internal ToolEnumerator(IDictionary<string, PToggleData> dict) {
				backing = dict.GetEnumerator();
				disposed = false;
			}

			public void Dispose() {
				if (!disposed) {
					backing.Dispose();
					disposed = true;
				}
			}

			public bool MoveNext() {
				if (disposed)
					throw new ObjectDisposedException("backing");
				return backing.MoveNext();
			}

			public void Reset() {
				if (disposed)
					throw new ObjectDisposedException("backing");
				backing.Reset();
			}
		}
		
		/// <summary>
		/// Enumerates the collection by tool value. Not really useful but satisfies the
		/// contract.
		/// </summary>
		private sealed class ToolStateEnumerator : IEnumerator<TS> {
			public TS Current {
				get {
					if (disposed)
						throw new ObjectDisposedException("backing");
					var result = backing.Current;
					return result.Value.State;
				}
			}

			object IEnumerator.Current => Current;
			
			/// <summary>
			/// Stores the tool toggle data by name.
			/// </summary>
			private readonly IEnumerator<KeyValuePair<string, PToggleData>> backing;

			private bool disposed;

			internal ToolStateEnumerator(IDictionary<string, PToggleData> dict) {
				backing = dict.GetEnumerator();
				disposed = false;
			}

			public void Dispose() {
				if (!disposed) {
					backing.Dispose();
					disposed = true;
				}
			}

			public bool MoveNext() {
				if (disposed)
					throw new ObjectDisposedException("backing");
				return backing.MoveNext();
			}

			public void Reset() {
				if (disposed)
					throw new ObjectDisposedException("backing");
				backing.Reset();
			}
		}
	}
}
