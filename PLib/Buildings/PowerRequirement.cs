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

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// Stores related information about a building's power requirements.
	/// </summary>
	public class PowerRequirement {
		/// <summary>
		/// The maximum building wattage.
		/// </summary>
		public float MaxWattage { get; }

		/// <summary>
		/// The location of the plug related to the foundation tile.
		/// </summary>
		public CellOffset PlugLocation { get; }

		public PowerRequirement(float wattage, CellOffset plugLocation) {
			if (wattage.IsNaNOrInfinity() || wattage < 0.0f)
				throw new ArgumentException("wattage");
			MaxWattage = wattage;
			PlugLocation = plugLocation;
		}

		public override string ToString() {
			return "Power[Watts={0:F0},Location={1}]".F(MaxWattage, PlugLocation);
		}
	}
}
