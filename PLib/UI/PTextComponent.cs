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

using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// The abstract parent of PLib UI components which display text and/or images.
	/// </summary>
	public abstract class PTextComponent : IDynamicSizable {
		/// <summary>
		/// Shared routine to spawn UI image objects.
		/// </summary>
		/// <param name="parent">The parent object for the image.</param>
		/// <param name="sprite">The sprite to display.</param>
		/// <param name="rotate">How to rotate or flip the sprite.</param>
		/// <param name="imageSize">The size to which to scale the sprite.</param>
		/// <returns>The child image object.</returns>
		protected static Image ImageChildHelper(GameObject parent, Sprite sprite,
				ImageTransform rotate = ImageTransform.None, Vector2 imageSize = default) {
			var imageChild = PUIElements.CreateUI("Image");
			var img = imageChild.AddComponent<Image>();
			PUIElements.SetParent(imageChild, parent);
			img.sprite = sprite;
			img.preserveAspect = true;
			// Set up transform
			var scale = Vector3.one;
			float rot = 0.0f;
			if ((rotate & ImageTransform.FlipHorizontal) != ImageTransform.None)
				scale.x = -1.0f;
			if ((rotate & ImageTransform.FlipVertical) != ImageTransform.None)
				scale.y = -1.0f;
			if ((rotate & ImageTransform.Rotate90) != ImageTransform.None)
				rot = 90.0f;
			if ((rotate & ImageTransform.Rotate180) != ImageTransform.None)
				rot += 180.0f;
			// Update transform
			var transform = imageChild.rectTransform();
			transform.localScale = scale;
			transform.Rotate(new Vector3(0.0f, 0.0f, rot));
			// Limit size if needed
			if (imageSize.x > 0.0f && imageSize.y > 0.0f)
				PUIElements.SetSizeImmediate(imageChild, imageSize);
			return img;
		}

		/// <summary>
		/// Shared routine to spawn UI text objects.
		/// </summary>
		/// <param name="parent">The parent object for the text.</param>
		/// <param name="fontSize">The font size.</param>
		/// <param name="contents">The default text.</param>
		/// <param name="wordWrap">Whether to enable word wrap.</param>
		/// <returns>The child text object.</returns>
		protected static LocText TextChildHelper(GameObject parent, TextStyleSetting style,
				string contents = "") {
			var textChild = PUIElements.CreateUI("Text");
			var locText = PUIElements.AddLocText(textChild);
			PUIElements.SetParent(textChild, parent);
			// Font needs to be set before the text
			locText.alignment = TMPro.TextAlignmentOptions.Center;
			locText.textStyleSetting = style;
			locText.text = contents;
			return locText;
		}

		public bool DynamicSize { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

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
		/// How to rotate or flip the sprite.
		/// </summary>
		public ImageTransform SpriteTransform { get; set; }

		/// <summary>
		/// The label's text.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// The text alignment in the label.
		/// </summary>
		public TextAnchor TextAlignment { get; set; }

		/// <summary>
		/// The label's text color, font, word wrap settings, and font size.
		/// </summary>
		public TextStyleSetting TextStyle { get; set; }

		/// <summary>
		/// The tool tip text.
		/// </summary>
		public string ToolTip { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		protected PTextComponent(string name) {
			DynamicSize = false;
			FlexSize = Vector2.zero;
			IconSpacing = 0;
			Name = name;
			Sprite = null;
			SpriteSize = Vector2.zero;
			SpriteTransform = ImageTransform.None;
			Text = null;
			TextAlignment = TextAnchor.MiddleCenter;
			TextStyle = null;
			ToolTip = "";
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
