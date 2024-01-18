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
using PeterHan.PLib.Detours;

using ReceptacleDirection = SingleEntityReceptacle.ReceptacleDirection;

namespace PeterHan.PipPlantOverlay {
	/// <summary>
	/// The dirty work to test tiles and determine why they are not suitable.
	/// </summary>
	internal static class PipPlantOverlayTests {
		private delegate int CountNearbyPlants(int cell, int radius);

		/// <summary>
		/// Reference to the current value of ObjectLayer.Building.
		/// </summary>
		private static readonly int BUILDINGS_LAYER = (int)PGameUtils.GetObjectLayer(
			nameof(ObjectLayer.Building), ObjectLayer.Building);

		private static readonly CountNearbyPlants COUNT_PLANTS = typeof(PlantableCellQuery).
			Detour<CountNearbyPlants>();

		private static readonly IDetouredField<PlantableCellQuery, int> GET_PLANT_RADIUS =
			PDetours.DetourField<PlantableCellQuery, int>("plantDetectionRadius");

		private static readonly IDetouredField<PlantableCellQuery, int> GET_PLANT_COUNT =
			PDetours.DetourField<PlantableCellQuery, int>("maxPlantsInRadius");

		/// <summary>
		/// This const is hardcoded in TemperatureVulnerable
		/// </summary>
		internal const float PRESSURE_THRESHOLD = 0.1f;

		/// <summary>
		/// Game versions of this value and higher allow Vacuum as a planting location (but
		/// not gases at low pressures?).
		///
		/// TODO Remove when these versions are all dead
		/// </summary>
		internal const uint PRESSURE_VERSION = 567980U;

		/// <summary>
		/// The maximum plantable temperature in K, for sanity - the highest plant surviving
		/// temperature is Sporechid at 564 K.
		/// </summary>
		internal const float TEMP_MAX = 563.15f;

		/// <summary>
		/// The minimum plantable temperature in K, for sanity - the lowest plant surviving
		/// temperature is Sleet Wheat at 120 K.
		/// </summary>
		internal const float TEMP_MIN = 118.15f;

		/// <summary>
		/// How many plants are allowed.
		/// </summary>
		internal static int PlantCount { get; private set; }

		/// <summary>
		/// How far to check for plants.
		/// </summary>
		internal static int PlantRadius { get; private set; }

		/// <summary>
		/// By default, an off by one error causes the Pip plant radius checked to be
		/// asymmetrical. Other mods fix this, track the flag here to update the view.
		/// </summary>
		internal static bool SymmetricalRadius { get; set; }

		/// <summary>
		/// Checks a cell to see if pips can plant there.
		/// 
		/// This method cannot check the available height criteria (some plants are 1x1), so it
		/// only checks for buildings in that cell.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <returns>Why pips can or cannot plant there.</returns>
		internal static PipPlantFailedReasons CheckCell(int cell) {
			var result = PipPlantFailedReasons.NoPlantablePlot;
			if (Grid.IsValidCell(cell) && !Grid.Solid[cell] && Grid.Objects[cell,
					BUILDINGS_LAYER] == null) {
				int above = Grid.CellAbove(cell), below = Grid.CellBelow(cell);
				bool nb = IsPlantable(below, ReceptacleDirection.Top), na = IsPlantable(above,
					ReceptacleDirection.Bottom);
				float temp = Grid.Temperature[cell];
				if (nb)
					// Check below
					result = CheckCellInternal(cell, below);
				if (na && result == PipPlantFailedReasons.NoPlantablePlot)
					// Check above
					result = CheckCellInternal(cell, above);
			}
			return result;
		}

		/// <summary>
		/// Checks a cell to see if pips can plant there.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="plant">The cell the plant will occupy when it is planted.</param>
		/// <returns>Why pips can or cannot plant there.</returns>
		private static PipPlantFailedReasons CheckCellInternal(int cell, int plant) {
			PipPlantFailedReasons result;
			float temp = Grid.Temperature[cell];
			// Check below
			if (IsTooHard(plant))
				result = PipPlantFailedReasons.Hardness;
			else if (COUNT_PLANTS.Invoke(plant, PlantRadius) > PlantCount)
				result = PipPlantFailedReasons.PlantCount;
			else if (IsUnderPressure(cell))
				result = PipPlantFailedReasons.Pressure;
			// Absolute zero (vacuum) is allowed in Song of the Moo
			else if ((temp < TEMP_MIN || temp > TEMP_MAX) && (PUtil.GameVersion <
					PRESSURE_VERSION || temp > 0.0f))
				result = PipPlantFailedReasons.Temperature;
			else
				result = PipPlantFailedReasons.CanPlant;
			return result;
		}

		/// <summary>
		/// Checks to see if a cell is a natural tile of sufficient hardness, or a valid
		/// farm plot.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="direction">The direction which a farm plot must be facing to be valid.</param>
		/// <returns>true if it is a valid planting location, or false otherwise.</returns>
		internal static bool IsAcceptableCell(int cell, ReceptacleDirection direction) {
			return IsPlantable(cell, direction) && !IsTooHard(cell);
		}

		/// <summary>
		/// Checks to see if a cell is a natural tile, or a valid farm plot.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="direction">The direction which a farm plot must be facing to be valid.</param>
		/// <returns>true if it is a valid planting location, or false otherwise.</returns>
		private static bool IsPlantable(int cell, ReceptacleDirection direction) {
			bool valid = false;
			if (Grid.IsSolidCell(cell)) {
				var building = Grid.Objects[cell, BUILDINGS_LAYER];
				if (building == null)
					valid = true;
				else if (building.TryGetComponent(out PlantablePlot plot))
					// Check direction
					valid = plot.Direction == direction;
			}
			return valid;
		}

		/// <summary>
		/// Checks to see if a cell is too hard to plant in.
		/// </summary>
		/// <param name="cell">The cell to check. Must be a valid tile.</param>
		/// <returns>true if it is too hard to plant, or false otherwise.</returns>
		private static bool IsTooHard(int cell) {
			var element = Grid.Element[cell];
			return element == null || element.hardness >= GameUtil.Hardness.NEARLY_IMPENETRABLE;
		}

		/// <summary>
		/// Checks to see if a cell has too low of a pressure to plant in, or is flooded which
		/// prevents plants other than Waterweed from being planted.
		/// </summary>
		/// <param name="cell">The cell to check. Must be a valid tile.</param>
		/// <returns>true if the pressure is too low or it is flooded, or false otherwise.</returns>
		private static bool IsUnderPressure(int cell) {
			var element = Grid.Element[cell];
			// Threshold is hardcoded in DrowningMonitor
			return element == null || (element.id == SimHashes.Vacuum ? PUtil.GameVersion <
				PRESSURE_VERSION : Grid.Mass[cell] < PRESSURE_THRESHOLD ||
				Grid.IsSubstantialLiquid(cell, 0.95f));
		}

		/// <summary>
		/// Queries PlantableCellQuery for the valid plant range.
		/// </summary>
		internal static void UpdatePlantCriteria() {
			var pcq = PathFinderQueries.plantableCellQuery;
			PlantRadius = GET_PLANT_RADIUS.Get(pcq);
			PlantCount = GET_PLANT_COUNT.Get(pcq);
		}
	}

	/// <summary>
	/// Why can pips not plant here?
	/// </summary>
	public enum PipPlantFailedReasons {
		CanPlant, NoPlantablePlot, Hardness, PlantCount, Pressure, Temperature
	}
}
