/*
 * Copyright 2020 Peter Han
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// A visualizer that colors cells with an overlay when a building is selected or being
	/// previewed.
	/// </summary>
	public abstract class ColoredRangeVisualizer : KMonoBehaviour {
		/// <summary>
		/// The anim name to use when visualizing.
		/// </summary>
		private const string ANIM_NAME = "transferarmgrid_kanim";

		/// <summary>
		/// The animations to play when the visualization is created.
		/// </summary>
		private static readonly HashedString[] PRE_ANIMS = new HashedString[] {
			"grid_pre",
			"grid_loop"
		};

		/// <summary>
		/// The animation to play when the visualization is destroyed.
		/// </summary>
		private static readonly HashedString POST_ANIM = "grid_pst";

		/// <summary>
		/// The layer on which to display the visualizer.
		/// </summary>
		public Grid.SceneLayer Layer { get; set; }

		// These components are automatically populated by KMonoBehaviour
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		[MyCmpGet]
		protected BuildingPreview preview;

		[MyCmpGet]
		protected Rotatable rotatable;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// The cells where animations are being displayed.
		/// </summary>
		private readonly HashSet<VisCellData> cells;

		protected ColoredRangeVisualizer() {
			cells = new HashSet<VisCellData>();
			Layer = Grid.SceneLayer.FXFront;
		}

		/// <summary>
		/// Creates or updates the visualizers as necessary.
		/// </summary>
		private void CreateVisualizers() {
			var visCells = HashSetPool<VisCellData, ColoredRangeVisualizer>.Allocate();
			var newCells = ListPool<VisCellData, ColoredRangeVisualizer>.Allocate();
			try {
				if (gameObject != null)
					VisualizeCells(visCells);
				// Destroy cells that are not used in the new one
				foreach (var cell in cells)
					if (visCells.Remove(cell))
						newCells.Add(cell);
					else
						cell.Destroy();
				// Newcomers get their controller created and added to the list
				foreach (var newCell in visCells) {
					newCell.CreateController(Layer);
					newCells.Add(newCell);
				}
				// Copy back to global
				cells.Clear();
				foreach (var cell in newCells)
					cells.Add(cell);
			} finally {
				visCells.Recycle();
				newCells.Recycle();
			}
		}

		/// <summary>
		/// Called when cells are changed in the building radius.
		/// </summary>
		private void OnCellChange() {
			CreateVisualizers();
		}

		protected override void OnCleanUp() {
			Unsubscribe((int)GameHashes.SelectObject);
			if (preview != null) {
				Singleton<CellChangeMonitor>.Instance.UnregisterCellChangedHandler(transform,
					OnCellChange);
				if (rotatable != null)
					Unsubscribe((int)GameHashes.Rotated);
			}
			RemoveVisualizers();
			base.OnCleanUp();
		}

		/// <summary>
		/// Called when the object is rotated.
		/// </summary>
		private void OnRotated(object _) {
			CreateVisualizers();
		}

		/// <summary>
		/// Called when the object is selected.
		/// </summary>
		/// <param name="data">true if selected, or false if deselected.</param>
		private void OnSelect(object data) {
			if (data is bool selected) {
				var position = transform.position;
				// Play the appropriate sound and update the visualizers
				if (selected) {
					PUtil.PlaySound("RadialGrid_form", position);
					CreateVisualizers();
				} else {
					PUtil.PlaySound("RadialGrid_disappear", position);
					RemoveVisualizers();
				}
			}
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.SelectObject, OnSelect);
			if (preview != null) {
				// Previews can be moved
				Singleton<CellChangeMonitor>.Instance.RegisterCellChangedHandler(transform,
					OnCellChange, nameof(ColoredRangeVisualizer) + ".OnSpawn");
				if (rotatable != null)
					Subscribe((int)GameHashes.Rotated, OnRotated);
			}
		}

		/// <summary>
		/// Removes all of the visualizers.
		/// </summary>
		private void RemoveVisualizers() {
			foreach (var cell in cells)
				cell.Destroy();
			cells.Clear();
		}

		/// <summary>
		/// Calculates the offset cell from the specified starting point, including the
		/// rotation of this object.
		/// </summary>
		/// <param name="baseCell">The starting cell.</param>
		/// <param name="offset">The offset if the building had its default rotation.</param>
		/// <returns>The computed destination cell.</returns>
		protected int RotateOffsetCell(int baseCell, CellOffset offset) {
			if (rotatable != null)
				offset = rotatable.GetRotatedCellOffset(offset);
			return Grid.OffsetCell(baseCell, offset);
		}

		/// <summary>
		/// Called when cell visualizations need to be updated. Visualized cells should be
		/// added to the collection supplied as an argument.
		/// </summary>
		/// <param name="newCells">The cells which should be visualized.</param>
		protected abstract void VisualizeCells(ICollection<VisCellData> newCells);

		/// <summary>
		/// Stores the data about a particular cell, including its anim controller and tint
		/// color.
		/// </summary>
		protected sealed class VisCellData : IComparable<VisCellData> {
			/// <summary>
			/// The target cell.
			/// </summary>
			public int Cell { get; }

			/// <summary>
			/// The anim controller for this cell.
			/// </summary>
			public KBatchedAnimController Controller { get; private set; }

			/// <summary>
			/// The tint used for this cell.
			/// </summary>
			public Color Tint { get; }

			/// <summary>
			/// Creates a visualized cell.
			/// </summary>
			/// <param name="cell">The cell to visualize.</param>
			public VisCellData(int cell) : this(cell, Color.white) { }

			/// <summary>
			/// Creates a visualized cell.
			/// </summary>
			/// <param name="cell">The cell to visualize.</param>
			/// <param name="tint">The color to tint it.</param>
			public VisCellData(int cell, Color tint) {
				Cell = cell;
				Controller = null;
				Tint = tint;
			}

			public int CompareTo(VisCellData other) {
				if (other == null)
					throw new ArgumentNullException("other");
				return Cell.CompareTo(other.Cell);
			}

			/// <summary>
			/// Creates the anim controller for this cell.
			/// </summary>
			/// <param name="sceneLayer">The layer on which to display the animation.</param>
			public void CreateController(Grid.SceneLayer sceneLayer) {
				Controller = FXHelpers.CreateEffect(ANIM_NAME, Grid.CellToPosCCC(Cell,
					sceneLayer), null, false, sceneLayer, true);
				Controller.destroyOnAnimComplete = false;
				Controller.visibilityType = KAnimControllerBase.VisibilityType.Always;
				Controller.gameObject.SetActive(true);
				Controller.Play(PRE_ANIMS, KAnim.PlayMode.Loop);
				Controller.TintColour = Tint;
			}

			/// <summary>
			/// Destroys the anim controller for this cell.
			/// </summary>
			public void Destroy() {
				if (Controller != null) {
					Controller.destroyOnAnimComplete = true;
					Controller.Play(POST_ANIM, KAnim.PlayMode.Once, 1f, 0f);
					Controller = null;
				}
			}

			public override bool Equals(object obj) {
				return obj is VisCellData other && other.Cell == Cell && Tint.Equals(other.
					Tint);
			}

			public override int GetHashCode() {
				return Cell;
			}

			public override string ToString() {
				return "CellData[cell={0:D},color={1}]".F(Cell, Tint);
			}
		}
	}
}
