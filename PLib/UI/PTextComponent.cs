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

using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// The abstract parent of PLib UI components which display text and/or images.
	/// </summary>
	public abstract class PTextComponent : IDynamicSizable {
		/// <summary>
		/// The center of an object for pivoting.
		/// </summary>
		private static readonly Vector2 CENTER = new Vector2(0.5f, 0.5f);

		/// <summary>
		/// Shared routine to spawn UI image objects.
		/// </summary>
		/// <param name="parent">The parent object for the image.</param>
		/// <param name="settings">The settings to use for displaying the image.</param>
		/// <returns>The child image object.</returns>
		protected static Image ImageChildHelper(GameObject parent, PTextComponent settings) {
			var imageChild = PUIElements.CreateUI(parent, "Image", true,
				PUIAnchoring.Beginning, PUIAnchoring.Beginning);
			var rt = imageChild.rectTransform();
			// The pivot is important here
			rt.pivot = CENTER;
			var img = imageChild.AddComponent<Image>();
			img.color = settings.SpriteTint;
			img.sprite = settings.Sprite;
			img.preserveAspect = true;
			// Set up transform
			var scale = Vector3.one;
			float rot = 0.0f;
			var rotate = settings.SpriteTransform;
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
			var imageSize = settings.SpriteSize;
			if (imageSize.x > 0.0f && imageSize.y > 0.0f)
				imageChild.SetUISize(imageSize, true);
			return img;
		}

		/// <summary>
		/// Shared routine to spawn UI text objects.
		/// </summary>
		/// <param name="parent">The parent object for the text.</param>
		/// <param name="style">The text style to use.</param>
		/// <param name="contents">The default text.</param>
		/// <returns>The child text object.</returns>
		protected static LocText TextChildHelper(GameObject parent, TextStyleSetting style,
				string contents = "") {
			var textChild = PUIElements.CreateUI(parent, "Text");
			var locText = PUIElements.AddLocText(textChild, style);
			// Font needs to be set before the text
			locText.alignment = TMPro.TextAlignmentOptions.Center;
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
		/// The color to tint the sprite. For no tint, use Color.white.
		/// </summary>
		public Color SpriteTint { get; set; }

		/// <summary>
		/// How to rotate or flip the sprite.
		/// </summary>
		public ImageTransform SpriteTransform { get; set; }

		/// <summary>
		/// The component's text.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// The text alignment in the component.
		/// </summary>
		public TextAnchor TextAlignment { get; set; }

		/// <summary>
		/// The component's text color, font, word wrap settings, and font size.
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
			SpriteTint = Color.white;
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
