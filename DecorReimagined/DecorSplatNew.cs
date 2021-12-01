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

using KSerialization;
using System;
using System.Collections.Generic;
using IntHandle = HandleVector<int>.Handle;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Replaces DecorProvider.Splat with something easier to maintain.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	[SkipSaveFileSerialization]
	internal sealed class DecorSplatNew : KMonoBehaviour, ISaveLoadable {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649

		/// <summary>
		/// Monitors building breakdowns.
		/// </summary>
		[MyCmpGet]
		private BuildingHP breakStatus;

		/// <summary>
		/// Monitors status modifiers like "glum".
		/// </summary>
		[MyCmpGet]
		private Klei.AI.Modifiers glumStatus;

		/// <summary>
		/// Monitors building disablement such as flooding, entombment, or floating in midair.
		/// </summary>
		[MyCmpGet]
		private Operational operational;

		/// <summary>
		/// The ID of this object.
		/// </summary>
		[MyCmpReq]
		private KPrefabID prefabID;

		/// <summary>
		/// The decor provider responsible for this splat.
		/// </summary>
		[MyCmpReq]
		private DecorProvider provider;

#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

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

		internal DecorSplatNew() {
			cacheDecor = 0.0f;
			cells = new List<int>(64);
			partitioner = IntHandle.InvalidHandle;
			solidChangedPartitioner = IntHandle.InvalidHandle;
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

		protected override void OnCleanUp() {
			RemoveDecor();
			Unsubscribe((int)GameHashes.FunctionalChanged, OnFunctionalChanged);
			base.OnCleanUp();
		}

		private void OnFunctionalChanged(object argument) {
			if (gameObject != null)
				RefreshDecor();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.FunctionalChanged, OnFunctionalChanged);
		}

		/// <summary>
		/// Refreshes this splat.
		/// </summary>
		/// <param name="broken">true if the building is treated as broken, or false otherwise.</param>
		/// <param name="disabled">true if the building is treated as disabled, or false otherwise.</param>
		private void RefreshCells(bool broken, bool disabled) {
			var obj = gameObject;
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
						Extents.OneCell(Grid.PosToCell(obj));
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
		/// Replaces the Refresh method of DecorProvider to handle the decor ourselves.
		/// </summary>
		internal void RefreshDecor() {
			// Get status of the object
			var happiness = glumStatus?.attributes?.Get(DecorCellManager.Instance.
				HappinessAttribute);
			// Entombed/disabled = 0 decor, broken = use value in DecorTuning for broken
			bool disabled = (operational != null && !operational.IsFunctional) ||
				(happiness != null && happiness.GetTotalValue() < 0.0f);
			bool broken = breakStatus != null && breakStatus.IsBroken;
			RefreshCells(broken, disabled);
			// Handle rooms which require an item with 20 decor: has to actually be functional
			bool hasTag = prefabID.HasTag(RoomConstraints.ConstraintTags.Decor20);
			bool needsTag = provider.decor.GetTotalValue() >= 20f && !broken && !disabled;
			if (hasTag != needsTag) {
				int pos = Grid.PosToCell(gameObject);
				// Tag needs to be added/removed
				if (needsTag)
					prefabID.AddTag(RoomConstraints.ConstraintTags.Decor20, false);
				else
					prefabID.RemoveTag(RoomConstraints.ConstraintTags.Decor20);
				// Force room recalculation
				if (Grid.IsValidCell(pos))
					Game.Instance.roomProber.SolidChangedEvent(pos, true);
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
