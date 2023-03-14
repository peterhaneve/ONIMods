/*
 * Copyright 2023 Peter Han
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

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// The results of checking the mod version.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class ModVersionCheckResults {
		/// <summary>
		/// true if the mod is up to date, or false if it is out of date.
		/// </summary>
		[JsonProperty]
		public bool IsUpToDate { get; set; }

		/// <summary>
		/// The mod whose version was queried. The current mod version is available on this
		/// mod through its packagedModInfo.
		/// </summary>
		[JsonProperty]
		public string ModChecked { get; set; }

		/// <summary>
		/// The new version of this mod. If it is not available, it can be null, even if
		/// IsUpdated is false. Not relevant if IsUpToDate reports true.
		/// </summary>
		[JsonProperty]
		public string NewVersion { get; set; }

		public ModVersionCheckResults() : this("", false) { }

		public ModVersionCheckResults(string id, bool updated, string newVersion = null) {
			IsUpToDate = updated;
			ModChecked = id;
			NewVersion = newVersion;
		}

		public override bool Equals(object obj) {
			return obj is ModVersionCheckResults other && other.ModChecked == ModChecked &&
				IsUpToDate == other.IsUpToDate && NewVersion == other.NewVersion;
		}

		public override int GetHashCode() {
			return ModChecked.GetHashCode();
		}

		public override string ToString() {
			return "ModVersionCheckResults[{0},updated={1},newVersion={2}]".F(
				ModChecked, IsUpToDate, NewVersion ?? "");
		}
	}
}
