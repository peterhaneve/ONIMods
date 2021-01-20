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
using PeterHan.PLib.Options;
using UnityEngine;

namespace PeterHan.OldPipeColor {
	/// <summary>
	/// The options class used for Custom Pipe Colors.
	/// </summary>
	[ModInfo("Custom Pipe Colors", "https://github.com/peterhaneve/ONIMods")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class OldPipeColorOptions {
		[Option("Red", "The RED component of the insulated pipe color from 0 to 255 inclusive", "Insulated")]
		[JsonProperty]
		[Limit(0, 255)]
		public int InsulatedRed { get; set; }

		[Option("Green", "The GREEN component of the insulated pipe color from 0 to 255 inclusive", "Insulated")]
		[JsonProperty]
		[Limit(0, 255)]
		public int InsulatedGreen { get; set; }

		[Option("Blue", "The BLUE component of the insulated pipe color from 0 to 255 inclusive", "Insulated")]
		[JsonProperty]
		[Limit(0, 255)]
		public int InsulatedBlue { get; set; }

		[Option("Red", "The RED component of the normal pipe color from 0 to 255 inclusive", "Normal")]
		[JsonProperty]
		[Limit(0, 255)]
		public int NormalRed { get; set; }

		[Option("Green", "The GREEN component of the normal pipe color from 0 to 255 inclusive", "Normal")]
		[JsonProperty]
		[Limit(0, 255)]
		public int NormalGreen { get; set; }

		[Option("Blue", "The BLUE component of the normal pipe color from 0 to 255 inclusive", "Normal")]
		[JsonProperty]
		[Limit(0, 255)]
		public int NormalBlue { get; set; }

		[Option("Red", "The RED component of the radiant pipe color from 0 to 255 inclusive", "Radiant")]
		[JsonProperty]
		[Limit(0, 255)]
		public int RadiantRed { get; set; }

		[Option("Green", "The GREEN component of the radiant pipe color from 0 to 255 inclusive", "Radiant")]
		[JsonProperty]
		[Limit(0, 255)]
		public int RadiantGreen { get; set; }

		[Option("Blue", "The BLUE component of the radiant pipe color from 0 to 255 inclusive", "Radiant")]
		[JsonProperty]
		[Limit(0, 255)]
		public int RadiantBlue { get; set; }

		[JsonIgnore]
		public Color32 InsulatedColor {
			get {
				return new Color32((byte)InsulatedRed.InRange(0, 255), (byte)InsulatedGreen.
					InRange(0, 255), (byte)InsulatedBlue.InRange(0, 255), 0);
			}
		}

		[JsonIgnore]
		public Color32 NormalColor {
			get {
				return new Color32((byte)NormalRed.InRange(0, 255), (byte)NormalGreen.
					InRange(0, 255), (byte)NormalBlue.InRange(0, 255), 0);
			}
		}

		[JsonIgnore]
		public Color32 RadiantColor {
			get {
				return new Color32((byte)RadiantRed.InRange(0, 255), (byte)RadiantGreen.
					InRange(0, 255), (byte)RadiantBlue.InRange(0, 255), 0);
			}
		}

		public OldPipeColorOptions() {
			InsulatedRed = 220;
			InsulatedGreen = 144;
			InsulatedBlue = 48;
			NormalRed = 201;
			NormalGreen = 201;
			NormalBlue = 201;
			RadiantRed = 231;
			RadiantGreen = 193;
			RadiantBlue = 68;
		}
	}
}
