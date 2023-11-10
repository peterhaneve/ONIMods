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
using PeterHan.PLib.Core;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Manages decor applying once per type in a cell.
	/// </summary>
	public sealed class DecorCellManager : IDisposable {
		/// <summary>
		/// The current singleton instance of DecorCellManager.
		/// </summary>
		public static DecorCellManager Instance { get; private set; }

		/// <summary>
		/// Creates the cell-level decor manager.
		/// </summary>
		public static void CreateInstance() {
			Instance = new DecorCellManager();
		}

		/// <summary>
		/// Destroys the cell-level decor manager.
		/// </summary>
		public static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The critter attribute for happiness.
		/// </summary>
		public Klei.AI.Attribute HappinessAttribute { get; }

		/// <summary>
		/// True if decor behind walls is disabled by another mod.
		/// </summary>
		public bool NoDecorBehindDrywall { get; }

		/// <summary>
		/// Tracks the "And It Feels Like Home" achievement.
		/// </summary>
		public int NumPositiveDecor { get; private set; }

		/// <summary>
		/// A very fast method to check for backwalls on the entire grid.
		/// </summary>
		private readonly bool[] backwall;

		/// <summary>
		/// Stores the decor providers at a given location.
		/// </summary>
		private readonly DecorCell[] decorGrid;

		/// <summary>
		/// True if critter decor is disabled.
		/// </summary>
		private readonly bool noCritterDecor;

		/// <summary>
		/// The size at creation time. Technically it could change.
		/// </summary>
		private readonly int size;

		private DecorCellManager() {
			HappinessAttribute = Db.Get().CritterAttributes.Happiness;
			size = Grid.CellCount;
			noCritterDecor = DecorReimaginedPatches.Options.AllCrittersZeroDecor;
			NoDecorBehindDrywall = PPatchTools.GetTypeSafe(
				"BlockDecorBehindWalls.BlockDecorBehindWalls") != null;
			NumPositiveDecor = 0;
			backwall = new bool[size];
			decorGrid = new DecorCell[size];
		}

		/// <summary>
		/// Adds a decor provider to a given cell.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="provider">The object providing decor.</param>
		/// <param name="prefabID">The prefab ID of this object.</param>
		/// <param name="decor">The quantity of decor to add or subtract.</param>
		public void AddDecorProvider(int cell, DecorProvider provider, Tag prefabID,
				float decor) {
			var parent = provider.gameObject;
			bool allowForCritter = parent != null && (!noCritterDecor || !parent.
				TryGetComponent(out CreatureBrain _));
			// Must be a valid cell, and the object must be either not a critter or critter
			// decor enabled
			if (Grid.IsValidCell(cell) && cell < size && cell >= 0 && allowForCritter)
				lock (decorGrid) {
					AddOrGet(cell).AddDecorProvider(prefabID, provider, decor);
				}
		}

		/// <summary>
		/// Gets the decor at the specified cell, creating and adding if necessary.
		/// </summary>
		/// <param name="cell">The cell to retrieve.</param>
		/// <returns>The decor list at that cell.</returns>
		private DecorCell AddOrGet(int cell) {
			var dc = decorGrid[cell];
			if (dc == null)
				decorGrid[cell] = dc = new DecorCell(cell);
			return dc;
		}

		public void Dispose() {
			lock (decorGrid) {
				for (int i = 0; i < size; i++) {
					decorGrid[i]?.Dispose();
					decorGrid[i] = null;
					backwall[i] = false;
				}
			}
		}

		/// <summary>
		/// Retrieves the decor provided by the specified provider.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="provider">The provider which could be providing decor.</param>
		/// <returns>The decor provided by that provider.</returns>
		internal float GetDecorProvided(int cell, DecorProvider provider) {
			float decor = 0.0f;
			if (Grid.IsValidCell(cell) && cell < size && cell >= 0)
				lock (decorGrid) {
					var dc = decorGrid[cell];
					if (dc != null)
						decor = dc.GetDecorProvidedBy(provider);
				}
			return decor;
		}

		/// <summary>
		/// Checks very quickly if a cell has a backwall.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>true if it has backwall, or false otherwise.</returns>
		public bool HasBackwall(int cell) {
			return Grid.IsValidCell(cell) && backwall[cell];
		}

		/// <summary>
		/// Refreshes all building decor providers at the specified location. Filtered to
		/// only refresh buildings that are visually behind a drywall.
		/// </summary>
		/// <param name="cell">The location to update.</param>
		internal void RefreshAllAt(int cell) {
			if (Grid.IsValidCell(cell)) {
				int n = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.NumLayers),
					ObjectLayer.NumLayers);
				for (int i = 0; i < n; i++)
					if (i != DecorSplatNew.BackwallLayer) {
						var go = Grid.Objects[cell, i];
						if (go != null && go.TryGetComponent(out DecorSplatNew splat) &&
								splat.IsBehindBackwall)
							splat.RefreshDecor();
					}
			}
		}

		/// <summary>
		/// Removes a decor provider from a given cell.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="provider">The object providing decor.</param>
		/// <param name="prefabID">The prefab ID of this object.</param>
		/// <param name="decor">The quantity of decor to add or subtract.</param>
		public void RemoveDecorProvider(int cell, DecorProvider provider, Tag prefabID,
				float decor) {
			if (Grid.IsValidCell(cell) && cell < size && cell >= 0)
				lock (decorGrid) {
					var dc = decorGrid[cell];
					if (dc != null) {
						dc.RemoveDecorProvider(prefabID, provider, decor);
						if (dc.Count == 0) {
							dc.Dispose();
							decorGrid[cell] = null;
						}
					}
				}
		}

		/// <summary>
		/// Tracks the presence of drywall or other backwall buildings at a specified location.
		/// </summary>
		/// <param name="cell">The cell to update.</param>
		/// <param name="hasBackwall">true if it has backwall, or false otherwise.</param>
		public void SetBackwall(int cell, bool hasBackwall) {
			if (Grid.IsValidCell(cell))
				backwall[cell] = hasBackwall;
		}

		/// <summary>
		/// Updates the most number of positive decor items seen affecting one tile.
		/// </summary>
		/// <param name="quantity">The quantity affecting a given tile.</param>
		internal void UpdateBestPositiveDecor(int quantity) {
			NumPositiveDecor = Math.Max(NumPositiveDecor, quantity);
		}
	}
}
