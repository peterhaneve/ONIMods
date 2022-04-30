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

		[Option("Flatten Averages", "Optimize Amounts and average calculations." + PERF_LOW, "Buildings")]
		[JsonProperty]
		public bool FlattenAverages { get; set; }

		[Option("Logic Optimizations", "Optimizes some buildings to not trigger logic network updates every frame." + PERF_LOW, "Buildings")]
		[JsonProperty]
		public bool LogicUpdates { get; set; }

		[Option("No Colony Reports", "Disables colony reports completely.\n<color=#FF0000>Disabling will prevent some achievements from being unlocked</color>" + PERF_MEDIUM, "Buildings")]
		[JsonProperty]
		public bool NoReports { get; set; }

		[Option("Optimize Heat Generation", "Reduces memory allocations used when generating heat." + PERF_MEDIUM, "Buildings")]
		[JsonProperty]
		public bool FastStructureTemperature { get; set; }

		[Option("Radiation Tweaks", "Speeds up radiation calculations." + PERF_LOW, "Buildings")]
		[JsonProperty]
		public bool RadiationOpts { get; set; }

		[Option("Threaded Conduit Updates", "Multi-threads some updates to liquid and gas conduits." + PERF_LOW, "Buildings")]
		[JsonProperty]
		public bool ConduitOpts { get; set; }

		[Option("Critter Monitors", "Optimizes critter Threat and Overcrowding monitors.\n<i>May conflict with mods that add new critters</i>" + PERF_MEDIUM, "Critters")]
		[JsonProperty]
		public bool ThreatOvercrowding { get; set; }

		[Option("Optimize Eating", "Optimize how Critters find objects to eat.\n<i>Some minor changes to Critter behaviour may occur</i>" + PERF_MEDIUM, "Critters")]
		[JsonProperty]
		public bool CritterConsumers { get; set; }

		[Option("Unstack Lights", "Reduces the visual effects shown when many light sources are stacked.\nIntended for ranching critters like Shine Bugs." + PERF_LOW, "Critters")]
		[JsonProperty]
		public bool UnstackLights { get; set; }

		[Option("Attribute Leveling", "Optimize attribute leveling and work efficiency calculation." + PERF_MEDIUM, "Duplicants")]
		[JsonProperty]
		public bool FastAttributesMode { get; set; }

		[Option("Background Pathing", "Moves some pathfinding calculations to a non-blocking thread." + PERF_HIGH, "Duplicants")]
		[JsonProperty]
		public bool AsyncPathProbe { get; set; }

		[Option("Cache Paths", "Cache frequently used paths and reuse them in future calculations." + PERF_MEDIUM, "Duplicants")]
		[JsonProperty]
		public bool CachePaths { get; set; }

		[Option("Disable Conversations", "Disables all Duplicant thought and speech balloons." + PERF_LOW, "Duplicants")]
		[JsonProperty]
		public bool NoConversations { get; set; }

		[Option("Fast Reachability Checks", "Only check items and chores for reachability when necessary." + PERF_MEDIUM, "Duplicants")]
		[JsonProperty]
		public bool FastReachability { get; set; }

		[Option("Optimize Sensors", "Only check for locations to Idle, Mingle, or Balloon Artist when necessary." + PERF_LOW, "Duplicants")]
		[JsonProperty]
		public bool SensorOpts { get; set; }

		[Option("Colony Tracker Reduction", "Reduces the update rate of Colony Diagnostics.\n<i>Some notifications may take longer to trigger</i>" + PERF_LOW, "Interface")]
		[JsonProperty]
		public bool ReduceColonyTracking { get; set; }

		[Option("Disable Achievements", "Turn off checking for Colony Initiatives.\n<color=#FF0000>Disabling will prevent unlocking any Steam achievement</color>" + PERF_LOW, "Interface")]
		[JsonProperty]
		public AchievementDisable DisableAchievements { get; set; }

		[Option("Disable Load Previews", "Disables loading of colony previews on the Load screen." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool DisableLoadPreviews { get; set; }

		[Option("Disable Tutorials", "Disables tutorial messages." + PERF_LOW, "Interface")]
		[JsonProperty]
		public TutorialMessageDisable DisableTutorial { get; set; }

		[Option("Fast Raycast", "Speeds up searching for UI elements under the cursor." + PERF_HIGH, "Interface")]
		[JsonProperty]
		public bool FastRaycast { get; set; }

		[Option("Less Loading", "Speeds up loading times." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool LoadOpts { get; set; }

		[JsonProperty]
		public bool ModLoadOpts { get; set; }

		[Option("Optimize Dialogs", "Speeds up a variety of dialog boxes." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool OptimizeDialogs { get; set; }

		[Option("Other UI Optimizations", "Optimizes many event and UI handlers." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool MiscOpts { get; set; }

		[Option("Reduce Allocations", "Reduces memory allocations in a variety of locations." + PERF_LOW, "Interface")]
		[JsonProperty]
		public bool AllocOpts { get; set; }

		[Option("Background Inventory", "Compiles the item quantites in the Resources panel on a non-blocking thread." + PERF_MEDIUM, "Items")]
		[JsonProperty]
		public bool ParallelInventory { get; set; }

		[Option("Info Card Optimization", "Optimizes the info cards shown on hover." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool InfoCardOpts { get; set; }

		[Option("Optimize Debris Collection", "Speed up inefficient and memory-intensive checks for items.\n<i>Not compatible with mods: Efficient Supply</i>" + PERF_LOW, "Items")]
		[JsonProperty]
		public bool FastUpdatePickups { get; set; }

		[Option("Reduce Debris Checks", "Only look for the closest items when required,\nrather than every frame for each Duplicant." + PERF_MEDIUM, "Items")]
		[JsonProperty]
		public bool PickupOpts { get; set; }

		[Option("Side Screens", "Optimizes the informational side screens." + PERF_MEDIUM, "Interface")]
		[JsonProperty]
		public bool SideScreenOpts { get; set; }

		[Option("Vector Minimization", "Reduces memory allocations in most game-wide lists of items." + PERF_LOW, "Items")]
		[JsonProperty]
		public bool MinimalKCV { get; set; }

		[Option("Batch Sounds", "Reduces the frequency of sound location updates." + PERF_LOW, "Sound")]
		[JsonProperty]
		public bool ReduceSoundUpdates { get; set; }

		[Option("Disable <color=#FF0000>ALL</color> Sounds", "Completely disables all sound playback." + PERF_LOW, "Sound")]
		[JsonProperty]
		public bool DisableSound { get; set; }

		[Option("Cull Buildings in Tiles", "Hide conduits and wires inside solid walls." + PERF_LOW, "Visual")]
		[JsonProperty]
		public bool CullConduits { get; set; }

		[Option("Faster Animations", "Optimizes slow code in animation playback." + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool AnimOpts { get; set; }

		[Option("Instant Place Graphics", "Disables the animation which appears when placing\nerrands like Dig or Mop." + PERF_LOW, "Visual")]
		[JsonProperty]
		public bool NoPlacerEasing { get; set; }

		[Option("No Notification Bounce", "Disables the bounce effect when new notifications appear." + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool NoBounce { get; set; }

		[Option("No Splashes", "Disables most puff, splash, and breath animations." + PERF_LOW, "Visual")]
		[JsonProperty]
		public bool NoSplash { get; set; }

		[Option("No Tile Caps", "Disables all ornaments and edges on constructed tiles." + PERF_LOW, "Visual")]
		[JsonProperty]
		public bool NoTileDecor { get; set; }

		[Option("Optimize Renderers", "Optimizes several renderers that run every frame.\n<i>Some visual artifacts may appear with no effect on gameplay</i>" + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool RenderTicks { get; set; }

		[Option("Pipe Animation Quality", "Controls the visual fidelity of pipe animations.\n<i>No changes to actual pipe mechanics will occur</i>" + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public ConduitAnimationQuality DisableConduitAnimation { get; set; }

		[Option("Quick Format", "Reduces memory allocations when formatting strings.\n<i>Not compatible with mods: DisplayAllTemps</i>" + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool CustomStringFormat { get; set; }

		[Option("Threaded Tile Updates", "Multi-threads updates to most tile textures." + PERF_HIGH, "Visual")]
		[JsonProperty]
		public bool ReduceTileUpdates { get; set; }

		[Option("Use Mesh Renderers", "Use faster mesh renderers instead of redrawing\nevery frame for these graphics." + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public MeshRendererSettings MeshRendererOptions { get; set; }

		[Option("Virtual Scroll", "Improves the speed of scrolling menus." + PERF_MEDIUM, "Visual")]
		[JsonProperty]
		public bool VirtualScroll { get; set; }

		[Option("Log Debug Metrics", "Logs extra debug information to the game log.\n\n<b>Only use this option if directed to do so by a developer.</b>", "Debug")]
		[JsonProperty]
		public bool Metrics { get; set; }

		public FastTrackOptions() {
			AllocOpts = true;
			AnimOpts = true;
			AsyncPathProbe = true;
			CachePaths = true;
			ConduitOpts = false;
			CritterConsumers = true;
			CullConduits = true;
			CustomStringFormat = true;
			DisableAchievements = AchievementDisable.SandboxDebug;
			DisableConduitAnimation = ConduitAnimationQuality.Full;
			DisableLoadPreviews = false;
			DisableSound = false;
			DisableTutorial = TutorialMessageDisable.WarningsOnly;
			FastAttributesMode = true;
			FastRaycast = true;
			FastReachability = true;
			FastStructureTemperature = true;
			FastUpdatePickups = false;
			FlattenAverages = true;
			InfoCardOpts = true;
			LoadOpts = true;
			LogicUpdates = true;
			MeshRendererOptions = MeshRendererSettings.All;
			Metrics = false;
			MinimalKCV = false;
			MiscOpts = true;
			ModLoadOpts = false;
			NoBounce = true;
			NoConversations = false;
			NoPlacerEasing = false;
			NoReports = false;
			NoSplash = true;
			NoTileDecor = false;
			OptimizeDialogs = true;
			ParallelInventory = true;
			PickupOpts = true;
			RadiationOpts = true;
			ReduceColonyTracking = false;
			ReduceSoundUpdates = true;
			ReduceTileUpdates = true;
			RenderTicks = true;
			SensorOpts = true;
			SideScreenOpts = true;
			ThreatOvercrowding = true;
			UnstackLights = true;
			VirtualScroll = true;
		}

		/// <summary>
		/// Controls when achievements are checked.
		/// </summary>
		public enum AchievementDisable {
			[Option("Never", "Achievements will always be checked.")]
			Never,
			[Option("In Sandbox/Debug", "Achievements will not be checked in sandbox or debug mode.")]
			SandboxDebug,
			[Option("Always Disabled", "<color=#FF0000>Achievements cannot be unlocked and\nno progress towards any achievement can be made.</color>")]
			Always
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

		/// <summary>
		/// Where to use mesh renderers.
		/// </summary>
		public enum MeshRendererSettings {
			[Option("All", "Use mesh renderers for all graphics.\r\n<i>Incompatible with mods: True Tiles</i>")]
			All,
			[Option("All But Tiles", "Use mesh renderers for all non-tileable graphics.")]
			AllButTiles,
			[Option("Disabled", "Do not use mesh renderers for any graphics.")]
			None
		}

		/// <summary>
		/// How many tutorial message and warnings to show.
		/// </summary>
		public enum TutorialMessageDisable {
			[Option("All", "All tutorial messages are shown when they would normally appear.")]
			All,
			[Option("Warnings", "Tutorial messages are disabled, but warnings such as\n<b>Insufficient Oxygen Generation</b> will still appear.")]
			WarningsOnly,
			[Option("Off", "Tutorial messages and these warnings will be disabled:\n" +
				"- Insufficient Oxygen Generation\n- Colony requires a food source\n- Long Commutes\n" +
				"- No Outhouse built\n- No Wash Basin built\n- No Oxygen Generator built\n" +
				"- Unrefrigerated Food\n- No Sick Bay built\n\n<i>Keep a careful eye on your colony...</i>")]
			None
		}
	}
}
