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

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PeterHan.FastSave {
	/// <summary>
	/// Stores either raw (native byte) data to be encoded as a PNG, or the PNG bytes
	/// themselves.
	/// </summary>
	internal sealed class ImageData {
		/// <summary>
		/// If the PNG was already encoded, stores the compressed texture.
		/// </summary>
		private readonly byte[] png;

		/// <summary>
		/// Otherwise, stores the raw bytes.
		/// </summary>
		private readonly byte[] raw;

		/// <summary>
		/// The original texture height.
		/// </summary>
		private readonly int height;

		/// <summary>
		/// The original texture width.
		/// </summary>
		private readonly int width;

		internal ImageData(int width, int height, byte[] raw) {
			this.height = height;
			png = null;
			this.raw = raw ?? throw new ArgumentNullException(nameof(raw));
			this.width = width;
		}

		internal ImageData(byte[] png) {
			height = 0;
			this.png = png ?? throw new ArgumentNullException(nameof(png));
			width = 0;
		}

		/// <summary>
		/// Encodes the data to a PNG as raw bytes if necessary.
		/// </summary>
		/// <returns>The compressed image data for the timelapse.</returns>
		public byte[] GetData() {
			return png ?? ImageConversion.EncodeArrayToPNG(raw, GraphicsFormatUtility.
				GetGraphicsFormat(TextureFormat.RGBA32, false), (uint)width, (uint)height);
		}
	}
}
