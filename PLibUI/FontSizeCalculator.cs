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
using System;
using System.Collections.Generic;
using System.Reflection;
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

		// TODO Massive cleanup when versions before U57-716056 no longer need to be supported
		private static readonly PropertyInfo FACE_SIZE_NEW;
		
		private static readonly PropertyInfo GLYPH_NEW;
		
		private static readonly PropertyInfo GLYPH_DICTIONARY_NEW;
		
		private static readonly MethodInfo GLYPH_LOOKUP_NEW;

		private static readonly Type FACE_INFO_OLD = PPatchTools.GetTypeSafe("TMPro.FaceInfo");
		
		private static readonly FieldInfo FACE_HEIGHT_OLD;

		private static readonly FieldInfo FACE_SCALE_OLD;

		private static readonly FieldInfo FACE_SIZE_OLD;
		
		private static readonly PropertyInfo GET_INFO;
		
		private static readonly FieldInfo GLYPH_WIDTH_OLD;

		private static readonly PropertyInfo GLYPH_DICTIONARY_OLD;
		
		private static readonly MethodInfo GLYPH_LOOKUP_OLD;

		static FontSizeCalculator() {
			Type dictType;
			if (FACE_INFO_OLD != null) {
				var tmpGlyph = PPatchTools.GetTypeSafe("TMPro.TMP_Glyph");

				FACE_HEIGHT_OLD = FACE_INFO_OLD.GetFieldSafe("LineHeight", false);
				FACE_SCALE_OLD = FACE_INFO_OLD.GetFieldSafe("Scale", false);
				FACE_SIZE_OLD = FACE_INFO_OLD.GetFieldSafe("PointSize", false);
				GLYPH_DICTIONARY_OLD = typeof(TMP_FontAsset).GetProperty(
					"characterDictionary", PPatchTools.BASE_FLAGS | BindingFlags.Instance);
				GET_INFO = typeof(TMP_FontAsset).GetProperty("fontInfo", PPatchTools.
					BASE_FLAGS | BindingFlags.Instance);

				if (tmpGlyph != null) {
					dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), tmpGlyph);

					GLYPH_LOOKUP_OLD = dictType.GetMethodSafe(nameof(Dictionary<int, int>.
						TryGetValue), false, typeof(int), tmpGlyph.MakeByRefType());
					GLYPH_WIDTH_OLD = tmpGlyph.GetFieldSafe("width", false);
				} else {
					GLYPH_LOOKUP_OLD = null;
					GLYPH_WIDTH_OLD = null;
				}
			} else {
				var glyphMetrics = PPatchTools.GetTypeSafe("UnityEngine.TextCore.GlyphMetrics");
				var tmpCharacter = PPatchTools.GetTypeSafe("TMPro.TMP_Character");
				var faceInfo = typeof(UnityEngine.TextCore.FaceInfo);
				
				FACE_SIZE_NEW = faceInfo.GetPropertySafe<float>("pointSize", false);
				GLYPH_NEW = typeof(TMP_TextElement).GetPropertySafe<Glyph>("glyph", false);
				GLYPH_DICTIONARY_NEW = typeof(TMP_FontAsset).GetProperty(
					"characterLookupTable", PPatchTools.BASE_FLAGS | BindingFlags.Instance);
				GET_INFO = typeof(TMP_Asset).GetProperty("faceInfo", PPatchTools.BASE_FLAGS |
					BindingFlags.Instance);

				if (tmpCharacter != null) {
					dictType = typeof(Dictionary<,>).MakeGenericType(typeof(uint),
						tmpCharacter);

					GLYPH_LOOKUP_NEW = dictType.GetMethodSafe(nameof(Dictionary<int, int>.
						TryGetValue), false, typeof(uint), tmpCharacter.MakeByRefType());
				} else {
					GLYPH_LOOKUP_NEW = null;
				}
			}
		}

		/// <summary>
		/// Gets the unscaled width of the character in the specified font.
		/// </summary>
		/// <param name="ch">The character to calculate.</param>
		/// <param name="font">The font to use for calculation.</param>
		/// <returns>The unscaled width of the specified character, or 0.0f if the character
		/// metrics are not available.</returns>
		internal static float GetCharWidth(char ch, TMP_FontAsset font) {
			float width = 0.0f;
			object outGlyph;

			if (GLYPH_DICTIONARY_NEW != null) {
				var dict = GLYPH_DICTIONARY_NEW.GetValue(font);
				var lookupParams = new object[] { (uint)ch, null };

				if (dict != null && GLYPH_LOOKUP_NEW?.Invoke(dict, lookupParams) is bool
						result && result && (outGlyph = lookupParams[1]) != null && GLYPH_NEW?.
						GetValue(outGlyph) is Glyph glyph)
					// Pull glyph from TMP_Character (parent class is TMP_TextElement)
					width = glyph.metrics.width;
			} else if (GLYPH_DICTIONARY_OLD != null) {
				var dict = GLYPH_DICTIONARY_OLD.GetValue(font);
				var lookupParams = new object[] { (int)ch, null };

				if (dict != null && GLYPH_LOOKUP_OLD?.Invoke(dict, lookupParams) is bool
						result && result && (outGlyph = lookupParams[1]) != null &&
						GLYPH_WIDTH_OLD?.GetValue(outGlyph) is float fv)
					width = fv;
			}
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
			float height = 0.0f, size = 0.0f, scale = 0.0f;
			var face = GET_INFO?.GetValue(font);

			if (face is UnityEngine.TextCore.FaceInfo faceInfo) {
				// New TMPro
				height = faceInfo.lineHeight;
				if (FACE_SIZE_NEW.GetValue(face) is float sv)
					// Unfortunately this is int in the old version
					size = sv;
				scale = faceInfo.scale;
			} else if (face != null && FACE_HEIGHT_OLD != null) {
				if (FACE_HEIGHT_OLD.GetValue(face) is float fv)
					height = fv;
				if (FACE_SIZE_OLD.GetValue(face) is float sv)
					size = sv;
				if (FACE_SCALE_OLD.GetValue(face) is float cv)
					scale = cv;
			}
			return new Metrics(height, size, scale);
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
