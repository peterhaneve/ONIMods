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
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace PeterHan.EfficientFetch {
	/// <summary>
	/// The options class used for Efficient Supply.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class EfficientFetchOptions {
		[Option("Minimum Amount (%)", "The minimum percentage of material required to\r\n" +
			"supply a chore, unless no other items are available (0-100)")]
		[Limit(0, 100)]
		[JsonProperty]
		public int MinimumAmountPercent { get; set; }

		public EfficientFetchOptions() {
			MinimumAmountPercent = 25;
		}

		/// <summary>
		/// Retrieves the minimum ratio of the total amount to accept.
		/// </summary>
		/// <returns>The minimum fetched quantity ratio.</returns>
		public float GetMinimumRatio() {
			return (MinimumAmountPercent * 0.01f).InRange(0.0f, 1.0f);
		}

		public override string ToString() {
			return "EfficientFetchOptions[minimumAmount={0}]".F(MinimumAmountPercent);
		}
	}
}
