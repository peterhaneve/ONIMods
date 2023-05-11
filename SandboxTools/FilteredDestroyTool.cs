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

using PeterHan.PLib.Core;
using System.Collections.Generic;
using UnityEngine;

using CellColorData = ToolMenu.CellColorData;
using PeterHan.PLib.Detours;

namespace PeterHan.SandboxTools {
	/// <summary>
	/// A replacement for "Destroy" that allows filtering of what to get rid of.
	/// </summary>
	public sealed class FilteredDestroyTool : BrushTool {
		private static readonly IDetouredField<SandboxDestroyerTool, Color> RECENTLY_AFFECTED =
			PDetours.DetourField<SandboxDestroyerTool, Color>("recentlyAffectedCellColor");

		/// <summary>
		/// Destroys the items in the set and recycles the list.
		/// </summary>
		/// <param name="destroy">The objects to destroy.</param>
		private static void DestroyAndRecycle(HashSetPool<GameObject, FilteredDestroyTool>.
				PooledHashSet destroy) {
			foreach (var gameObject in destroy)
				Util.KDestroyGameObject(gameObject);
			destroy.Recycle();
		}
		
		/// <summary>
		/// Destroys Duplicants and Critters in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private static void DestroyCreatures(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			// Critters, Duplicants, etc
			foreach (var brain in Components.Brains.Items)
				if (brain != null) {
					var go = brain.gameObject;
					if (Grid.PosToCell(go.transform.position) == cell)
						destroy.Add(go);
				}
			DestroyAndRecycle(destroy);
		}
		
		/// <summary>
		/// Destroys plants in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private static void DestroyPlants(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			foreach (var crop in Components.Uprootables.Items)
				if (crop != null) {
					var go = crop.gameObject;
					if (Grid.PosToCell(go.transform.position) == cell)
						destroy.Add(go);
				}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// The filters which can be used when destroying.
		/// </summary>
		private readonly IList<DestroyFilter> modes;

		/// <summary>
		/// The number of object layers, determined at RUNTIME.
		/// </summary>
		private readonly int numObjectLayers;

		/// <summary>
		/// The cells recently destroyed by the tool.
		/// </summary>
		private readonly HashSet<int> pendingCells;

		/// <summary>
		/// The color to highlight the recently affected cells.
		/// </summary>
		private readonly Color pendingHighlightColor;

		/// <summary>
		/// The layer for dropped items, determined at RUNTIME.
		/// </summary>
		private readonly int pickupLayer;

		internal FilteredDestroyTool() {
			Color color;
			modes = new List<DestroyFilter>(12) {
				new DestroyFilter("Elements", OverlayModes.TileMode.ID,
					SandboxToolsStrings.DESTROY_ELEMENTS, DestroyElement),
				new DestroyFilter("Items", HashedString.Invalid, SandboxToolsStrings.
					DESTROY_ITEMS, DestroyItems),
				new DestroyFilter("Creatures", HashedString.Invalid,
					SandboxToolsStrings.DESTROY_CREATURES, DestroyCreatures),
				new DestroyFilter("Plants", OverlayModes.Crop.ID,
					SandboxToolsStrings.DESTROY_PLANTS, DestroyPlants),
				new DestroyFilter("Buildings", OverlayModes.Decor.ID,
					SandboxToolsStrings.DESTROY_BUILDINGS),
				new DestroyFilter("BackWall", HashedString.Invalid, SandboxToolsStrings.
					DESTROY_DRYWALL),
				new DestroyFilter("LiquidPipes", OverlayModes.LiquidConduits.ID,
					SandboxToolsStrings.DESTROY_LPIPES),
				new DestroyFilter("GasPipes", OverlayModes.GasConduits.ID,
					SandboxToolsStrings.DESTROY_GPIPES),
				new DestroyFilter("Wires", OverlayModes.Power.ID, SandboxToolsStrings.
					DESTROY_POWER),
				new DestroyFilter("Logic", OverlayModes.Logic.ID, SandboxToolsStrings.
					DESTROY_AUTO),
				new DestroyFilter("SolidConduits", OverlayModes.SolidConveyor.ID,
					SandboxToolsStrings.DESTROY_SHIPPING)
			};
			// "All" checkbox to destroy everything
			if (!SandboxToolsPatches.AdvancedFilterEnabled)
				modes.Insert(0, new DestroyFilter("All", HashedString.Invalid,
					SandboxToolsStrings.DESTROY_ALL, DestroyAll));
			pendingCells = new HashSet<int>();
			try {
				// Take from stock tool if possible
				color = RECENTLY_AFFECTED.Get(SandboxDestroyerTool.instance);
			} catch (System.Exception e) {
#if DEBUG
				PUtil.LogExcWarn(e);
#endif
				// Use default
				color = new Color(1f, 1f, 1f, 0.1f);
			}
			// Read value at runtime if possible
			numObjectLayers = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.NumLayers),
				ObjectLayer.NumLayers);
			pickupLayer = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Pickupables),
				ObjectLayer.Pickupables);
			pendingHighlightColor = color;
		}

		/// <summary>
		/// Destroys everything in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyAll(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			DestroyElement(cell);
			DestroyItems(cell);
			DestroyPlants(cell);
			DestroyCreatures(cell);
			// All buildings, no exceptions
			for (int i = 0; i < numObjectLayers; i++) {
				var obj = Grid.Objects[cell, i];
				if (obj != null)
					destroy.Add(obj);
			}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys buildings in the cell which match the filter layer as used by the
		/// DeconstructTool.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		/// <param name="filter">The layer to match.</param>
		private void DestroyBuildings(int cell, string filter) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			var inst = DeconstructTool.Instance;
			for (int i = 0; i < numObjectLayers; i++) {
				var obj = Grid.Objects[cell, i];
				if (obj != null && inst.GetFilterLayerFromGameObject(obj) == filter)
					// Buldings, either finished or under construction
					destroy.Add(obj);
			}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys the element (liquid, solid, or gas) in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyElement(int cell) {
			pendingCells.Add(cell);
			// Register a sim callback to unhighlight the cells when destroyed
			int index = Game.Instance.callbackManager.Add(new Game.CallbackInfo(delegate {
				pendingCells.Remove(cell);
			})).index;
			SimMessages.ReplaceElement(cell, SimHashes.Vacuum, CellEventLogger.Instance.
				SandBoxTool, 0.0f, 0.0f, Klei.SimUtil.DiseaseInfo.Invalid.idx, 0, index);
			// Destroy any solid tiles / doors on the area as well to avoid bad states
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			for (int i = 0; i < numObjectLayers; i++) {
				var obj = Grid.Objects[cell, i];
				if (obj != null && obj.TryGetComponent(out SimCellOccupier _))
					destroy.Add(obj);
			}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys debris in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyItems(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			var go = Grid.Objects[cell, pickupLayer];
			if (go != null && go.TryGetComponent(out Pickupable pickupable)) {
				// Linked list of debris in pickupable layer
				var objectListNode = pickupable.objectLayerListItem;
				while (objectListNode != null) {
					var content = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					// Ignore Duplicants
					if (content != null && !content.TryGetComponent(out Brain _) &&
							content.TryGetComponent(out Clearable cc) && cc.isClearable)
						destroy.Add(content);
				}
			}
			DestroyAndRecycle(destroy);
		}

		public override void GetOverlayColorData(out HashSet<CellColorData> colors) {
			colors = new HashSet<CellColorData>();
			// Highlight recently destroyed cells
			foreach (int cell in pendingCells)
				colors.Add(new CellColorData(cell, pendingHighlightColor));
			// Highlight cells in destroy radius
			foreach (int cell in cellsInRadius)
				colors.Add(new CellColorData(cell, radiusIndicatorColor));
		}

		protected override void OnPrefabInit() {
			var menu = DestroyParameterMenu.Instance;
			base.OnPrefabInit();
			affectFoundation = true;
			var config = gameObject.AddComponent<HoverTextConfiguration>();
			// Copy settings from the default destroy tool (which are set in a prefab)
			if (menu != null && menu.TryGetComponent(out HoverTextConfiguration hConfig)) {
				// Tool tip and name
				config.ActionName = hConfig.ActionName;
				config.ActionStringKey = hConfig.ActionStringKey;
				config.ToolNameStringKey = hConfig.ToolNameStringKey;
				// Fonts
				config.ToolTitleTextStyle = hConfig.ToolTitleTextStyle;
				config.Styles_Title = hConfig.Styles_Title;
				config.Styles_BodyText = hConfig.Styles_BodyText;
				config.Styles_Instruction = hConfig.Styles_Instruction;
				config.Styles_Values = hConfig.Styles_Values;
			}
		}

		protected override void OnActivateTool() {
			var menu = DestroyParameterMenu.Instance;
			var inst = Game.Instance;
			base.OnActivateTool();
			if (menu != null) {
				if (!menu.HasOptions)
					menu.PopulateMenu(modes);
				menu.ShowMenu();
			}
			// Show the radius slider
			var sandboxMenu = SandboxToolParameterMenu.instance;
			sandboxMenu.gameObject.SetActive(true);
			sandboxMenu.DisableParameters();
			sandboxMenu.brushRadiusSlider.row.SetActive(true);
			if (inst != null) {
				inst.Subscribe((int)GameHashes.EnableOverlay, OnUpdateOverlay);
				inst.Subscribe((int)GameHashes.OverlayChanged, OnUpdateOverlay);
			}
			UpdateOverlay(OverlayScreen.Instance.mode);
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			var inst = Game.Instance;
			var menu = DestroyParameterMenu.Instance;
			base.OnDeactivateTool(newTool);
			if (menu != null)
				menu.HideMenu();
			SandboxToolParameterMenu.instance.gameObject.SetActive(false);
			if (inst != null) {
				inst.Unsubscribe((int)GameHashes.EnableOverlay);
				inst.Unsubscribe((int)GameHashes.OverlayChanged);
			}
		}

		protected override void OnPaintCell(int cell, int distFromOrigin) {
			var menu = DestroyParameterMenu.Instance;
			base.OnPaintCell(cell, distFromOrigin);
			if (menu != null) {
				if (menu.AllSelected)
					// Ensure that everything in the cell that the filters might have missed
					// is destroyed
					DestroyAll(cell);
				else
					foreach (var mode in modes)
						// Look for the enabled layers
						if (menu.GetState(mode.ID) == ToolParameterMenu.ToggleState.On) {
							var handler = mode.OnPaintCell;
							if (handler != null)
								handler(cell);
							else
								DestroyBuildings(cell, mode.ID);
						}
			}
		}

		/// <summary>
		/// Triggered when the game overlay is changed.
		/// </summary>
		/// <param name="mode">The new overlay mode.</param>
		private void OnUpdateOverlay(object mode) {
			if (mode is HashedString str)
				UpdateOverlay(str);
		}

		/// <summary>
		/// Updates the selected filters to match the current overlay.
		/// </summary>
		/// <param name="str"></param>
		private void UpdateOverlay(HashedString str) {
			var menu = DestroyParameterMenu.Instance;
			if (!SandboxToolsPatches.AdvancedFilterEnabled && menu != null && str !=
					HashedString.Invalid) {
				// Look in the list for an option matching this mode
				foreach (var mode in modes)
					if (mode.OverlayMode == str) {
						menu.SetTo(mode.ID);
						break;
					}
			}
		}
	}
}
