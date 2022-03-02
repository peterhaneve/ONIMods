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
		/// <summary>
		/// Shared performance impact descriptions used in multiple options.
		/// </summary>
		private const string PERF_LOW = "\n\n<b>Performance Impact: <color=#00CC00>Low</color></b>";
		private const string PERF_MEDIUM = "\n\n<b>Performance Impact: <color=#FF8827>Medium</color></b>";
		private const string PERF_HIGH = "\n\n<b>Performance Impact: <color=#FF3300>High</color></b>";

		[Option("Critter Monitors", "Optimizes critter Threat and Overcrowding monitors.\n<i>May conflict with mods that add new critters</i>" + PERF_MEDIUM, "Critters")]
		[JsonProperty]
		public bool ThreatOvercrowding { get; set; }

		[Option("Simplify Eating", "Optimize how Critters find objects to eat.\n<i>Some minor changes to Critter behaviour may occur</i>" + PERF_MEDIUM, "Critters")]
		[JsonProperty]
		public bool CritterConsumers { get; set; }

		[Option("Unstack Lights", "Reduces the visual effects shown when many light sources are stacked.\nIntended for ranching critters like Shine Bugs." + PERF_LOW, "Critters")]
		[JsonProperty]
		public bool UnstackLights { get; set; }

		[Option("Background Pathing", "Moves some pathfinding calculations to a non-blocking thread." + PERF_HIGH, "Duplicants")]
		[JsonProperty]
		public bool AsyncPathProbe { get; set; }

		[Option("Cache Paths", "Cache frequently used paths and reuse them in future calculations." + PERF_HIGH, "Duplicants")]
		[JsonProperty]
		public bool CachePaths { get; set; }

		[Option("Disable Conversations", "Disables all Duplicant thought and speech balloons." + PERF_LOW, "Duplicants")]
		[JsonProperty]
		public bool NoConversations { get; set; }

		[Option("Optimize Sensors", "Only check for locations to Idle, Mingle, or Balloon Artist when necessary." + PERF_LOW, "Duplicants")]
		[JsonProperty]
		public bool SensorOpts { get; set; }

		[Option("Optimize Debris Collection", "Speed up inefficient and memory-intensive checks for items.\n<i>Not compatible with mods: Efficient Supply</i>" + PERF_LOW, "Items")]
		[JsonProperty]
		public bool FastUpdatePickups { get; set; }

		[Option("Reduce Debris Checks", "Only look for the closest items when required,\nrather than every frame for each Duplicant." + PERF_MEDIUM, "Items")]
		[JsonProperty]
		public bool PickupOpts { get; set; }

		[Option("Batch Sounds", "Reduce the frequency of sound location updates." + PERF_LOW, "Interface")]
		[JsonProperty]
		public bool ReduceSoundUpdates { get; set; }

		[Option("Info Card Optimization", "Optimize slow code in the info card handlers." + PERF_LOW, "Interface")]
		[JsonProperty]
		public bool InfoCardOpts { get; set; }

		[Option("No Notification Bounce", "Disables the bounce effect when new notifications appear." + PERF_LOW, "Visual")]
		[JsonProperty]
		public bool NoBounce { get; set; }

		[Option("Optimize Renderers", "Optimizes several renderers that run every frame.\n<i>Some visual artifacts may appear with no effect on gameplay</i>" + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool RenderTicks { get; set; }

		[Option("Pipe Animation Quality", "Controls the visual fidelity of pipe animations.\nNo changes to actual pipe mechanics will occur." + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public ConduitAnimationQuality DisableConduitAnimation { get; set; }

		[Option("Reduce Tile Updates", "Reduces the frequency of updates to tile textures." + PERF_HIGH, "Visual")]
		[JsonProperty]
		public bool ReduceTileUpdates { get; set; }

		[Option("Log Debug Metrics", "Logs extra debug information to the game log.\n\n<b>Only use this option if directed to do so by a developer.</b>", "Miscellaneous")]
		[JsonProperty]
		public bool Metrics { get; set; }

		public FastTrackOptions() {
			AsyncPathProbe = true;
			CachePaths = true;
			CritterConsumers = true;
			DisableConduitAnimation = ConduitAnimationQuality.Reduced;
			FastUpdatePickups = false;
			InfoCardOpts = true;
			SensorOpts = true;
			Metrics = false;
			NoBounce = true;
			NoConversations = false;
			PickupOpts = true;
			ReduceSoundUpdates = true;
			ReduceTileUpdates = true;
			RenderTicks = true;
			ThreatOvercrowding = true;
			UnstackLights = true;
		}

		/// <summary>
		/// The quality to use for conduit rendering.
		/// </summary>
		public enum ConduitAnimationQuality {
			[Option("Full", "Pipe animation quality is unchanged from the base game.")]
			Full,
			[Option("Reduced", "Pipe animations update slower when outside the Liquid or Gas overlay.")]
			Reduced,
			[Option("Minimal", "Pipe animations are disabled outside the Liquid or Gas overlay.")]
			Minimal
		}
	}
}
