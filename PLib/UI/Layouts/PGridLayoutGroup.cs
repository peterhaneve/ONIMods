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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Implements a flexible version of the base GridLayout.
	/// </summary>
	public sealed class PGridLayoutGroup : UIBehaviour, ISettableFlexSize, ILayoutElement {
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
		/// The flexible height of the completed layout group can be set.
		/// </summary>
		public float flexibleHeight { get; set; }

		/// <summary>
		/// The flexible width of the completed layout group can be set.
		/// </summary>
		public float flexibleWidth { get; set; }

		public float minWidth { get; private set; }

		public float preferredWidth { get; private set; }

		public float minHeight { get; private set; }

		public float preferredHeight { get; private set; }

		/// <summary>
		/// The priority of this layout group.
		/// </summary>
		public int layoutPriority { get; set; }

		/// <summary>
		/// The children of this panel.
		/// </summary>
		private readonly ICollection<GridComponent<GameObject>> children;

		/// <summary>
		/// The columns in this panel.
		/// </summary>
		private readonly IList<GridColumnSpec> columns;

		/// <summary>
		/// The current layout status.
		/// </summary>
		private LayoutResults results;

		/// <summary>
		/// The rows in this panel.
		/// </summary>
		private readonly IList<GridRowSpec> rows;

		internal PGridLayoutGroup() {
			children = new List<GridComponent<GameObject>>(16);
			columns = new List<GridColumnSpec>(16);
			rows = new List<GridRowSpec>(16);
			layoutPriority = 1;
			results = null;
		}

		/// <summary>
		/// Adds a column to this grid layout.
		/// </summary>
		/// <param name="column">The specification for that column.</param>
		public void AddColumn(GridColumnSpec column) {
			if (column == null)
				throw new ArgumentNullException("column");
			columns.Add(column);
		}

		/// <summary>
		/// Adds a component to this layout. Components added through other means to the
		/// transform will not be laid out at all!
		/// </summary>
		/// <param name="child">The child to add.</param>
		/// <param name="spec">The location where the child will be placed.</param>
		public void AddComponent(GameObject child, GridComponentSpec spec) {
			if (child == null)
				throw new ArgumentNullException("child");
			if (spec == null)
				throw new ArgumentNullException("spec");
			children.Add(new GridComponent<GameObject>(spec, child));
			child.SetParent(gameObject);
		}

		/// <summary>
		/// Adds a row to this grid layout.
		/// </summary>
		/// <param name="row">The specification for that row.</param>
		public void AddRow(GridRowSpec row) {
			if (row == null)
				throw new ArgumentNullException("row");
			rows.Add(row);
		}

		public void CalculateLayoutInputHorizontal() {
			results = new LayoutResults(rows, columns, children);
			var elements = ListPool<Component, PGridLayoutGroup>.Allocate();
			foreach (var component in results.Components) {
				// Cache size of children
				var obj = component.HorizontalSize.source;
				var margin = component.Margin;
				elements.Clear();
				obj.GetComponents(elements);
				var sz = PUIUtils.CalcSizes(obj, PanelDirection.Horizontal, elements);
				if (!sz.ignore) {
					// Add borders
					int border = (margin == null) ? 0 : margin.left + margin.right;
					sz.min += border;
					sz.preferred += border;
				}
				component.HorizontalSize = sz;
			}
			elements.Recycle();
			// Calculate columns sizes and our size
			results.CalcBaseWidths();
			minWidth = results.MinWidth;
			preferredWidth = results.MinWidth;
			flexibleWidth = (results.TotalFlexWidth > 0.0f) ? 1.0f : 0.0f;
		}

		public void CalculateLayoutInputVertical() {
#if DEBUG
			if (results == null)
				throw new InvalidOperationException("CalculateLayoutInputVertical before CalculateLayoutInputHorizontal");
#endif
			if (results != null) {
				var elements = ListPool<Component, PGridLayoutGroup>.Allocate();
				foreach (var component in results.Components) {
					// Cache size of children
					var obj = component.VerticalSize.source;
					var margin = component.Margin;
					elements.Clear();
					obj.GetComponents(elements);
					var sz = PUIUtils.CalcSizes(obj, PanelDirection.Vertical, elements);
					if (!sz.ignore) {
						// Add borders
						int border = (margin == null) ? 0 : margin.top + margin.bottom;
						sz.min += border;
						sz.preferred += border;
					}
					component.VerticalSize = sz;
				}
				elements.Recycle();
				// Calculate row sizes and our size
				results.CalcBaseHeights();
				minHeight = results.MinHeight;
				preferredHeight = results.MinHeight;
				flexibleHeight = (results.TotalFlexHeight > 0.0f) ? 1.0f : 0.0f;
			}
		}

		protected override void OnDidApplyAnimationProperties() {
			base.OnDidApplyAnimationProperties();
			SetDirty();
			results = null;
		}

		protected override void OnDisable() {
			base.OnDisable();
			SetDirty();
			results = null;
		}

		protected override void OnEnable() {
			base.OnEnable();
			SetDirty();
			results = null;
		}

		protected override void OnRectTransformDimensionsChange() {
			base.OnRectTransformDimensionsChange();
			SetDirty();
		}

		/// <summary>
		/// Sets this layout as dirty.
		/// </summary>
		private void SetDirty() {
			if (gameObject != null && IsActive())
				LayoutRebuilder.MarkLayoutForRebuild(gameObject.rectTransform());
		}

		public void SetLayoutHorizontal() {
			var obj = gameObject;
			int columns;
#if DEBUG
			if (results == null)
				throw new InvalidOperationException("SetLayoutHorizontal before CalculateLayoutInputHorizontal");
#endif
			if (results != null && obj != null && (columns = results.Columns) > 0) {
				// Find out how much flexible size can be given out
				float actualWidth = obj.rectTransform().rect.width, position = 0.0f;
				float totalFlex = results.TotalFlexWidth, excess = (totalFlex > 0.0f) ?
					(actualWidth - results.MinWidth) / totalFlex : 0.0f;
				float[] colX = new float[columns + 1];
				// Determine start of columns
				for (int i = 0; i < columns; i++) {
					var spec = results.ColumnSpecs[i];
					colX[i] = position;
					position += spec.Width + spec.FlexWidth * excess;
				}
				colX[columns] = position;
				// All components lay out
				var controllers = ListPool<ILayoutController, PGridLayoutGroup>.Allocate();
				foreach (var component in results.Components) {
					var sizes = component.HorizontalSize;
					var item = sizes.source;
					if (!sizes.ignore && item != null) {
						var margin = component.Margin;
						// Clamp first and last column occupied by this object
						int first = component.Column, last = first + component.ColumnSpan;
						first = first.InRange(0, columns - 1);
						last = last.InRange(1, columns);
						// Align correctly in the cell box
						float x = colX[first], width = colX[last] - x;
						if (margin != null) {
							x += margin.left;
							width -= margin.left + margin.right;
						}
						float setWidth = PUIUtils.GetProperSize(sizes, width);
						item.rectTransform().SetInsetAndSizeFromParentEdge(RectTransform.Edge.
							Left, x + PUIUtils.GetOffset(component.Alignment, PanelDirection.
							Horizontal, width - setWidth), setWidth);
						// Lay out all children
						controllers.Clear();
						item.GetComponents(controllers);
						foreach (var controller in controllers)
							controller.SetLayoutHorizontal();
					}
				}
				controllers.Recycle();
			}
		}

		public void SetLayoutVertical() {
			var obj = gameObject;
			int rows;
#if DEBUG
			if (results == null)
				throw new InvalidOperationException("SetLayoutVertical before CalculateLayoutInputVertical");
#endif
			if (results != null && obj != null && (rows = results.Rows) > 0) {
				// Find out how much flexible size can be given out
				float actualWidth = obj.rectTransform().rect.height, position = 0.0f;
				float totalFlex = results.TotalFlexHeight, excess = (totalFlex > 0.0f) ?
					(actualWidth - results.MinHeight) / totalFlex : 0.0f;
				float[] rowY = new float[rows + 1];
				// Determine start of rows
				for (int i = 0; i < rows; i++) {
					var spec = results.RowSpecs[i];
					rowY[i] = position;
					position += spec.Height + spec.FlexHeight * excess;
				}
				rowY[rows] = position;
				// All components lay out
				var controllers = ListPool<ILayoutController, PGridLayoutGroup>.Allocate();
				foreach (var component in results.Components) {
					var sizes = component.VerticalSize;
					var item = sizes.source;
					if (!sizes.ignore && item != null) {
						var margin = component.Margin;
						// Clamp first and last row occupied by this object
						int first = component.Row, last = first + component.RowSpan;
						first = first.InRange(0, rows - 1);
						last = last.InRange(1, rows);
						// Align correctly in the cell box
						float y = rowY[first], height = rowY[last] - y;
						if (margin != null) {
							y += margin.top;
							height -= margin.top + margin.bottom;
						}
						float setHeight = PUIUtils.GetProperSize(sizes, height);
						item.rectTransform().SetInsetAndSizeFromParentEdge(RectTransform.Edge.
							Top, y + PUIUtils.GetOffset(component.Alignment, PanelDirection.
							Vertical, height - setHeight), setHeight);
						// Lay out all children
						controllers.Clear();
						item.GetComponents(controllers);
						foreach (var controller in controllers)
							controller.SetLayoutVertical();
					}
				}
				controllers.Recycle();
			}
		}

		/// <summary>
		/// A component in the grid with its sizes computed.
		/// </summary>
		private sealed class SizedGridComponent : GridComponentSpec {
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

		/// <summary>
		/// A class which stores the results of a single layout calculation pass.
		/// </summary>
		private sealed class LayoutResults {
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

			internal LayoutResults(IList<GridRowSpec> rows, IList<GridColumnSpec> columns,
					ICollection<GridComponent<GameObject>> components) {
				if (rows == null)
					throw new ArgumentNullException("rows");
				if (columns == null)
					throw new ArgumentNullException("columns");
				if (components == null)
					throw new ArgumentNullException("components");
				Columns = columns.Count;
				Rows = rows.Count;
				ColumnSpecs = columns;
				MinHeight = MinWidth = 0.0f;
				RowSpecs = rows;
				TotalFlexHeight = TotalFlexWidth = 0.0f;
				// Populate alive components
				int n = Math.Max(components.Count, 8);
				Components = new List<SizedGridComponent>(n);
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
				MinHeight = TotalFlexHeight = 0.0f;
				for (int row = 0; row < Rows; row++) {
					var spec = RowSpecs[row];
					float height = spec.Height, flex = spec.FlexHeight;
					if (height <= 0.0f)
						// Auto height
						for (int i = 0; i < Columns; i++)
							height = Math.Max(height, PreferredHeightAt(row, i));
					MinHeight += height;
					if (flex > 0.0f)
						TotalFlexHeight += flex;
					RowSpecs[row] = new GridRowSpec(height, flex);
				}
			}

			/// <summary>
			/// Calculates the base width of each row, the minimum it gets before extra space
			/// is distributed.
			/// </summary>
			internal void CalcBaseWidths() {
				MinWidth = TotalFlexWidth = 0.0f;
				for (int column = 0; column < Columns; column++) {
					var spec = ColumnSpecs[column];
					float width = spec.Width, flex = spec.FlexWidth;
					if (width <= 0.0f)
						// Auto width
						for (int i = 0; i < Rows; i++)
							width = Math.Max(width, PreferredWidthAt(i, column));
					MinWidth += width;
					if (flex > 0.0f)
						TotalFlexWidth += flex;
					ColumnSpecs[column] = new GridColumnSpec(width, flex);
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
	}
}
