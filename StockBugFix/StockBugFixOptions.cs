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
		/// If true, overheat temperature patches will be applied.
		/// </summary>
		[Option("Fix Overheat Temperatures", "Adds missing overheat temperatures to some buildings, and\r\nremoves it from other buildings where it does not make sense.")]
		[JsonProperty]
		public bool FixOverheat { get; set; }

		public StockBugFixOptions() {
			AllowNeutroniumDig = false;
			FixOverheat = true;
		}

		public override string ToString() {
			return "StockBugFixOptions[allowNeutronium={1},fixOverheat={0}]".F(FixOverheat,
				AllowNeutroniumDig);
		}
	}
}
