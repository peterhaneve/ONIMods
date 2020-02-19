/*
 * Copyright 2020 Peter Han
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

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// The options class used for Debug Not Included.
	/// </summary>
	[ModInfo("Debug Not Included", "https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization.OptIn)]
	[RestartRequired]
	public sealed class DebugNotIncludedOptions : POptions.SingletonOptions<
			DebugNotIncludedOptions> {
		[Option("Log Assert Failures", "Logs a stack trace of every failing assert to the log.")]
		[JsonProperty]
		public bool LogAsserts { get; set; }

		[Option("Log Detailed Backtrace", "Adds more information to stack traces from crashes.")]
		[JsonProperty]
		public bool DetailedBacktrace { get; set; }

		[Option("Log Sound Info", "Logs each assignment of sounds to an animation.")]
		[JsonProperty]
		public bool LogSounds { get; set; }

		[Option("Show Log Senders", "Includes the source method name on every log message.")]
		[JsonProperty]
		public bool ShowLogSenders { get; set; }

		public DebugNotIncludedOptions() {
			DetailedBacktrace = true;
			LogAsserts = true;
			LogSounds = false;
			ShowLogSenders = false;
		}

		public override string ToString() {
			return "DebugNotIncludedOptions[senders={0},assert={1},backtrace={2},sounds={3}]".
				F(ShowLogSenders, LogAsserts, DetailedBacktrace, LogSounds);
		}
	}
}
