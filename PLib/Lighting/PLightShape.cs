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

using System;
using System.Collections.Generic;
using UnityEngine;

using BrightnessDict = System.Collections.Generic.IDictionary<int, float>;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Represents a light shape which can be used by mods.
	/// </summary>
	public sealed class PLightShape {
		/// <summary>
		/// Implemented by classes which want to handle light.
		/// </summary>
		/// <param name="source">The source game object.</param>
		/// <param name="args">The parameters to use for lighting, and the location to
		/// store results. See the LightingArgs class documentation for details.</param>
		public delegate void CastLight(GameObject source, LightingArgs args);

		/// <summary>
		/// Calculates the brightness falloff as it would be in the stock game.
		/// </summary>
		/// <param name="falloffRate">The falloff rate to use.</param>
		/// <param name="cell">The cell where falloff is being computed.</param>
		/// <param name="origin">The light origin cell.</param>
		/// <returns>The brightness at that location from 0 to 1.</returns>
		public static float GetDefaultFalloff(float falloffRate, int cell, int origin) {
			return 1.0f / Math.Max(1.0f, Mathf.RoundToInt(falloffRate * Math.Max(Grid.
				GetCellDistance(origin, cell), 1)));
		}

		/// <summary>
		/// Calculates the brightness falloff similar to the default falloff, but far smoother.
		/// Slightly heavier on computation however.
		/// </summary>
		/// <param name="falloffRate">The falloff rate to use.</param>
		/// <param name="cell">The cell where falloff is being computed.</param>
		/// <param name="origin">The light origin cell.</param>
		/// <returns>The brightness at that location from 0 to 1.</returns>
		public static float GetSmoothFalloff(float falloffRate, int cell, int origin) {
			Vector2I newCell = Grid.CellToXY(cell), start = Grid.CellToXY(origin);
			return 1.0f / Math.Max(1.0f, falloffRate * PUtil.Distance(start.X, start.Y,
				newCell.X, newCell.Y));
		}

		/// <summary>
		/// Registers a light shape handler.
		/// </summary>
		/// <param name="identifier">A unique identifier for this shape. If another mod has
		/// already registered that identifier, the previous mod will take precedence.</param>
		/// <param name="handler">The handler for that shape.</param>
		/// <returns>The light shape which can be used.</returns>
		public static PLightShape Register(string identifier, CastLight handler) {
			PLightShape lightShape;
			// In case this call is used before the library was initialized
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"PLightShape.Register!");
			}
			lock (PSharedData.GetLock(PRegistry.KEY_LIGHTING_LOCK)) {
				// Get list holding lighting information
				var list = PSharedData.GetData<IList<object>>(PRegistry.KEY_LIGHTING_TABLE);
				if (list == null)
					PSharedData.PutData(PRegistry.KEY_LIGHTING_TABLE, list =
						new List<object>(8));
				// Try to find a match for this identifier
				object ls = null;
				int n = list.Count, index = 0;
				for (int i = 0; i < n; i++) {
					var light = list[i];
					// Might be from another assembly so the types may or may not be compatible
					if (light != null && light.ToString() == identifier && light.GetType().
							Name == typeof(PLightShape).Name) {
						index = i;
						break;
					}
				}
				if (ls == null) {
					// Not currently existing
					lightShape = new PLightShape(n + 1, identifier, handler);
					PUtil.LogDebug("Registered new light shape: " + identifier);
					list.Add(lightShape);
				} else {
					// Exists already
					PUtil.LogDebug("Found existing light shape: " + identifier);
					lightShape = new PLightShape(n + 1, identifier, null);
				}
			}
			return lightShape;
		}

		/// <summary>
		/// The handler for this light shape. It may or may not be initialized.
		/// </summary>
		internal CastLight Handler { get; }

		/// <summary>
		/// The light shape identifier.
		/// </summary>
		public string Identifier { get; }

		/// <summary>
		/// The light shape ID.
		/// </summary>
		internal int ShapeID { get; }

		internal PLightShape(int id, string identifier, CastLight handler) {
			Handler = handler;
			Identifier = identifier ?? throw new ArgumentNullException("identifier");
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
		internal void FillLight(GameObject source, int cell, int range,
				BrightnessDict brightness) {
			// Found handler!
			if (source == null)
				PUtil.LogWarning("FillLight: Calling game object is null!");
			else
				Handler?.Invoke(source, new LightingArgs(cell, range, brightness));
		}

		public override int GetHashCode() {
			return Identifier.GetHashCode();
		}

		/// <summary>
		/// Returns the Klei LightShape represented by this light shape.
		/// </summary>
		/// <returns>The Klei light shape.</returns>
		public LightShape GetKLightShape() {
			return ShapeID + LightShape.Cone;
		}

		public override string ToString() {
			// Warning: used to compare to incoming light shapes!
			return Identifier;
		}
	}
}
