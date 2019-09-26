/*
 * Copyright 2019 Peter Han
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
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI panel factory which can arrange its children horizontally or vertically.
	/// </summary>
	public class PPanel : IDynamicSizable {
		/// <summary>
		/// The alignment position to use for child elements if they are smaller than the
		/// required size.
		/// </summary>
		public TextAnchor Alignment { get; set; }

		/// <summary>
		/// The background color of this panel. If null, no color will be used.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// The direction in which components will be laid out.
		/// </summary>
		public PanelDirection Direction { get; set; }

		/// <summary>
		/// Defaults to true for panels, but can be set to false to freeze them for a small
		/// performance gain on panels which will have a perpetually fixed size.
		/// </summary>
		public bool DynamicSize { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }
		
		/// <summary>
		/// The margin left around the contained components in pixels. If null, no margin will
		/// be used.
		/// </summary>
		public RectOffset Margin { get; set; }

		public string Name { get; }

		/// <summary>
		/// The spacing between components in pixels.
		/// </summary>
		public int Spacing { get; set; }

		/// <summary>
		/// The children of this panel.
		/// </summary>
		protected readonly ICollection<IUIComponent> children;

		public event PUIDelegates.OnRealize OnRealize;

		public PPanel() : this(null) { }

		public PPanel(string name) {
			Alignment = TextAnchor.MiddleCenter;
			children = new List<IUIComponent>();
			DynamicSize = true;
			FlexSize = Vector2.zero;
			Name = name ?? "Panel";
			BackColor = PUITuning.Colors.Transparent;
			Direction = PanelDirection.Vertical;
			Margin = null;
			Spacing = 0;
		}

		/// <summary>
		/// Adds a child to this panel.
		/// </summary>
		/// <param name="child">The child to add.</param>
		/// <returns>This panel for call chaining.</returns>
		public PPanel AddChild(IUIComponent child) {
			if (child == null)
				throw new ArgumentNullException("child");
			children.Add(child);
			return this;
		}

		public GameObject Build() {
			var panel = PUIElements.CreateUI(Name);
			if (BackColor.a > 0.0f)
				panel.AddComponent<Image>().color = BackColor;
			panel.layer = LayerMask.NameToLayer("UI");
			// Add children
			foreach (var child in children)
				PUIElements.SetParent(child.Build(), panel);
			// Add layout component
			var args = new BoxLayoutParams() {
				Direction = Direction, Alignment = Alignment, Spacing = Spacing,
				Margin = Margin
			};
			// Gotta love freezable layouts
			if (DynamicSize) {
				var lg = panel.AddComponent<BoxLayoutGroup>();
				lg.Params = args;
				lg.flexibleWidth = FlexSize.x;
				lg.flexibleHeight = FlexSize.y;
			} else
				BoxLayoutGroup.LayoutNow(panel, args).SetFlexUISize(FlexSize);
			OnRealize?.Invoke(panel);
			return panel;
		}

		/// <summary>
		/// Removes a child from this panel.
		/// </summary>
		/// <param name="child">The child to remove.</param>
		/// <returns>This panel for call chaining.</returns>
		public PPanel RemoveChild(IUIComponent child) {
			if (child == null)
				throw new ArgumentNullException("child");
			children.Remove(child);
			return this;
		}

		/// <summary>
		/// Sets the background color to the default Klei dialog blue.
		/// </summary>
		/// <returns>This panel for call chaining.</returns>
		public PPanel SetKleiBlueColor() {
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the background color to the Klei dialog header pink.
		/// </summary>
		/// <returns>This panel for call chaining.</returns>
		public PPanel SetKleiPinkColor() {
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			return this;
		}

		public override string ToString() {
			return "PPanel[Name={0},Direction={1}]".F(Name, Direction);
		}
	}

	/// <summary>
	/// The direction in which PPanel lays out components.
	/// </summary>
	public enum PanelDirection {
		Horizontal, Vertical
	}
}
