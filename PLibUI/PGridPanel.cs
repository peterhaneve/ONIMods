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

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A panel which lays out its components using grid-type constraints.
	/// </summary>
	public class PGridPanel : PContainer, IDynamicSizable {
		/// <summary>
		/// The number of columns currently defined.
		/// </summary>
		public int Columns => columns.Count;

		public bool DynamicSize { get; set; }

		/// <summary>
		/// The number of rows currently defined.
		/// </summary>
		public int Rows => rows.Count;

		/// <summary>
		/// The children of this panel.
		/// </summary>
		private readonly ICollection<GridComponent<IUIComponent>> children;

		/// <summary>
		/// The columns in this panel.
		/// </summary>
		private readonly IList<GridColumnSpec> columns;

		/// <summary>
		/// The rows in this panel.
		/// </summary>
		private readonly IList<GridRowSpec> rows;

		public PGridPanel() : this(null) { }

		public PGridPanel(string name) : base(name ?? "GridPanel") {
			children = new List<GridComponent<IUIComponent>>(16);
			columns = new List<GridColumnSpec>(16);
			rows = new List<GridRowSpec>(16);
			DynamicSize = true;
			Margin = null;
		}

		/// <summary>
		/// Adds a child to this panel.
		/// </summary>
		/// <param name="child">The child to add.</param>
		/// <param name="spec">The location where the child will be placed.</param>
		/// <returns>This panel for call chaining.</returns>
		public PGridPanel AddChild(IUIComponent child, GridComponentSpec spec) {
			if (child == null)
				throw new ArgumentNullException(nameof(child));
			if (spec == null)
				throw new ArgumentNullException(nameof(spec));
			children.Add(new GridComponent<IUIComponent>(spec, child));
			return this;
		}

		/// <summary>
		/// Adds a column to this panel.
		/// </summary>
		/// <param name="column">The specification for that column.</param>
		/// <returns>This panel for call chaining.</returns>
		public PGridPanel AddColumn(GridColumnSpec column) {
			if (column == null)
				throw new ArgumentNullException(nameof(column));
			columns.Add(column);
			return this;
		}

		/// <summary>
		/// Adds a handler when this panel is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This panel for call chaining.</returns>
		public PGridPanel AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		/// <summary>
		/// Adds a row to this panel.
		/// </summary>
		/// <param name="row">The specification for that row.</param>
		/// <returns>This panel for call chaining.</returns>
		public PGridPanel AddRow(GridRowSpec row) {
			if (row == null)
				throw new ArgumentNullException(nameof(row));
			rows.Add(row);
			return this;
		}

		public override GameObject Build() {
			if (Columns < 1)
				throw new InvalidOperationException("At least one column must be defined");
			if (Rows < 1)
				throw new InvalidOperationException("At least one row must be defined");
			var panel = PUIElements.CreateUI(null, Name);
			SetImage(panel);
			// Add layout component
			var layout = panel.AddComponent<PGridLayoutGroup>();
			layout.Margin = Margin;
			foreach (var column in columns)
				layout.AddColumn(column);
			foreach (var row in rows)
				layout.AddRow(row);
			// Add children
			foreach (var child in children)
				layout.AddComponent(child.Item.Build(), child);
			if (!DynamicSize)
				layout.LockLayout();
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			InvokeRealize(panel);
			return panel;
		}

		public override string ToString() {
			return string.Format("PGridPanel[Name={0},Rows={1:D},Columns={2:D}]", Name, Rows,
				Columns);
		}
	}
}
