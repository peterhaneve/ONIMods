/*
 * Copyright 2022 Peter Han
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
using PeterHan.PLib.Options;

namespace PeterHan.FastTrack {
	/// <summary>
	/// The options class used for Fast Track.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods")]
	[ConfigFile(SharedConfigLocation: true)]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class FastTrackOptions : SingletonOptions<FastTrackOptions> {
		[Option("Background Pathing", "Moves some pathfinding calculations to a non-blocking thread.\n\n<b>Performance Impact: High</b>", "Duplicants")]
		[JsonProperty]
		public bool AsyncPathProbe { get; set; }

		[Option("Cache Paths", "Cache frequently used paths and reuse them in future calculations.\n\n<b>Performance Impact: High</b>", "Duplicants")]
		[JsonProperty]
		public bool CachePaths { get; set; }

		[Option("Disable Conversations", "Disables all Duplicant thought and speech balloons.\n\n<b>Performance Impact: Low</b>", "Duplicants")]
		[JsonProperty]
		public bool NoConversations { get; set; }

		[Option("Optimize Sensors", "Only check for locations to Idle, Mingle, or Balloon Artist when necessary.\n\n<b>Performance Impact: Low</b>", "Duplicants")]
		[JsonProperty]
		public bool SensorOpts { get; set; }

		[Option("Optimize Debris Collection", "Speed up inefficient and memory-intensive checks for items.\n<i>Not compatible with mods: Efficient Supply</i>\n\n<b>Performance Impact: Low</b>", "Items")]
		[JsonProperty]
		public bool FastUpdatePickups { get; set; }

		[Option("Reduce Debris Checks", "Only look for the closest items every sim tick,\nrather than every frame for each Duplicant.\n\n<b>Performance Impact: Medium</b>", "Items")]
		[JsonProperty]
		public bool PickupOpts { get; set; }

		[Option("Batch Sounds", "Reduce the frequency of sound location updates.\n\n<b>Performance Impact: Low</b>", "Interface")]
		[JsonProperty]
		public bool ReduceSoundUpdates { get; set; }

		[Option("Optimize Renderers", "Optimizes several renderers that run every frame.\n<i>Some visual artifacts may appear with no effect on gameplay</i>\n\n<b>Performance Impact: Medium</b>", "Interface")]
		[JsonProperty]
		public bool RenderTicks { get; set; }

		[Option("Log Debug Metrics", "Logs extra debug information to the game log.\n\n<b>Only use this option if directed to do so by a developer.</b>", "Miscellaneous")]
		[JsonProperty]
		public bool Metrics { get; set; }

		public FastTrackOptions() {
			AsyncPathProbe = true;
			CachePaths = true;
			FastUpdatePickups = false;
			SensorOpts = true;
			Metrics = false;
			NoConversations = false;
			PickupOpts = true;
			ReduceSoundUpdates = true;
			RenderTicks = true;
		}
	}
}
