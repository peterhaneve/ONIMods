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

using Newtonsoft.Json;
using PeterHan.PLib;

namespace PeterHan.DecorRework {
	/// <summary>
	/// The options class used for Decor Reimagined.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class DecorReimaginedOptions {
		/// <summary>
		/// Whether all critters unconditionally get 0 decor. Improves performance but makes
		/// the game harder.
		/// </summary>
		[Option("No Critter Decor", "Removes decor from all critters.\r\nIncreases " +
			"performance on large critter farms, but may make the game harder.")]
		[JsonProperty]
		public bool AllCrittersZeroDecor { get; set; }

		/// <summary>
		/// The decor of the Atmo Suit and Jet Suit items.
		/// </summary>
		[JsonProperty]
		public int AtmoSuitDecor { get; set; }

		/// <summary>
		/// The decor of broken buildings.
		/// </summary>
		[JsonProperty]
		public float BrokenBuildingDecor { get; set; }

		/// <summary>
		/// The decor of each different debris pile.
		/// </summary>
		[JsonProperty]
		public float DebrisDecor { get; set; }

		/// <summary>
		/// The radius of decor from debris.
		/// </summary>
		[JsonProperty]
		public int DebrisRadius { get; set; }

		/// <summary>
		/// Whether hard mode is enabled.
		/// </summary>
		[Option("Hard Mode", "Make your Duplicants more picky about decor, and your life much harder")]
		[JsonProperty]
		public bool HardMode { get; set; }

		/// <summary>
		/// The decor of the Snazzy Suit item.
		/// </summary>
		[JsonProperty]
		public int SnazzySuitDecor { get; set; }

		/// <summary>
		/// The decor of the Warm Vest and Cool Vest items.
		/// </summary>
		[JsonProperty]
		public int VestDecor { get; set; }

		public DecorReimaginedOptions() {
			AllCrittersZeroDecor = false;
			AtmoSuitDecor = -10;
			BrokenBuildingDecor = -60.0f;
			DebrisDecor = -10.0f;
			DebrisRadius = 2;
			SnazzySuitDecor = 15;
			HardMode = false;
			VestDecor = 0;
		}

		public override string ToString() {
			return "DecorReimaginedOptions[hard={0}]".F(HardMode);
		}
	}
}
