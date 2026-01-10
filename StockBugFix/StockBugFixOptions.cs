/*
 * Copyright 2026 Peter Han
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
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// The options class used for Stock Bug Fix.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	[ModInfo("https://github.com/peterhaneve/ONIMods", "preview.png")]
	[RestartRequired]
	public sealed class StockBugFixOptions : SingletonOptions<StockBugFixOptions> {
		/// <summary>
		/// If true, tepidizer pulsing will be allowed to heat material past the intended
		/// temperature. Some builds rely on this.
		/// </summary>
		[Option("Allow Tepidizer Pulsing", "Allow the Liquid Tepidizer to be pulsed rapidly to increase its temperature beyond its usual limits.")]
		[JsonProperty]
		public bool AllowTepidizerPulsing { get; set; }
		
		/// <summary>
		/// If true, duplicate attributes in minion selection screens that do not actually add
		/// the listed value to the Duplicant's final attributes will be hidden.
		/// </summary>
		[Option("Clarify Attributes", "Hide duplicate skill-granted attributes in Duplicant selection that do not actually add to the final attributes.")]
		[JsonProperty]
		public bool FixMultipleAttributes { get; set; }

		/// <summary>
		/// If true, overheat temperature patches will be applied.
		/// </summary>
		[Option("Fix Overheat Temperatures", "Adds missing overheat temperatures to some buildings, and\r\nremoves it from other buildings where it does not make sense.")]
		[JsonProperty]
		public bool FixOverheat { get; set; }
		
		/// <summary>
		/// If true, plant irrigation patches will be applied.
		/// </summary>
		[Option("Fix Plants", "Prevent consumption of fertilizer and irrigation by plants that cannot grow.")]
		[JsonProperty]
		public bool FixPlants { get; set; }

		/// <summary>
		/// If true, trait conflict patches will be applied.
		/// </summary>
		[Option("Fix Trait Conflicts", "Prevents nonsensical combinations of traits and interests that contradict each other from appearing.")]
		[JsonProperty]
		public bool FixTraits { get; set; }
		
		/// <summary>
		/// If true, fixes a bug where generator minimum output temperatures are ignored.
		/// </summary>
		[Option("Minimum Output Temperatures", "Corrects the minimum output exhaust temperatures of many generators.")]
		[JsonProperty]
		public bool MinOutputTemperature { get; set; }

		/// <summary>
		/// If true, locks out the Mods button until the race condition which reinstalls all
		/// mods clears out, or until the timeout passes.
		/// </summary>
		[Option("Prevent Mod Reinstalls", "Disable the Mods menu until the vanilla game race condition that could reinstall all mods resolves.")]
		[JsonProperty]
		public bool DelayModsMenu { get; set; }

		/// <summary>
		/// Allows changing food and equipment storage to a store errand. Does not affect cooking supply.
		/// </summary>
		[Option("Storage Chore Type", "Selects which type of chore is used for storing food in Ration Boxes or Refrigerators,\r\nand storing equipment such as Atmo Suits.\r\n\r\n" +
			"Does not affect deliveries to the Electric Grill, Microbe Musher, or Gas Range.")]
		[JsonProperty]
		public StoreFoodCategory StoreFoodChoreType { get; set; }

		public StockBugFixOptions() {
			AllowTepidizerPulsing = false;
			DelayModsMenu = true;
			FixMultipleAttributes = true;
			FixOverheat = true;
			FixPlants = true;
			FixTraits = true;
			MinOutputTemperature = false;
			StoreFoodChoreType = StoreFoodCategory.Store;
		}

		public override string ToString() {
			return "StockBugFixOptions[allowTepidizer={1},fixOverheat={0},foodChoreType={2},fixAttributes={3},fixTraits={4},delayModsMenu={5},minOutput={6}]".F(
				FixOverheat, AllowTepidizerPulsing, StoreFoodChoreType, FixMultipleAttributes,
				FixTraits, DelayModsMenu, MinOutputTemperature);
		}
	}

	/// <summary>
	/// The chore type to use for storing food so your build team does not keep running off to
	/// put food in the fridges...
	/// </summary>
	public enum StoreFoodCategory {
		[Option("Store", "Store Food and Store Equipment use the Store priority")]
		Store,
		[Option("Supply", "Store Food and Store Equipment use the Supply priority")]
		Supply,
	}
}
