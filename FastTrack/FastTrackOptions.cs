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
using PeterHan.PLib.Options;

namespace PeterHan.FastTrack {
	/// <summary>
	/// The options class used for Fast Track.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods")]
	[ConfigFile(SharedConfigLocation: true)]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class FastTrackOptions {
		public const int CURRENT_CONFIG_VERSION = 1;

		/// <summary>
		/// The only instance of the Fast Track options.
		/// </summary>
		private static FastTrackOptions instance;

		/// <summary>
		/// Retrieves the Fast Track options, or lazily initializes them if not yet loaded.
		/// </summary>
		public static FastTrackOptions Instance {
			get {
				var opts = instance;
				if (opts == null) {
					opts = POptions.ReadSettings<FastTrackOptions>();
					if (opts == null || opts.ConfigVersion < CURRENT_CONFIG_VERSION) {
						opts = new FastTrackOptions();
						POptions.WriteSettings(opts);
					}
					instance = opts;
				}
				return opts;
			}
		}

		[JsonProperty]
		public int ConfigVersion { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.BACKGROUNDROOMREBUILD", "STRINGS.UI.TOOLTIPS.FASTTRACK.BACKGROUNDROOMREBUILD", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool BackgroundRoomRebuild { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.ENETOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.ENETOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool ENetOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.FLATTENAVERAGES", "STRINGS.UI.TOOLTIPS.FASTTRACK.FLATTENAVERAGES", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool FlattenAverages { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.LOGICUPDATES", "STRINGS.UI.TOOLTIPS.FASTTRACK.LOGICUPDATES", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool LogicUpdates { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOREPORTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOREPORTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool NoReports { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.FASTSTRUCTURETEMPERATURE", "STRINGS.UI.TOOLTIPS.FASTTRACK.FASTSTRUCTURETEMPERATURE", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool FastStructureTemperature { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.RADIATIONOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.RADIATIONOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool RadiationOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CONDUITOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.CONDUITOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_BUILDINGS")]
		[JsonProperty]
		public bool ConduitOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.THREATOVERCROWDING", "STRINGS.UI.TOOLTIPS.FASTTRACK.THREATOVERCROWDING", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_CRITTERS")]
		[JsonProperty]
		public bool ThreatOvercrowding { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CRITTERCONSUMERS", "STRINGS.UI.TOOLTIPS.FASTTRACK.CRITTERCONSUMERS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_CRITTERS")]
		[JsonProperty]
		public bool CritterConsumers { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.UNSTACKLIGHTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.UNSTACKLIGHTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_CRITTERS")]
		[JsonProperty]
		public bool UnstackLights { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.FASTATTRIBUTESMODE", "STRINGS.UI.TOOLTIPS.FASTTRACK.FASTATTRIBUTESMODE", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool FastAttributesMode { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.ASYNCPATHPROBE", "STRINGS.UI.TOOLTIPS.FASTTRACK.ASYNCPATHPROBE", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool AsyncPathProbe { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CACHEPATHS", "STRINGS.UI.TOOLTIPS.FASTTRACK.CACHEPATHS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool CachePaths { get; set; }
		
		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CHOREOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.CHOREOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool ChoreOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOCONVERSATIONS", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOCONVERSATIONS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool NoConversations { get; set; }
		
		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NODISEASE", "STRINGS.UI.TOOLTIPS.FASTTRACK.NODISEASE", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool NoDisease { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.FASTREACHABILITY", "STRINGS.UI.TOOLTIPS.FASTTRACK.FASTREACHABILITY", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool FastReachability { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.SENSOROPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.SENSOROPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DUPLICANTS")]
		[JsonProperty]
		public bool SensorOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.REDUCECOLONYTRACKING", "STRINGS.UI.TOOLTIPS.FASTTRACK.REDUCECOLONYTRACKING", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool ReduceColonyTracking { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.DISABLEACHIEVEMENTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.DISABLEACHIEVEMENTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public AchievementDisable DisableAchievements { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.DISABLELOADPREVIEWS", "STRINGS.UI.TOOLTIPS.FASTTRACK.DISABLELOADPREVIEWS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool DisableLoadPreviews { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.DISABLETUTORIAL", "STRINGS.UI.TOOLTIPS.FASTTRACK.DISABLETUTORIAL", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public TutorialMessageDisable DisableTutorial { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.LOADOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.LOADOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool LoadOpts { get; set; }

		[JsonProperty]
		public bool ModLoadOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.OPTIMIZEDIALOGS", "STRINGS.UI.TOOLTIPS.FASTTRACK.OPTIMIZEDIALOGS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool OptimizeDialogs { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.MISCOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.MISCOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool MiscOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.ALLOCOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.ALLOCOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool AllocOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.PARALLELINVENTORY", "STRINGS.UI.TOOLTIPS.FASTTRACK.PARALLELINVENTORY", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_ITEMS")]
		[JsonProperty]
		public bool ParallelInventory { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.INFOCARDOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.INFOCARDOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool InfoCardOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.FASTUPDATEPICKUPS", "STRINGS.UI.TOOLTIPS.FASTTRACK.FASTUPDATEPICKUPS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_ITEMS")]
		[JsonProperty]
		public bool FastUpdatePickups { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.PICKUPOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.PICKUPOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_ITEMS")]
		[JsonProperty]
		public bool PickupOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.SIDESCREENOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.SIDESCREENOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_INTERFACE")]
		[JsonProperty]
		public bool SideScreenOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.MINIMALKCV", "STRINGS.UI.TOOLTIPS.FASTTRACK.MINIMALKCV", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_ITEMS")]
		[JsonProperty]
		public bool MinimalKCV { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.REDUCESOUNDUPDATES", "STRINGS.UI.TOOLTIPS.FASTTRACK.REDUCESOUNDUPDATES", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_SOUND")]
		[JsonProperty]
		public bool ReduceSoundUpdates { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.DISABLESOUND", "STRINGS.UI.TOOLTIPS.FASTTRACK.DISABLESOUND", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_SOUND")]
		[JsonProperty]
		public bool DisableSound { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CLUSTERMAPQUALITY", "STRINGS.UI.TOOLTIPS.FASTTRACK.CLUSTERMAPQUALITY", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		[RequireDLC(DlcManager.EXPANSION1_ID)]
		public bool ClusterMapReduce { get; set; }
		
		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CULLCONDUITS", "STRINGS.UI.TOOLTIPS.FASTTRACK.CULLCONDUITS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool CullConduits { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.ANIMOPTS", "STRINGS.UI.TOOLTIPS.FASTTRACK.ANIMOPTS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool AnimOpts { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOPLACEREASING", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOPLACEREASING", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool NoPlacerEasing { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOBOUNCE", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOBOUNCE", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool NoBounce { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOSPLASH", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOSPLASH", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool NoSplash { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.NOTILEDECOR", "STRINGS.UI.TOOLTIPS.FASTTRACK.NOTILEDECOR", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool NoTileDecor { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.RENDERTICKS", "STRINGS.UI.TOOLTIPS.FASTTRACK.RENDERTICKS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool RenderTicks { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.DISABLECONDUITANIMATION", "STRINGS.UI.TOOLTIPS.FASTTRACK.DISABLECONDUITANIMATION", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public ConduitAnimationQuality DisableConduitAnimation { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.CUSTOMSTRINGFORMAT", "STRINGS.UI.TOOLTIPS.FASTTRACK.CUSTOMSTRINGFORMAT", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool CustomStringFormat { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.REDUCETILEUPDATES", "STRINGS.UI.TOOLTIPS.FASTTRACK.REDUCETILEUPDATES", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool ReduceTileUpdates { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.MESHRENDEREROPTIONS", "STRINGS.UI.TOOLTIPS.FASTTRACK.MESHRENDEREROPTIONS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public MeshRendererSettings MeshRendererOptions { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.VIRTUALSCROLL", "STRINGS.UI.TOOLTIPS.FASTTRACK.VIRTUALSCROLL", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_VISUAL")]
		[JsonProperty]
		public bool VirtualScroll { get; set; }

		[Option("STRINGS.UI.FRONTEND.FASTTRACK.METRICS", "STRINGS.UI.TOOLTIPS.FASTTRACK.METRICS", "STRINGS.UI.FRONTEND.FASTTRACK.CATEGORY_DEBUG")]
		[JsonProperty]
		public bool Metrics { get; set; }

		public FastTrackOptions() {
			AllocOpts = true;
			AnimOpts = true;
			AsyncPathProbe = true;
			BackgroundRoomRebuild = true;
			CachePaths = true;
			ChoreOpts = true;
			ClusterMapReduce = true;
			ConduitOpts = false;
			ConfigVersion = CURRENT_CONFIG_VERSION;
			CritterConsumers = true;
			CullConduits = true;
			CustomStringFormat = true;
			DisableAchievements = AchievementDisable.SandboxDebug;
			DisableConduitAnimation = ConduitAnimationQuality.Full;
			DisableLoadPreviews = false;
			DisableSound = false;
			DisableTutorial = TutorialMessageDisable.WarningsOnly;
			ENetOpts = true;
			FastAttributesMode = true;
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
			NoDisease = false;
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
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.ACHIEVEDISABLE.NEVER", "STRINGS.UI.TOOLTIPS.FASTTRACK.ACHIEVEDISABLE.NEVER")]
			Never,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.ACHIEVEDISABLE.SANDBOXDEBUG", "STRINGS.UI.TOOLTIPS.FASTTRACK.ACHIEVEDISABLE.SANDBOXDEBUG")]
			SandboxDebug,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.ACHIEVEDISABLE.DISABLED", "STRINGS.UI.TOOLTIPS.FASTTRACK.ACHIEVEDISABLE.DISABLED")]
			Always
		}

		/// <summary>
		/// The quality to use for conduit rendering.
		/// </summary>
		public enum ConduitAnimationQuality {
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.PIPEANIM.FULL", "STRINGS.UI.TOOLTIPS.FASTTRACK.PIPEANIM.FULL")]
			Full,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.PIPEANIM.REDUCED", "STRINGS.UI.TOOLTIPS.FASTTRACK.PIPEANIM.REDUCED")]
			Reduced,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.PIPEANIM.MINIMAL", "STRINGS.UI.TOOLTIPS.FASTTRACK.PIPEANIM.MINIMAL")]
			Minimal
		}

		/// <summary>
		/// Where to use mesh renderers.
		/// </summary>
		public enum MeshRendererSettings {
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.MESHRENDERERS.ALL", "STRINGS.UI.TOOLTIPS.FASTTRACK.MESHRENDERERS.ALL")]
			All,
			// Shows up as unused, but it being neither All nor None is meaningful!
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.MESHRENDERERS.EXCEPTTILES", "STRINGS.UI.TOOLTIPS.FASTTRACK.MESHRENDERERS.EXCEPTTILES")]
			AllButTiles,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.MESHRENDERERS.NONE", "STRINGS.UI.TOOLTIPS.FASTTRACK.MESHRENDERERS.NONE")]
			None
		}

		/// <summary>
		/// How many tutorial message and warnings to show.
		/// </summary>
		public enum TutorialMessageDisable {
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.TUTORIALS.ALL", "STRINGS.UI.TOOLTIPS.FASTTRACK.TUTORIALS.ALL")]
			All,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.TUTORIALS.WARNINGS", "STRINGS.UI.TOOLTIPS.FASTTRACK.TUTORIALS.WARNINGS")]
			WarningsOnly,
			[Option("STRINGS.UI.FRONTEND.FASTTRACK.TUTORIALS.OFF", "STRINGS.UI.TOOLTIPS.FASTTRACK.TUTORIALS.OFF")]
			None
		}
	}
}
