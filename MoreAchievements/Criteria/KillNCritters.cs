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

using Database;
using System;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires the killing of a specified number of critters, through causes other than old
	/// age.
	/// </summary>
	public class KillNCritters : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The number of critters killed, when deserialized from a legacy save.
		/// </summary>
		protected int killed;

		/// <summary>
		/// The number of critters which must be killed.
		/// </summary>
		protected int required;

		public KillNCritters(int required) {
			killed = 0;
			this.required = Math.Max(1, required);
		}

		public void Deserialize(IReader reader) {
			required = Math.Max(reader.ReadInt32(), 1);
			killed = Math.Max(reader.ReadInt32(), 0);
		}

		public override string GetProgress(bool complete) {
			var inst = AchievementStateComponent.Instance;
			return string.Format(AchievementStrings.YOUMONSTER.PROGRESS, complete ?
				required : inst.CrittersKilled, required);
		}

		public override bool Success() {
			var inst = AchievementStateComponent.Instance;
			if (inst != null && killed > 0) {
				inst.CrittersKilled = killed;
				killed = 0;
			}
			return inst != null && inst.CrittersKilled >= required;
		}
	}
}
