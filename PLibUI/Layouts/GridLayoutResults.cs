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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// A class which stores the results of a single grid layout calculation pass.
	/// </summary>
	internal sealed class GridLayoutResults {
		/// <summary>
		/// Builds a matrix of the components at each given location. Components only are
		/// entered at their origin cell (ignoring row and column span).
		/// </summary>
		/// <param name="rows">The maximum number of rows.</param>
		/// <param name="columns">The maximum number of columns.</param>
		/// <param name="components">The components to add.</param>
		/// <returns>A 2-D array of the components at a given row/column location.</returns>
		private static ICollection<SizedGridComponent>[,] GetMatrix(int rows, int columns,
				ICollection<SizedGridComponent> components) {
			var spec = new ICollection<SizedGridComponent>[rows, columns];
			foreach (var component in components) {
				int x = component.Row, y = component.Column;
				if (x >= 0 && x < rows && y >= 0 && y < columns) {
					// Multiple components are allowed at a given cell
					var atLocation = spec[x, y];
					if (atLocation == null)
						spec[x, y] = atLocation = new List<SizedGridComponent>(8);
					atLocation.Add(component);
				}
			}
			return spec;
		}

		/// <summary>
		/// The columns in the grid.
		/// </summary>
		public IList<GridColumnSpec> ColumnSpecs { get; }

		/// <summary>
		/// The components in the grid, in order of addition.
		/// </summary>
		public ICollection<SizedGridComponent> Components { get; }

		/// <summary>
		/// The number of columns in the grid.
		/// </summary>
		public int Columns { get; }

		/// <summary>
		/// The columns in the grid with their calculated widths.
		/// </summary>
		public IList<GridColumnSpec> ComputedColumnSpecs { get; }

		/// <summary>
		/// The rows in the grid with their calculated heights.
		/// </summary>
		public IList<GridRowSpec> ComputedRowSpecs { get; }

		/// <summary>
		/// The minimum total height.
		/// </summary>
		public float MinHeight { get; private set; }

		/// <summary>
		/// The minimum total width.
		/// </summary>
		public float MinWidth { get; private set; }

		/// <summary>
		/// The components which were laid out.
		/// </summary>
		public ICollection<SizedGridComponent>[,] Matrix { get; }

		/// <summary>
		/// The rows in the grid.
		/// </summary>
		public IList<GridRowSpec> RowSpecs { get; }

		/// <summary>
		/// The number of rows in the grid.
		/// </summary>
		public int Rows { get; }

		/// <summary>
		/// The total flexible height weights.
		/// </summary>
		public float TotalFlexHeight { get; private set; }

		/// <summary>
		/// The total flexible width weights.
		/// </summary>
		public float TotalFlexWidth { get; private set; }

		internal GridLayoutResults(IList<GridRowSpec> rows, IList<GridColumnSpec> columns,
				ICollection<GridComponent<GameObject>> components) {
			if (rows == null)
				throw new ArgumentNullException(nameof(rows));
			if (columns == null)
				throw new ArgumentNullException(nameof(columns));
			if (components == null)
				throw new ArgumentNullException(nameof(components));
			Columns = columns.Count;
			Rows = rows.Count;
			ColumnSpecs = columns;
			MinHeight = MinWidth = 0.0f;
			RowSpecs = rows;
			ComputedColumnSpecs = new List<GridColumnSpec>(Columns);
			ComputedRowSpecs = new List<GridRowSpec>(Rows);
			TotalFlexHeight = TotalFlexWidth = 0.0f;
			// Populate alive components
			Components = new List<SizedGridComponent>(Math.Max(components.Count, 4));
			foreach (var component in components) {
				var item = component.Item;
				if (item != null)
					Components.Add(new SizedGridComponent(component, item));
			}
			Matrix = GetMatrix(Rows, Columns, Components);
		}

		/// <summary>
		/// Calculates the base height of each row, the minimum it gets before extra space
		/// is distributed.
		/// </summary>
		internal void CalcBaseHeights() {
			int rows = Rows;
			MinHeight = TotalFlexHeight = 0.0f;
			ComputedRowSpecs.Clear();
			for (int row = 0; row < rows; row++) {
				var spec = RowSpecs[row];
				float height = spec.Height, flex = spec.FlexHeight;
				if (height <= 0.0f)
					// Auto height
					for (int i = 0; i < Columns; i++)
						height = Math.Max(height, PreferredHeightAt(row, i));
				if (flex > 0.0f)
					TotalFlexHeight += flex;
				ComputedRowSpecs.Add(new GridRowSpec(height, flex));
			}
			foreach (var component in Components)
				if (component.RowSpan > 1)
					ExpandMultiRow(component);
			// Min height is calculated after all multirow components are distributed
			for (int row = 0; row < rows; row++)
				MinHeight += ComputedRowSpecs[row].Height;
		}

		/// <summary>
		/// Calculates the base width of each row, the minimum it gets before extra space
		/// is distributed.
		/// </summary>
		internal void CalcBaseWidths() {
			int columns = Columns;
			MinWidth = TotalFlexWidth = 0.0f;
			ComputedColumnSpecs.Clear();
			for (int column = 0; column < columns; column++) {
				var spec = ColumnSpecs[column];
				float width = spec.Width, flex = spec.FlexWidth;
				if (width <= 0.0f)
					// Auto width
					for (int i = 0; i < Rows; i++)
						width = Math.Max(width, PreferredWidthAt(i, column));
				if (flex > 0.0f)
					TotalFlexWidth += flex;
				ComputedColumnSpecs.Add(new GridColumnSpec(width, flex));
			}
			foreach (var component in Components)
				if (component.ColumnSpan > 1)
					ExpandMultiColumn(component);
			// Min width is calculated after all multicolumn components are distributed
			for (int column = 0; column < columns; column++)
				MinWidth += ComputedColumnSpecs[column].Width;
		}

		/// <summary>
		/// For a multicolumn component, ratiometrically splits up any excess preferred size
		/// among the columns in its span that have a flexible width.
		/// </summary>
		/// <param name="component">The component to reallocate sizes.</param>
		private void ExpandMultiColumn(SizedGridComponent component) {
			float need = component.HorizontalSize.preferred, totalFlex = 0.0f;
			int start = component.Column, end = start + component.ColumnSpan;
			for (int i = start; i < end; i++) {
				var spec = ComputedColumnSpecs[i];
				if (spec.FlexWidth > 0.0f)
					totalFlex += spec.FlexWidth;
				need -= spec.Width;
			}
			if (need > 0.0f && totalFlex > 0.0f)
				// No flex = we can do nothing about it
				for (int i = start; i < end; i++) {
					var spec = ComputedColumnSpecs[i];
					float flex = spec.FlexWidth;
					if (flex > 0.0f)
						ComputedColumnSpecs[i] = new GridColumnSpec(spec.Width + flex * need /
							totalFlex, flex);
				}
		}

		/// <summary>
		/// For a multirow component, ratiometrically splits up any excess preferred size
		/// among the rows in its span that have a flexible height.
		/// </summary>
		/// <param name="component">The component to reallocate sizes.</param>
		private void ExpandMultiRow(SizedGridComponent component) {
			float need = component.VerticalSize.preferred, totalFlex = 0.0f;
			int start = component.Row, end = start + component.RowSpan;
			for (int i = start; i < end; i++) {
				var spec = ComputedRowSpecs[i];
				if (spec.FlexHeight > 0.0f)
					totalFlex += spec.FlexHeight;
				need -= spec.Height;
			}
			if (need > 0.0f && totalFlex > 0.0f)
				// No flex = we can do nothing about it
				for (int i = start; i < end; i++) {
					var spec = ComputedRowSpecs[i];
					float flex = spec.FlexHeight;
					if (flex > 0.0f)
						ComputedRowSpecs[i] = new GridRowSpec(spec.Height + flex * need /
							totalFlex, flex);
				}
		}

		/// <summary>
		/// Retrieves the preferred height of a cell.
		/// </summary>
		/// <param name="row">The cell's row.</param>
		/// <param name="column">The cell's column.</param>
		/// <returns>The preferred height.</returns>
		private float PreferredHeightAt(int row, int column) {
			float size = 0.0f;
			var atLocation = Matrix[row, column];
			if (atLocation != null && atLocation.Count > 0)
				foreach (var component in atLocation) {
					var sizes = component.VerticalSize;
					if (component.RowSpan < 2)
						size = Math.Max(size, sizes.preferred);
				}
			return size;
		}

		/// <summary>
		/// Retrieves the preferred width of a cell.
		/// </summary>
		/// <param name="row">The cell's row.</param>
		/// <param name="column">The cell's column.</param>
		/// <returns>The preferred width.</returns>
		private float PreferredWidthAt(int row, int column) {
			float size = 0.0f;
			var atLocation = Matrix[row, column];
			if (atLocation != null && atLocation.Count > 0)
				foreach (var component in atLocation) {
					var sizes = component.HorizontalSize;
					if (component.ColumnSpan < 2)
						size = Math.Max(size, sizes.preferred);
				}
			return size;
		}
	}

	/// <summary>
	/// A component in the grid with its sizes computed.
	/// </summary>
	internal sealed class SizedGridComponent : GridComponentSpec {
		/// <summary>
		/// The object and its computed horizontal sizes.
		/// </summary>
		public LayoutSizes HorizontalSize { get; set; }

		/// <summary>
		/// The object and its computed vertical sizes.
		/// </summary>
		public LayoutSizes VerticalSize { get; set; }

		internal SizedGridComponent(GridComponentSpec spec, GameObject item) {
			Alignment = spec.Alignment;
			Column = spec.Column;
			ColumnSpan = spec.ColumnSpan;
			Margin = spec.Margin;
			Row = spec.Row;
			RowSpan = spec.RowSpan;
			HorizontalSize = new LayoutSizes(item);
			VerticalSize = new LayoutSizes(item);
		}
	}
}
