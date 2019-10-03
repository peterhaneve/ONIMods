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

using PeterHan.PLib;
using System;
using System.Collections.Generic;
using IntHandle = HandleVector<int>.Handle;

namespace PeterHan.DecorRework {
	/// <summary>
	/// Replaces DecorProvider.Splat with something easier to maintain.
	/// </summary>
	internal sealed class DecorSplatNew : IDisposable {
		/// <summary>
		/// The cached decor value.
		/// </summary>
		private float cacheDecor;
		
		/// <summary>
		/// The cells this decor splat affects.
		/// </summary>
		private readonly IList<int> cells;

		/// <summary>
		/// The partitioner used for decor changes.
		/// </summary>
		private IntHandle partitioner;

		/// <summary>
		/// The solid partitioner used for decor changes;
		/// </summary>
		private IntHandle solidChangedPartitioner;

		/// <summary>
		/// The decor provider responsible for this splat.
		/// </summary>
		private readonly DecorProvider provider;

		internal DecorSplatNew(DecorProvider provider) {
			this.provider = provider ?? throw new ArgumentNullException("provider");
			cacheDecor = 0.0f;
			cells = new List<int>(256);
			partitioner = IntHandle.InvalidHandle;
			solidChangedPartitioner = IntHandle.InvalidHandle;
			provider.Subscribe((int)GameHashes.OperationalFlagChanged,
				OnOperationalFlagChanged);
		}

		/// <summary>
		/// Adds this decor splat to its affecting cells. Line of sight checks are performed.
		/// </summary>
		/// <param name="cell">The originating cell.</param>
		/// <param name="decor">The decor value to add.</param>
		/// <param name="extents">The locations where it should be added.</param>
		private void AddDecor(int cell, float decor, Extents extents) {
			int maxX = extents.x + extents.width, maxY = extents.y + extents.height;
			// Bounds were already checked for us
			var inst = DecorCellManager.Instance;
			if (inst != null) {
				cells.Clear();
				for (int x = extents.x; x < maxX; x++)
					for (int y = extents.y; y < maxY; y++) {
						int target = Grid.XYToCell(x, y);
						if (Grid.IsValidCell(target) && Grid.VisibilityTest(cell, target,
								false)) {
							inst.AddDecorProvider(target, provider, decor);
							cells.Add(target);
						}
					}
			}
		}

		public void Dispose() {
			RemoveDecor();
			provider.Unsubscribe((int)GameHashes.OperationalFlagChanged,
				OnOperationalFlagChanged);
		}

		private void OnOperationalFlagChanged(object argument) {
			provider?.Refresh();
		}

		/// <summary>
		/// Refreshes this splat.
		/// </summary>
		/// <param name="broken">true if the building is treated as broken, or false otherwise.</param>
		/// <param name="disabled">true if the building is treated as disabled, or false otherwise.</param>
		public void Refresh(bool broken, bool disabled) {
			var obj = provider.gameObject;
			int cell, x, y;
			RemoveDecor();
			if (obj != null && Grid.IsValidCell(cell = Grid.PosToCell(obj))) {
				float decor = provider.decor?.GetTotalValue() ?? 0.0f;
				int radius = (int?)provider.decorRadius?.GetTotalValue() ?? 5;
				// Hide decor in bins?
				if (DecorTuning.HIDE_DECOR_IN_STORAGE && obj.HasTag(GameTags.Stored))
					decor = 0.0f;
				// Hide decor in walls?
				if (DecorTuning.HIDE_DECOR_IN_WALLS && !Grid.Transparent[cell] && Grid.Solid[
						cell] && provider.simCellOccupier == null)
					decor = 0.0f;
				// Broken buildings are ugly!
				if (broken)
					decor = DecorReimaginedPatches.Options.BrokenBuildingDecor;
				if (decor != 0.0f && (!disabled || decor < 0.0f) && radius > 0) {
					// Decor actually can be applied
					var rot = provider.rotatable;
					var orientation = rot ? rot.GetOrientation() : Orientation.Neutral;
					// Calculate expanded extents
					Extents extents, be = provider.occupyArea?.GetExtents(orientation) ??
						new Extents();
					extents.x = x = Math.Max(0, be.x - radius);
					extents.y = y = Math.Max(0, be.y - radius);
					extents.width = Math.Min(Grid.WidthInCells - 1, be.x + be.width + radius) -
						x;
					extents.height = Math.Min(Grid.HeightInCells - 1, be.y + be.height +
						radius) - y;
					// Names are the same as the base game
					partitioner = GameScenePartitioner.Instance.Add(
						"DecorProvider.SplatCollectDecorProviders", obj, extents,
						GameScenePartitioner.Instance.decorProviderLayer, provider.
						onCollectDecorProvidersCallback);
					solidChangedPartitioner = GameScenePartitioner.Instance.Add(
						"DecorProvider.SplatSolidCheck", obj, extents, GameScenePartitioner.
						Instance.solidChangedLayer, provider.refreshPartionerCallback);
					AddDecor(cell, decor, extents);
					cacheDecor = decor;
				}
			}
		}

		/// <summary>
		/// Removes this decor splat from its affecting cells.
		/// </summary>
		private void RemoveDecor() {
			var inst = DecorCellManager.Instance;
			if (partitioner != IntHandle.InvalidHandle) {
				GameScenePartitioner.Instance?.Free(ref partitioner);
				partitioner = IntHandle.InvalidHandle;
			}
			if (solidChangedPartitioner != IntHandle.InvalidHandle) {
				GameScenePartitioner.Instance?.Free(ref solidChangedPartitioner);
				solidChangedPartitioner = IntHandle.InvalidHandle;
			}
			if (inst != null) {
				if (cacheDecor != 0.0f)
					foreach (int cell in cells)
						inst.RemoveDecorProvider(cell, provider, cacheDecor);
				cacheDecor = 0.0f;
				cells.Clear();
			}
		}
	}
}
