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

using PeterHan.PLib.Core;
using PeterHan.PLib.UI.Layouts;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Implements a flexible version of the base GridLayout.
	/// </summary>
	public sealed class PGridLayoutGroup : AbstractLayoutGroup {
		/// <summary>
		/// Calculates all column widths.
		/// </summary>
		/// <param name="results">The results from layout.</param>
		/// <param name="width">The current container width.</param>
		/// <param name="margin">The margins within the borders.</param>
		/// <returns>The column widths.</returns>
		private static float[] GetColumnWidths(GridLayoutResults results, float width,
				RectOffset margin) {
			int columns = results.Columns;
			// Find out how much flexible size can be given out
			float position = margin?.left ?? 0, right = margin?.right ?? 0;
			float actualWidth = width - position - right, totalFlex = results.TotalFlexWidth,
				excess = (totalFlex > 0.0f) ? (actualWidth - results.MinWidth) / totalFlex :
				0.0f;
			float[] colX = new float[columns + 1];
			// Determine start of columns
			for (int i = 0; i < columns; i++) {
				var spec = results.ComputedColumnSpecs[i];
				colX[i] = position;
				position += spec.Width + spec.FlexWidth * excess;
			}
			colX[columns] = position;
			return colX;
		}

		/// <summary>
		/// Calculates all row heights.
		/// </summary>
		/// <param name="results">The results from layout.</param>
		/// <param name="height">The current container height.</param>
		/// <param name="margin">The margins within the borders.</param>
		/// <returns>The row heights.</returns>
		private static float[] GetRowHeights(GridLayoutResults results, float height,
				RectOffset margin) {
			int rows = results.Rows;
			// Find out how much flexible size can be given out
			float position = margin?.bottom ?? 0, top = margin?.top ?? 0;
			float actualWidth = height - position - top, totalFlex = results.TotalFlexHeight,
				excess = (totalFlex > 0.0f) ? (actualWidth - results.MinHeight) / totalFlex :
				0.0f;
			float[] rowY = new float[rows + 1];
			// Determine start of rows
			for (int i = 0; i < rows; i++) {
				var spec = results.ComputedRowSpecs[i];
				rowY[i] = position;
				position += spec.Height + spec.FlexHeight * excess;
			}
			rowY[rows] = position;
			return rowY;
		}

		/// <summary>
		/// Calculates the final height of this component and applies it to the component.
		/// </summary>
		/// <param name="component">The component to calculate.</param>
		/// <param name="rowY">The row locations from GetRowHeights.</param>
		/// <returns>true if the height was applied, or false if the component was not laid out
		/// due to being disposed or set to ignore layout.</returns>
		private static bool SetFinalHeight(SizedGridComponent component, float[] rowY) {
			var margin = component.Margin;
			var sizes = component.VerticalSize;
			var target = sizes.source;
			bool ok = !sizes.ignore && target != null;
			if (ok) {
				int rows = rowY.Length - 1;
				// Clamp first and last row occupied by this object
				int first = component.Row, last = first + component.RowSpan;
				first = first.InRange(0, rows - 1);
				last = last.InRange(1, rows);
				// Align correctly in the cell box
				float y = rowY[first], rowHeight = rowY[last] - y;
				if (margin != null) {
					float border = margin.top + margin.bottom;
					y += margin.top;
					rowHeight -= border;
					sizes.min -= border;
					sizes.preferred -= border;
				}
				float actualHeight = PUIUtils.GetProperSize(sizes, rowHeight);
				// Take alignment into account
				y += PUIUtils.GetOffset(component.Alignment, PanelDirection.Vertical,
					rowHeight - actualHeight);
				target.rectTransform().SetInsetAndSizeFromParentEdge(RectTransform.Edge.
					Top, y, actualHeight);
			}
			return ok;
		}

		/// <summary>
		/// Calculates the final width of this component and applies it to the component.
		/// </summary>
		/// <param name="component">The component to calculate.</param>
		/// <param name="colX">The column locations from GetColumnWidths.</param>
		/// <returns>true if the width was applied, or false if the component was not laid out
		/// due to being disposed or set to ignore layout.</returns>
		private static bool SetFinalWidth(SizedGridComponent component, float[] colX) {
			var margin = component.Margin;
			var sizes = component.HorizontalSize;
			var target = sizes.source;
			bool ok = !sizes.ignore && target != null;
			if (ok) {
				int columns = colX.Length - 1;
				// Clamp first and last column occupied by this object
				int first = component.Column, last = first + component.ColumnSpan;
				first = first.InRange(0, columns - 1);
				last = last.InRange(1, columns);
				// Align correctly in the cell box
				float x = colX[first], colWidth = colX[last] - x;
				if (margin != null) {
					float border = margin.left + margin.right;
					x += margin.left;
					colWidth -= border;
					sizes.min -= border;
					sizes.preferred -= border;
				}
				float actualWidth = PUIUtils.GetProperSize(sizes, colWidth);
				// Take alignment into account
				x += PUIUtils.GetOffset(component.Alignment, PanelDirection.Horizontal,
					colWidth - actualWidth);
				target.rectTransform().SetInsetAndSizeFromParentEdge(RectTransform.Edge.
					Left, x, actualWidth);
			}
			return ok;
		}

		/// <summary>
		/// The margin around the components as a whole.
		/// </summary>
		public RectOffset Margin {
			get {
				return margin;
			}
			set {
				margin = value;
			}
		}

#pragma warning disable IDE0044 // Cannot be readonly for Unity serialization to work
		/// <summary>
		/// The children of this panel.
		/// </summary>
		[SerializeField]
		private IList<GridComponent<GameObject>> children;

		/// <summary>
		/// The columns in this panel.
		/// </summary>
		[SerializeField]
		private IList<GridColumnSpec> columns;

		/// <summary>
		/// The margin around the components as a whole.
		/// </summary>
		[SerializeField]
		private RectOffset margin;

		/// <summary>
		/// The current layout status.
		/// </summary>
		private GridLayoutResults results;

		/// <summary>
		/// The rows in this panel.
		/// </summary>
		[SerializeField]
		private IList<GridRowSpec> rows;
#pragma warning restore IDE0044

		internal PGridLayoutGroup() {
			children = new List<GridComponent<GameObject>>(16);
			columns = new List<GridColumnSpec>(16);
			rows = new List<GridRowSpec>(16);
			layoutPriority = 1;
			Margin = null;
			results = null;
		}

		/// <summary>
		/// Adds a column to this grid layout.
		/// </summary>
		/// <param name="column">The specification for that column.</param>
		public void AddColumn(GridColumnSpec column) {
			if (column == null)
				throw new ArgumentNullException(nameof(column));
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
				throw new ArgumentNullException(nameof(child));
			if (spec == null)
				throw new ArgumentNullException(nameof(spec));
			children.Add(new GridComponent<GameObject>(spec, child));
			child.SetParent(gameObject);
		}

		/// <summary>
		/// Adds a row to this grid layout.
		/// </summary>
		/// <param name="row">The specification for that row.</param>
		public void AddRow(GridRowSpec row) {
			if (row == null)
				throw new ArgumentNullException(nameof(row));
			rows.Add(row);
		}

		public override void CalculateLayoutInputHorizontal() {
			if (!locked) {
				results = new GridLayoutResults(rows, columns, children);
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
				float width = results.MinWidth;
				if (Margin != null)
					width += Margin.left + Margin.right;
				minWidth = preferredWidth = width;
				flexibleWidth = (results.TotalFlexWidth > 0.0f) ? 1.0f : 0.0f;
			}
		}

		public override void CalculateLayoutInputVertical() {
			if (results != null && !locked) {
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
				float height = results.MinHeight;
				if (Margin != null)
					height += Margin.bottom + Margin.top;
				minHeight = preferredHeight = height;
				flexibleHeight = (results.TotalFlexHeight > 0.0f) ? 1.0f : 0.0f;
			}
		}

		protected override void OnDisable() {
			base.OnDisable();
			results = null;
		}

		protected override void OnEnable() {
			base.OnEnable();
			results = null;
		}

		public override void SetLayoutHorizontal() {
			var obj = gameObject;
			if (results != null && obj != null && results.Columns > 0 && !locked) {
				float[] colX = GetColumnWidths(results, rectTransform.rect.width, Margin);
				// All components lay out
				var controllers = ListPool<ILayoutController, PGridLayoutGroup>.Allocate();
				foreach (var component in results.Components)
					if (SetFinalWidth(component, colX)) {
						// Lay out all children
						controllers.Clear();
						component.HorizontalSize.source.GetComponents(controllers);
						foreach (var controller in controllers)
							controller.SetLayoutHorizontal();
					}
				controllers.Recycle();
			}
		}

		public override void SetLayoutVertical() {
			var obj = gameObject;
			if (results != null && obj != null && results.Rows > 0 && !locked) {
				float[] rowY = GetRowHeights(results, rectTransform.rect.height, Margin);
				// All components lay out
				var controllers = ListPool<ILayoutController, PGridLayoutGroup>.Allocate();
				foreach (var component in results.Components)
					if (!SetFinalHeight(component, rowY)) {
						// Lay out all children
						controllers.Clear();
						component.VerticalSize.source.GetComponents(controllers);
						foreach (var controller in controllers)
							controller.SetLayoutVertical();
					}
				controllers.Recycle();
			}
		}
	}
}
