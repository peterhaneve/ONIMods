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

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// The options class used for Claustrophobia.
	/// </summary>
	[ModInfo("Claustrophobia - Stuck Duplicant Alert", "https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class ClaustrophobiaOptions {
		[Option("Strict Confined Warning", "If true, Confined notifications will " +
			"only\r\nbe shown for Duplicants who are also Trapped.")]
		[JsonProperty]
		public bool StrictConfined { get; set; }

		[Option("Stuck Threshold (s)", "The minimum time for which a Duplicant must be " +
			"stuck before a notification is displayed.")]
		[Limit(0.0, 30.0)]
		[JsonProperty]
		public int StuckThreshold { get; set; }

		public ClaustrophobiaOptions() {
			StrictConfined = false;
			StuckThreshold = 3;
		}

		public override string ToString() {
			return "ClaustrophobiaOptions[strict={0},threshold{1:D}]".F(StrictConfined,
				StuckThreshold);
		}
	}
}
