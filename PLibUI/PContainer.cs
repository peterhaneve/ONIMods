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

using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// The abstract parent of PLib UI objects that are meant to contain other UI objects.
	/// </summary>
	public abstract class PContainer : IUIComponent {
		/// <summary>
		/// The background color of this panel.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// The background image of this panel. Tinted by the background color, acts as all
		/// white if left null.
		/// 
		/// Note that the default background color is transparent, so unless it is set to
		/// some other color this image will be invisible!
		/// </summary>
		public Sprite BackImage { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The mode to use when displaying the background image.
		/// </summary>
		public Image.Type ImageMode { get; set; }

		/// <summary>
		/// The margin left around the contained components in pixels. If null, no margin will
		/// be used.
		/// </summary>
		public RectOffset Margin { get; set; }

		public string Name { get; protected set; }

		public event PUIDelegates.OnRealize OnRealize;

		protected PContainer(string name) {
			BackColor = PUITuning.Colors.Transparent;
			BackImage = null;
			FlexSize = Vector2.zero;
			ImageMode = Image.Type.Simple;
			Margin = null;
			Name = name ?? "Container";
		}

		public abstract GameObject Build();
		
		/// <summary>
		/// Invokes the OnRealize event.
		/// </summary>
		/// <param name="obj">The realized text component.</param>
		protected void InvokeRealize(GameObject obj) {
			OnRealize?.Invoke(obj);
		}

		/// <summary>
		/// Configures the background color and/or image for this panel.
		/// </summary>
		/// <param name="panel">The realized panel object.</param>
		protected void SetImage(GameObject panel) {
			if (BackColor.a > 0.0f || BackImage != null) {
				var img = panel.AddComponent<Image>();
				img.color = BackColor;
				if (BackImage != null) {
					img.sprite = BackImage;
					img.type = ImageMode;
				}
			}
		}

		public override string ToString() {
			return string.Format("PContainer[Name={0}]", Name);
		}
	}
}
