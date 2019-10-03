/*
 * Copyright 2019 Peter Han
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

using Harmony;
using PeterHan.PLib;
using System;
using System.Collections.Generic;

namespace PeterHan.DecorRework {
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
			if (Instance != null) {
				Instance.Dispose();
			}
			Instance = null;
		}

		/// <summary>
		/// The flag to check for broken buildings.
		/// </summary>
		private readonly Operational.Flag brokenFlag;

		/// <summary>
		/// Stores the decor providers at a given location.
		/// </summary>
		private readonly DecorCell[] decorGrid;

		/// <summary>
		/// The critter attribute for happiness.
		/// </summary>
		private readonly Klei.AI.Attribute happinessAttribute;

		/// <summary>
		/// True if critter decor is disabled.
		/// </summary>
		private readonly bool noCritterDecor;

		/// <summary>
		/// Lists all decor providers and handles rebuilding their splats.
		/// </summary>
		private readonly IDictionary<DecorProvider, DecorSplatNew> provInfo;

		/// <summary>
		/// The size at creation time. Technically it could change.
		/// </summary>
		private readonly int size;

		private DecorCellManager() {
			brokenFlag = Traverse.Create(typeof(BuildingHP.States)).
				GetField<Operational.Flag>("healthyFlag");
			happinessAttribute = Db.Get().CritterAttributes.Happiness;
			size = Grid.CellCount;
			noCritterDecor = DecorReimaginedPatches.Options.AllCrittersZeroDecor;
			decorGrid = new DecorCell[size];
			provInfo = new Dictionary<DecorProvider, DecorSplatNew>(1024);
		}

		/// <summary>
		/// Adds a decor provider to a given cell.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="provider">The object providing decor.</param>
		/// <param name="decor">The quantity of decor to add or subtract.</param>
		public void AddDecorProvider(int cell, DecorProvider provider, float decor) {
			var parent = provider.gameObject;
			bool allowForCritter = (parent == null) ? false : (!noCritterDecor ||
				parent.GetComponent<CreatureBrain>() == null);
			// Must be a valid cell, and the object must be either not a critter or critter
			// decor enabled
			if (Grid.IsValidCell(cell) && cell < size && cell >= 0 && allowForCritter)
				lock (decorGrid) {
					AddOrGet(cell).AddDecorProvider(provider.PrefabID(), provider, decor);
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

		/// <summary>
		/// Destroys all references to the specified decor provider in the decor system.
		/// </summary>
		/// <param name="instance">The DecorProvider that is being destroyed.</param>
		internal void DestroyDecor(DecorProvider instance) {
			DecorSplatNew splat;
			lock (provInfo) {
				if (provInfo.TryGetValue(instance, out splat))
					provInfo.Remove(instance);
			}
			if (splat != null)
				splat.Dispose();
		}

		public void Dispose() {
			lock (provInfo) {
				foreach (var provider in provInfo)
					provider.Value.Dispose();
				provInfo.Clear();
			}
			lock (decorGrid) {
				for (int i = 0; i < size; i++) {
					decorGrid[i]?.Dispose();
					decorGrid[i] = null;
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
		/// Replaces the Refresh method of DecorProvider to handle the decor ourselves.
		/// </summary>
		/// <param name="provider">The DecorProvider that is being refreshed.</param>
		internal void RefreshDecor(DecorProvider provider) {
			if (provider == null)
				throw new ArgumentNullException("provider");
			var obj = provider.gameObject;
			// Get status of the object
			var prefabID = obj.GetComponent<KPrefabID>();
			var entombStatus = obj.GetComponent<Structure>();
			var disableStatus = obj.GetComponent<BuildingEnabledButton>();
			var breakStatus = obj.GetComponent<BuildingHP>();
			var glumStatus = obj.GetComponent<Klei.AI.Modifiers>()?.attributes?.Get(
				happinessAttribute);
			// Entombed/disabled = 0 decor, broken = use value in DecorTuning for broken
			bool broken = brokenFlag != null && breakStatus != null && breakStatus.IsBroken;
			bool disabled = (entombStatus != null && entombStatus.IsEntombed()) ||
				(disableStatus != null && !disableStatus.IsEnabled) || (glumStatus != null &&
				glumStatus.GetTotalValue() < 0.0f);
			if (provInfo.TryGetValue(provider, out DecorSplatNew splat))
				splat?.Refresh(broken, disabled);
			// Handle rooms which require an item with 20 decor: has to actually be functional
			bool hasTag = prefabID.HasTag(RoomConstraints.ConstraintTags.Decor20);
			bool needsTag = provider.decor.GetTotalValue() >= 20f && !broken && !disabled;
			if (hasTag != needsTag) {
				// Tag needs to be added/removed
				if (needsTag)
					prefabID.AddTag(RoomConstraints.ConstraintTags.Decor20, false);
				else
					prefabID.RemoveTag(RoomConstraints.ConstraintTags.Decor20);
				// Force room recalculation
				Game.Instance.roomProber.SolidChangedEvent(Grid.PosToCell(obj), true);
			}
		}

		/// <summary>
		/// Registers a decor provider with the system.
		/// </summary>
		/// <param name="instance">The decor provider to register.</param>
		internal void RegisterDecor(DecorProvider instance) {
			lock (provInfo) {
				if (!provInfo.ContainsKey(instance))
					provInfo.Add(instance, new DecorSplatNew(instance));
			}
		}

		/// <summary>
		/// Removes a decor provider from a given cell.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="provider">The object providing decor.</param>
		/// <param name="decor">The quantity of decor to add or subtract.</param>
		public void RemoveDecorProvider(int cell, DecorProvider provider, float decor) {
			if (Grid.IsValidCell(cell) && cell < size && cell >= 0)
				lock (decorGrid) {
					var dc = decorGrid[cell];
					if (dc != null) {
						dc.RemoveDecorProvider(provider.PrefabID(), provider, decor);
						if (dc.Count == 0)
							decorGrid[cell] = null;
					}
				}
		}
	}
}
