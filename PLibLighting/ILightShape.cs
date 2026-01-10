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

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// An interface describing local and remote instances of PLightShape.
	/// </summary>
	public interface ILightShape {
		/// <summary>
		/// The light shape identifier.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// The Klei LightShape represented by this light shape, used in Light2D definitions.
		/// </summary>
		LightShape KleiLightShape { get; }

		/// <summary>
		/// The raycast mode used by this light shape. (-1) if no rays are to be emitted.
		/// </summary>
		LightShape RayMode { get; }

		/// <summary>
		/// Invokes the light handler with the provided light information.
		/// </summary>
		/// <param name="args">The arguments passed to the user light handler.</param>
		void FillLight(LightingArgs args);
	}
}
