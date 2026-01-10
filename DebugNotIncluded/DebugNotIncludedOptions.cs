/*
 * Copyright 2026 Peter Han
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
using System.Collections.Generic;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// The options class used for Debug Not Included.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods", collapse: true)]
	[ConfigFile(SharedConfigLocation: true)]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class DebugNotIncludedOptions : SingletonOptions<DebugNotIncludedOptions>, IOptions {
		[Option("Debug Not Included is a debug mod intended for mod developers.\n" +
			"Most users should <b>only use this mod if directed</b> by a mod owner to generate better logs.\n")]
		public LocText Description => null;

		[Option("Disable Crash Dialog", "Crash to desktop instead of trying to show the Klei crash dialog.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool DisableCrashDialog { get; set; }
		
		[Option("Force Reload New Worlds", "Reloads all world gen files when the New Game dialog is first opened.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool ForceReloadWorldgen { get; set; }

		[Option("Localize All Mods", "Localize all mods on the next load.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool LocalizeMods { get; set; }

		[Option("Log Assert Failures", "Logs a stack trace of every failing assert to the log.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool LogAsserts { get; set; }

		[Option("Log Detailed Backtrace", "Adds more information to stack traces from crashes.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool DetailedBacktrace { get; set; }

		[Option("Log Sound Info", "Logs each assignment of sounds to an animation.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool LogSounds { get; set; }

		[Option("Show Log Senders", "Includes the source method name on every log message.", "Debugging")]
		[RestartRequired]
		[JsonProperty]
		public bool ShowLogSenders { get; set; }

		[Option("Extended Features", "Enables extra features of this mod.", "Quality of Life")]
		[RestartRequired]
		[JsonProperty]
		public bool PowerUserMode { get; set; }

		[Option("Skip First Mod Check", "Suppresses the warning dialog if Debug Not Included is not the first mod in the load order.", "Quality of Life")]
		[RestartRequired]
		[JsonProperty]
		public bool SkipFirstModCheck { get; set; }

		[Option("Sort Schedules", "Sorts schedules alphabetically on load.", "Quality of Life")]
		[JsonProperty]
		public bool SortSchedules { get; set; }

		public DebugNotIncludedOptions() {
			DetailedBacktrace = true;
			DisableCrashDialog = false;
			ForceReloadWorldgen = false;
			LocalizeMods = false;
			LogAsserts = true;
			LogSounds = false;
			PowerUserMode = false;
			ShowLogSenders = false;
			SkipFirstModCheck = false;
			SortSchedules = false;
		}

		public IEnumerable<IOptionsEntry> CreateOptions() {
			return null;
		}

		public void OnOptionsChanged() {
			instance = this;
		}
	}
}
