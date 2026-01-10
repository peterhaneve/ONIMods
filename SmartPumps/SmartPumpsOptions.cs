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
using PeterHan.PLib;
using PeterHan.PLib.Options;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// The options class used for Smart Pumps.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods")]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class SmartPumpsOptions : SingletonOptions<SmartPumpsOptions> {
		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_LARGEGASPUMP")]
		[Limit(5.0, 480.0)]
		[JsonProperty]
		public float PowerLargeGasPump { get; set; }

		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_LARGELIQUIDPUMP")]
		[Limit(5.0, 480.0)]
		[JsonProperty]
		public float PowerLargeLiquidPump { get; set; }

		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.POWER", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_SMALLGASPUMP")]
		[Limit(5.0, 480.0)]
		[JsonProperty]
		public float PowerSmallGasPump { get; set; }

		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_LARGEGASPUMP")]
		[DynamicOption(typeof(LogFloatOptionsEntry))]
		[Limit(0.01, 1.0)]
		[JsonProperty]
		public float RateLargeGasPump { get; set; }

		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_LARGELIQUIDPUMP")]
		[DynamicOption(typeof(LogFloatOptionsEntry))]
		[Limit(0.01, 10.0)]
		[JsonProperty]
		public float RateLargeLiquidPump { get; set; }

		[Option("STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.RATE", "STRINGS.UI.FRONTEND.SMARTPUMPS.CATEGORY_SMALLGASPUMP")]
		[DynamicOption(typeof(LogFloatOptionsEntry))]
		[Limit(0.01, 1.0)]
		[JsonProperty]
		public float RateSmallGasPump { get; set; }
		
		public SmartPumpsOptions() {
			PowerLargeGasPump = 240.0f;
			PowerLargeLiquidPump = 240.0f;
			PowerSmallGasPump = 90.0f;
			RateLargeGasPump = 0.5f;
			RateLargeLiquidPump = 10.0f;
			RateSmallGasPump = 0.05f;
		}
	}
}
