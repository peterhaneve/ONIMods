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

namespace PeterHan.FastTrack {
	/// <summary>
	/// Stores the strings used for Fast Track.
	/// </summary>
	public static class FastTrackStrings {
		/// <summary>
		/// Shared performance impact descriptions used in multiple options.
		/// </summary>
		private const string PERF_LOW = "\n\n<b>Performance Impact: <color=#00CC00>Low</color></b>";
		private const string PERF_MEDIUM = "\n\n<b>Performance Impact: <color=#FF8827>Medium</color></b>";
		private const string PERF_HIGH = "\n\n<b>Performance Impact: <color=#FF3300>High</color></b>";

		public static class UI {
			public static class FRONTEND {
				public static class FASTTRACK {
					public static LocString CATEGORY_BUILDINGS = "Buildings";
					public static LocString CATEGORY_CRITTERS = "Critters";
					public static LocString CATEGORY_DEBUG = "Debug";
					public static LocString CATEGORY_DUPLICANTS = "Duplicants";
					public static LocString CATEGORY_INTERFACE = "Interface";
					public static LocString CATEGORY_ITEMS = "Items";
					public static LocString CATEGORY_SOUND = "Sound";
					public static LocString CATEGORY_VISUAL = "Visual";

					public static LocString BACKGROUNDROOMREBUILD = "Background Room Rebuild";
					public static LocString ENETOPTS = "Electrical Optimizations";
					public static LocString FLATTENAVERAGES = "Flatten Averages";
					public static LocString LOGICUPDATES = "Logic Optimizations";
					public static LocString NOREPORTS = "No Colony Reports";
					public static LocString FASTSTRUCTURETEMPERATURE = "Optimize Heat Generation";
					public static LocString RADIATIONOPTS = "Radiation Tweaks";
					public static LocString CONDUITOPTS = "Threaded Conduit Updates";

					public static LocString THREATOVERCROWDING = "Critter Monitors";
					public static LocString CRITTERCONSUMERS = "Optimize Eating";
					public static LocString UNSTACKLIGHTS = "Unstack Lights";
					public static LocString REDUCECRITTERIDLEMOVE = "Reduce Idle Movement";

					public static LocString FASTATTRIBUTESMODE = "Attribute Leveling";
					public static LocString ASYNCPATHPROBE = "Background Pathing";
					public static LocString CACHEPATHS = "Cache Paths";
					public static LocString CHOREOPTS = "Chore Optimizations";
					public static LocString NOCONVERSATIONS = "Disable Conversations";
					public static LocString NODISEASE = "Disable Disease";
					public static LocString FASTREACHABILITY = "Fast Reachability Checks";
					public static LocString SENSOROPTS = "Optimize Idling";
					public static LocString REDUCEDUPLICANTIDLEMOVE = "Reduce Idle Movement";

					public static LocString REDUCECOLONYTRACKING = "Colony Tracker Reduction";
					public static LocString DISABLEACHIEVEMENTS = "Disable Achievements";
					public static LocString DISABLELOADPREVIEWS = "Disable Load Previews";
					public static LocString DISABLETUTORIAL = "Disable Tutorials";
					public static LocString LOADOPTS = "Less Loading";
					public static LocString OPTIMIZEDIALOGS = "Optimize Dialogs";
					public static LocString MISCOPTS = "Other UI Optimizations";
					public static LocString ALLOCOPTS = "Reduce Allocations";

					public static LocString PARALLELINVENTORY = "Background Inventory";
					public static LocString INFOCARDOPTS = "Info Card Optimization";
					public static LocString FASTUPDATEPICKUPS = "Optimize Debris Collection";
					public static LocString PICKUPOPTS = "Reduce Debris Checks";
					public static LocString SIDESCREENOPTS = "Side Screens";
					public static LocString MINIMALKCV = "Vector Minimization";

					public static LocString REDUCESOUNDUPDATES = "Batch Sounds";
					public static LocString DISABLESOUND = "Disable <color=#FF0000>ALL</color> Sounds";

					public static LocString VERINWATERMARK = "Build Watermark";
					public static LocString CLUSTERMAPQUALITY = "Cluster Map Resolution";
					public static LocString CULLCONDUITS = "Cull Buildings in Tiles";
					public static LocString ANIMOPTS = "Faster Animations";
					public static LocString NOPLACEREASING = "Instant Place Graphics";
					public static LocString NOBOUNCE = "No Notification Bounce";
					public static LocString NOSPLASH = "No Splashes";
					public static LocString NOTILEDECOR = "No Tile Caps";
					public static LocString RENDERTICKS = "Optimize Renderers";
					public static LocString DISABLECONDUITANIMATION = "Pipe Animation Quality";
					public static LocString CUSTOMSTRINGFORMAT = "Quick Format";
					public static LocString REDUCETILEUPDATES = "Threaded Tile Updates";
					public static LocString MESHRENDEREROPTIONS = "Use Mesh Renderers";
					public static LocString VIRTUALSCROLL = "Virtual Scroll";

					public static LocString METRICS = "Log Debug Metrics";

					public static class ACHIEVEDISABLE {
						public static LocString NEVER = "Never";
						public static LocString SANDBOXDEBUG = "In Sandbox/Debug";
						public static LocString DISABLED = "Always Disabled";
					}

					public static class MESHRENDERERS {
						public static LocString ALL = "All";
						public static LocString EXCEPTTILES = "All but Tiles";
						public static LocString NONE = "None";
					}

					public static class PIPEANIM {
						public static LocString FULL = "Full";
						public static LocString REDUCED = "Reduced";
						public static LocString MINIMAL = "Minimal";
					}

					public static class TUTORIALS {
						public static LocString ALL = "All";
						public static LocString WARNINGS = "Warnings";
						public static LocString OFF = "Off";
					}

					public static class WATERMARK {
						public static LocString SHOW = "Show version";
						public static LocString HIDE = "Hide version";
						public static LocString OFF = "Hide completely";
					}
				}
			}

			public static class TOOLTIPS {
				public static class FASTTRACK {
					public static LocString BACKGROUNDROOMREBUILD = "Processes most room changes on a background thread." + PERF_MEDIUM;
					public static LocString ENETOPTS = "Speeds up calculations of electrical networks." + PERF_MEDIUM;
					public static LocString FLATTENAVERAGES = "Optimize Amounts and average calculations." + PERF_LOW;
					public static LocString LOGICUPDATES = "Optimizes some buildings to not trigger logic network updates every frame." + PERF_LOW;
					public static LocString NOREPORTS = "Disables colony reports completely.\n<color=#FF0000>Disabling will prevent some achievements from being unlocked</color>" + PERF_MEDIUM;
					public static LocString FASTSTRUCTURETEMPERATURE = "Reduces memory allocations used when generating heat." + PERF_MEDIUM;
					public static LocString RADIATIONOPTS = "Speeds up radiation calculations." + PERF_LOW;
					public static LocString CONDUITOPTS = "Multi-threads some updates to liquid and gas conduits." + PERF_LOW;

					public static LocString THREATOVERCROWDING = "Optimizes critter Threat and Overcrowding monitors.\n<i>May conflict with mods that add new critters</i>" + PERF_LOW;
					public static LocString CRITTERCONSUMERS = "Optimize how Critters find objects to eat." + PERF_LOW;
					public static LocString UNSTACKLIGHTS = "Reduces the visual effects shown when many light sources are stacked.\nIntended for ranching critters like Shine Bugs." + PERF_LOW;
					public static LocString REDUCECRITTERIDLEMOVE = "Reduces amount of idle movement of critters that are currently not visible on the screen." + PERF_LOW;

					public static LocString FASTATTRIBUTESMODE = "Optimize attribute leveling and work efficiency calculation." + PERF_MEDIUM;
					public static LocString ASYNCPATHPROBE = "Moves some pathfinding calculations to a non-blocking thread." + PERF_HIGH;
					public static LocString CACHEPATHS = "Cache frequently used paths and reuse them in future calculations." + PERF_MEDIUM;
					public static LocString CHOREOPTS = "Reduces the number of chores scanned when choosing each Duplicant's next task." + PERF_MEDIUM;
					public static LocString NOCONVERSATIONS = "Disables all Duplicant thought and speech balloons.\n\n<color=#FF0000>Disables the Mysterious Hermit's special trait.</color>" + PERF_LOW;
					public static LocString NODISEASE = "Completely disable the entire disease system.\n" +
						Constants.BULLETSTRING + "Sick Bay and Disease Clinic cannot be built or used\n" +
						Constants.BULLETSTRING + "No new diseases can be contracted\n" +
						Constants.BULLETSTRING + "Existing diseases will be immediately removed\n" +
						Constants.BULLETSTRING + "All germs will be removed from pipes and buildings\n" +
						"<i>Not compatible with mods: Diseases Restored, Diseases Expanded</i>" + PERF_MEDIUM;
					public static LocString FASTREACHABILITY = "Only check items and chores for reachability when necessary." + PERF_MEDIUM;
					public static LocString SENSOROPTS = "Only check for locations to Idle, Mingle, or Balloon Artist when necessary." + PERF_LOW;
					public static LocString REDUCEDUPLICANTIDLEMOVE = "Reduces amount of idle movement of duplicants that are currently not visible on the screen." + PERF_LOW;

					public static LocString REDUCECOLONYTRACKING = "Reduces the update rate of Colony Diagnostics.\n<i>Some notifications may take longer to trigger</i>" + PERF_LOW;
					public static LocString DISABLEACHIEVEMENTS = "Turn off checking for Colony Initiatives.\n<color=#FF0000>Disabling will prevent unlocking any Steam achievement</color>" + PERF_LOW;
					public static LocString DISABLELOADPREVIEWS = "Disables loading of colony previews on the Load screen." + PERF_MEDIUM;
					public static LocString DISABLETUTORIAL = "Disables tutorial messages." + PERF_LOW;
					public static LocString LOADOPTS = "Speeds up loading times." + PERF_LOW;
					public static LocString OPTIMIZEDIALOGS = "Speeds up a variety of dialog boxes." + PERF_LOW;
					public static LocString MISCOPTS = "Optimizes many event and UI handlers." + PERF_MEDIUM;
					public static LocString ALLOCOPTS = "Reduces memory allocations in a variety of locations." + PERF_LOW;

					public static LocString PARALLELINVENTORY = "Compiles the item quantites in the Resources panel on a non-blocking thread." + PERF_MEDIUM;
					public static LocString INFOCARDOPTS = "Optimizes the info cards shown on hover." + PERF_MEDIUM;
					public static LocString FASTUPDATEPICKUPS = "Speed up inefficient and memory-intensive checks for items.\n<i>Not compatible with mods: Efficient Supply</i>" + PERF_LOW;
					public static LocString PICKUPOPTS = "Dramatically speed up scanning for items to store or supply." + PERF_MEDIUM;
					public static LocString SIDESCREENOPTS = "Optimizes the informational side screens." + PERF_MEDIUM;
					public static LocString MINIMALKCV = "Reduces memory allocations in most game-wide lists of items." + PERF_LOW;

					public static LocString REDUCESOUNDUPDATES = "Reduces the frequency of sound location updates." + PERF_LOW;
					public static LocString DISABLESOUND = "Completely disables <color=#FF0000>all</color> sound playback." + PERF_LOW;

					public static LocString VERINWATERMARK = "Show the Fast Track version in the build watermark.\n\n<i>No performance impact</i>";
					public static LocString CLUSTERMAPQUALITY = "Slightly reduce the graphics quality of the Spaced Out! Starmap\nin exchange for a significant performance boost." + PERF_HIGH;
					public static LocString CULLCONDUITS = "Hide conduits and wires inside solid walls." + PERF_LOW;
					public static LocString ANIMOPTS = "Optimizes slow code in animation playback." + PERF_MEDIUM;
					public static LocString NOPLACEREASING = "Disables the animation which appears when placing\nerrands like Dig or Mop." + PERF_LOW;
					public static LocString NOBOUNCE = "Disables the bounce effect when new notifications appear." + PERF_MEDIUM;
					public static LocString NOSPLASH = "Disables most puff, splash, and breath animations." + PERF_LOW;
					public static LocString NOTILEDECOR = "Disables all ornaments and edges on constructed tiles.\n<i>Not compatible with mods: True Tiles</i>" + PERF_LOW;
					public static LocString RENDERTICKS = "Optimizes several renderers that run every frame.\n<i>Some visual artifacts may appear with no effect on gameplay</i>" + PERF_MEDIUM;
					public static LocString DISABLECONDUITANIMATION = "Controls the visual fidelity of pipe animations.\n<i>No changes to actual pipe mechanics will occur</i>" + PERF_MEDIUM;
					public static LocString CUSTOMSTRINGFORMAT = "Reduces memory allocations when formatting strings.\n<i>Not compatible with mods: DisplayAllTemps</i>" + PERF_MEDIUM;
					public static LocString REDUCETILEUPDATES = "Multi-threads updates to most tile textures." + PERF_HIGH;
					public static LocString MESHRENDEREROPTIONS = "Use faster mesh renderers instead of redrawing\nevery frame for these graphics." + PERF_MEDIUM;
					public static LocString VIRTUALSCROLL = "Improves the speed of scrolling menus." + PERF_MEDIUM;

					public static LocString METRICS = "Logs extra debug information to the game log.\n\n<b>Only use this option if directed to do so by a developer.</b>";

					public static class ACHIEVEDISABLE {
						public static LocString NEVER = "Achievements will always be checked.";
						public static LocString SANDBOXDEBUG = "Achievements will not be checked in sandbox or debug mode.";
						public static LocString DISABLED = "<color=#FF0000>Achievements cannot be unlocked and\nno progress towards any achievement can be made.</color>";
					}

					public static class MESHRENDERERS {
						public static LocString ALL = "Use mesh renderers for all graphics.\r\n<i>Incompatible with mods: True Tiles</i>";
						public static LocString EXCEPTTILES = "Use mesh renderers for all non-tileable graphics.";
						public static LocString NONE = "Do not use mesh renderers for any graphics.";
					}

					public static class PIPEANIM {
						public static LocString FULL = "Pipe animation quality is unchanged from the base game.";
						public static LocString REDUCED = "Pipe animations update slower when outside the Liquid or Gas overlay.";
						public static LocString MINIMAL = "Pipe animations are disabled outside the Liquid or Gas overlay.";
					}

					public static class TUTORIALS {
						public static LocString ALL = "All tutorial messages are shown when they would normally appear.";
						public static LocString WARNINGS = "Tutorial messages are disabled, but warnings such as\n<b>Insufficient Oxygen Generation</b> will still appear.";
						public static LocString OFF = "Tutorial messages and these warnings will be disabled:\n" +
							Constants.BULLETSTRING + "Insufficient Oxygen Generation\n" +
							Constants.BULLETSTRING + "Colony requires a food source\n" +
							Constants.BULLETSTRING + "Long Commutes\n" +
							Constants.BULLETSTRING + "No Outhouse built\n" +
							Constants.BULLETSTRING + "No Wash Basin built\n" +
							Constants.BULLETSTRING + "No Oxygen Generator built\n" +
							Constants.BULLETSTRING + "Unrefrigerated Food\n" +
							Constants.BULLETSTRING + "No Sick Bay built\n\n<i>Keep a careful eye on your colony...</i>";
					}

					public static class WATERMARK {
						public static LocString SHOW = "Show the current Fast Track version and game version in the build watermark.";
						public static LocString HIDE = "Show only the current game version in the build watermark.";
						public static LocString OFF = "Hide the build watermark completely in-game.";
					}
				}
			}
		}
	}
}
