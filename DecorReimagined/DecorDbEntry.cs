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

namespace ReimaginationTeam.DecorRework {
	/// <summary> 
	/// A database 
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	internal sealed class DecorDbEntry {
		/// <summary>
		/// The decor of that building.
		/// </summary>
		[JsonProperty]
		public float decor { get; set; }

		/// <summary>
		/// The ID of the building to modify.
		/// </summary>
		[JsonProperty]
		public string id { get; set; }

		/// <summary>
		/// The radius where it applies.
		/// </summary>
		[JsonProperty]
		public int radius { get; set; }

		public DecorDbEntry() {
			id = "";
			decor = 0.0f;
			radius = 0;
		}

		public override string ToString() {
			return "DecorDbEntry[id={0},decor={1:F0},radius={2}]".F(id, decor, radius);
		}
	}
}
