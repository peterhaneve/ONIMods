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

using KSerialization;
using System;
using System.Collections.Generic;
using PeterHan.PLib.Core;

using IntHandle = HandleVector<int>.Handle;

namespace ReimaginationTeam.DecorRework {
	/// <summary>
	/// Replaces DecorProvider.Splat with something easier to maintain.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	[SkipSaveFileSerialization]
	internal sealed class DecorSplatNew : KMonoBehaviour {
		/// <summary>
		/// The layer used for drywall and other backwall buildings.
		/// </summary>
		internal static readonly int BackwallLayer = (int)PGameUtils.GetObjectLayer(
			nameof(ObjectLayer.Backwall), ObjectLayer.Backwall);

		/// <summary>
		/// Reports if this building is visually behind backwalls like drywall or wallpaper.
		/// </summary>
		internal bool IsBehindBackwall { get; private set; }
		
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		/// <summary>
		/// Monitors building breakdowns.
		/// </summary>
		[MyCmpGet]
		private BuildingHP breakStatus;

		/// <summary>
		/// Used to calculate extents for buildings.
		/// </summary>
		[MyCmpGet]
		private BuildingComplete building;

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

		/// <summary>
		/// Avoid infinite loops when resolving some buildings like the sauna.
		/// </summary>
		[MyCmpGet]
		private RoomTracker tracker;

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
		/// The building's layer if available.
		/// </summary>
		private int layer;

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
			layer = -1;
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
				var pid = prefabID.PrefabTag;
				cells.Clear();
				for (int x = extents.x; x < maxX; x++)
					for (int y = extents.y; y < maxY; y++) {
						int target = Grid.XYToCell(x, y);
						if (Grid.IsValidCell(target) && Grid.VisibilityTest(cell, target)) {
							inst.AddDecorProvider(target, provider, pid, decor);
							cells.Add(target);
						}
					}
			}
		}

		/// <summary>
		/// Reports true if this building is completely hidden by backwall.
		/// </summary>
		/// <returns>true if the building should be hidden by backwalls, or false otherwise.</returns>
		private bool IsHiddenByBackwall() {
			bool hidden = IsBehindBackwall;
			if (hidden) {
				var inst = DecorCellManager.Instance;
				var buildCells = building.PlacementCells;
				int n = buildCells.Length;
				for (int i = 0; i < n && hidden; i++)
					hidden = inst.HasBackwall(buildCells[i]);
			}
			return hidden;
		}

		protected override void OnCleanUp() {
			var inst = DecorCellManager.Instance;
			RemoveDecor();
			// For drywall, TSP and so forth, clean up backwall tracking
			if (layer == BackwallLayer && inst != null && inst.NoDecorBehindDrywall) {
				var buildCells = building.PlacementCells;
				int n = buildCells.Length;
				for (int i = 0; i < n; i++) {
					int cell = buildCells[i];
					inst.SetBackwall(cell, false);
					inst.RefreshAllAt(cell);
				}
			}
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
			if (building != null) {
				var def = building.Def;
				var inst = DecorCellManager.Instance;
				layer = (int)def.ObjectLayer;
				IsBehindBackwall = def.SceneLayer < Grid.SceneLayer.LogicGatesFront;
				// For drywall, TSP and so forth, enable backwall tracking
				if (layer == BackwallLayer && inst != null && inst.NoDecorBehindDrywall) {
					var buildCells = building.PlacementCells;
					int n = buildCells.Length;
					for (int i = 0; i < n; i++) {
						int cell = buildCells[i];
						inst.SetBackwall(cell, true);
						inst.RefreshAllAt(cell);
					}
				}
			}
		}
		
		/// <summary>
		/// Refreshes this splat.
		/// </summary>
		/// <param name="broken">true if the building is treated as broken, or false otherwise.</param>
		/// <param name="disabled">true if the building is treated as disabled, or false otherwise.</param>
		private void RefreshCells(bool broken, bool disabled) {
			var obj = gameObject;
			int cell;
			RemoveDecor();
			if (obj != null && Grid.IsValidCell(cell = Grid.PosToCell(obj))) {
				float decor = provider.decor?.GetTotalValue() ?? 0.0f;
				int radius = UnityEngine.Mathf.RoundToInt(provider.decorRadius?.
					GetTotalValue() ?? 5.0f);
				// Hide decor in bins?
				if (DecorTuning.HIDE_DECOR_IN_STORAGE && prefabID.HasTag(GameTags.Stored))
					decor = 0.0f;
				// Hide decor in walls?
				if (DecorTuning.HIDE_DECOR_IN_WALLS && !Grid.Transparent[cell] && Grid.Solid[
						cell] && provider.simCellOccupier == null)
					decor = 0.0f;
				// Broken buildings are ugly!
				if (broken)
					decor = DecorReimaginedPatches.Options.BrokenBuildingDecor;
				// Hide decor behind drywall?
				if (decor != 0.0f && DecorCellManager.Instance.NoDecorBehindDrywall &&
						IsHiddenByBackwall())
					decor = 0.0f;
				if (decor != 0.0f && (!disabled || decor < 0.0f) && radius > 0) {
					// Decor actually can be applied
					var area = provider.occupyArea;
					Extents extents, be = (area == null) ? Extents.OneCell(cell) : area.
						GetExtents();
					extents.x = Math.Max(0, be.x - radius);
					extents.y = Math.Max(0, be.y - radius);
					extents.width = Math.Min(Grid.WidthInCells - 1, be.width + radius * 2);
					extents.height = Math.Min(Grid.HeightInCells - 1, be.height + radius * 2);
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
			Klei.AI.AttributeInstance happiness = null;
			if (glumStatus != null)
				happiness = glumStatus.attributes?.Get(DecorCellManager.Instance.
					HappinessAttribute);
			// Entombed/disabled = 0 decor, broken = use value in DecorTuning for broken
			bool disabled = (operational != null && !operational.IsFunctional) ||
				(happiness != null && happiness.GetTotalValue() < 0.0f);
			bool broken = breakStatus != null && breakStatus.IsBroken;
			RefreshCells(broken, disabled);
			// Handle rooms which require an item with 20 decor: has to actually be functional
			bool hasTag = prefabID.HasTag(RoomConstraints.ConstraintTags.Decor20);
			bool needsTag = provider.decor.GetTotalValue() >= 20f && !broken && !disabled;
			// Do not trigger on buildings with a room tracker, as that could set up an
			// infinite loop
			if ((tracker == null || tracker.requirement == RoomTracker.Requirement.
					TrackingOnly) && hasTag != needsTag) {
				int pos = Grid.PosToCell(gameObject);
				// Tag needs to be added/removed
				if (needsTag)
					prefabID.AddTag(RoomConstraints.ConstraintTags.Decor20);
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
			var gsp = GameScenePartitioner.Instance;
			if (gsp != null) {
				if (partitioner != IntHandle.InvalidHandle) {
					gsp.Free(ref partitioner);
					partitioner = IntHandle.InvalidHandle;
				}
				if (solidChangedPartitioner != IntHandle.InvalidHandle) {
					gsp.Free(ref solidChangedPartitioner);
					solidChangedPartitioner = IntHandle.InvalidHandle;
				}
			}
			if (inst != null) {
				if (cacheDecor != 0.0f) {
					int n = cells.Count;
					var pid = prefabID.PrefabTag;
					for (int i = 0; i < n; i++)
						inst.RemoveDecorProvider(cells[i], provider, pid, cacheDecor);
				}
				cacheDecor = 0.0f;
				cells.Clear();
			}
		}
	}
}
