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

namespace PeterHan.FastSave {
	/// <summary>
	/// The options class used for Fast Save.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class FastSaveOptions {
		/// <summary>
		/// Time entries ending this many in-game seconds before the current time will be
		/// removed in Safe mode.
		/// </summary>
		internal const float USAGE_SAFE = 6200.0f;
		/// <summary>
		/// Time entries ending this many in-game seconds before the current time will be
		/// removed in Moderate mode.
		/// </summary>
		internal const float USAGE_MODERATE = 620.0f;
		/// <summary>
		/// Time entries ending this many in-game seconds before the current time will be
		/// removed in Aggressive mode.
		/// </summary>
		internal const float USAGE_AGGRESSIVE = 62.0f;

		/// <summary>
		/// Cycles to retain of colony summary when in Moderate mode.
		/// </summary>
		internal const int SUMMARY_MODERATE = 20;
		/// <summary>
		/// Cycles to retain of colony summary when in Aggressive mode.
		/// </summary>
		internal const int SUMMARY_AGGRESSIVE = 4;

		[Option("Save Optimization", "Trades off impact to colony statistics with save / load time")]
		[JsonProperty]
		public FastSaveMode Mode { get; set; }

		public FastSaveOptions() {
			Mode = FastSaveMode.Safe;
		}

		public override string ToString() {
			return "FastSaveOptions[mode={0}]".F(Mode);
		}

		/// <summary>
		/// The available modes.
		/// 
		/// Safe mode does not affect colony summary or uptime. Moderate retains 20 cycles of
		/// colony summary and 600s of uptime. Aggressive retains 4 cycles of colony summary
		/// and 60s of uptime.
		/// </summary>
		public enum FastSaveMode {
			[Option("Safe", "Daily Reports: All, Operational History: 10 Cycles")]
			Safe,
			[Option("Moderate", "Daily Reports: 20 Cycles, Operational History: 1 Cycle")]
			Moderate,
			[Option("Aggressive", "Daily Reports: 2 Cycles, Operational History: 60 Seconds")]
			Aggressive
		}
	}
}
