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

using BrightnessDict = System.Collections.Generic.IDictionary<int, float>;
using Octant = DiscreteShadowCaster.Octant;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// A builder class which creates default light patterns based on octants.
	/// </summary>
	public sealed class OctantBuilder {
		/// <summary>
		/// The method to call to scan octants.
		/// </summary>
		private static readonly ScanOctantFunc OCTANT_SCAN;

		static OctantBuilder() {
			// Cache the method for faster execution
			OCTANT_SCAN = typeof(DiscreteShadowCaster).CreateStaticDelegate<ScanOctantFunc>(
				"ScanOctant", typeof(Vector2I), typeof(int), typeof(int), typeof(Octant),
				typeof(double), typeof(double), typeof(List<int>));
			if (OCTANT_SCAN == null)
				PUtil.LogError("OctantBuilder cannot find default octant scanner!");
		}

		private delegate void ScanOctantFunc(Vector2I cellPos, int range, int depth,
			Octant octant, double startSlope, double endSlope, List<int> visiblePoints);

		/// <summary>
		/// The fallout to use when building the light.
		/// </summary>
		public float Falloff { get; set; }

		/// <summary>
		/// If false, uses the default game smoothing. If true, uses better smoothing.
		/// </summary>
		public bool SmoothLight { get; set; }

		/// <summary>
		/// The origin cell.
		/// </summary>
		public int SourceCell { get; }

		/// <summary>
		/// The location where light cells are added.
		/// </summary>
		private readonly BrightnessDict destination;

		/// <summary>
		/// Creates a new octant builder.
		/// </summary>
		/// <param name="destination">The location where the lit cells will be placed.</param>
		/// <param name="sourceCell">The origin cell of the light.</param>
		public OctantBuilder(BrightnessDict destination, int sourceCell) {
			if (!Grid.IsValidCell(sourceCell))
				throw new ArgumentException("sourceCell");
			this.destination = destination ?? throw new ArgumentNullException("destination");
			destination[sourceCell] = 1.0f;
			Falloff = 0.5f;
			// Use the default game's light algorithm
			SmoothLight = false;
			SourceCell = sourceCell;
		}

		/// <summary>
		/// Adds an octant of light.
		/// </summary>
		/// <param name="range">The range of the light.</param>
		/// <param name="octant">The octant to scan.</param>
		/// <returns>This object, for call chaining.</returns>
		public OctantBuilder AddOctant(int range, Octant octant) {
			var points = ListPool<int, OctantBuilder>.Allocate();
			OCTANT_SCAN?.Invoke(Grid.CellToXY(SourceCell), range, 1, octant, 1.0, 0.0, points);
			// Transfer to our array using:
			foreach (int cell in points) {
				float intensity;
				if (SmoothLight)
					// Better, not rounded falloff
					intensity = PLightShape.GetSmoothFalloff(Falloff, cell, SourceCell);
				else
					// Default falloff
					intensity = PLightShape.GetDefaultFalloff(Falloff, cell, SourceCell);
				destination[cell] = intensity;
			}
			points.Recycle();
			return this;
		}

		public override string ToString() {
			return "OctantBuilder[Cell {0:D}, {1:D} lit]".F(SourceCell, destination.Count);
		}
	}
}
