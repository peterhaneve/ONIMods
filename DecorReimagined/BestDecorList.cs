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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Lists all the items of one type that are providing decor, and gives the user the
	/// benefit of the doubt by selecting the one with the highest decor.
	/// </summary>
	internal sealed class BestDecorList : IDisposable {
		/// <summary>
		/// The best decor score of this list.
		/// </summary>
		public float BestDecor {
			get {
				return best.Decor;
			}
		}

		/// <summary>
		/// The decor provider which provided this best score.
		/// </summary>
		public DecorProvider BestProvider {
			get {
				return best.Provider;
			}
		}

		/// <summary>
		/// The number of decor items tracked.
		/// </summary>
		public int Count {
			get {
				return decorByValue.Count;
			}
		}

		/// <summary>
		/// The best decor tracked in this list.
		/// </summary>
		private DecorWrapper best;

		/// <summary>
		/// The cell for this list.
		/// </summary>
		private readonly int cell;

		/// <summary>
		/// Sorts the decor by value. SortedList works better in most cases, but the really
		/// pathological setups with tons of debris etc cause that one to degrade to
		/// unacceptable performance.
		/// </summary>
		private readonly SortedDictionary<DecorWrapper, bool> decorByValue;

		internal BestDecorList(int cell) {
			this.cell = cell;
			decorByValue = new SortedDictionary<DecorWrapper, bool>();
		}

		/// <summary>
		/// Adds a decor item.
		/// </summary>
		/// <param name="decor">The current decor of the item.</param>
		/// <param name="provider">The decor item to add.</param>
		/// <returns>true if the decor score changed, or false otherwise.</returns>
		public bool AddDecorItem(float decor, DecorProvider provider) {
			decorByValue[new DecorWrapper(decor, provider)] = true;
			return UpdateBestDecor();
		}

		public void Dispose() {
			var decor = Grid.Decor;
			if (decor != null)
				decor[cell] -= BestDecor;
			decorByValue.Clear();
		}

		/// <summary>
		/// Removes a decor item.
		/// </summary>
		/// <param name="decor">The current decor of the item.</param>
		/// <param name="provider">The decor item to remove.</param>
		/// <returns>true if the decor score changed, or false otherwise.</returns>
		public bool RemoveDecorItem(float decor, DecorProvider provider) {
			return decorByValue.Remove(new DecorWrapper(decor, provider)) && UpdateBestDecor();
		}

		public override string ToString() {
			return "BestDecorList[n={0:D},best={1:F0}]".F(Count, BestDecor);
		}

		/// <summary>
		/// Updates the best decor score.
		/// </summary>
		/// <returns>true if the score changed, or false otherwise.</returns>
		private bool UpdateBestDecor() {
			float decor = 0.0f, oldDecor = BestDecor;
			best = new DecorWrapper(0.0f, null);
			using (var it = decorByValue.GetEnumerator()) {
				if (it.MoveNext()) {
					best = it.Current.Key;
					decor = best.Decor;
				}
			}
			// Update grid if changed
			bool changed = oldDecor != decor;
			if (changed)
				Grid.Decor[cell] = Grid.Decor[cell] - oldDecor + decor;
			return changed;
		}

		/// <summary>
		/// The key for the decor list - compares on full equality but sorts on decor value.
		/// </summary>
		private struct DecorWrapper : IComparable<DecorWrapper> {
			/// <summary>
			/// The decor value of this object.
			/// </summary>
			public float Decor { get; }

			/// <summary>
			/// The provider of the decor.
			/// </summary>
			public DecorProvider Provider { get; }

			internal DecorWrapper(float decor, DecorProvider provider) {
				Decor = decor;
				Provider = provider;
			}

			public int CompareTo(DecorWrapper other) {
				int result;
				float oDecor = other.Decor, decor = Decor;
				if (oDecor > decor)
					result = 1;
				else if (oDecor < decor)
					result = -1;
				else
					// Break the tie somehow
					result = Provider.GetHashCode().CompareTo(other.Provider.GetHashCode());
				return result;
			}

			public override bool Equals(object obj) {
				return obj is DecorWrapper other && other.Provider == Provider;
			}

			public override int GetHashCode() {
				return Decor.GetHashCode();
			}

			public override string ToString() {
				return "{0} = {1:F0}".F(Provider?.name ?? "?", Decor);
			}
		}
	}
}
