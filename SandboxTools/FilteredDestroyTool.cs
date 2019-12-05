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
using UnityEngine;

using CellColorData = ToolMenu.CellColorData;

namespace PeterHan.SandboxTools {
	/// <summary>
	/// A replacement for "Destroy" that allows filtering of what to get rid of.
	/// </summary>
	public sealed class FilteredDestroyTool : BrushTool {
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
		/// The filters which can be used when destroying.
		/// </summary>
		private readonly IList<DestroyFilter> modes;

		/// <summary>
		/// The options available for this tool.
		/// </summary>
		private IDictionary<string, ToolParameterMenu.ToggleState> options;

		/// <summary>
		/// The cells recently destroyed by the tool.
		/// </summary>
		private readonly HashSet<int> pendingCells;

		/// <summary>
		/// The color to highlight the recently affected cells.
		/// </summary>
		private readonly Color pendingHighlightColor;

		internal FilteredDestroyTool() {
			Color color;
			modes = new List<DestroyFilter>(12) {
				new DestroyFilter("All", SandboxToolsStrings.DESTROY_ALL, DestroyAll),
				new DestroyFilter("Elements", SandboxToolsStrings.DESTROY_ELEMENTS,
					DestroyElement),
				new DestroyFilter("Items", SandboxToolsStrings.DESTROY_ITEMS, DestroyItems),
				new DestroyFilter("Creatures", SandboxToolsStrings.DESTROY_CREATURES,
					DestroyCreatures),
				new DestroyFilter("Plants", SandboxToolsStrings.DESTROY_PLANTS,
					DestroyPlants),
				new DestroyFilter("Buildings", SandboxToolsStrings.DESTROY_BUILDINGS),
				new DestroyFilter("BackWall", SandboxToolsStrings.DESTROY_DRYWALL),
				new DestroyFilter("LiquidPipes", SandboxToolsStrings.DESTROY_LPIPES),
				new DestroyFilter("GasPipes", SandboxToolsStrings.DESTROY_GPIPES),
				new DestroyFilter("Wires", SandboxToolsStrings.DESTROY_POWER),
				new DestroyFilter("Logic", SandboxToolsStrings.DESTROY_AUTO),
				new DestroyFilter("SolidConduits", SandboxToolsStrings.DESTROY_SHIPPING)
			};
			pendingCells = new HashSet<int>();
			try {
				// Take from stock tool if possible
				color = Traverse.Create(SandboxDestroyerTool.instance).GetField<Color>(
					"recentlyAffectedCellColor");
			} catch (Exception) {
				// Use default
				color = new Color(1f, 1f, 1f, 0.1f);
			}
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
			for (int i = 0; i < (int)ObjectLayer.NumLayers; i++) {
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
			for (int i = 0; i < (int)ObjectLayer.NumLayers; i++) {
				var obj = Grid.Objects[cell, i];
				if (obj != null && inst.GetFilterLayerFromGameObject(obj) == filter)
					// Buldings, either finished or under construction
					destroy.Add(obj);
			}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys Duplicants and Critters in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyCreatures(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			// Critters, Duplicants, etc
			foreach (var brain in Components.Brains.Items)
				if (Grid.PosToCell(brain) == cell)
					destroy.Add(brain.gameObject);
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys the element (liquid, solid, or gas) in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyElement(int cell) {
			pendingCells.Add(cell);
			// Register a sim callback to unhighlight the cells when destroyed
			int index = Game.Instance.callbackManager.Add(new Game.CallbackInfo(delegate () {
				pendingCells.Remove(cell);
			})).index;
			SimMessages.ReplaceElement(cell, SimHashes.Vacuum, CellEventLogger.Instance.
				SandBoxTool, 0.0f, 0.0f, Klei.SimUtil.DiseaseInfo.Invalid.idx, 0, index);
			// Destroy any solid tiles / doors on the area as well to avoid bad states
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			for (int i = 0; i < (int)ObjectLayer.NumLayers; i++) {
				var obj = Grid.Objects[cell, i];
				if (obj != null && obj.GetComponentSafe<SimCellOccupier>() != null)
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
			var gameObject = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (gameObject != null) {
				// Linked list of debris in layer 3
				var objectListNode = gameObject.GetComponent<Pickupable>().objectLayerListItem;
				while (objectListNode != null) {
					var content = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					// Ignore Duplicants
					if (content != null && content.GetComponent<MinionIdentity>() == null) {
						var cc = content.GetComponent<Clearable>();
						if (cc != null && cc.isClearable)
							destroy.Add(content);
					}
				}
			}
			DestroyAndRecycle(destroy);
		}

		/// <summary>
		/// Destroys plants in the cell.
		/// </summary>
		/// <param name="cell">The cell to destroy.</param>
		private void DestroyPlants(int cell) {
			var destroy = HashSetPool<GameObject, FilteredDestroyTool>.Allocate();
			foreach (var crop in Components.Uprootables.Items)
				if (Grid.PosToCell(crop) == cell)
					destroy.Add(crop.gameObject);
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
			base.OnPrefabInit();
			affectFoundation = true;
			var config = gameObject.AddComponent<HoverTextConfiguration>();
			// Copy settings from the default destroy tool (which are set in a prefab)
			var hConfig = SandboxDestroyerTool.instance?.gameObject.
				GetComponentSafe<HoverTextConfiguration>();
			if (hConfig != null) {
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
			var menu = ToolMenu.Instance.toolParameterMenu;
			var toolModes = ListPool<PToolMode, PToolMode>.Allocate();
			int index = 0;
			base.OnActivateTool();
			// Create list of layers which can be destroyed
			foreach (var mode in modes)
				toolModes.Add(mode.GetMode((index++ == 0) ? ToolParameterMenu.ToggleState.On :
					ToolParameterMenu.ToggleState.Off));
			if (menu != null)
				options = PToolMode.PopulateMenu(menu, toolModes);
			toolModes.Recycle();
			// Show the radius slider
			SandboxToolParameterMenu.instance.gameObject.SetActive(true);
			SandboxToolParameterMenu.instance.DisableParameters();
			SandboxToolParameterMenu.instance.brushRadiusSlider.row.SetActive(true);
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			base.OnDeactivateTool(newTool);
			ToolMenu.Instance.toolParameterMenu?.ClearMenu();
			SandboxToolParameterMenu.instance.gameObject.SetActive(false);
		}

		protected override void OnPaintCell(int cell, int distFromOrigin) {
			base.OnPaintCell(cell, distFromOrigin);
			foreach (var mode in modes)
				// Look for the enabled layer
				if (options[mode.ID] == ToolParameterMenu.ToggleState.On) {
					var handler = mode.OnPaintCell;
					if (handler != null)
						handler(cell);
					else
						DestroyBuildings(cell, mode.ID);
				}
		}

		/// <summary>
		/// A filter option for the sandbox destroy tool.
		/// </summary>
		private sealed class DestroyFilter {
			/// <summary>
			/// The ID to use internally.
			/// </summary>
			public string ID { get; }

			/// <summary>
			/// The action to perform when this filter is selected for each destroyed cell.
			/// </summary>
			public Action<int> OnPaintCell { get; }

			/// <summary>
			/// The title of the filter.
			/// </summary>
			public string Title { get; }

			public DestroyFilter(string id, string title) : this(id, title, null) { }

			public DestroyFilter(string id, string title, Action<int> onPaintCell) {
				if (string.IsNullOrEmpty(id))
					throw new ArgumentNullException("id");
				if (string.IsNullOrEmpty(title))
					throw new ArgumentNullException("title");
				ID = id;
				Title = title;
				OnPaintCell = onPaintCell;
			}

			/// <summary>
			/// Gets the matching tool mode.
			/// </summary>
			/// <param name="state">Whether this option should be initially selected.</param>
			/// <returns>The mode to be added to the options menu.</returns>
			public PToolMode GetMode(ToolParameterMenu.ToggleState state) {
				return new PToolMode(ID, Title, state);
			}

			public override string ToString() {
				return "SandboxDestroyFilter[ID={0},Title={1}]".F(ID, Title);
			}
		}
	}
}
