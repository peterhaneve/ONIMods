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

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Stores the strings used in Thermal Tooltips.
	/// </summary>
	static class ThermalTooltipsStrings {
		// State change message (fallback if icon not found)
		public static LocString CHANGES = "Changes";

		// Total heat energy in kDTU
		public static LocString HEAT_ENERGY = "Heat Energy: {0:##0.#} {1}";

		// Thermal mass is the amount of kDTU required to shift by 1 degree C/K/F
		public static LocString THERMAL_MASS = "Thermal Mass: {0:##0.#} {1}/{2}";

		// <heat icon> to <element> (temperature)
		public static LocString TO_JOIN = " to ";
	}
}
