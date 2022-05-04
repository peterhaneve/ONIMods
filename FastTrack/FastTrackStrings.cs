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

using System;

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

					public static LocString FASTATTRIBUTESMODE = "Attribute Leveling";
					public static LocString ASYNCPATHPROBE = "Background Pathing";
					public static LocString CACHEPATHS = "Cache Paths";
					public static LocString NOCONVERSATIONS = "Disable Conversations";
					public static LocString FASTREACHABILITY = "Fast Reachability Checks";
					public static LocString SENSOROPTS = "Optimize Sensors";

					public static LocString REDUCECOLONYTRACKING = "Colony Tracker Reduction";
					public static LocString DISABLEACHIEVEMENTS = "Disable Achievements";
					public static LocString DISABLELOADPREVIEWS = "Disable Load Previews";
					public static LocString DISABLETUTORIAL = "Disable Tutorials";
					public static LocString FASTRAYCAST = "Fast Raycast";
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

					public static LocString THREATOVERCROWDING = "Optimizes critter Threat and Overcrowding monitors.\n<i>May conflict with mods that add new critters</i>" + PERF_MEDIUM;
					public static LocString CRITTERCONSUMERS = "Optimize how Critters find objects to eat.\n<i>Some minor changes to Critter behaviour may occur</i>" + PERF_MEDIUM;
					public static LocString UNSTACKLIGHTS = "Reduces the visual effects shown when many light sources are stacked.\nIntended for ranching critters like Shine Bugs." + PERF_LOW;

					public static LocString FASTATTRIBUTESMODE = "Optimize attribute leveling and work efficiency calculation." + PERF_MEDIUM;
					public static LocString ASYNCPATHPROBE = "Moves some pathfinding calculations to a non-blocking thread." + PERF_HIGH;
					public static LocString CACHEPATHS = "Cache frequently used paths and reuse them in future calculations." + PERF_MEDIUM;
					public static LocString NOCONVERSATIONS = "Disables all Duplicant thought and speech balloons." + PERF_LOW;
					public static LocString FASTREACHABILITY = "Only check items and chores for reachability when necessary." + PERF_MEDIUM;
					public static LocString SENSOROPTS = "Only check for locations to Idle, Mingle, or Balloon Artist when necessary." + PERF_LOW;

					public static LocString REDUCECOLONYTRACKING = "Reduces the update rate of Colony Diagnostics.\n<i>Some notifications may take longer to trigger</i>" + PERF_LOW;
					public static LocString DISABLEACHIEVEMENTS = "Turn off checking for Colony Initiatives.\n<color=#FF0000>Disabling will prevent unlocking any Steam achievement</color>" + PERF_LOW;
					public static LocString DISABLELOADPREVIEWS = "Disables loading of colony previews on the Load screen." + PERF_MEDIUM;
					public static LocString DISABLETUTORIAL = "Disables tutorial messages." + PERF_LOW;
					public static LocString FASTRAYCAST = "Speeds up searching for UI elements under the cursor." + PERF_HIGH;
					public static LocString LOADOPTS = "Speeds up loading times." + PERF_MEDIUM;
					public static LocString OPTIMIZEDIALOGS = "Speeds up a variety of dialog boxes." + PERF_MEDIUM;
					public static LocString MISCOPTS = "Optimizes many event and UI handlers." + PERF_MEDIUM;
					public static LocString ALLOCOPTS = "Reduces memory allocations in a variety of locations." + PERF_LOW;

					public static LocString PARALLELINVENTORY = "Compiles the item quantites in the Resources panel on a non-blocking thread." + PERF_MEDIUM;
					public static LocString INFOCARDOPTS = "Optimizes the info cards shown on hover." + PERF_MEDIUM;
					public static LocString FASTUPDATEPICKUPS = "Speed up inefficient and memory-intensive checks for items.\n<i>Not compatible with mods: Efficient Supply</i>" + PERF_LOW;
					public static LocString PICKUPOPTS = "Only look for the closest items when required,\nrather than every frame for each Duplicant." + PERF_MEDIUM;
					public static LocString SIDESCREENOPTS = "Optimizes the informational side screens." + PERF_MEDIUM;
					public static LocString MINIMALKCV = "Reduces memory allocations in most game-wide lists of items." + PERF_LOW;

					public static LocString REDUCESOUNDUPDATES = "Reduces the frequency of sound location updates." + PERF_LOW;
					public static LocString DISABLESOUND = "Completely disables all sound playback." + PERF_LOW;

					public static LocString CULLCONDUITS = "Hide conduits and wires inside solid walls." + PERF_LOW;
					public static LocString ANIMOPTS = "Optimizes slow code in animation playback." + PERF_MEDIUM;
					public static LocString NOPLACEREASING = "Disables the animation which appears when placing\nerrands like Dig or Mop." + PERF_LOW;
					public static LocString NOBOUNCE = "Disables the bounce effect when new notifications appear." + PERF_MEDIUM;
					public static LocString NOSPLASH = "Disables most puff, splash, and breath animations." + PERF_LOW;
					public static LocString NOTILEDECOR = "Disables all ornaments and edges on constructed tiles." + PERF_LOW;
					public static LocString RENDERTICKS = "Optimizes several renderers that run every frame.\n<i>Some visual artifacts may appear with no effect on gameplay</i>" + PERF_MEDIUM;
					public static LocString DISABLECONDUITANIMATION = "Controls the visual fidelity of pipe animations.\n<i>No changes to actual pipe mechanics will occur</i>" + PERF_MEDIUM;
					public static LocString CUSTOMSTRINGFORMAT = "Reduces memory allocations when formatting strings.\n<i>Not compatible with mods: DisplayAllTemps</i>" + PERF_MEDIUM;
					public static LocString REDUCETILEUPDATES = "Multi-threads updates to most tile textures." + PERF_HIGH;
					public static LocString MESHRENDEREROPTIONS = "Use faster mesh renderers instead of redrawing\nevery frame for these graphics." + PERF_MEDIUM;
					public static LocString VIRTUALSCROLL = "Improves the speed of scrolling menus." + PERF_MEDIUM;

					public static LocString METRICS = "Logs extra debug information to the game log.\n\n<b>Only use this option if directed to do so by a developer.</b>";
				}
			}
		}
	}
}
