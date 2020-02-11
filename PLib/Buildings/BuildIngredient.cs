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
	/// An ingredient to be used in a building.
	/// </summary>
	public class BuildIngredient {
		/// <summary>
		/// The material tag name.
		/// </summary>
		public string Material { get; }

		/// <summary>
		/// The quantity required in kg.
		/// </summary>
		public float Quantity { get; }

		public BuildIngredient(string name, float quantity) {
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			if (quantity.IsNaNOrInfinity() || quantity <= 0.0f)
				throw new ArgumentException("quantity");
			Material = name;
			Quantity = quantity;
		}

		public BuildIngredient(string[] material, int tier) : this(material[0], tier) { }

		public BuildIngredient(string material, int tier) {
			// Intended for use from the MATERIALS and BUILDINGS presets
			if (string.IsNullOrEmpty(material))
				throw new ArgumentNullException("material");
			Material = material;
			float qty;
			switch (tier) {
			case -1:
				// Tier -1: 5
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER_TINY[0];
				break;
			case 0:
				// Tier 0: 25
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER0[0];
				break;
			case 1:
				// Tier 1: 50
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER1[0];
				break;
			case 2:
				// Tier 2: 100
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER2[0];
				break;
			case 3:
				// Tier 3: 200
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3[0];
				break;
			case 4:
				// Tier 4: 400
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER4[0];
				break;
			case 5:
				// Tier 5: 800
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER5[0];
				break;
			case 6:
				// Tier 6: 1200
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER6[0];
				break;
			case 7:
				// Tier 7: 2000
				qty = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER7[0];
				break;
			default:
				throw new ArgumentException("tier must be between -1 and 7 inclusive");
			}
			Quantity = qty;
		}

		public override bool Equals(object obj) {
			return obj is BuildIngredient other && other.Material == Material && other.
				Quantity == Quantity;
		}

		public override int GetHashCode() {
			return Material.GetHashCode();
		}

		public override string ToString() {
			return "Material[Tag={0},Quantity={1:F0}]".F(Material, Quantity);
		}
	}
}
