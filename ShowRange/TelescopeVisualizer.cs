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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ShowRange {
    /// <summary>
	/// Shows the range which must be clear for the Telescope.
	/// </summary>
	internal sealed class TelescopeVisualizer : ColoredRangeVisualizer {
		/// <summary>
		/// The color used on tiles when the telescope is blocked.
		/// </summary>
		private static readonly Color BLOCKED_TINT = new Color(1.0f, 0.5f, 0.5f);

		/// <summary>
		/// Creates a new telescope visualizer.
		/// </summary>
		/// <param name="template">The parent game object.</param>
		public static void Create(GameObject template) {
			var prefabID = template.GetComponentSafe<KPrefabID>();
			if (prefabID != null)
				prefabID.instantiateFn += (obj) => obj.AddComponent<TelescopeVisualizer>();
		}

		// These components are automatically populated by KMonoBehaviour
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		private Telescope telescope;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// The radius within which objects can block the telescope.
		/// </summary>
		private int scanRadius;

		internal TelescopeVisualizer() {
			// Is hard coded in TelescopeConfig, will be updated using the Telescope object if
			// available
			scanRadius = 5;
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			// Only available on completed buildings
			if (telescope != null)
				scanRadius = telescope.clearScanCellRadius;
		}

		protected override void VisualizeCells(ICollection<VisCellData> newCells) {
			int baseCell = Grid.PosToCell(gameObject), width = Grid.WidthInCells;
			if (Grid.IsValidCell(baseCell)) {
				Grid.CellToXY(baseCell, out int startX, out int startY);
				// Currently telescopes cannot be rotated in game
				for (int x = -scanRadius; x <= scanRadius; x++)
					// Up to height limit
					for (int y = startY + 4; y < Grid.HeightInCells; y++) {
						int newX = startX + x - 1, cell = Grid.XYToCell(newX, y);
						if (Grid.IsValidCell(cell) && newX >= 0 && newX <= width)
							newCells.Add(new VisCellData(cell, SpaceScannerVisualizer.
								IsSunBlocked(cell) ? BLOCKED_TINT : Color.white));
					}
			}
		}
	}
}
