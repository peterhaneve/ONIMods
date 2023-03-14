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
using System;
using UnityEngine;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Stores the state of a component in a grid layout.
	/// </summary>
	public class GridComponentSpec {
		/// <summary>
		/// The alignment of the component.
		/// </summary>
		public TextAnchor Alignment { get; set; }

		/// <summary>
		/// The column of the component.
		/// </summary>
		public int Column { get; set; }

		/// <summary>
		/// The number of columns this component spans.
		/// </summary>
		public int ColumnSpan { get; set; }

		/// <summary>
		/// The margin to allocate around each component.
		/// </summary>
		public RectOffset Margin { get; set; }

		/// <summary>
		/// The row of the component.
		/// </summary>
		public int Row { get; set; }

		/// <summary>
		/// The number of rows this component spans.
		/// </summary>
		public int RowSpan { get; set; }

		internal GridComponentSpec() { }

		/// <summary>
		/// Creates a new grid component specification. While the row and column are mandatory,
		/// the other attributes can be optionally specified in the initializer.
		/// </summary>
		/// <param name="row">The row to place the component.</param>
		/// <param name="column">The column to place the component.</param>
		public GridComponentSpec(int row, int column) {
			if (row < 0)
				throw new ArgumentOutOfRangeException(nameof(row));
			if (column < 0)
				throw new ArgumentOutOfRangeException(nameof(column));
			Alignment = TextAnchor.MiddleCenter;
			Row = row;
			Column = column;
			Margin = null;
			RowSpan = 1;
			ColumnSpan = 1;
		}

		public override string ToString() {
			return string.Format("GridComponentSpec[Row={0:D},Column={1:D},RowSpan={2:D},ColumnSpan={3:D}]",
				Row, Column, RowSpan, ColumnSpan);
		}
	}

	/// <summary>
	/// The specifications for one column in a grid layout.
	/// </summary>
	[Serializable]
	public sealed class GridColumnSpec {
		/// <summary>
		/// The flexible width of this grid column. If there is space left after all
		/// columns get their nominal width, each column will get a fraction of the space
		/// left proportional to their FlexWidth value as a ratio to the total flexible
		/// width values.
		/// </summary>
		public float FlexWidth { get; }

		/// <summary>
		/// The nominal width of this grid column. If zero, the preferred width of the
		/// largest component is used. If there are no components in this column (possibly
		/// because the only components in this row all have column spans from other
		/// columns), the width will be zero!
		/// </summary>
		public float Width { get; }

		/// <summary>
		/// Creates a new grid column specification.
		/// </summary>
		/// <param name="width">The column's base width, or 0 to auto-size the column to the
		/// preferred width of its largest component.</param>
		/// <param name="flex">The percentage of the leftover width the column should occupy.</param>
		public GridColumnSpec(float width = 0.0f, float flex = 0.0f) {
			if (width.IsNaNOrInfinity() || width < 0.0f)
				throw new ArgumentOutOfRangeException(nameof(width));
			if (flex.IsNaNOrInfinity() || flex < 0.0f)
				throw new ArgumentOutOfRangeException(nameof(flex));
			Width = width;
			FlexWidth = flex;
		}

		public override string ToString() {
			return string.Format("GridColumnSpec[Width={0:F2}]", Width);
		}
	}

	/// <summary>
	/// The specifications for one row in a grid layout.
	/// </summary>
	[Serializable]
	public sealed class GridRowSpec {
		/// <summary>
		/// The flexible height of this grid row. If there is space left after all rows
		/// get their nominal height, each row will get a fraction of the space left
		/// proportional to their FlexHeight value as a ratio to the total flexible
		/// height values.
		/// </summary>
		public float FlexHeight { get; }

		/// <summary>
		/// The nominal height of this grid row. If zero, the preferred height of the
		/// largest component is used. If there are no components in this row (possibly
		/// because the only components in this row all have row spans from other rows),
		/// the height will be zero!
		/// </summary>
		public float Height { get; }

		/// <summary>
		/// Creates a new grid row specification.
		/// </summary>
		/// <param name="height">The row's base width, or 0 to auto-size the row to the
		/// preferred height of its largest component.</param>
		/// <param name="flex">The percentage of the leftover height the row should occupy.</param>
		public GridRowSpec(float height = 0.0f, float flex = 0.0f) {
			if (height.IsNaNOrInfinity() || height < 0.0f)
				throw new ArgumentOutOfRangeException(nameof(height));
			if (flex.IsNaNOrInfinity() || flex < 0.0f)
				throw new ArgumentOutOfRangeException(nameof(flex));
			Height = height;
			FlexHeight = flex;
		}

		public override string ToString() {
			return string.Format("GridRowSpec[Height={0:F2}]", Height);
		}
	}
}
