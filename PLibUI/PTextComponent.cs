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
		/// Arranges a component in the parent layout in both directions.
		/// </summary>
		/// <param name="layout">The layout to modify.</param>
		/// <param name="target">The target object to arrange.</param>
		/// <param name="alignment">The object alignment to use.</param>
		protected static void ArrangeComponent(RelativeLayoutGroup layout, GameObject target,
				TextAnchor alignment) {
			// X
			switch (alignment) {
			case TextAnchor.LowerLeft:
			case TextAnchor.MiddleLeft:
			case TextAnchor.UpperLeft:
				layout.SetLeftEdge(target, fraction: 0.0f);
				break;
			case TextAnchor.LowerRight:
			case TextAnchor.MiddleRight:
			case TextAnchor.UpperRight:
				layout.SetRightEdge(target, fraction: 1.0f);
				break;
			default:
				// MiddleCenter, LowerCenter, UpperCenter
				layout.AnchorXAxis(target, 0.5f);
				break;
			}
			// Y
			switch (alignment) {
			case TextAnchor.LowerLeft:
			case TextAnchor.LowerCenter:
			case TextAnchor.LowerRight:
				layout.SetBottomEdge(target, fraction: 0.0f);
				break;
			case TextAnchor.UpperLeft:
			case TextAnchor.UpperCenter:
			case TextAnchor.UpperRight:
				layout.SetTopEdge(target, fraction: 1.0f);
				break;
			default:
				// MiddleCenter, LowerCenter, UpperCenter
				layout.AnchorYAxis(target, 0.5f);
				break;
			}
		}

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
			img.type = settings.SpriteMode;
			img.preserveAspect = settings.MaintainSpriteAspect;
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

		/// <summary>
		/// If true, the sprite aspect ratio will be maintained even if it is resized.
		/// </summary>
		public bool MaintainSpriteAspect { get; set; }

		/// <summary>
		/// The margin around the component.
		/// </summary>
		public RectOffset Margin { get; set; }

		public string Name { get; }

		/// <summary>
		/// The sprite to display, or null to display no sprite.
		/// </summary>
		public Sprite Sprite { get; set; }

		/// <summary>
		/// The image mode to use for the sprite.
		/// </summary>
		public Image.Type SpriteMode { get; set; }

		/// <summary>
		/// The position to use for the sprite relative to the text.
		/// 
		/// If TextAnchor.MiddleCenter is used, the image will directly overlap the text.
		/// Otherwise, it will be placed in the specified location relative to the text.
		/// </summary>
		public TextAnchor SpritePosition { get; set; }

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
		/// The text alignment in the component. Controls the placement of the text and sprite
		/// combination relative to the component's overall outline if the component is
		/// expanded from its default size.
		/// 
		/// The text and sprite will move as a unit to follow this text alignment. Note that
		/// incorrect positions will result if this alignment is centered in the same direction
		/// as the sprite position offset, if both a sprite and text are defined.
		/// 
		/// If the SpritePosition uses any variant of Left or Right, using UpperCenter,
		/// MiddleCenter, or LowerCenter for TextAlignment would result in undefined text and
		/// sprite positioning. Likewise, a SpritePosition using any variant of Lower or Upper
		/// would cause undefined positioning if TextAlignment was MiddleLeft, MiddleCenter,
		/// or MiddleRight.
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
			MaintainSpriteAspect = true;
			Margin = null;
			Name = name;
			Sprite = null;
			SpriteMode = Image.Type.Simple;
			SpritePosition = TextAnchor.MiddleLeft;
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
		/// If the flex size is zero and dynamic size is false, the layout group can be
		/// completely destroyed on a text component after the layout is locked.
		/// </summary>
		/// <param name="component">The realized text component.</param>
		protected void DestroyLayoutIfPossible(GameObject component) {
			if (FlexSize.x == 0.0f && FlexSize.y == 0.0f && !DynamicSize)
				AbstractLayoutGroup.DestroyAndReplaceLayout(component);
		}

		/// <summary>
		/// Invokes the OnRealize event.
		/// </summary>
		/// <param name="obj">The realized text component.</param>
		protected void InvokeRealize(GameObject obj) {
			OnRealize?.Invoke(obj);
		}

		public override string ToString() {
			return string.Format("{3}[Name={0},Text={1},Sprite={2}]", Name, Text, Sprite,
				GetType().Name);
		}

		/// <summary>
		/// Wraps the text and sprite into a single GameObject that properly positions them
		/// relative to each other, if necessary.
		/// </summary>
		/// <param name="text">The text component.</param>
		/// <param name="sprite">The sprite component.</param>
		/// <returns>A game object that contains both of them, or null if both are null.</returns>
		protected GameObject WrapTextAndSprite(GameObject text, GameObject sprite) {
			GameObject result = null;
			if (text != null && sprite != null) {
				// Automatically hoist them into a new game object
				result = PUIElements.CreateUI(text.GetParent(), "AlignmentWrapper");
				text.SetParent(result);
				sprite.SetParent(result);
				var layout = result.AddOrGet<RelativeLayoutGroup>();
				// X
				switch (SpritePosition) {
				case TextAnchor.MiddleLeft:
				case TextAnchor.LowerLeft:
				case TextAnchor.UpperLeft:
					layout.SetLeftEdge(sprite, fraction: 0.0f).SetLeftEdge(text, toRight:
						sprite).SetMargin(sprite, new RectOffset(0, IconSpacing, 0, 0));
					break;
				case TextAnchor.MiddleRight:
				case TextAnchor.LowerRight:
				case TextAnchor.UpperRight:
					layout.SetRightEdge(sprite, fraction: 1.0f).SetRightEdge(text, toLeft:
						sprite).SetMargin(sprite, new RectOffset(IconSpacing, 0, 0, 0));
					break;
				default:
					// MiddleCenter, UpperCenter, LowerCenter
					layout.AnchorXAxis(text).AnchorXAxis(sprite);
					break;
				}
				// Y
				switch (SpritePosition) {
				case TextAnchor.UpperCenter:
				case TextAnchor.UpperLeft:
				case TextAnchor.UpperRight:
					layout.SetTopEdge(sprite, fraction: 1.0f).SetTopEdge(text, below:
						sprite).SetMargin(sprite, new RectOffset(0, 0, 0, IconSpacing));
					break;
				case TextAnchor.LowerCenter:
				case TextAnchor.LowerLeft:
				case TextAnchor.LowerRight:
					layout.SetBottomEdge(sprite, fraction: 0.0f).SetBottomEdge(text, above:
						sprite).SetMargin(sprite, new RectOffset(0, 0, IconSpacing, 0));
					break;
				default:
					// MiddleCenter, MiddleLeft, MiddleRight
					layout.AnchorYAxis(text).AnchorYAxis(sprite);
					break;
				}
				if (!DynamicSize)
					layout.LockLayout();
			} else if (text != null)
				result = text;
			else if (sprite != null)
				result = sprite;
			return result;
		}
	}
}
