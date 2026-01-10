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

namespace PeterHan.FastSave {
	/// <summary>
	/// The options class used for Fast Save.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class FastSaveOptions : SingletonOptions<FastSaveOptions> {
		/// <summary>
		/// Cycles to retain of colony summary when in Moderate mode.
		/// </summary>
		internal const int SUMMARY_MODERATE = 20;
		/// <summary>
		/// Cycles to retain of colony summary when in Aggressive mode.
		/// </summary>
		internal const int SUMMARY_AGGRESSIVE = 4;

		[Option("Background Save", "Performs autosaves in the background, allowing\r\ngame play to continue while saving")]
		[JsonProperty]
		public bool BackgroundSave { get; set; }

		[Option("Save Optimization", "Trades off impact to colony statistics with save / load time")]
		[JsonProperty]
		public FastSaveMode Mode { get; set; }

		[Option("Use Delegates", "Reduces time consuming reflection during saves to reduce total time to save")]
		[JsonProperty]
		public bool DelegateSave { get; set; }

		public FastSaveOptions() {
			Mode = FastSaveMode.Safe;
			DelegateSave = false;
			BackgroundSave = true;
		}

		public override string ToString() {
			return "FastSaveOptions[mode={0},background={1},delegate={2}]".F(Mode,
				BackgroundSave, DelegateSave);
		}

		/// <summary>
		/// The available modes.
		/// 
		/// Safe mode does not affect colony summary. Moderate retains 20 cycles of colony
		/// summary. Aggressive retains 4 cycles of colony summary.
		/// </summary>
		public enum FastSaveMode {
			[Option("Safe", "Daily Reports: All")]
			Safe,
			[Option("Moderate", "Daily Reports: 20 Cycles")]
			Moderate,
			[Option("Aggressive", "Daily Reports: 4 Cycles")]
			Aggressive
		}
	}
}
