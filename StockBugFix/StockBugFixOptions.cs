/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.Options;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// The options class used for Stock Bug Fix.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	[ModInfo("Stock Bug Fix", "https://github.com/peterhaneve/ONIMods", "preview.png")]
	[RestartRequired]
	public sealed class StockBugFixOptions : POptions.SingletonOptions<StockBugFixOptions> {
		/// <summary>
		/// If true, neutronium digging errands will be allowed. These will only ever complete
		/// with the "Super Productive" trait active.
		/// </summary>
		[Option("Allow Neutronium Digging", "Allows Dig errands to be scheduled on Neutronium tiles.")]
		[JsonProperty]
		public bool AllowNeutroniumDig { get; set; }

		/// <summary>
		/// If true, tepidizer pulsing will be allowed to heat material past the intended
		/// temperature. Some builds rely on this.
		/// </summary>
		[Option("Allow Tepidizer Pulsing", "Allow the Liquid Tepidizer to be pulsed rapidly to increase its temperature beyond its usual limits.")]
		[JsonProperty]
		public bool AllowTepidizerPulsing { get; set; }

		/// <summary>
		/// If true, overheat temperature patches will be applied.
		/// </summary>
		[Option("Fix Overheat Temperatures", "Adds missing overheat temperatures to some buildings, and\r\nremoves it from other buildings where it does not make sense.")]
		[JsonProperty]
		public bool FixOverheat { get; set; }

		/// <summary>
		/// Allows changing food storage to a store errand. Does not affect cooking supply.
		/// </summary>
		[Option("Store Food Chore Type", "Selects which type of chore is used for storing food in Ration Boxes or Refrigerators.\r\nDoes not affect deliveries to the Electric Grill, Microbe Musher, or Gas Range.")]
		[JsonProperty]
		public StoreFoodCategory StoreFoodChoreType { get; set; }

		public StockBugFixOptions() {
			AllowNeutroniumDig = false;
			AllowTepidizerPulsing = false;
			FixOverheat = true;
			StoreFoodChoreType = StoreFoodCategory.Store;
		}

		public override string ToString() {
			return "StockBugFixOptions[allowNeutronium={1},allowTepidizer={2},fixOverheat={0},foodChoreType={3}]".F(
				FixOverheat, AllowNeutroniumDig, AllowTepidizerPulsing, StoreFoodChoreType);
		}
	}

	/// <summary>
	/// The chore type to use for storing food so your build team does not keep running off to
	/// put food in the fridges...
	/// </summary>
	public enum StoreFoodCategory {
		[Option("Store", "Store Food uses the Store priority")]
		Store,
		[Option("Supply", "Store Food uses the Supply priority")]
		Supply,
	}
}
