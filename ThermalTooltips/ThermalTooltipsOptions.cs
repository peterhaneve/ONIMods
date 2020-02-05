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

using Newtonsoft.Json;
using PeterHan.PLib;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// The options class used for Thermal Tooltips.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class ThermalTooltipsOptions {
		/// <summary>
		/// Whether to display all temperature units.
		/// </summary>
		[Option("Display All Units", "Displays thermal information in Fahrenheit, Celsius, and Kelvin.")]
		[JsonProperty]
		public bool AllUnits { get; set; }

		[Option("Test 1", category: "Fake Options")]
		public string String { get; set; }

		[Option("Test 2", category: "Fake Options")]
		public int Item1 { get; set; }

		[Option("Test 3", category: "Fake Options")]
		public int Item2 { get; set; }

		[Option("Test 4", category: "Fake Options")]
		public int Item3 { get; set; }

		[Option("Test 5", category: "Fake Options")]
		public int Item4 { get; set; }

		[Option("Test 6", category: "Fake Options")]
		public int Item5 { get; set; }

		[Option("Test 7", category: "Fake Options")]
		public int Item6 { get; set; }

		[Option("Test 8", category: "Fake Options")]
		public int Item7 { get; set; }

		[Option("Test 9", category: "Fake Options")]
		public int Item8 { get; set; }

		[Option("Test 10", category: "Fake Options")]
		public int Item9 { get; set; }

		[Option("Test 11", category: "Fake Options")]
		public int Item10 { get; set; }

		[Option("Test 12", category: "Fake Options")]
		public int Item11 { get; set; }

		[Option("Test 13", category: "Fake Options")]
		public int Item12 { get; set; }

		[Option("Test 14", category: "Fake Options")]
		public int Item13 { get; set; }

		[Option("Test 15", category: "Fake Options")]
		public int Item14 { get; set; }

		[Option("Test 16", category: "Fake Options")]
		public int Item15 { get; set; }

		[Option("Test 17", category: "Fake Options")]
		public int Item16 { get; set; }

		[Option("Test 18", category: "Fake Options")]
		public int Item17 { get; set; }

		/// <summary>
		/// Whether to only show tooltips on the thermal overlay.
		/// </summary>
		[Option("Only on Thermal Overlay", "Shows thermal information only when the Temperature Overlay is selected.")]
		[JsonProperty]
		public bool OnlyOnThermalOverlay { get; set; }

		public ThermalTooltipsOptions() {
			AllUnits = false;
			OnlyOnThermalOverlay = true;
		}

		public override string ToString() {
			return "ThermalTooltipsOptions[onlyOverlay={0},allUnits={1}]".F(
				OnlyOnThermalOverlay, AllUnits);
		}
	}
}
