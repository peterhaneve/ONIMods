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

namespace PeterHan.PLib.AVC {
	/// <summary>
	/// Implemented by classes which can check the current mod version and detect if it is out
	/// of date.
	/// </summary>
	public interface IModVersionChecker {
		/// <summary>
		/// The event to subscribe for when the check completes.
		/// </summary>
		event PVersionCheck.OnVersionCheckComplete OnVersionCheckCompleted;

		/// <summary>
		/// Checks the mod and reports if it is out of date. The mod's current version as
		/// reported by its mod_info.yaml file is available on the packagedModInfo member.
		/// 
		/// This method might not be run on the foreground thread. Do not create new behaviors
		/// or components without a coroutine to an existing GameObject.
		/// </summary>
		/// <param name="mod">The mod whose version is being checked.</param>
		/// <returns>true if the version check has started, or false if it could not be
		/// started, which will trigger the next version checker in line.</returns>
		bool CheckVersion(KMod.Mod mod);
	}
}
