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

using Database;
using Harmony;
using System;
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires the digging of a specified total number of tiles.
	/// </summary>
	public class DigNTiles : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The event ID for completing a dig. Obtained via Hash.SDBMLower("DigComplete").
		/// </summary>
		public const int DigComplete = -1485451493;

		/// <summary>
		/// The number of tiles dug.
		/// </summary>
		protected int dug;

		/// <summary>
		/// The number of tiles which must be dug.
		/// </summary>
		protected int required;

		public DigNTiles(int required) {
			dug = 0;
			this.required = Math.Max(1, required);
		}

		/// <summary>
		/// Adds a dug tile.
		/// </summary>
		public void AddDugTile() {
			dug++;
		}

		public void Deserialize(IReader reader) {
			required = Math.Max(reader.ReadInt32(), 1);
			dug = Math.Max(reader.ReadInt32(), 0);
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.JOHNHENRY.PROGRESS, complete ?
				required : dug, required);
		}

		public override void Serialize(BinaryWriter writer) {
			writer.Write(required);
			writer.Write(dug);
		}

		public override bool Success() {
			return dug >= required;
		}
	}
}
