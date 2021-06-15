/*
 * Copyright 2021 Peter Han
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
using UnityEngine;

using BrightnessDict = System.Collections.Generic.IDictionary<int, float>;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Represents a light shape which can be used by mods.
	/// </summary>
	internal sealed class PLightShape : ILightShape {
		/// <summary>
		/// The handler for this light shape.
		/// </summary>
		private readonly PLightManager.CastLightDelegate handler;

		public string Identifier { get; }

		public LightShape KleiLightShape {
			get {
				return ShapeID + LightShape.Cone;
			}
		}

		public LightShape RayMode { get; }

		/// <summary>
		/// The light shape ID.
		/// </summary>
		internal int ShapeID { get; }

		internal PLightShape(int id, string identifier, PLightManager.CastLightDelegate handler,
				LightShape rayMode) {
			this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
			Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
			RayMode = rayMode;
			ShapeID = id;
		}

		public override bool Equals(object obj) {
			return obj is PLightShape other && other.Identifier == Identifier;
		}

		/// <summary>
		/// Invokes the light handler with the provided light information.
		/// </summary>
		/// <param name="source">The source of the light.</param>
		/// <param name="cell">The origin cell.</param>
		/// <param name="range">The range to fill.</param>
		/// <param name="brightness">The location where lit points will be stored.</param>
		internal void DoFillLight(GameObject source, int cell, int range,
				BrightnessDict brightness) {
			handler.Invoke(new LightingArgs(source, cell, range, brightness));
		}

		public void FillLight(LightingArgs args) {
			handler.Invoke(args);
		}

		public override int GetHashCode() {
			return Identifier.GetHashCode();
		}

		public override string ToString() {
			return "PLightShape[ID=" + Identifier + "]";
		}
	}
}
