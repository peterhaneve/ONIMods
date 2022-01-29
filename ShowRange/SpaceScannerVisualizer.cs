/*
 * Copyright 2022 Peter Han
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
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ShowRange {
	/// <summary>
	/// Shows the range which must be clear for the Space Scanner.
	/// </summary>
	internal sealed class SpaceScannerVisualizer : ColoredRangeVisualizer {
		/// <summary>
		/// The color used on tiles when the scanner is blocked.
		/// </summary>
		private static readonly Color BLOCKED_TINT = new Color(1.0f, 0.5f, 0.5f);

		/// <summary>
		/// The color used on tiles when the scanner is interfering with heavy machinery.
		/// </summary>
		private static readonly Color INTERFERE_TINT = Color.red;

		/// <summary>
		/// Creates a new space scanner visualizer.
		/// </summary>
		/// <param name="template">The parent game object.</param>
		public static void Create(GameObject template) {
			var prefabID = template.GetComponentSafe<KPrefabID>();
			if (prefabID != null)
				prefabID.instantiateFn += (obj) => obj.AddComponent<SpaceScannerVisualizer>();
		}

		/// <summary>
		/// Reports whether sunlight is blocked from the given cell.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>true if sunlight cannot shine fully on that cell, or false otherwise.</returns>
		internal static bool IsSunBlocked(int cell) {
			return Grid.ExposedToSunlight[cell] < Sim.ClearSkyGridValue || Grid.Element[cell]?.
				IsSolid != false;
		}

		/// <summary>
		/// The radius within which heavy machinery will interfere with the scanner.
		/// </summary>
		private int interferenceRadius;

		internal SpaceScannerVisualizer() {
			interferenceRadius = 15;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			// Update the radius from CometDetector, but it is private :(
			try {
				interferenceRadius = (int)PPatchTools.GetFieldSafe(typeof(CometDetector.
					Instance), "INTERFERENCE_RADIUS", true).GetValue(null);
			} catch (Exception) {
				PUtil.LogWarning("Unable to determine space scanner radius, using default");
			}
		}

		protected override void VisualizeCells(ICollection<VisCellData> newCells) {
			var basePos = gameObject.transform.GetPosition();
			int baseCell = Grid.PosToCell(basePos);
			if (Grid.IsValidCell(baseCell)) {
				// Currently scanners cannot be rotated in game, but maybe rotate everything
				// will allow it?
				for (int x = -interferenceRadius; x < interferenceRadius - 1; x++)
					for (int y = Math.Abs(x); y <= interferenceRadius; y++) {
						int cell = RotateOffsetCell(baseCell, new CellOffset(x, y + 1));
						if (Grid.IsValidCell(cell))
							newCells.Add(new VisCellData(cell, IsSunBlocked(cell) ?
								BLOCKED_TINT : Color.white));
					}
				// Scan for heavy machinery
				var extents = new Extents(baseCell, interferenceRadius);
				var machinery = ListPool<ScenePartitionerEntry, SpaceScannerVisualizer>.
					Allocate();
				try {
					Vector2 delta2 = default;
					// Use the partitioner instead of iterating each cell, more efficient
					GameScenePartitioner.Instance.GatherEntries(extents, GameScenePartitioner.
						Instance.industrialBuildings, machinery);
					foreach (var entry in machinery) {
						var machineObj = (GameObject)entry.obj;
						if (machineObj != null && machineObj != gameObject) {
							// Machinery factor is actually based on true distance
							var delta3 = machineObj.transform.GetPosition() - basePos;
							delta2.x = delta3.x;
							delta2.y = delta3.y;
							if (delta2.magnitude < interferenceRadius)
								// Highlight the offending heavy machinery - when placing, the
								// interference preview is white since overlay mode dims the
								// screen and makes red hard to see
								newCells.Add(new VisCellData(Grid.PosToCell(machineObj),
									preview == null ? INTERFERE_TINT : Color.white));
						}
					}
				} finally {
					machinery.Recycle();
				}
			}
		}
	}
}
