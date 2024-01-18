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

using System.Collections.Generic;

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// Updates all Thermal Interface Plates on the map. While it leads to more stutter than
	/// slicing them, this is the only way to solve the fact that the Sim only allows one
	/// temperature update per sim frame.
	/// </summary>
	public sealed class ThermalInterfaceManager : KMonoBehaviour, ISim200ms {
		public static ThermalInterfaceManager Instance { get; private set; }

		/// <summary>
		/// Destroys the singleton instance of this object.
		/// </summary>
		internal static void DestroyInstance() {
			Instance = null;
		}

		/// <summary>
		/// Stores a list of all thermal interface plates.
		/// </summary>
		private readonly IList<ThermalInterfacePlate> plates;

		/// <summary>
		/// Used to store temperatures when batch updating plates.
		/// </summary>
		private readonly IDictionary<BuildingComplete, float> temperatures;

		internal ThermalInterfaceManager() {
			Instance = this;
			plates = new List<ThermalInterfacePlate>(32);
			temperatures = new Dictionary<BuildingComplete, float>(128);
		}

		/// <summary>
		/// Adds a thermal interface plate.
		/// </summary>
		/// <param name="plate">The thermal interface plate to add.</param>
		public void AddThermalPlate(ThermalInterfacePlate plate) {
			plates.Add(plate);
		}

		protected override void OnCleanUp() {
			plates.Clear();
			base.OnCleanUp();
		}

		/// <summary>
		/// Removes a thermal interface plate.
		/// </summary>
		/// <param name="plate">The thermal interface plate to remove.</param>
		public void RemoveThermalPlate(ThermalInterfacePlate plate) {
			plates.Remove(plate);
		}

		public void Sim200ms(float dt) {
			int n = plates.Count;
			for (int i = 0; i < n; i++) {
				var plate = plates[i];
				if (plate == null) {
					// Should be uncommon, plates should be removed by their OnCleanUp
					plates.RemoveAt(i--);
					n--;
				} else {
					plate.RunSim(dt, temperatures);
				}
			}
			foreach (var pair in temperatures) {
				var building = pair.Key;
				if (building != null)
					// Temperature will be set by a Sim callback
					building.primaryElement.Temperature = pair.Value;
			}
			temperatures.Clear();
		}
	}
}
