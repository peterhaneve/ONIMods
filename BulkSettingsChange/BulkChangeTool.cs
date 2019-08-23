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

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// A tool which can change the settings of many buildings at once. Supports enable/disable
	/// disinfect, enable/disable auto repair, and enable/disable building.
	/// </summary>
	sealed class BulkChangeTool : DragTool {
		/// <summary>
		/// The shared instance of this tool.
		/// </summary>
		private static BulkChangeTool tool;

		/// <summary>
		/// The singleton instance of this tool.
		/// </summary>
		public static BulkChangeTool Instance {
			get {
				if (tool == null)
					tool = new BulkChangeTool();
				return tool;
			}
		}

		/// <summary>
		/// Destroys the singleton instance. It will be recreated if used again.
		/// </summary>
		public static void DestroyInstance() {
			tool = null;
		}

		/// <summary>
		/// Creates a popup on the cell of all buildings where a tool is applied.
		/// </summary>
		/// <param name="enable">true if the "enable" tool was used, false for "disable".</param>
		/// <param name="enabled">The enable tool.</param>
		/// <param name="disabled">The disable tool.</param>
		/// <param name="cell">The cell where the change occurred.</param>
		private static void ShowPopup(bool enable, BulkToolMode enabled, BulkToolMode disabled,
				int cell) {
			PLibUtil.CreatePopup(enable ? PopFXManager.Instance.sprite_Plus : PopFXManager.
				Instance.sprite_Negative, enable ? enabled.PopupText : disabled.PopupText,
				cell);
		}

		/// <summary>
		/// The options available for this tool.
		/// </summary>
		private IDictionary<string, ToolParameterMenu.ToggleState> options;

		protected override string GetConfirmSound() {
			string sound = base.GetConfirmSound();
			// "Enable" use default, "Disable" uses cancel sound
			if (BulkChangeTools.DisableBuildings.IsOn(options) || BulkChangeTools.
					DisableDisinfect.IsOn(options) || BulkChangeTools.DisableRepair.IsOn(
					options))
				sound = "Tile_Confirm_NegativeTool";
			return sound;
		}

		protected override string GetDragSound() {
			string sound = base.GetDragSound();
			// "Enable" use default, "Disable" uses cancel sound
			if (BulkChangeTools.DisableBuildings.IsOn(options) || BulkChangeTools.
					DisableDisinfect.IsOn(options) || BulkChangeTools.DisableRepair.IsOn(
					options))
				sound = "Tile_Drag_NegativeTool";
			return sound;
		}

		protected override void OnActivateTool() {
			var menu = ToolMenu.Instance.toolParameterMenu;
			var modes = ListPool<ToolMode, BulkChangeTool>.Allocate();
			base.OnActivateTool();
			// Create mode list
			foreach (var mode in BulkToolMode.AllTools())
				modes.Add(mode.ToToolMode(modes.Count == 0 ? ToolParameterMenu.ToggleState.On :
					ToolParameterMenu.ToggleState.Off));
			options = ToolMode.PopulateMenu(menu, modes);
			modes.Recycle();
			// When the parameters are changed, update the view settings
			menu.onParametersChanged += UpdateViewMode;
			SetMode(Mode.Box);
			UpdateViewMode();
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			base.OnDeactivateTool(newTool);
			ToolMenu.Instance.toolParameterMenu.ClearMenu();
			ToolMenu.Instance.PriorityScreen.Show(false);
		}

		protected override void OnDragTool(int cell, int distFromOrigin) {
			// Invoked when the tool drags over a cell
			if (Grid.IsValidCell(cell)) {
				bool enable = BulkChangeTools.EnableBuildings.IsOn(options), changed = false,
					disinfect = BulkChangeTools.EnableDisinfect.IsOn(options),
					repair = BulkChangeTools.EnableRepair.IsOn(options);
#if DEBUG
				// Log what we are about to do
				PLibUtil.LogDebug("{0} at cell {1:D}".F(ToolMenu.Instance.toolParameterMenu.
					GetLastEnabledFilter(), cell));
#endif
				if (enable || BulkChangeTools.DisableBuildings.IsOn(options)) {
					// Enable/disable buildings
					for (int i = 0; i < (int)Grid.SceneLayer.SceneMAX; i++)
						changed |= ToggleBuilding(cell, Grid.Objects[cell, i], enable);
					if (changed)
						ShowPopup(enable, BulkChangeTools.EnableBuildings, BulkChangeTools.
							DisableBuildings, cell);
				} else if (disinfect || BulkChangeTools.DisableDisinfect.IsOn(options)) {
					// Enable/disable disinfect
					for (int i = 0; i < (int)Grid.SceneLayer.SceneMAX; i++)
						changed |= ToggleDisinfect(cell, Grid.Objects[cell, i], disinfect);
					if (changed)
						ShowPopup(disinfect, BulkChangeTools.EnableDisinfect, BulkChangeTools.
							DisableDisinfect, cell);
				} else if (repair || BulkChangeTools.DisableRepair.IsOn(options)) {
					// Enable/disable repair
					for (int i = 0; i < (int)Grid.SceneLayer.SceneMAX; i++)
						changed |= ToggleRepair(cell, Grid.Objects[cell, i], repair);
					if (changed)
						ShowPopup(repair, BulkChangeTools.EnableRepair, BulkChangeTools.
							DisableRepair, cell);
				}
			}
		}

		protected override void OnPrefabInit() {
			var us = Traverse.Create(this);
			base.OnPrefabInit();
			// Allow priority setting for the enable/disable building chores
			interceptNumberKeysForPriority = true;
			gameObject.AddComponent<BulkChangeHover>();
			// HACK: Get the cursor from the disinfect tool
			var trDisinfect = Traverse.Create(DisinfectTool.Instance);
			cursor = trDisinfect.GetField<Texture2D>("cursor");
			us.SetField("boxCursor", cursor);
			// HACK: Get the area visualizer from the disinfect tool
			var avTemplate = trDisinfect.GetField<GameObject>("areaVisualizer");
			if (avTemplate != null) {
				var areaVisualizer = Util.KInstantiate(avTemplate, gameObject,
					"BulkChangeToolAreaVisualizer");
				areaVisualizer.SetActive(false);
				areaVisualizerSpriteRenderer = areaVisualizer.GetComponent<SpriteRenderer>();
				// The visualizer is private so we need to set it with reflection
				us.SetField("areaVisualizer", areaVisualizer);
				us.SetField("areaVisualizerTextPrefab", trDisinfect.GetField<GameObject>(
					"areaVisualizerTextPrefab"));
			}
			visualizer = new GameObject("BulkChangeToolVisualizer");
			var spriteRenderer = visualizer.AddComponent<SpriteRenderer>();
			// Set up the color and parent
			if (spriteRenderer != null) {
				// Determine the scaling amount since size is known
				var sprite = SpriteRegistry.GetPlaceIcon();
				float widthInM = sprite.texture.width / sprite.pixelsPerUnit,
					scaleWidth = Grid.CellSizeInMeters / widthInM;
				spriteRenderer.name = "BulkChangeToolSprite";
				spriteRenderer.transform.localPosition = new Vector3(0.0f, 100.0f, 0.0f);
				spriteRenderer.transform.localScale = new Vector3(scaleWidth, scaleWidth, 1.0f);
				spriteRenderer.sprite = sprite;
				spriteRenderer.enabled = true;
			}
			visualizer.SetLayerRecursively(LayerMask.NameToLayer("Overlay"));
			visualizer.SetActive(false);
		}

		/// <summary>
		/// Creates a settings chore to enable/disable the specified building.
		/// </summary>
		/// <param name="cell">The cell this building occupies.</param>
		/// <param name="building">The building to toggle if it exists.</param>
		/// <param name="enable">true to enable it, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private bool ToggleBuilding(int cell, GameObject building, bool enable) {
			var ed = building?.GetComponent<BuildingEnabledButton>();
			bool changed = false;
			if (ed != null) {
				var trEnableDisable = Traverse.Create(ed);
				// Check to see if a work errand is pending
				int toggleIndex = trEnableDisable.GetField<int>("ToggleIdx");
				bool curEnabled = ed.IsEnabled, toggleQueued = building.GetComponent<
					Toggleable>()?.IsToggleQueued(toggleIndex) ?? false;
#if DEBUG
				PLibUtil.LogDebug("Checking building @{0:D}: on={1}, queued={2}, desired={3}".
					F(cell, curEnabled, toggleQueued, enable));
#endif
				// Only continue if we are cancelling the toggle errand or (the building state
				// is different than desired and no toggle errand is queued)
				if (toggleQueued != (curEnabled != enable)) {
					// Private methods grrr
					trEnableDisable.CallMethod("OnMenuToggle");
					// Set priority according to the chosen level
					var priority = building.GetComponent<Prioritizable>();
					if (priority != null)
						priority.SetMasterPriority(ToolMenu.Instance.PriorityScreen.
							GetLastSelectedPriority());
					PLibUtil.LogDebug("Enable {2} @{0:D} = {1}".F(cell, enable, building.
						GetProperName()));
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles auto disinfect on the object.
		/// </summary>
		/// <param name="cell">The cell this building occupies.</param>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to enable auto disinfect, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private bool ToggleDisinfect(int cell, GameObject item, bool enable) {
			var ad = item?.GetComponent<AutoDisinfectable>();
			bool changed = false;
			if (ad != null) {
				var trAutoDisinfect = Traverse.Create(ad);
				// Private methods grrr
				if (trAutoDisinfect.GetField<bool>("enableAutoDisinfect") != enable) {
					trAutoDisinfect.CallMethod(enable ? "EnableAutoDisinfect" :
						"DisableAutoDisinfect");
					PLibUtil.LogDebug("Auto disinfect {2} @{0:D} = {1}".F(cell, enable,
						item.GetProperName()));
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles auto repair on the object.
		/// </summary>
		/// <param name="cell">The cell this building occupies.</param>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to enable auto repair, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private bool ToggleRepair(int cell, GameObject item, bool enable) {
			var ar = item?.GetComponent<Repairable>();
			bool changed = false;
			if (ar != null) {
				// Private methods grrr
				Traverse.Create(ar).CallMethod(enable ? "AllowRepair" : "CancelRepair");
				PLibUtil.LogDebug("Auto repair {2} @{0:D} = {1}".F(cell, enable, item.
					GetProperName()));
				changed = true;
			}
			return changed;
		}

		/// <summary>
		/// Based on the current tool mode, updates the overlay mode.
		/// </summary>
		private void UpdateViewMode() {
			if (BulkChangeTools.EnableDisinfect.IsOn(options) || BulkChangeTools.
					DisableDisinfect.IsOn(options)) {
				// Enable/Disable Disinfect
				ToolMenu.Instance.PriorityScreen.Show(false);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.Disease.ID);
			} else if (BulkChangeTools.EnableBuildings.IsOn(options) || BulkChangeTools.
					DisableBuildings.IsOn(options)) {
				// Enable/Disable Building
				ToolMenu.Instance.PriorityScreen.Show(true);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.Priorities.ID);
			} else {
				// Enable/Disable Auto-Repair
				ToolMenu.Instance.PriorityScreen.Show(false);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID);
			}
		}
	}
}
