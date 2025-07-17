/*
 * Copyright 2024 Peter Han
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
using PeterHan.PLib.Detours;
using System;
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

		/// <summary>
		/// Enables or disables tinker.
		/// </summary>
		private static readonly Action<Tinkerable> TINKER_TOGGLE =
			typeof(Tinkerable).Detour<Action<Tinkerable>>("UpdateChore");

		/// <summary>
		/// Reports the status of tinker.
		/// </summary>
		private static readonly IDetouredField<Tinkerable, bool> TINKER_FLAG =
			PDetours.DetourField<Tinkerable, bool>("userMenuAllowed");
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
			if (obj != null && obj.TryGetComponent(out Pickupable pickupable)) {
				if (comp.isMarkedForCompost)
					EntitySplitter.Split(pickupable, pickupable.TotalAmount, comp.
						originalPrefab);
				else {
					pickupable.storage?.Drop(obj);
					EntitySplitter.Split(pickupable, pickupable.TotalAmount, comp.
						compostPrefab);
				}
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
		/// Creates a settings chore to enable/disable the specified building.
		/// </summary>
		/// <param name="building">The building to toggle if it exists.</param>
		/// <param name="enable">true to enable it, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleBuilding(GameObject building, bool enable) {
			bool changed = false;
			if (building != null && building.TryGetComponent(out BuildingEnabledButton ed)) {
				if (ed != null && ENABLE_TOGGLEIDX != null) {
					int toggleIndex = ENABLE_TOGGLEIDX.Get(ed);
					// Check to see if a work errand is pending
					bool curEnabled = ed.IsEnabled, toggleQueued = building.TryGetComponent(
						out Toggleable toggle) && toggle.IsToggleQueued(toggleIndex);
					// Only continue if we are cancelling the toggle errand or (the building
					// state is different than desired and no toggle errand is queued)
					if (toggleQueued != (curEnabled != enable)) {
						ENABLE_DISABLE(ed);
						// Set priority according to the chosen level
						if (building.TryGetComponent(out Prioritizable priority))
							priority.SetMasterPriority(ToolMenu.Instance.PriorityScreen.
								GetLastSelectedPriority());
						changed = true;
					}
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles compost on the object.
		/// </summary>
		/// <param name="item">The object to toggle.</param>
		/// <param name="enable">true to mark for compost, or false to unmark it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleCompost(GameObject item, bool enable) {
			bool changed = false;
			if (item != null && item.TryGetComponent(out Pickupable pickupable)) {
				var objectListNode = pickupable.objectLayerListItem;
				while (objectListNode != null) {
					var go = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					// Duplicants cannot be composted... this is not Rim World
					if (go != null && go.TryGetComponent(out Compostable comp) && comp.
							isMarkedForCompost != enable) {
						// OnToggleCompost method causes a crash because select tool is not
						// active
						DoToggleCompost(comp);
						changed = true;
					}
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles auto disinfect on the object.
		/// </summary>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to enable auto disinfect, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleDisinfect(GameObject item, bool enable) {
			bool changed = false;
			// Private methods grrr
			if (item != null && item.TryGetComponent(out AutoDisinfectable ad) &&
					DISINFECT_AUTO != null && DISINFECT_AUTO.Get(ad) != enable) {
				if (enable)
					DISINFECT_ENABLE(ad);
				else
					DISINFECT_DISABLE(ad);
				changed = true;
			}
			return changed;
		}

		/// <summary>
		/// Toggles empty storage on the object.
		/// </summary>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to schedule for emptying, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleEmptyStorage(GameObject item, bool enable) {
			bool changed = false;
			if (item != null && item.TryGetComponent(out DropAllWorkable daw) && (EMPTY_CHORE.
					Get(daw) != null) != enable) {
				daw.DropAll();
				changed = true;
			}
			return changed;
		}
		
		/// <summary>
		/// Toggles forbid on the object.
		/// </summary>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to forbid, or false to reclaim.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleForbid(GameObject item, bool enable) {
			bool changed = false;
			var tag = BulkChangePatches.Forbidden;
			if (item != null && item.TryGetComponent(out Pickupable pickupable) &&
					BulkChangePatches.CanForbidItems) {
				var objectListNode = pickupable.objectLayerListItem;
				while (objectListNode != null) {
					var go = objectListNode.gameObject;
					objectListNode = objectListNode.nextItem;
					if (go != null && go.TryGetComponent(out KPrefabID kpid) && !kpid.HasTag(
							GameTags.Stored) && go.GetComponent("Forbiddable") != null &&
							enable != kpid.HasTag(tag)) {
						if (enable)
							kpid.AddTag(tag, true);
						else {
							// Order of operations bug in KPrefabID requires 2 tag removals
							// for serialized tags
							kpid.RemoveTag(tag);
							kpid.RemoveTag(tag);
						}
						changed = true;
					}
				}
			}
			return changed;
		}

		/// <summary>
		/// Toggles auto repair on the object.
		/// </summary>
		/// <param name="item">The item to toggle.</param>
		/// <param name="enable">true to enable auto repair, or false to disable it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleRepair(GameObject item, bool enable) {
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
			}
			return changed;
		}

		/// <summary>
		/// Toggles tinker on the specified building/plant.
		/// </summary>
		/// <param name="item">The building/plant to toggle if it exists.</param>
		/// <param name="enable">true to allow tinker, or false to disallow it.</param>
		/// <returns>true if changes were made, or false otherwise.</returns>
		private static bool ToggleTinker(GameObject item, bool enable) {
			bool changed = false;
			if (item != null && item.TryGetComponent(out Tinkerable tinkerableComponent)) {
				if (tinkerableComponent != null && TINKER_FLAG != null) {
					if (TINKER_FLAG.Get(tinkerableComponent) != enable && TINKER_TOGGLE != null) { 
						TINKER_FLAG.Set(tinkerableComponent, enable);
						TINKER_TOGGLE(tinkerableComponent);
						changed = true;
					}
				}
			}
			return changed;
		}

		/// <summary>
		/// Based on the current tool mode, updates the overlay mode.
		/// </summary>
		internal static void UpdateViewMode() {
			var inst = BulkParameterMenu.Instance;
			if (BulkChangeTools.EnableDisinfect.IsOn(inst) || BulkChangeTools.
					DisableDisinfect.IsOn(inst)) {
				// Enable/Disable Disinfect
				ToolMenu.Instance.PriorityScreen.Show(false);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.Disease.ID);
			} else if (BulkChangeTools.EnableBuildings.IsOn(inst) || BulkChangeTools.
					DisableBuildings.IsOn(inst)) {
				// Enable/Disable Building
				ToolMenu.Instance.PriorityScreen.Show();
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.Priorities.ID);
			} else {
				// Enable/Disable Auto-Repair, Compost, Empty Storage
				ToolMenu.Instance.PriorityScreen.Show(false);
				OverlayScreen.Instance.ToggleOverlay(OverlayModes.None.ID);
			}
		}

		/// <summary>
		/// The number of object layers, determined at RUNTIME.
		/// </summary>
		private readonly int numObjectLayers;

		/// <summary>
		/// The layer to check for dropped items.
		/// </summary>
		private readonly int pickupableLayer;

		public BulkChangeTool() {
			numObjectLayers = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.NumLayers),
				ObjectLayer.NumLayers);
			pickupableLayer = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Pickupables),
				ObjectLayer.Pickupables);
		}

		protected override string GetConfirmSound() {
			string sound = base.GetConfirmSound();
			var menu = BulkParameterMenu.Instance;
			// "Enable" use default, "Disable" uses cancel sound
			if (BulkChangeTools.DisableBuildings.IsOn(menu) || BulkChangeTools.
					DisableDisinfect.IsOn(menu) || BulkChangeTools.DisableRepair.IsOn(menu))
				sound = "Tile_Confirm_NegativeTool";
			return sound;
		}

		protected override string GetDragSound() {
			string sound = base.GetDragSound();
			var menu = BulkParameterMenu.Instance;
			// "Enable" use default, "Disable" uses cancel sound
			if (BulkChangeTools.DisableBuildings.IsOn(menu) || BulkChangeTools.
					DisableDisinfect.IsOn(menu) || BulkChangeTools.DisableRepair.IsOn(menu))
				sound = "Tile_Drag_NegativeTool";
			return sound;
		}

		protected override void OnActivateTool() {
			var menu = BulkParameterMenu.Instance;
			base.OnActivateTool();
			if (!menu.HasOptions)
				menu.PopulateMenu(BulkToolMode.AllTools());
			menu.ShowMenu();
			SetMode(Mode.Box);
			UpdateViewMode();
		}

		protected override void OnCleanUp() {
			base.OnCleanUp();
			PUtil.LogDebug("Destroying BulkChangeTool");
		}

		protected override void OnDeactivateTool(InterfaceTool newTool) {
			base.OnDeactivateTool(newTool);
			BulkParameterMenu.Instance.HideMenu();
			ToolMenu.Instance.PriorityScreen.Show(false);
		}

		protected override void OnDragTool(int cell, int distFromOrigin) {
			var menu = BulkParameterMenu.Instance;
			// Invoked when the tool drags over a cell
			if (Grid.IsValidCell(cell)) {
				bool enable = BulkChangeTools.EnableBuildings.IsOn(menu), changed = false,
					disinfect = BulkChangeTools.EnableDisinfect.IsOn(menu),
					repair = BulkChangeTools.EnableRepair.IsOn(menu),
					empty = BulkChangeTools.EnableEmpty.IsOn(menu),
					compost = BulkChangeTools.EnableCompost.IsOn(menu),
					tinker = BulkChangeTools.EnableTinker.IsOn(menu),
					forbid = BulkChangeTools.DisablePickup.IsOn(menu);
#if DEBUG
				// Log what we are about to do
				var xy = Grid.CellToXY(cell);
				PUtil.LogDebug("{0} at cell ({1:D},{2:D})".F(ToolMenu.Instance.
					toolParameterMenu.GetLastEnabledFilter(), xy.X, xy.Y));
#endif
				if (enable || BulkChangeTools.DisableBuildings.IsOn(menu)) {
					// Enable/disable buildings
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleBuilding(Grid.Objects[cell, i], enable);
					if (changed)
						ShowPopup(enable, BulkChangeTools.EnableBuildings, BulkChangeTools.
							DisableBuildings, cell);
				} else if (disinfect || BulkChangeTools.DisableDisinfect.IsOn(menu)) {
					// Enable/disable disinfect
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleDisinfect(Grid.Objects[cell, i], disinfect);
					if (changed)
						ShowPopup(disinfect, BulkChangeTools.EnableDisinfect, BulkChangeTools.
							DisableDisinfect, cell);
				} else if (repair || BulkChangeTools.DisableRepair.IsOn(menu)) {
					// Enable/disable repair
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleRepair(Grid.Objects[cell, i], repair);
					if (changed)
						ShowPopup(repair, BulkChangeTools.EnableRepair, BulkChangeTools.
							DisableRepair, cell);
				} else if (empty || BulkChangeTools.DisableEmpty.IsOn(menu)) {
					// Enable/disable empty storage
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleEmptyStorage(Grid.Objects[cell, i], empty);
					if (changed)
						ShowPopup(empty, BulkChangeTools.EnableEmpty, BulkChangeTools.
							DisableEmpty, cell);
				} else if (compost || BulkChangeTools.DisableCompost.IsOn(menu)) {
					// Enable/disable compost
					if (ToggleCompost(Grid.Objects[cell, pickupableLayer], compost))
						ShowPopup(compost, BulkChangeTools.EnableCompost, BulkChangeTools.
							DisableCompost, cell);
				} else if (tinker || BulkChangeTools.DisableTinker.IsOn(menu)) {
					// Allow/disallow tinker
					for (int i = 0; i < numObjectLayers; i++)
						changed |= ToggleTinker(Grid.Objects[cell, i], tinker);
					if (changed)
						ShowPopup(tinker, BulkChangeTools.EnableTinker, BulkChangeTools.
							DisableTinker, cell);
				} else if (forbid || BulkChangeTools.EnablePickup.IsOn(menu)) {
					// Enable/disable forbid (yeah the tool names are suboptimal)
					if (ToggleForbid(Grid.Objects[cell, pickupableLayer], forbid))
						ShowPopup(forbid, BulkChangeTools.DisablePickup, BulkChangeTools.
							EnablePickup, cell);
				}
			}
		}

		protected override void OnPrefabInit() {
			Sprite sprite;
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
	}
}
