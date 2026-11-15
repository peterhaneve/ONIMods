/*
 * Copyright 2026 Peter Han
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
using System.Collections.Generic;
using TMPro;
using UnityEngine.TextCore;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Calculates and caches metrics about TextMeshPro fonts.
	/// </summary>
	internal sealed class FontSizeCalculator {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly FontSizeCalculator Instance = new FontSizeCalculator();

		/// <summary>
		/// Gets the unscaled width of the character in the specified font.
		/// </summary>
		/// <param name="ch">The character to calculate.</param>
		/// <param name="font">The font to use for calculation.</param>
		/// <returns>The unscaled width of the specified character, or 0.0f if the character
		/// metrics are not available.</returns>
		internal static float GetCharWidth(char ch, TMP_FontAsset font) {
			float width = 0.0f;
			Glyph glyph;
			var dict = font.characterLookupTable;
			if (dict != null && dict.TryGetValue(ch, out TMP_Character outGlyph) &&
					(glyph = outGlyph?.glyph) != null)
				// Pull glyph from TMP_Character (parent class is TMP_TextElement)
				width = glyph.metrics.width;
			return width;
		}

		/// <summary>
		/// Stores calculated metrics about fonts. Although this could theoretically leak font
		/// assets, font assets should not be created and destroyed at runtime anyways.
		/// </summary>
		private readonly IDictionary<TMP_FontAsset, Metrics> fontMetrics;

		private FontSizeCalculator() {
			fontMetrics = new Dictionary<TMP_FontAsset, Metrics>(32);
		}

		/// <summary>
		/// Calculates the font metrics for a given font.
		/// </summary>
		/// <param name="font">The font to calculate.</param>
		/// <returns>The calculated metrics.</returns>
		private Metrics CalculateMetrics(TMP_FontAsset font) {
			var faceInfo = font.faceInfo;
			return new Metrics(faceInfo.lineHeight, faceInfo.pointSize, faceInfo.scale);
		}

		/// <summary>
		/// Clears all cached fonts.
		/// </summary>
		internal void Cleanup() {
			fontMetrics.Clear();
		}

		/// <summary>
		/// Gets the cached font metrics for a specified font.
		/// </summary>
		/// <param name="font">The font to look up.</param>
		/// <returns>The font's metrics.</returns>
		internal Metrics Get(TMP_FontAsset font) {
			if (!fontMetrics.TryGetValue(font, out Metrics metrics)) {
				metrics = CalculateMetrics(font);
				fontMetrics.Add(font, metrics);
			}
			return metrics;
		}

		/// <summary>
		/// Stores cached metrics of a font.
		/// </summary>
		internal sealed class Metrics {
			public readonly float lineHeight;

			public readonly float pointSize;

			public readonly float scale;

			public Metrics(float lineHeight, float pointSize, float scale) {
				this.lineHeight = lineHeight;
				this.pointSize = pointSize;
				this.scale = scale;
			}

			public override string ToString() {
				return "FontMetrics[lineHeight={0:F2},pointSize={1:F2},scale={2:F2}]".F(
					lineHeight, pointSize, scale);
			}
		}
	}
}
