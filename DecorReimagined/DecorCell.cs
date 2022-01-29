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

using System;
using System.Collections.Generic;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Stores decor providers which affect a given cell.
	/// </summary>
	internal sealed class DecorCell : IDisposable {
		/// <summary>
		/// The number of different decor provider tags affecting this cell.
		/// </summary>
		public int Count {
			get {
				return decorProviders.Count + (negativeDecor == null ? 0 : negativeDecor.
					Count);
			}
		}

		/// <summary>
		/// The cell this decor provider is tracking.
		/// </summary>
		private readonly int cell;

		/// <summary>
		/// The decor providers for buildings applied to this cell.
		/// </summary>
		private readonly IDictionary<Tag, BestDecorList> decorProviders;

		/// <summary>
		/// In hard mode, holds the negative decor items.
		/// </summary>
		private readonly IDictionary<DecorProvider, float> negativeDecor;

		internal DecorCell(int cell) {
			this.cell = cell;
			decorProviders = new Dictionary<Tag, BestDecorList>(24);
			if (DecorReimaginedPatches.Options?.HardMode ?? false)
				negativeDecor = new Dictionary<DecorProvider, float>(24);
			else
				negativeDecor = null;
		}

		/// <summary>
		/// Adds a decor provider to the list.
		/// </summary>
		/// <param name="prefabID">The prefab ID of the provider.</param>
		/// <param name="provider">The provider to add.</param>
		/// <param name="decor">The provider's current decor score.</param>
		/// <returns>true if the decor score was changed, or false otherwise.</returns>
		public bool AddDecorProvider(Tag prefabID, DecorProvider provider, float decor) {
			BestDecorList values = null;
			bool add = false;
			if (provider == null)
				throw new ArgumentNullException("provider");
			lock (decorProviders) {
				if (decor < 0.0f && negativeDecor != null) {
					// Hard mode mwahaha
					if (!negativeDecor.ContainsKey(provider)) {
						negativeDecor.Add(provider, decor);
						Grid.Decor[cell] += decor;
						add = true;
					}
				} else if (!decorProviders.TryGetValue(prefabID, out values)) {
					decorProviders.Add(prefabID, values = new BestDecorList(cell));
					UpdateNumPositive();
				}
			}
			if (values != null)
				lock (values) {
					add = values.AddDecorItem(decor, provider);
				}
			return add;
		}

		public void Dispose() {
			lock (decorProviders) {
				if (negativeDecor != null) {
					var gd = Grid.Decor;
					// Hard mode cleanup
					if (gd != null)
						foreach (var pair in negativeDecor)
							gd[cell] -= pair.Value;
					negativeDecor.Clear();
				}
				foreach (var pair in decorProviders)
					pair.Value.Dispose();
				decorProviders.Clear();
			}
		}

		/// <summary>
		/// Retrieves the decor score provided by the provider.
		/// </summary>
		/// <param name="provider">The decor provider to check.</param>
		/// <returns>The score provided by that provider, or 0 if it does not provide decor there.</returns>
		public float GetDecorProvidedBy(DecorProvider provider) {
			BestDecorList values = null;
			float decor = 0.0f;
			if (provider == null)
				throw new ArgumentNullException("provider");
			lock (decorProviders) {
				if (negativeDecor == null || !negativeDecor.TryGetValue(provider, out decor))
					decorProviders.TryGetValue(provider.PrefabID(), out values);
			}
			if (values != null && provider == values.BestProvider)
				decor = values.BestDecor;
			return decor;
		}

		/// <summary>
		/// Removes a decor provider from the list.
		/// </summary>
		/// <param name="prefabID">The prefab ID of the provider.</param>
		/// <param name="provider">The provider to remove.</param>
		/// <param name="decor">The provider's current decor score.</param>
		/// <returns>true if the decor score was changed, or false otherwise.</returns>
		public bool RemoveDecorProvider(Tag prefabID, DecorProvider provider, float decor) {
			BestDecorList values = null;
			bool removed = false;
			if (provider == null)
				throw new ArgumentNullException("provider");
			lock (decorProviders) {
				if (decor < 0.0f && negativeDecor != null) {
					// Remove in hard mode, single = is intentional
					if (removed = negativeDecor.Remove(provider))
						Grid.Decor[cell] -= decor;
				} else
					decorProviders.TryGetValue(prefabID, out values);
			}
			if (values != null) {
				int count;
				// Lock the right things at the right times
				lock (values) {
					removed = values.RemoveDecorItem(decor, provider);
					count = values.Count;
				}
				if (count < 1)
					lock (decorProviders) {
						decorProviders.Remove(prefabID);
					}
			}
			return removed;
		}

		/// <summary>
		/// Counts the total number of unique positive decor items (as currently calculated).
		/// </summary>
		private void UpdateNumPositive() {
			int positive = 0;
			foreach (var pair in decorProviders)
				if (pair.Value.BestDecor > 0.0f)
					positive++;
			DecorCellManager.Instance?.UpdateBestPositiveDecor(positive);
		}
	}
}
