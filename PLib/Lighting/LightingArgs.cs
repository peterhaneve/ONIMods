/*
 * Copyright 2020 Peter Han
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

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Arguments which are passed to lighting callbacks to perform lighting calculations.
	/// 
	/// The range is the light radius supplied during the Light2D creation; do not light up
	/// tiles outside of this radius (measured by taxicab distance to SourceCell)!
	/// 
	/// The source cell is the cell nearest to where the Light2D is currently located.
	/// 
	/// Use the IDictionary interface to store the relative brightness of cells by their cell
	/// location. These values should be between 0 and 1 normally, with the maximum brightness
	/// being set by the intensity parameter of the Light2D.
	/// </summary>
	public sealed class LightingArgs : EventArgs, IDictionary<int, float> {
		/// <summary>
		/// The location where lighting results are stored.
		/// </summary>
		public IDictionary<int, float> Brightness { get; }
		
		/// <summary>
		/// The maximum range to use for cell lighting. Do not light up cells beyond this
		/// range from SourceCell.
		/// </summary>
		public int Range { get; }

		/// <summary>
		/// The originating cell. Actual lighting can begin elsewhere, but the range limit is
		/// measured from this cell.
		/// </summary>
		public int SourceCell { get; }

		internal LightingArgs(int sourceCell, int range, IDictionary<int, float> output) {
			Brightness = output ?? throw new ArgumentNullException("output");
			Range = range;
			SourceCell = sourceCell;
		}

		#region IDictionary

		public ICollection<int> Keys => Brightness.Keys;

		public ICollection<float> Values => Brightness.Values;

		public int Count => Brightness.Count;

		public bool IsReadOnly => Brightness.IsReadOnly;

		public float this[int key] { get => Brightness[key]; set => Brightness[key] = value; }

		public bool ContainsKey(int key) {
			return Brightness.ContainsKey(key);
		}

		public void Add(int key, float value) {
			Brightness.Add(key, value);
		}

		public bool Remove(int key) {
			return Brightness.Remove(key);
		}

		public bool TryGetValue(int key, out float value) {
			return Brightness.TryGetValue(key, out value);
		}

		public void Add(KeyValuePair<int, float> item) {
			Brightness.Add(item);
		}

		public void Clear() {
			Brightness.Clear();
		}

		public bool Contains(KeyValuePair<int, float> item) {
			return Brightness.Contains(item);
		}

		public void CopyTo(KeyValuePair<int, float>[] array, int arrayIndex) {
			Brightness.CopyTo(array, arrayIndex);
		}

		public bool Remove(KeyValuePair<int, float> item) {
			return Brightness.Remove(item);
		}

		public IEnumerator<KeyValuePair<int, float>> GetEnumerator() {
			return Brightness.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return Brightness.GetEnumerator();
		}
		#endregion

		public override string ToString() {
			return "LightingArgs[source={0:D},range={1:D}]".F(SourceCell, Range);
		}
	}
}
