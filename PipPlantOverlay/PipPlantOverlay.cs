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

using PeterHan.PLib.Buildings;
using System.Collections.Generic;
using UnityEngine;

using PPO = PeterHan.PipPlantOverlay.PipPlantOverlayStrings.UI.OVERLAYS.PIPPLANTING;

namespace PeterHan.PipPlantOverlay {
	/// <summary>
	/// An overlay which shows valid locations for Pip planting.
	/// </summary>
	public class PipPlantOverlay : OverlayModes.Mode {
		/// <summary>
		/// The ID of this overlay mode.
		/// </summary>
		public static readonly HashedString ID = new HashedString("PIPPLANT");

		/// <summary>
		/// The current value of the Plants layer.
		/// </summary>
		private static readonly int PLANT_LAYER = (int)PBuilding.GetObjectLayer(nameof(
			ObjectLayer.Plants), ObjectLayer.Plants);

		/// <summary>
		/// Retrieves the overlay color for a particular cell when in the crop view.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>The overlay color for this cell.</returns>
		internal static Color GetColor(SimDebugView _, int cell) {
			var shade = Color.black;
			var colors = GlobalAssets.Instance.colorSet;
			var reason = Instance.cells[cell];
			switch (reason) {
			case PipPlantFailedReasons.PlantCount:
				shade = colors.cropGrowing;
				break;
			case PipPlantFailedReasons.NoPlantablePlot:
				break;
			case PipPlantFailedReasons.CanPlant:
				shade = colors.cropGrown;
				break;
			case PipPlantFailedReasons.Pressure:
				shade = colors.heatflowThreshold0;
				break;
			case PipPlantFailedReasons.Temperature:
			default:
				shade = colors.cropHalted;
				break;
			}
			return shade;
		}

		/// <summary>
		/// The instance of this class created by OverlayScreen.
		/// </summary>
		internal static PipPlantOverlay Instance { get; private set; }

		/// <summary>
		/// The types of objects that are visible in the overlay.
		/// </summary>
		private readonly int cameraLayerMask;

		/// <summary>
		/// The cells used for the overlay.
		/// </summary>
		private readonly PipPlantFailedReasons[] cells;

		/// <summary>
		/// The conditions used to highlight plants that are in range of the current cell.
		/// </summary>
		private readonly OverlayModes.ColorHighlightCondition[] conditions;

		/// <summary>
		/// The target plants that are visible on screen.
		/// </summary>
		private readonly ICollection<Uprootable> layerTargets;

		/// <summary>
		/// The partitioner used to selectively iterate plants.
		/// </summary>
		private UniformGrid<Uprootable> partition;

		/// <summary>
		/// Cached legend colors used for pip planting.
		/// </summary>
		private readonly List<LegendEntry> pipPlantLegend;

		/// <summary>
		/// A collection of all prefab IDs that are considered valid plants.
		/// </summary>
		private readonly ICollection<Tag> plants;

		/// <summary>
		/// The types of objects that can be selected in the overlay.
		/// </summary>
		private readonly int selectionMask;

		/// <summary>
		/// The layer to be used for the overlay.
		/// </summary>
		private readonly int targetLayer;

		public PipPlantOverlay() {
			var colors = GlobalAssets.Instance.colorSet;
			int pc = PipPlantOverlayTests.PlantCount;
			// Plural forms are annoying
			string plantCountText = string.Format((pc == 1) ? PPO.TOOLTIPS.PLANTCOUNT_1 :
				PPO.TOOLTIPS.PLANTCOUNT, pc, PipPlantOverlayTests.PlantRadius);
			cameraLayerMask = LayerMask.GetMask(new string[] {
				"MaskedOverlay",
				"MaskedOverlayBG"
			});
			cells = new PipPlantFailedReasons[Grid.CellCount];
			conditions = new OverlayModes.ColorHighlightCondition[] {
				new OverlayModes.ColorHighlightCondition(GetHighlightColor, ShouldHighlight)
			};
			layerTargets = new HashSet<Uprootable>();
			legendFilters = CreateDefaultFilters();
			Instance = this;
			PipPlantOverlayTests.UpdatePlantCriteria();
			pipPlantLegend = new List<LegendEntry>
			{
				new LegendEntry(PPO.CANPLANT, PPO.TOOLTIPS.CANPLANT,
					colors.cropGrown),
				new LegendEntry(PPO.HARDNESS, string.Format(PPO.TOOLTIPS.HARDNESS,
						GameUtil.Hardness.NEARLY_IMPENETRABLE),
					colors.cropHalted),
				new LegendEntry(PPO.PLANTCOUNT, plantCountText, colors.cropGrowing),
				new LegendEntry(PPO.PRESSURE, string.Format(PPO.TOOLTIPS.PRESSURE,
						GameUtil.GetFormattedMass(PipPlantOverlayTests.PRESSURE_THRESHOLD)),
					colors.heatflowThreshold0),
				new LegendEntry(PPO.TEMPERATURE, string.Format(PPO.TOOLTIPS.TEMPERATURE,
						GameUtil.GetFormattedTemperature(PipPlantOverlayTests.TEMP_MIN),
						GameUtil.GetFormattedTemperature(PipPlantOverlayTests.TEMP_MAX)),
					colors.cropHalted)
			};
			partition = null;
			plants = new HashSet<Tag>(Assets.GetPrefabTagsWithComponent<Uprootable>());
			selectionMask = LayerMask.GetMask(new string[] {
				"MaskedOverlay"
			});
			targetLayer = LayerMask.NameToLayer("MaskedOverlay");
		}

		public override void Disable() {
			UnregisterSaveLoadListeners();
			DisableHighlightTypeOverlay(layerTargets);
			CameraController.Instance.ToggleColouredOverlayView(false);
			Camera.main.cullingMask &= ~cameraLayerMask;
			partition?.Clear();
			layerTargets.Clear();
			SelectTool.Instance.ClearLayerMask();
			base.Disable();
		}

		public override void Enable() {
			base.Enable();
			RegisterSaveLoadListeners();
			partition = PopulatePartition<Uprootable>(plants);
			CameraController.Instance.ToggleColouredOverlayView(true);
			Camera.main.cullingMask |= cameraLayerMask;
			SelectTool.Instance.SetLayerMask(selectionMask);
		}

		public override List<LegendEntry> GetCustomLegendData() {
			return pipPlantLegend;
		}

		/// <summary>
		/// Calculates the color to tint the plants found by the overlay.
		/// </summary>
		/// <returns>The color to tint the plant - red if too many, green if OK.</returns>
		private Color GetHighlightColor(KMonoBehaviour _) {
			var color = Color.black;
			// Same method as used by the decor overlay
			int mouseCell = Grid.PosToCell(CameraController.Instance.baseCamera.
				ScreenToWorldPoint(KInputManager.GetMousePos()));
			var colors = GlobalAssets.Instance.colorSet;
			if (Grid.IsValidCell(mouseCell))
				color = (cells[mouseCell] == PipPlantFailedReasons.PlantCount) ?
					colors.cropHalted : colors.cropGrown;
			return color;
		}

		public override string GetSoundName() {
			return "Harvest";
		}

		protected override void OnSaveLoadRootRegistered(SaveLoadRoot root) {
			// Add new plants to partitioner
			var tag = root.GetComponent<KPrefabID>().GetSaveLoadTag();
			if (plants.Contains(tag)) {
				var uprootable = root.GetComponent<Uprootable>();
				if (uprootable != null)
					partition.Add(uprootable);
			}
		}

		protected override void OnSaveLoadRootUnregistered(SaveLoadRoot root) {
			// Remove plants from partitioner if they die
			if (root != null && root.gameObject != null) {
				var uprootable = root.GetComponent<Uprootable>();
				if (uprootable != null) {
					layerTargets.Remove(uprootable);
					partition.Remove(uprootable);
				}
			}
		}

		/// <summary>
		/// Calculates if the specified plant should be tinted.
		/// </summary>
		/// <param name="plant">The plant that was found.</param>
		/// <returns>Whether the plant is in range of the cell under the cursor.</returns>
		private bool ShouldHighlight(KMonoBehaviour plant) {
			bool hl = false;
			int plantCell;
			if (plant != null && Grid.IsValidCell(plantCell = Grid.PosToCell(plant))) {
				// Same method as used by the decor overlay
				int mouseCell = Grid.PosToCell(CameraController.Instance.baseCamera.
					ScreenToWorldPoint(KInputManager.GetMousePos()));
				// It goes from [x - radius, y - radius] to (x + radius, y + radius)
				if (Grid.IsValidCell(mouseCell) && cells[mouseCell] != PipPlantFailedReasons.
						NoPlantablePlot) {
					int radius = PipPlantOverlayTests.PlantRadius;
					var area = plant.GetComponent<OccupyArea>();
					Grid.CellToXY(mouseCell, out int mouseX, out int mouseY);
					Grid.CellToXY(plantCell, out int x, out int y);
					int startX = x, startY = y;
					// Need to expand the test area by the plant's size since the top tile is
					// just as valid of an obstruction as the bottom
					if (area != null) {
						var extents = area.GetExtents();
						startX = extents.x;
						startY = extents.y;
						x = extents.x + extents.width - 1;
						y = extents.y + extents.height - 1;
					}
					if (PipPlantOverlayTests.SymmetricalRadius) {
						startX--;
						startY--;
					}
					// Symmetrical radius adds another tile on the right and top to match
					hl = mouseX > startX - radius && mouseX <= x + radius && mouseY > startY -
						radius && mouseY <= y + radius;
				}
			}
			return hl;
		}

		public override void Update() {
			int x1, x2, y1, y2;
			var intersecting = HashSetPool<Uprootable, PipPlantOverlay>.Allocate();
			base.Update();
			// SimDebugView is updated on a background thread, so since plant checking
			// must be done on the FG thread, it is updated here
			Grid.GetVisibleExtents(out Vector2I min, out Vector2I max);
			x1 = min.x; x2 = max.x;
			y1 = min.y; y2 = max.y;
			// Refresh plant list with plants on the screen
			RemoveOffscreenTargets(layerTargets, min, max, null);
			partition.GetAllIntersecting(new Vector2(x1, y1), new Vector2(x2, y2),
				intersecting);
			foreach (var uprootable in intersecting)
				AddTargetIfVisible(uprootable, min, max, layerTargets, targetLayer);
			for (int y = y1; y <= y2; y++)
				for (int x = x1; x <= x2; x++) {
					int cell = Grid.XYToCell(x, y);
					if (Grid.IsValidCell(cell))
						cells[cell] = PipPlantOverlayTests.CheckCell(cell);
				}
			UpdateHighlightTypeOverlay(min, max, layerTargets, plants, conditions,
				OverlayModes.BringToFrontLayerSetting.Constant, targetLayer);
			intersecting.Recycle();
		}

		public override HashedString ViewMode() {
			return ID;
		}
	}
}
