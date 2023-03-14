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
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// A background image which displays a gradient between two different colors using HSV
	/// interpolation.
	/// </summary>
	internal sealed class ColorGradient : Image {
		/// <summary>
		/// The position to use on the track.
		/// </summary>
		public float Position {
			get => position;
			set {
				position = Mathf.Clamp01(value);
				SetPosition();
			}
		}

		/// <summary>
		/// The currently selected color.
		/// </summary>
		public Color SelectedColor {
			get => current;
			set {
				current = value;
				EstimatePosition();
			}
		}

		/// <summary>
		/// The currently selected color.
		/// </summary>
		private Color current;

		/// <summary>
		/// Whether the image texture needs to be regenerated.
		/// </summary>
		private bool dirty;

		/// <summary>
		/// Gradient unfortunately always uses RGB. Run curves manually using HSV.
		/// </summary>
		private Vector2 hue;

		/// <summary>
		/// The position to use on the track.
		/// </summary>
		private float position;

		/// <summary>
		/// The texture used when drawing the background.
		/// </summary>
		private Texture2D preview;

		private Vector2 sat;

		private Vector2 val;

		public ColorGradient() {
			current = Color.black;
			position = 0.0f;
			preview = null;
			dirty = true;
			hue = Vector2.right;
			sat = Vector2.right;
			val = Vector2.right;
		}

		/// <summary>
		/// Estimates a position based on the selected color.
		/// </summary>
		internal void EstimatePosition() {
			Color.RGBToHSV(current, out float h, out float s, out float v);
			float newPos = 0.0f, divider = 0.0f;
			if (!Mathf.Approximately(hue.x, hue.y)) {
				newPos += Mathf.Clamp01(Mathf.InverseLerp(hue.x, hue.y, h));
				divider += 1.0f;
			}
			if (!Mathf.Approximately(sat.x, sat.y)) {
				newPos += Mathf.Clamp01(Mathf.InverseLerp(sat.x, sat.y, s));
				divider += 1.0f;
			}
			if (!Mathf.Approximately(val.x, val.y)) {
				newPos += Mathf.Clamp01(Mathf.InverseLerp(val.x, val.y, v));
				divider += 1.0f;
			}
			if (divider > 0.0f) {
				position = newPos / divider;
				SetPosition();
			}
		}
		
		/// <summary>
		/// Cleans up the preview when the component is disposed.
		/// </summary>
		protected override void OnDestroy() {
			if (preview != null) {
				Destroy(preview);
				preview = null;
			}
			if (sprite != null) {
				Destroy(sprite);
				sprite = null;
			}
			base.OnDestroy();
		}

		/// <summary>
		/// Called when the image needs to be resized.
		/// </summary>
		protected override void OnRectTransformDimensionsChange() {
			base.OnRectTransformDimensionsChange();
			dirty = true;
		}

		/// <summary>
		/// Updates the currently selected color with the interpolated value based on the
		/// current position.
		/// </summary>
		internal void SetPosition() {
			float h = Mathf.Clamp01(Mathf.Lerp(hue.x, hue.y, position));
			float s = Mathf.Clamp01(Mathf.Lerp(sat.x, sat.y, position));
			float v = Mathf.Clamp01(Mathf.Lerp(val.x, val.y, position));
			current = Color.HSVToRGB(h, s, v);
		}

		/// <summary>
		/// Sets the range of colors to be displayed.
		/// </summary>
		/// <param name="hMin">The minimum hue.</param>
		/// <param name="hMax">The maximum hue.</param>
		/// <param name="sMin">The minimum saturation.</param>
		/// <param name="sMax">The maximum saturation.</param>
		/// <param name="vMin">The minimum value.</param>
		/// <param name="vMax">The maximum value.</param>
		public void SetRange(float hMin, float hMax, float sMin, float sMax, float vMin,
				float vMax) {
			// Ensure correct order
			if (hMin > hMax) {
				hue.x = hMax; hue.y = hMin;
			} else {
				hue.x = hMin; hue.y = hMax;
			}
			if (sMin > sMax) {
				sat.x = sMax; sat.y = sMin;
			} else {
				sat.x = sMin; sat.y = sMax;
			}
			if (vMin > vMax) {
				val.x = vMax; val.y = vMin;
			} else {
				val.x = vMin; val.y = vMax;
			}
			EstimatePosition();
			dirty = true;
		}

		/// <summary>
		/// Called by Unity when the component is created.
		/// </summary>
		protected override void Start() {
			base.Start();
			dirty = true;
		}

		/// <summary>
		/// Regenerates the color gradient image if necessary.
		/// </summary>
		internal void Update() {
			if (dirty || preview == null || sprite == null) {
				var rt = rectTransform.rect;
				int w = Mathf.RoundToInt(rt.width), h = Mathf.RoundToInt(rt.height);
				if (w > 0 && h > 0) {
					var newData = new Color[w * h];
					float hMin = hue.x, hMax = hue.y, sMin = sat.x, sMax = sat.y, vMin = val.x,
						vMax = val.y;
					var oldSprite = sprite;
					bool rebuild = preview == null || oldSprite == null || preview.width !=
						w || preview.height != h;
					if (rebuild) {
						if (preview != null)
							Destroy(preview);
						if (oldSprite != null)
							Destroy(oldSprite);
						preview = new Texture2D(w, h);
					}
					// Generate one row of colors
					for (int i = 0; i < w; i++) {
						float t = (float)i / w;
						newData[i] = Color.HSVToRGB(Mathf.Lerp(hMin, hMax, t), Mathf.Lerp(sMin,
							sMax, t), Mathf.Lerp(vMin, vMax, t));
					}
					// Native copy row to all others
					for (int i = 1; i < h; i++)
						Array.Copy(newData, 0, newData, i * w, w);
					preview.SetPixels(newData);
					preview.Apply();
					if (rebuild)
						sprite = Sprite.Create(preview, new Rect(0.0f, 0.0f, w, h),
							new Vector2(0.5f, 0.5f));
				}
				dirty = false;
			}
		}
	}
}
