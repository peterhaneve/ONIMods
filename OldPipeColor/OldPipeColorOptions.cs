/*
 * Copyright 2024 Peter Han
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

using System.Collections.Generic;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using UnityEngine;

namespace PeterHan.OldPipeColor {
	/// <summary>
	/// The options class used for Custom Pipe Colors.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class OldPipeColorOptions : IOptions {
		[JsonProperty]
		public int InsulatedRed { get; set; }

		[JsonProperty]
		public int InsulatedGreen { get; set; }

		[JsonProperty]
		public int InsulatedBlue { get; set; }

		[JsonProperty]
		public int NormalRed { get; set; }

		[JsonProperty]
		public int NormalGreen { get; set; }

		[JsonProperty]
		public int NormalBlue { get; set; }

		[JsonProperty]
		public int RadiantRed { get; set; }

		[JsonProperty]
		public int RadiantGreen { get; set; }

		[JsonProperty]
		public int RadiantBlue { get; set; }

		[Option("Insulated Color", "The color displayed in piping overlays for insulated pipes.")]
		[JsonProperty]
		public Color32 InsulatedColor { get; set; }

		[Option("Normal Color", "The color displayed in piping overlays for normal pipes.")]
		[JsonProperty]
		public Color32 NormalColor { get; set; }

		[Option("Radiant Color", "The color displayed in piping overlays for radiant pipes.")]
		[JsonProperty]
		public Color32 RadiantColor { get; set; }

		public OldPipeColorOptions() {
			InsulatedColor = new Color32(220, 144, 48, 255);
			NormalColor = new Color32(201, 201, 201, 255);
			RadiantColor = new Color32(231, 193, 68, 255);
		}

		/// <summary>
		/// Loads the color set from previous versions of this mod, if somehow the config
		/// survived Steam updating.
		/// </summary>
		internal void LoadCompatibility() {
			if (InsulatedRed != 0 || InsulatedGreen != 0 || InsulatedBlue != 0) {
				InsulatedColor = new Color32((byte)InsulatedRed.InRange(0, 255),
					(byte)InsulatedGreen.InRange(0, 255), (byte)InsulatedBlue.InRange(0, 255),
					255);
				InsulatedRed = 0;
				InsulatedGreen = 0;
				InsulatedBlue = 0;
			}
			if (NormalRed != 0 || NormalGreen != 0 || NormalBlue != 0) {
				NormalColor = new Color32((byte)NormalRed.InRange(0, 255), (byte)NormalGreen.
					InRange(0, 255), (byte)NormalBlue.InRange(0, 255), 255);
				NormalRed = 0;
				NormalGreen = 0;
				NormalBlue = 0;
			}
			if (RadiantRed != 0 || RadiantGreen != 0 || RadiantBlue != 0) {
				RadiantColor = new Color32((byte)RadiantRed.InRange(0, 255),
					(byte)RadiantGreen.InRange(0, 255), (byte)RadiantBlue.InRange(0, 255),
					255);
				RadiantRed = 0;
				RadiantGreen = 0;
				RadiantBlue = 0;
			}
		}

		public IEnumerable<IOptionsEntry> CreateOptions() {
			LoadCompatibility();
			return null;
		}

		public void OnOptionsChanged() { }
	}
}
