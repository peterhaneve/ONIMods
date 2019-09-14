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
using System;
using UnityEngine;
using PeterHan.PLib;
using System.Collections.Generic;

using ToolToggleState = ToolParameterMenu.ToggleState;
using UnityEngine.UI;

namespace PeterHan.SweepByType {
	/// <summary>
	/// A version of ClearTool (sweep) that allows filtering.
	/// </summary>
	public sealed class FilteredClearTool : DragTool {
		/// <summary>
		/// The singleton instance of this tool.
		/// </summary>
		public static FilteredClearTool Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance.
		/// </summary>
		internal static void DestroyInstance() {
			if (Instance != null)
				Instance.Cleanup();
			Instance = null;
		}

		/// <summary>
		/// The currently selected item type to sweep.
		/// </summary>
		internal Tag SelectedItemTag {
			get {
				return typeSelect?.CurrentSelectedElement ?? GameTags.Solid;
			}
		}

		/// <summary>
		/// The state of each tool option.
		/// </summary>
		private IDictionary<string, ToolToggleState> optionState;

		/// <summary>
		/// The tool options shown in the filter menu.
		/// </summary>
		private readonly ICollection<PToolMode> toolOptions;

		/// <summary>
		/// A fake recipe allowing to sweep any solid.
		/// </summary>
		private readonly Recipe sweepRecipe;

		/// <summary>
		/// Allows selection of the type to sweep.
		/// </summary>
		private MaterialSelector typeSelect;

		public FilteredClearTool() {
			optionState = null;
			// Require 0 of any solid
			sweepRecipe = new Recipe() {
				Name = SweepByTypeStrings.MATERIAL_TYPE,
				recipeDescription = SweepByTypeStrings.MATERIAL_TYPE,
				Ingredients = new List<Recipe.Ingredient>() {
					new Recipe.Ingredient(GameTags.Solid, float.Epsilon)
				}
			};
			toolOptions = new List<PToolMode>(2) {
				new PToolMode(SweepByTypeStrings.TOOL_KEY_DEFAULT, SweepByTypeStrings.
					TOOL_MODE_DEFAULT, ToolToggleState.On),
				new PToolMode(SweepByTypeStrings.TOOL_KEY_FILTERED, SweepByTypeStrings.
					TOOL_MODE_FILTERED)
			};
			typeSelect = null;
		}

		/// <summary>
		/// Cleans up the type selector.
		/// </summary>
		private void Cleanup() {
			if (typeSelect != null)
				Destroy(typeSelect.gameObject);
			typeSelect = null;
		}

		/// <summary>
		/// Builds the "Select Material" window.
		/// </summary>
		private void InitMaterialSelector() {
			// Reuse the "Product Info" asset from BuildMenu to allow resource selection
			var pis = Util.KInstantiateUI<ProductInfoScreen>(Traverse.Create(
				PlanScreen.Instance).GetField<GameObject>("productInfoScreenPrefab"),
				gameObject);
			typeSelect = Util.KInstantiateUI<MaterialSelector>(pis.materialSelectionPanel.
				MaterialSelectorTemplate, visualizer);
			// Create a single MaterialSelector which is all we need
			typeSelect.name = "FilteredClearToolMaterials";
			typeSelect.ToggleShowDescriptorsPanel(false);
			typeSelect.Deactivate();
			typeSelect.gameObject.layer = 5;
			// Just for a prefab...
			pis.Deactivate();
			//Destroy(pis.gameObject);
		}

		protected override void OnPrefabInit() {
			var us = Traverse.Create(this);
			base.OnPrefabInit();
			Instance = this;
			interceptNumberKeysForPriority = true;
			populateHitsList = true;
			gameObject.AddComponent<FilteredSweepHover>();
			// Get the cursor from the existing sweep tool
			var trSweep = Traverse.Create(ClearTool.Instance);
			cursor = trSweep.GetField<Texture2D>("cursor");
			us.SetField("boxCursor", cursor);
			// Get the area visualizer from the sweep tool
			var avTemplate = trSweep.GetField<GameObject>("areaVisualizer");
			if (avTemplate != null) {
				var areaVisualizer = Util.KInstantiate(avTemplate, gameObject,
					"FilteredClearToolAreaVisualizer");
				areaVisualizer.SetActive(false);
				areaVisualizerSpriteRenderer = areaVisualizer.GetComponent<SpriteRenderer>();
				// The visualizer is private so we need to set it with reflection
				us.SetField("areaVisualizer", areaVisualizer);
				us.SetField("areaVisualizerTextPrefab", trSweep.GetField<GameObject>(
					"areaVisualizerTextPrefab"));
			}
			visualizer = Util.KInstantiate(trSweep.GetField<GameObject>("visualizer"),
				gameObject, "FilteredClearToolSprite");
			visualizer.SetActive(false);
		}

		protected override void OnDragTool(int cell, int distFromOrigin) {
			var gameObject = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
			if (gameObject != null && optionState != null) {
				// Linked list of debris in layer 3
				var objectListNode = gameObject.GetComponent<Pickupable>().objectLayerListItem;
				var priority = ToolMenu.Instance.PriorityScreen.GetLastSelectedPriority();
				bool byType = optionState[SweepByTypeStrings.TOOL_KEY_FILTERED] ==
					ToolToggleState.On;
				var targetTag = SelectedItemTag;
				while (objectListNode != null) {
					var content = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					// Ignore Duplicants
					if (content != null && content.GetComponent<MinionIdentity>() == null) {
						var cc = content.GetComponent<Clearable>();
						if (cc != null && cc.isClearable && (!byType || content.HasTag(
								targetTag))) {
							// Parameter is whether to force, not remove sweep errand!
							cc.MarkForClear(false);
							var pr = content.GetComponent<Prioritizable>();
							if (pr != null)
								pr.SetMasterPriority(priority);
						}
					}
				}
			}
		}

		protected override void OnActivateTool() {
			var menu = ToolMenu.Instance.toolParameterMenu;
			base.OnActivateTool();
			ToolMenu.Instance.PriorityScreen.Show(true);
			// Default to "sweep all"
			optionState = PToolMode.PopulateMenu(menu, toolOptions);
			menu.onParametersChanged += UpdateViewMode;
			if (typeSelect == null)
				InitMaterialSelector();
			UpdateViewMode();
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			typeSelect?.Deactivate();
			base.OnDeactivateTool(newTool);
			optionState = null;
			ToolMenu.Instance.PriorityScreen.Show(false);
			ToolMenu.Instance.toolParameterMenu.onParametersChanged -= UpdateViewMode;
		}

		/// <summary>
		/// Based on the current tool mode, updates the overlay mode.
		/// </summary>
		private void UpdateViewMode() {
			if (optionState != null && typeSelect != null) {
				if (optionState[SweepByTypeStrings.TOOL_KEY_FILTERED] == ToolToggleState.On) {
					// Filtered
					typeSelect.ConfigureScreen(sweepRecipe.Ingredients[0], sweepRecipe);
					typeSelect.AutoSelectAvailableMaterial();
					typeSelect.Activate();
				} else
					// Default
					typeSelect.Deactivate();
			}
		}
	}
}
