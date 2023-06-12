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

using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ShowRange {
	/// <summary>
	/// Shows the range which must be clear for the Space Scanner.
	///
	/// TODO: Legacy code for versions less than 559498
	/// </summary>
	internal sealed class SpaceScannerVisualizer : ColoredRangeVisualizer {
		/// <summary>
		/// Pattern was changed from asymmetrical and 253 light to symmetrical and 1 light in
		/// this version.
		/// </summary>
		public const uint PATTERN_CHANGED_VERSION = 534889U;

		/// <summary>
		/// The color used on tiles when the scanner is blocked.
		/// </summary>
		private static readonly Color BLOCKED_TINT = new Color(1.0f, 0.5f, 0.5f);

		/// <summary>
		/// The color used on tiles when the scanner is interfering with heavy machinery.
		/// </summary>
		private static readonly Color INTERFERE_TINT = Color.red;

		/// <summary>
		/// Reflects the private field for detector radius on space scanners.
		/// </summary>
		private static readonly IDetouredField<CometDetector.Instance, int> RADIUS_FIELD =
			PDetours.DetourFieldLazy<CometDetector.Instance, int>("INTERFERENCE_RADIUS");

		/// <summary>
		/// Creates a new space scanner visualizer.
		/// </summary>
		/// <param name="template">The parent game object.</param>
		public static void Create(GameObject template) {
			if (template.TryGetComponent(out KPrefabID prefabID))
				prefabID.instantiateFn += (obj) => obj.AddOrGet<SpaceScannerVisualizer>();
		}

		/// <summary>
		/// Reports whether sunlight is blocked from the given cell.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>true if sunlight cannot shine on that cell, or false otherwise.</returns>
		internal static bool IsSunBlocked(int cell) {
			Element element;
			int exposed = Grid.ExposedToSunlight[cell];
			// TODO Remove when versions less than 534889 no longer need to be supported
			return (PUtil.GameVersion < PATTERN_CHANGED_VERSION ? exposed < Sim.
				ClearSkyGridValue || (element = Grid.Element[cell]) == null || element.
				IsSolid : exposed < 1);
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
				interferenceRadius = RADIUS_FIELD.Get(null);
			} catch (Exception) {
				PUtil.LogWarning("Unable to determine space scanner radius, using default");
			}
		}

		protected override void VisualizeCells(ICollection<VisCellData> newCells) {
			var go = gameObject;
			var basePos = go.transform.GetPosition();
			int baseCell = Grid.PosToCell(basePos);
			if (Grid.IsValidCell(baseCell)) {
				// TODO Remove when versions less than 534889 no longer need to be supported
				int min = -interferenceRadius, max = interferenceRadius - 1;
				if (PUtil.GameVersion >= PATTERN_CHANGED_VERSION)
					max += 2;
				// Currently scanners cannot be rotated in game, but maybe rotate everything
				// will allow it?
				for (int x = min; x < max; x++)
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
				float radSq = interferenceRadius * interferenceRadius;
				// Use the partitioner instead of iterating each cell, more efficient
				GameScenePartitioner.Instance.GatherEntries(extents, GameScenePartitioner.
					Instance.industrialBuildings, machinery);
				foreach (var entry in machinery)
					if (entry.obj is GameObject machineObj && machineObj != null &&
							machineObj != go) {
						// Machinery factor is actually based on true distance
						var delta3 = machineObj.transform.GetPosition() - basePos;
						float x = delta3.x, y = delta3.y;
						if ((x * x + y * y) < radSq)
							// Highlight the offending heavy machinery - when placing, the
							// interference preview is white since overlay mode dims the
							// screen and makes red hard to see
							newCells.Add(new VisCellData(Grid.PosToCell(machineObj),
								preview == null ? INTERFERE_TINT : Color.white));
					}
				machinery.Recycle();
			}
		}
	}
}
