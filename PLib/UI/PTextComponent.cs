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
using TMPro;
using UnityEngine;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// The abstract parent of PLib UI components which display text and/or images.
	/// </summary>
	public abstract class PTextComponent : IDynamicSizable {
		public bool DynamicSize { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The font size of this component.
		/// </summary>
		public float FontSize { get; set; }

		/// <summary>
		/// The spacing between text and icon.
		/// </summary>
		public int IconSpacing { get; set; }

		public string Name { get; }

		/// <summary>
		/// The sprite to display, or null to display no sprite.
		/// </summary>
		public Sprite Sprite { get; set; }

		/// <summary>
		/// The size to scale the sprite. If 0x0, it will not be scaled.
		/// </summary>
		public Vector2 SpriteSize { get; set; }

		/// <summary>
		/// The label's text.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// The text alignment in the label.
		/// </summary>
		public TextAlignmentOptions TextAlignment { get; set; }

		/// <summary>
		/// The label's text color.
		/// </summary>
		public TextStyleSetting TextColor { get; set; }

		/// <summary>
		/// The tool tip text.
		/// </summary>
		public string ToolTip { get; set; }

		/// <summary>
		/// Whether word wrap is enabled.
		/// </summary>
		public bool WordWrap { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		protected PTextComponent(string name) {
			DynamicSize = false;
			FlexSize = Vector2.zero;
			FontSize = 0.0f;
			IconSpacing = 0;
			Name = name;
			Sprite = null;
			SpriteSize = Vector2.zero;
			Text = null;
			TextAlignment = TextAlignmentOptions.Center;
			TextColor = PUITuning.UITextStyle;
			ToolTip = "";
			WordWrap = false;
		}

		public abstract GameObject Build();

		/// <summary>
		/// Invokes the OnRealize event.
		/// </summary>
		/// <param name="obj">The realized text component.</param>
		protected void InvokeRealize(GameObject obj) {
			OnRealize?.Invoke(obj);
		}

		public override string ToString() {
			return "{3}[Name={0},Text={1},Sprite={2}]".F(Name, Text, Sprite, GetType().Name);
		}
	}
}
