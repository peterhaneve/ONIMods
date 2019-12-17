/*
 * Copyright 2019 Peter Han
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

using PeterHan.PLib;
using System;

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// A component which transfers heat among buildings and entities in its cell.
	/// </summary>
	public sealed class ThermalInterfacePlate : KMonoBehaviour, ISim200ms {
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		/// <summary>
		/// The completed thermal interface plate building.
		/// </summary>
		[MyCmpGet]
		private BuildingComplete building;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		public void Sim200ms(float dt) {
			// Might be expensive, but heat transfer needs to be simulated relatively accurately
			var pe = building?.primaryElement;
			if (pe != null) {
				int cell = Grid.PosToCell(this);
				// https://forums.kleientertainment.com/forums/topic/84275-decrypting-heat-transfer/
				float temp = pe.Temperature;
				for (int i = 0; i < (int)ObjectLayer.NumLayers; i++) {
					var newBuilding = Grid.Objects[cell, i].GetComponentSafe<BuildingComplete>();
					if (newBuilding != null && newBuilding != building)
						temp = TransferHeatTo(pe, newBuilding, temp, dt);
				}
				pe.Temperature = temp;
			}
		}

		/// <summary>
		/// Transfers heat from this object to and from the specified building.
		/// </summary>
		/// <param name="thisElement">The element of this thermal interface plate.</param>
		/// <param name="newBuilding">The target building to transfer heat.</param>
		/// <param name="temp1">The temperature of this building.</param>
		/// <param name="dt">The time delta from the last transfer.</param>
		/// <returns>The new temperature of this building.</returns>
		private float TransferHeatTo(PrimaryElement thisElement, BuildingComplete newBuilding,
				float temp1, float dt) {
			var thatElement = newBuilding.primaryElement;
			int area2 = newBuilding.Def.WidthInCells * newBuilding.Def.HeightInCells,
				area1 = building.Def.WidthInCells * building.Def.HeightInCells;
			// Capacity per cell for target building
			Element element1 = thisElement.Element, element2 = thatElement?.Element;
			float mass1 = thisElement.Mass, tnew1 = temp1;
			if (element1 != null && element2 != null && area1 > 0 && area2 > 0) {
				// No /5 factor since we are transferring building to building, it cancels out
				float k2 = element2.specificHeatCapacity, k1 = element1.specificHeatCapacity,
					temp2 = thatElement.Temperature, c2 = k2 * thatElement.Mass / area2,
					chot = c2, c1 = mass1 * k1 / area1;
				if (temp1 > temp2)
					chot = c1;
				float tmin = Math.Min(temp1, temp2), tmax = Math.Max(temp1, temp2), dq = dt *
					k1 * k2 * chot * (temp1 - temp2) * 0.5f, tnew2 = (temp2 + dq / c2).
					InRange(tmin, tmax);
				tnew1 = (temp1 - dq / c1).InRange(tmin, tmax);
				// No order swaps, reach equilibrium
				if ((temp1 - temp2) * (tnew1 - tnew2) < 0.0f)
					tnew1 = tnew2 = (c1 * temp1 + c2 * temp2) / (c1 + c2);
				// Temperature is modified in sim via a callback
				thatElement.Temperature = tnew2;
			}
			return tnew1;
		}
	}
}
