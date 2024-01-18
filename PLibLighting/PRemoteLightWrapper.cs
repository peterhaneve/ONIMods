/*
 * Copyright 2024 Peter Han
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
	/// Wraps a lighting system call from another mod's namespace.
	/// </summary>
	internal sealed class PRemoteLightWrapper : ILightShape {
		// The delegate type covering calls to FillLight from other mods.
		private delegate void FillLightDelegate(GameObject source, int cell, int range,
			BrightnessDict brightness);

		/// <summary>
		/// Creates a light shape instance from another mod.
		/// </summary>
		/// <param name="other">The object to convert.</param>
		/// <returns>A light shape object in this mod's namespace that delegates lighting
		/// calls to the other mod if necessary.</returns>
		internal static ILightShape LightToInstance(object other) {
			return (other == null || other.GetType().Name != nameof(PLightShape)) ? null :
				((other is ILightShape ls) ? ls : new PRemoteLightWrapper(other));
		}

		/// <summary>
		/// The method to call when lighting system handling is requested.
		/// </summary>
		private readonly FillLightDelegate fillLight;

		public string Identifier { get; }

		public LightShape KleiLightShape { get; }

		public LightShape RayMode { get; }

		internal PRemoteLightWrapper(object other) {
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			if (!PPatchTools.TryGetPropertyValue(other, nameof(ILightShape.KleiLightShape),
					out LightShape ls))
				throw new ArgumentException("Light shape is missing KleiLightShape");
			KleiLightShape = ls;
			if (!PPatchTools.TryGetPropertyValue(other, nameof(ILightShape.Identifier),
					out string id) || id == null)
				throw new ArgumentException("Light shape is missing Identifier");
			Identifier = id;
			if (!PPatchTools.TryGetPropertyValue(other, nameof(ILightShape.RayMode),
					out LightShape rm))
				rm = (LightShape)(-1);
			RayMode = rm;
			var otherType = other.GetType();
			fillLight = otherType.CreateDelegate<FillLightDelegate>(nameof(PLightShape.
				DoFillLight), other, typeof(GameObject), typeof(int), typeof(int),
				typeof(BrightnessDict));
			if (fillLight == null)
				throw new ArgumentException("Light shape is missing FillLight");
		}

		public void FillLight(LightingArgs args) {
			fillLight.Invoke(args.Source, args.SourceCell, args.Range, args.Brightness);
		}
	}
}
