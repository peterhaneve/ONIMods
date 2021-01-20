/*
 * Copyright 2021 Peter Han
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

namespace PeterHan.MoreAchievements {
	/// <summary>
	/// The options class used for One Giant Leap.
	/// </summary>
	[ModInfo("One Giant Leap", "https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public sealed class MoreAchievementsOptions {
		/// <summary>
		/// If true, achievements will not be serialized to file, allowing removal of this mod.
		/// </summary>
		[Option("Remove from Save Files", "Erases this mod's achievements from the game when saved, allowing safe uninstallation of this mod.\r\n\r\nResets your progress on achievements added by this mod!")]
		[JsonProperty]
		public bool DoNotSerialize { get; set; }

		public MoreAchievementsOptions() {
			DoNotSerialize = false;
		}

		public override string ToString() {
			return "MoreAchievementsOptions[doNotSerialize={0}]".F(DoNotSerialize);
		}
	}
}
