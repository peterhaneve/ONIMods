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
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// The options class used for Debug Not Included.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods", collapse: true)]
	[ConfigFile(SharedConfigLocation: true)]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class DebugNotIncludedOptions : SingletonOptions<DebugNotIncludedOptions> {
		[Option("Debug Not Included is a debug mod intended for mod developers.\n" +
			"Most users should <b>only use this mod if directed</b> by a mod owner to generate better logs.\n")]
		public LocText Description => null;

		[Option("Extended Features", "Enables extra features of this mod.", "Quality of Life")]
		[JsonProperty]
		public bool PowerUserMode { get; set; }

		[Option("Disable Crash Dialog", "Crash to desktop instead of trying to show the Klei crash dialog.", "Debugging")]
		[JsonProperty]
		public bool DisableCrashDialog { get; set; }

		[Option("Log Assert Failures", "Logs a stack trace of every failing assert to the log.", "Debugging")]
		[JsonProperty]
		public bool LogAsserts { get; set; }

		[Option("Log Detailed Backtrace", "Adds more information to stack traces from crashes.", "Debugging")]
		[JsonProperty]
		public bool DetailedBacktrace { get; set; }

		[Option("Log Sound Info", "Logs each assignment of sounds to an animation.", "Debugging")]
		[JsonProperty]
		public bool LogSounds { get; set; }

		[Option("Show Log Senders", "Includes the source method name on every log message.", "Debugging")]
		[JsonProperty]
		public bool ShowLogSenders { get; set; }

		[Option("Skip First Mod Check", "Suppresses the warning dialog if Debug Not Included is not the first mod in the load order.", "Quality of Life")]
		[JsonProperty]
		public bool SkipFirstModCheck { get; set; }

		[Option("Sort Schedules", "Sorts schedules alphabetically on load.", "Quality of Life")]
		[JsonProperty]
		public bool SortSchedules { get; set; }

		public DebugNotIncludedOptions() {
			DetailedBacktrace = true;
			DisableCrashDialog = false;
			LogAsserts = true;
			LogSounds = false;
			PowerUserMode = false;
			ShowLogSenders = false;
			SkipFirstModCheck = false;
			SortSchedules = false;
		}

		public override string ToString() {
			return "DebugNotIncludedOptions[senders={0},assert={1},backtrace={2},sounds={3}]".
				F(ShowLogSenders, LogAsserts, DetailedBacktrace, LogSounds);
		}
	}
}
