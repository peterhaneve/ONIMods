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

using PeterHan.PLib;
using PeterHan.PLib.Buildings;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.ShowRange {
	/// <summary>
	/// Displays the building range for element consumers.
	/// </summary>
	internal sealed class ElementConsumerVisualizer : ColoredRangeVisualizer {
		/// <summary>
		/// Creates a new element consumer range finder.
		/// </summary>
		/// <param name="template">The parent game object.</param>
		/// <param name="offset">The offset from the object's home cell where elements are
		/// consumed.</param>
		/// <param name="radius">The element consumer's radius.</param>
		/// <param name="color">The color to use when displaying the range.</param>
		public static void Create(GameObject template, CellOffset offset, int radius,
				Color color = default) {
			if (color == default)
				color = Color.white;
			if (template == null)
				throw new ArgumentNullException("template");
			if (radius > 0) {
				var prefabID = template.GetComponent<KPrefabID>();
				if (prefabID != null)
					// Only when instantiated, not on the template
					prefabID.instantiateFn += (obj) => {
						var visualizer = obj.AddComponent<ElementConsumerVisualizer>();
						visualizer.color = color;
						visualizer.offset = offset;
						visualizer.radius = radius;
					};
			}
		}

		private static void EnqueueIfPassable(ICollection<int> seen, int newCell, int cost,
				PriorityDictionary<int, int> queue) {
			if (Grid.IsValidCell(newCell) && Grid.Element[newCell]?.IsSolid == false &&
					!seen.Contains(newCell)) {
				seen.Add(newCell);
				queue.Enqueue(cost, newCell);
			}
		}

		/// <summary>
		/// The tint color to use when displaying the visualization.
		/// </summary>
		private Color color;

		/// <summary>
		/// The offset from the object's home cell where elements are consumed.
		/// </summary>
		private CellOffset offset;

		/// <summary>
		/// The effective radius of the element consumer.
		/// </summary>
		private int radius;

		internal ElementConsumerVisualizer() {
			color = Color.white;
			offset = new CellOffset();
			radius = 1;
		}

		protected override void VisualizeCells(ICollection<VisCellData> newCells) {
			// Rotation is only used to rotate the offset, radius is the same in all directions
			int startCell = RotateOffsetCell(Grid.PosToCell(gameObject), offset);
			if (Grid.IsValidCell(startCell) && Grid.Element[startCell]?.IsSolid == false) {
				var queue = new PriorityDictionary<int, int>(radius * radius);
				// Initial cell is seen
				var seen = HashSetPool<int, ElementConsumerVisualizer>.Allocate();
				try {
					queue.Enqueue(0, startCell);
					seen.Add(startCell);
					// Dijkstra's algorithm
					do {
						queue.Dequeue(out int cost, out int newCell);
						if (cost < radius - 1) {
							// Cardinal directions
							EnqueueIfPassable(seen, Grid.CellLeft(newCell), cost + 1, queue);
							EnqueueIfPassable(seen, Grid.CellRight(newCell), cost + 1, queue);
							EnqueueIfPassable(seen, Grid.CellAbove(newCell), cost + 1, queue);
							EnqueueIfPassable(seen, Grid.CellBelow(newCell), cost + 1, queue);
						}
					} while (queue.Count > 0);
					// Add all cells as normal color
					foreach (var cell in seen)
						newCells.Add(new VisCellData(cell, color));
				} finally {
					seen.Recycle();
				}
			}
		}
	}
}
