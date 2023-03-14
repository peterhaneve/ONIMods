/*
 * Copyright 2023 Peter Han
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
using KSerialization;
using UnityEngine;

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// A component which transfers heat among buildings and entities in its cell.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class ThermalInterfacePlate : KMonoBehaviour, ISim1000ms {
		/// <summary>
		/// Transfers heat from this object to and from the specified building.
		/// </summary>
		/// <param name="thisElement">The element of this thermal interface plate.</param>
		/// <param name="newBuilding">The target building to transfer heat.</param>
		/// <param name="temperatures">The location where the intermediate temperature will be stored.</param>
		/// <param name="temp1">The temperature of this building.</param>
		/// <param name="c1">The modified specific heat per cell of the thermal interface plate.</param>
		/// <param name="dt">The time delta from the last transfer.</param>
		/// <returns>The new temperature of this building.</returns>
		private static float TransferHeatTo(PrimaryElement thisElement,
				BuildingComplete newBuilding, IDictionary<BuildingComplete, float> temperatures,
				float temp1, float c1, float dt) {
			var thatElement = newBuilding.primaryElement;
			var def = newBuilding.Def;
			int area2 = 0;
			float tnew1 = temp1;
			Element element2;
			if (def != null)
				area2 = def.WidthInCells * def.HeightInCells;
			if (thatElement != null && (element2 = thatElement.Element) != null && area2 > 0) {
				if (!temperatures.TryGetValue(newBuilding, out float temp2))
					temp2 = thatElement.Temperature;
				// No /5 factor since we are transferring building to building, it cancels out
				float k2 = element2.specificHeatCapacity, k1 = thisElement.Element.
					specificHeatCapacity, c2 = k2 * thatElement.Mass / area2, chot = c2;
				if (temp1 > temp2)
					chot = c1;
				float tmin = Math.Min(temp1, temp2), tmax = Math.Max(temp1, temp2), dq = dt *
					k1 * k2 * chot * (temp1 - temp2) * 0.5f, tnew2 = (temp2 + dq / c2).
					InRange(tmin, tmax);
				tnew1 = (temp1 - dq / c1).InRange(tmin, tmax);
				// No order swaps, reach equilibrium
				if ((temp1 - temp2) * (tnew1 - tnew2) < 0.0f)
					tnew1 = tnew2 = (c1 * temp1 + c2 * temp2) / (c1 + c2);
				temperatures[newBuilding] = tnew2;
			}
			return tnew1;
		}

		/// <summary>
		/// The layer for tempshift plates.
		/// </summary>
		private readonly int backwallLayer;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		/// <summary>
		/// The completed thermal interface plate building.
		/// </summary>
		[MyCmpReq]
		private BuildingComplete building;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

		/// <summary>
		/// Ensures that the thermal interface plate is added to the batched sim.
		/// </summary>
		private bool initialized;

		/// <summary>
		/// The number of object layers, determined at RUNTIME.
		/// </summary>
		private readonly int numObjectLayers;

		/// <summary>
		/// The buildings to which heat will be transferred.
		/// </summary>
		private readonly ICollection<TransferTarget> transferCache;

		public ThermalInterfacePlate() {
			backwallLayer = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Backwall),
				ObjectLayer.Backwall);
			numObjectLayers = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.NumLayers),
				ObjectLayer.NumLayers);
			transferCache = new HashSet<TransferTarget>(TransferTargetComparer.INSTANCE);
			initialized = false;
		}

		/// <summary>
		/// Adds the thermal interface plate to the manager if it is not already there.
		/// </summary>
		private void InitIfNeeded() {
			var inst = ThermalInterfaceManager.Instance;
			if (inst != null && !initialized) {
				inst.AddThermalPlate(this);
				initialized = true;
			}
		}

		protected override void OnCleanUp() {
			var inst = ThermalInterfaceManager.Instance;
			if (inst != null && initialized)
				inst.RemoveThermalPlate(this);
			transferCache.Clear();
			initialized = false;
			base.OnCleanUp();
		}

		protected override void OnSpawn() {
			base.OnSpawn();
			InitIfNeeded();
		}

		/// <summary>
		/// Runs the heat transfer simulation from this thermal interface plate to other
		/// buildings.
		/// </summary>
		/// <param name="dt">The time delta from the last transfer.</param>
		/// <param name="temperatures">The building temperatures already modified by other TSPs.</param>
		public void RunSim(float dt, IDictionary<BuildingComplete, float> temperatures) {
			var pe = building.primaryElement;
			var def = building.Def;
			Element element;
			if (pe != null && def != null && (element = pe.Element) != null) {
				// TSPs are 1x1, the area should never be zero
				float temp = pe.Temperature;
				float c1 = pe.Mass * element.specificHeatCapacity / (def.WidthInCells * def.
					HeightInCells);
				// https://forums.kleientertainment.com/forums/topic/84275-decrypting-heat-transfer/
				if (c1 > 0.0f) {
					foreach (var target in transferCache)
						if (target.IsValid)
							temp = TransferHeatTo(pe, target.building, temperatures, temp, c1,
								dt);
					pe.Temperature = temp;
				}
			}
		}

		public void Sim1000ms(float ignore) {
			InitIfNeeded();
			transferCache.Clear();
			// Update the buildings to which heat should be transferred
			int cell = Grid.PosToCell(this);
			for (int i = 0; i < numObjectLayers; i++) {
				var go = Grid.Objects[cell, i];
				// Ignore solid tiles
				if (go != null && go.TryGetComponent(out BuildingComplete newBuilding) &&
						newBuilding != building && !go.TryGetComponent(out SimCellOccupier _))
					transferCache.Add(new TransferTarget(go, newBuilding));
			}
			// Transfer heat to adjacent tempshift plates since those have 3x3 radii
			for (int dx = -1; dx <= 1; dx++)
				for (int dy = -1; dy <= 1; dy++) {
					int newCell = Grid.OffsetCell(cell, new CellOffset(dx, dy));
					if (newCell != cell && Grid.IsValidBuildingCell(newCell)) {
						var go = Grid.Objects[newCell, backwallLayer];
						if (go != null && go.TryGetComponent(out BuildingComplete plate) &&
								plate.Def?.PrefabID == ThermalBlockConfig.ID)
							// Is it a completed tempshift plate?
							transferCache.Add(new TransferTarget(go, plate));
					}
				}
		}

		/// <summary>
		/// A potential target for heat transfer via TIP.
		/// </summary>
		private readonly struct TransferTarget : IEquatable<TransferTarget> {
			/// <summary>
			/// Reports whether this building is valid (otherwise, it was destroyed).
			/// </summary>
			public bool IsValid => go != null;

			/// <summary>
			/// The building to transfer.
			/// </summary>
			internal readonly BuildingComplete building;

			/// <summary>
			/// The base game object which will receive the heat.
			/// </summary>
			private readonly GameObject go;

			public TransferTarget(GameObject go, BuildingComplete building) {
				this.building = building;
				this.go = go;
			}

			public override bool Equals(object obj) {
				return obj is TransferTarget other && go != null && go.Equals(other.go);
			}

			public bool Equals(TransferTarget other) {
				return go != null && go.Equals(other.go);
			}

			public override int GetHashCode() {
				return go.GetHashCode();
			}
		}

		/// <summary>
		/// Compares two transfer targets without any boxing or memory allocations.
		/// </summary>
		private sealed class TransferTargetComparer : IEqualityComparer<TransferTarget> {
			internal static readonly IEqualityComparer<TransferTarget> INSTANCE =
				new TransferTargetComparer();

			private TransferTargetComparer() { }

			public bool Equals(TransferTarget x, TransferTarget y) {
				return x.building != null && x.building.Equals(y.building);
			}

			public int GetHashCode(TransferTarget obj) {
				return obj.GetHashCode();
			}
		}
	}
}
