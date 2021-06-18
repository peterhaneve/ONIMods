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

using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// A tool which can change the settings of many buildings at once. Supports enable/disable
	/// disinfect, enable/disable auto repair, and enable/disable building.
	/// </summary>
	internal sealed class BulkChangeTool : DragTool {
		#region Reflection
		// Detours for private fields in InterfaceTool
		private static readonly IDetouredField<DragTool, GameObject> AREA_VISUALIZER =
			PDetours.DetourField<DragTool, GameObject>("areaVisualizer");
		private static readonly IDetouredField<DragTool, GameObject> AREA_VISUALIZER_TEXT_PREFAB =
			PDetours.DetourField<DragTool, GameObject>("areaVisualizerTextPrefab");
		private static readonly IDetouredField<DragTool, Texture2D> BOX_CURSOR =
			PDetours.DetourField<DragTool, Texture2D>("boxCursor");
		private static readonly IDetouredField<InterfaceTool, Texture2D> CURSOR =
			PDetours.DetourField<InterfaceTool, Texture2D>(nameof(cursor));

		/// <summary>
		/// Reports the status of auto-disinfection.
		/// </summary>
		private static readonly IDetouredField<AutoDisinfectable, bool> DISINFECT_AUTO =
			PDetours.DetourField<AutoDisinfectable, bool>("enableAutoDisinfect");

		/// <summary>
		/// Disables automatic disinfect.
		/// </summary>
		private static readonly Action<AutoDisinfectable> DISINFECT_DISABLE =
			typeof(AutoDisinfectable).Detour<Action<AutoDisinfectable>>("DisableAutoDisinfect");

		/// <summary>
		/// Enables automatic disinfect.
		/// </summary>
		private static readonly Action<AutoDisinfectable> DISINFECT_ENABLE =
			typeof(AutoDisinfectable).Detour<Action<AutoDisinfectable>>("EnableAutoDisinfect");

		/// <summary>
		/// The empty storage chore if one is active.
		/// </summary>
		private static readonly IDetouredField<DropAllWorkable, Chore> EMPTY_CHORE =
			PDetours.DetourField<DropAllWorkable, Chore>("Chore");

		/// <summary>
		/// Enables or disables a building.
		/// </summary>
		private static readonly Action<BuildingEnabledButton> ENABLE_DISABLE =
			typeof(BuildingEnabledButton).Detour<Action<BuildingEnabledButton>>("OnMenuToggle");

		/// <summary>
		/// Reports the current enable/disable status of a building.
		/// </summary>
		private static readonly IDetouredField<BuildingEnabledButton, int> ENABLE_TOGGLEIDX =
			PDetours.DetourField<BuildingEnabledButton, int>("ToggleIdx");

		/// <summary>
		/// Disables auto-repair.
		/// </summary>
		private static readonly Action<Repairable> REPAIR_DISABLE =
			typeof(Repairable).Detour<Action<Repairable>>("CancelRepair");

		/// <summary>
		/// Enables auto-repair.
		/// </summary>
		private static readonly Action<Repairable> REPAIR_ENABLE =
			typeof(Repairable).Detour<Action<Repairable>>("AllowRepair");

		/// <summary>
		/// The state machine instance for repairable objects.
		/// </summary>
		private static readonly IDetouredField<Repairable, Repairable.SMInstance> REPAIR_SMI =
			PDetours.DetourField<Repairable, Repairable.SMInstance>("smi");
		#endregion

		/// <summary>
		/// The color to use for this tool's placer icon.
		/// </summary>
		private static readonly Color32 TOOL_COLOR = new Color32(255, 172, 52, 255);

		/// <summary>
		/// A version of Compostable.OnToggleCompost that does not crash if the select tool
		/// is not in use.
		/// </summary>
		/// <param name="comp">The item to toggle compost.</param>
		private static void DoToggleCompost(Compostable comp) {
			var obj = comp.gameObject;
			var pickupable = obj.GetComponent<Pickupable>();
			if (comp.isMarkedForCompost)
				EntitySplitter.Split(pickupable, pickupable.TotalAmount, comp.originalPrefab);
			else {
				pickupable.storage?.Drop(obj, true);
				EntitySplitter.Split(pickupable, pickupable.TotalAmount, comp.compostPrefab);
			}
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
			PGameUtils.CreatePopup(enable ? PopFXManager.Instance.sprite_Plus : PopFXManager.
				Instance.sprite_Negative, enable ? enabled.PopupText : disabled.PopupText,
				cell);
		}

		/// <summary>
		/// The last selected tool option.
		/// </summary>
		private string lastSelected;

		/// <summary>
		/// The number of object layers, determined at RUNTIME.
		/// </summary>
		private readonly int numObjectLayers;

		/// <summary>
		/// The layer to check for dropped items.
		/// </summary>
		private readonly int pickupableLayer;

		/// <summary>
		/// The options available for this tool.
		/// </summary>
		private IDictionary<string, ToolParameterMenu.ToggleState> options;

		public BulkChangeTool() {
			numObjectLayers = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.NumLayers),
				ObjectLayer.NumLayers);
			pickupableLayer = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Pickupables),
				ObjectLayer.Pickupables);
		}

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
			var modes = ListPool<PToolMode, BulkChangeTool>.Allocate();
			base.OnActivateTool();
			// Create mode list
			foreach (var mode in BulkToolMode.AllTools()) {
				bool select = mode.Key == lastSelected || (lastSelected == null && modes.
					Count == 0);
				modes.Add(mode.ToToolMode(select ? ToolParameterMenu.ToggleState.On :
					ToolParameterMenu.ToggleState.Off));
			}
			options = PToolMode.PopulateMenu(menu, modes);
			modes.Recycle();
			// When the parameters are changed, update the view settings
			menu.onParametersChanged += UpdateViewMode;
			SetMode(Mode.Box);
			UpdateViewMode();
		}

		protected override void OnCleanUp() {
			base.OnCleanUp();
			lastSelected = null;
			PUtil.LogDebug("Destroying BulkChangeTool");
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			base.OnDeactivateTool(newTool);
			var menu = ToolMenu.Instance.toolParameterMenu;
			// Unregister events
			menu.ClearMenu();
			menu.onParametersChanged -= UpdateViewMode;
			ToolMenu.Instance.PriorityScreen.Show(false);
		}

		protected override void OnDragTool(int cell, int distFromOrigin) {
			// Invoked when the tool drags over a cell
			if (Grid.IsValidCell(cell)) {
				bool enable = BulkChangeTools.EnableBuildings.IsOn(options), changed = false,
					disinfect = BulkChangeTools.EnableDisinfect.IsOn(options),
					repair = BulkChangeTools.EnableRepair.IsOn(options),
					empty = BulkChangeTools.EnableEmpty.IsOn(options),
					compost = BulkChangeTools.EnableCompost.IsOn(options);
#if DEBUG
				// Log what we are about to do
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("{0} at cell ({1:D},{2:D})".F(ToolMenu.Instance.
					toolParameterMenu.GetLastEnabledFilter(), xy.X, xy.Y));
#endif
				if (enable || BulkChangeTools.DisableBuildings.IsOn(options)) {
					// Enable/disable buildings
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleBuilding(cell, Grid.Objects[cell, i], enable);
					if (changed)
						ShowPopup(enable, BulkChangeTools.EnableBuildings, BulkChangeTools.
							DisableBuildings, cell);
				} else if (disinfect || BulkChangeTools.DisableDisinfect.IsOn(options)) {
					// Enable/disable disinfect
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleDisinfect(cell, Grid.Objects[cell, i], disinfect);
					if (changed)
						ShowPopup(disinfect, BulkChangeTools.EnableDisinfect, BulkChangeTools.
							DisableDisinfect, cell);
				} else if (repair || BulkChangeTools.DisableRepair.IsOn(options)) {
					// Enable/disable repair
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleRepair(cell, Grid.Objects[cell, i], repair);
					if (changed)
						ShowPopup(repair, BulkChangeTools.EnableRepair, BulkChangeTools.
							DisableRepair, cell);
				} else if (empty || BulkChangeTools.DisableEmpty.IsOn(options)) {
					// Enable/disable empty storage
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleEmptyStorage(cell, Grid.Objects[cell, i], empty);
					if (changed)
						ShowPopup(empty, BulkChangeTools.EnableEmpty, BulkChangeTools.
							DisableEmpty, cell);
				} else if (compost || BulkChangeTools.DisableCompost.IsOn(options)) {
					// Enable/disable compost
					if (ToggleCompost(cell, Grid.Objects[cell, pickupableLayer], compost))
						ShowPopup(compost, BulkChangeTools.EnableCompost, BulkChangeTools.
							DisableCompost, cell);
				}
			}
		}

		protected override void OnPrefabInit() {
			Sprite sprite;
			lastSelected = null;
			base.OnPrefabInit();
			gameObject.AddComponent<BulkChangeHover>();
			// Allow priority setting for the enable/disable building chores
			interceptNumberKeysForPriority = true;
			// HACK: Get the cursor from the disinfect tool
			var inst = DisinfectTool.Instance;
			cursor = CURSOR.Get(inst);
			BOX_CURSOR.Set(this, cursor);
			// HACK: Get the area visualizer from the disinfect tool
			var avTemplate = AREA_VISUALIZER.Get(inst);
			if (avTemplate != null) {
				var areaVisualizer = Util.KInstantiate(avTemplate, gameObject,
					"BulkChangeToolAreaVisualizer");
				areaVisualizer.SetActive(false);
				areaVisualizerSpriteRenderer = areaVisualizer.GetComponent<SpriteRenderer>();
				// The visualizer is private so we need to set it with reflection
				AREA_VISUALIZER.Set(this, areaVisualizer);
				AREA_VISUALIZER_TEXT_PREFAB.Set(this, AREA_VISUALIZER_TEXT_PREFAB.Get(inst));
			}
			visualizer = new GameObject("BulkChangeToolVisualizer");
			// Actually fix the position to not be off by a grid cell
			var offs = new GameObject("BulkChangeToolOffset");
			var offsTransform = offs.transform;
			offsTransform.SetParent(visualizer.transform);
			offsTransform.SetLocalPosition(new Vector3(0.0f, Grid.HalfCellSizeInMeters, 0.0f));
			offs.SetLayerRecursively(LayerMask.NameToLayer("Overlay"));
			var spriteRenderer = offs.AddComponent<SpriteRenderer>();
			// Set up the color and parent
			if (spriteRenderer != null && (sprite = SpriteRegistry.GetPlaceIcon()) != null) {
				// Determine the scaling amount since pixel size is known
				float widthInM = sprite.texture.width / sprite.pixelsPerUnit,
					scaleWidth = Grid.CellSizeInMeters / widthInM;
				spriteRenderer.name = "BulkChangeToolSprite";
				// Set sprite color to match other placement tools
				spriteRenderer.color = TOOL_COLOR;
				spriteRenderer.sprite = sprite;
				spriteRenderer.enabled = true;
				// Set scale to match 1 tile
				offsTransform.localScale = new Vector3(scaleWidth, scaleWidth, 1.0f);
			}
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
			var ed = building.GetComponentSafe<BuildingEnabledButton>();
			bool changed = false;
			if (ed != null && ENABLE_TOGGLEIDX != null) {
				int toggleIndex = ENABLE_TOGGLEIDX.Get(ed);
				// Check to see if a work errand is pending
				bool curEnabled = ed.IsEnabled, toggleQueued = building.GetComponent<
					Toggleable>()?.IsToggleQueued(toggleIndex) ?? false;
#if false
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("Checking building @({0:D},{1:D}): on={2}, queued={3}, " +
					"desired={4}".F(xy.X, xy.Y, curEnabled, toggleQueued, enable));
#endif
				// Only continue if we are cancelling the toggle errand or (the building state
				// is different than desired and no toggle errand is queued)
				if (toggleQueued != (curEnabled != enable)) {
					ENABLE_DISABLE(ed);
					// Set priority according to the chosen level
					var priority = building.GetComponent<Prioritizable>();
					if (priority != null)
						priority.SetMasterPriority(ToolMenu.Instance.PriorityScreen.
							GetLastSelectedPriority());
#if DEBUG
					var xy = Grid.CellToXY(cell);
					PUtil.LogDebug("Enable {3} @({0:D},{1:D}) = {2}".F(xy.X, xy.Y, enable,
						building.GetProperName()));
#endif
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles compost on the object.
		/// </summary>
		/// <param name="cell">The cell this object occupies.</param>
		/// <param name="item">The object to toggle.</param>
		/// <param name="enable">true to mark for compost, or false to unmark it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private bool ToggleCompost(int cell, GameObject item, bool enable) {
			var pickupable = item.GetComponentSafe<Pickupable>();
			bool changed = false;
			if (pickupable != null) {
				var objectListNode = pickupable.objectLayerListItem;
				while (objectListNode != null) {
					var comp = objectListNode.gameObject.GetComponentSafe<Compostable>();
					objectListNode = objectListNode.nextItem;
					// Duplicants cannot be composted... this is not Rim World
					if (comp != null && comp.isMarkedForCompost != enable) {
						// OnToggleCompost method causes a crash because select tool is not
						// active
						DoToggleCompost(comp);
#if DEBUG
						var xy = Grid.CellToXY(cell);
						PUtil.LogDebug("Compost {3} @({0:D},{1:D}) = {2}".F(xy.X, xy.Y,
							enable, item.GetProperName()));
#endif
						changed = true;
					}
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
			var ad = item.GetComponentSafe<AutoDisinfectable>();
			bool changed = false, status;
			// Private methods grrr
			if (ad != null && DISINFECT_AUTO != null && (status = DISINFECT_AUTO.Get(ad)) !=
					enable) {
				if (enable)
					DISINFECT_ENABLE(ad);
				else
					DISINFECT_DISABLE(ad);
#if DEBUG
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("Auto disinfect {3} @({0:D},{1:D}) = {2}".F(xy.X, xy.Y,
					enable, item.GetProperName()));
#endif
				changed = true;
			}
			return changed;
		}

		/// <summary>
		/// Toggles empty storage on the object.
		/// </summary>
		/// <param name="cell">The cell this building occupies.</param>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to schedule for emptying, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private bool ToggleEmptyStorage(int cell, GameObject item, bool enable) {
			var daw = item.GetComponentSafe<DropAllWorkable>();
			bool changed = false;
			if (daw != null && (EMPTY_CHORE.Get(daw) != null) != enable) {
				daw.DropAll();
#if DEBUG
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("Empty storage {3} @({0:D},{1:D}) = {2}".F(xy.X, xy.Y,
					enable, item.GetProperName()));
#endif
				changed = true;
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
			var ar = item.GetComponentSafe<Repairable>();
			bool changed = false;
			if (ar != null && REPAIR_SMI != null) {
				var smi = REPAIR_SMI.Get(ar);
				// Need to check the state machine directly
				var currentState = smi.GetCurrentState();
				// Prevent buildings in the allow state from being repaired again
				if (enable) {
					if (currentState == smi.sm.forbidden) {
						REPAIR_ENABLE(ar);
						changed = true;
					}
				} else if (currentState != smi.sm.forbidden) {
					REPAIR_DISABLE(ar);
					changed = true;
				}
#if DEBUG
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("Auto repair {3} @({0:D},{1:D}) = {2}".F(xy.X, xy.Y, enable,
					item.GetProperName()));
#endif
			}
			return changed;
		}

		/// <summary>
		/// Based on the current tool mode, updates the overlay mode.
		/// </summary>
		private void UpdateViewMode() {
			foreach (var option in options)
				if (option.Value == ToolParameterMenu.ToggleState.On) {
					lastSelected = option.Key;
					break;
				}
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
				// Enable/Disable Auto-Repair, Compost, Empty Storage
				ToolMenu.Instance.PriorityScreen.Show(false);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID);
			}
		}
	}
}
