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

namespace PeterHan.PLib.PatchManager {
	/// <summary>
	/// Describes when a PLibPatch or PLibMethod should be invoked.
	/// 
	/// Due to a bug in ILRepack an enum type in PLib cannot be used as a parameter for a
	/// custom attribute. ILmerge does not have this bug.
	/// </summary>
	public static class RunAt {
		/// <summary>
		/// Runs the method/patch now.
		/// 
		/// Note that mods may load in any order and thus not all mods may be initialized at
		/// this time.
		/// </summary>
		public const uint Immediately = 0U;

		/// <summary>
		/// Runs after all mods load, but before most other aspects of the game (including
		/// Assets, Db, and so forth) are initialized. This will run before any other mod
		/// has their UserMod2.AfterModsLoad executed. All PLib components will be initialized
		/// by this point.
		/// </summary>
		public const uint AfterModsLoad = 1U;

		/// <summary>
		/// Runs immediately before Db.Initialize.
		/// </summary>
		public const uint BeforeDbInit = 2U;

		/// <summary>
		/// Runs immediately after Db.Initialize.
		/// </summary>
		public const uint AfterDbInit = 3U;

		/// <summary>
		/// Runs when the main menu has loaded.
		/// </summary>
		public const uint InMainMenu = 4U;

		/// <summary>
		/// Runs when Game.OnPrefabInit has completed.
		/// </summary>
		public const uint OnStartGame = 5U;

		/// <summary>
		/// Runs when Game.DestroyInstances is executed.
		/// </summary>
		public const uint OnEndGame = 6U;

		/// <summary>
		/// Runs after all mod data (including layerable files like world gen and codex/
		/// elements) are loaded. This comes after all UserMod2.AfterModsLoad handlers execute.
		/// All PLib components will be initialized by this point.
		/// </summary>
		public const uint AfterLayerableLoad = 7U;

		/// <summary>
		/// The string equivalents of each constant for debugging.
		/// </summary>
		private static readonly string[] STRING_VALUES = new string[] {
			nameof(Immediately), nameof(AfterModsLoad), nameof(BeforeDbInit),
			nameof(AfterDbInit), nameof(InMainMenu), nameof(OnStartGame), nameof(OnEndGame),
			nameof(AfterLayerableLoad)
		};

		/// <summary>
		/// Gets a human readable representation of a run time constant.
		/// </summary>
		/// <param name="runtime">The time when the patch should be run.</param>
		public static string ToString(uint runtime) {
			return (runtime < STRING_VALUES.Length) ? STRING_VALUES[runtime] : runtime.
				ToString();
		}
	}
}
