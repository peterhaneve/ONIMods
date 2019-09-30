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
		[JsonProperty]
		public float BrokenBuildingDecor { get; set; }

		[JsonProperty]
		public float DebrisDecor { get; set; }

		[JsonProperty]
		public int DebrisRadius { get; set; }

		[Option("Hard Mode", "Make your Duplicants more picky about decor, and your life much harder")]
		[JsonProperty]
		public bool HardMode { get; set; }

		public DecorReimaginedOptions() {
			BrokenBuildingDecor = -60.0f;
			DebrisDecor = -10.0f;
			DebrisRadius = 2;
			HardMode = false;
		}

		public override string ToString() {
			return "DecorReimaginedOptions[hard={0}]".F(HardMode);
		}
	}
}
