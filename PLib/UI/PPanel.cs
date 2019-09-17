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
	public class PPanel : IUIComponent {
		/// <summary>
		/// The background color of this panel. If null, no color will be used.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// Whether children's dimensions in the opposite direction are controlled at all.
		/// </summary>
		public bool ControlOpposite { get; set; }

		/// <summary>
		/// The direction in which components will be laid out.
		/// </summary>
		public PanelDirection Direction { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }
		
		/// <summary>
		/// Whether children will be stretched to fill the opposite direction from the layout.
		/// </summary>
		public bool ForceStretch { get; set; }

		/// <summary>
		/// The margin left around the contained components in pixels. If null, 0 margin is
		/// used.
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

		public PPanel() : this(null) { }

		public PPanel(string name) {
			children = new List<IUIComponent>();
			ControlOpposite = true;
			FlexSize = Vector2.zero;
			ForceStretch = false;
			Name = name ?? "Panel";
			BackColor = PUITuning.DialogBackground;
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
			HorizontalOrVerticalLayoutGroup lg;
			var panel = new GameObject(Name);
			panel.AddComponent<Image>().color = BackColor;
			// Add layout component
			if (Direction == PanelDirection.Horizontal) {
				lg = panel.AddComponent<HorizontalLayoutGroup>();
				lg.childControlHeight = ControlOpposite;
				lg.childForceExpandHeight = ControlOpposite && ForceStretch;
			} else {
				lg = panel.AddComponent<VerticalLayoutGroup>();
				lg.childControlWidth = ControlOpposite;
				lg.childForceExpandWidth = ControlOpposite && ForceStretch;
			}
			if (Spacing > 0)
				lg.spacing = Spacing;
			if (Margin != null)
				lg.padding = Margin;
			// Set flex size
			var le = panel.AddComponent<LayoutElement>();
			le.flexibleWidth = FlexSize.x;
			le.flexibleHeight = FlexSize.y;
			// Add children
			foreach (var child in children)
				PUIElements.SetParent(child.Build(), panel);
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
			BackColor = PUITuning.ButtonStyleBlue.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the background color to the Klei dialog header pink.
		/// </summary>
		/// <returns>This panel for call chaining.</returns>
		public PPanel SetKleiPinkColor() {
			BackColor = PUITuning.ButtonStylePink.inactiveColor;
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
