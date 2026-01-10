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

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Handles debugging existing mod loading.
	/// </summary>
	internal static class ModLoadHandler {
		/// <summary>
		/// The mod which caused the first unhandled crash. Clears the crash when accessed,
		/// allowing the next unhandled crash to again be logged.
		/// </summary>
		internal static ModDebugInfo CrashingMod {
			get {
				var mod = lastCrashedMod;
				lastCrashedMod = null;
				return mod;
			}
			set {
				if (value != null && lastCrashedMod == null)
					lastCrashedMod = value;
			}
		}

		/// <summary>
		/// The last mod which crashed, or null if none have / the crash has been cleared.
		/// </summary>
		private static ModDebugInfo lastCrashedMod;
	}
}
