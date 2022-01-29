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

using Database;
using System;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires the construction of a specified total number of buildings.
	/// </summary>
	public class BuildNBuildings : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The number of buildings built, when deserialized from a legacy save.
		/// </summary>
		protected int built;

		/// <summary>
		/// The number of buildings required.
		/// </summary>
		protected int required;

		public BuildNBuildings(int required) {
			built = 0;
			this.required = Math.Max(1, required);
		}

		public void Deserialize(IReader reader) {
			required = Math.Max(reader.ReadInt32(), 1);
			built = Math.Max(reader.ReadInt32(), 0);
		}

		public override string GetProgress(bool complete) {
			var inst = AchievementStateComponent.Instance;
			return string.Format(AchievementStrings.EMPIREBUILDER.PROGRESS, complete ?
				required : inst.BuildingsBuilt, required);
		}

		public override bool Success() {
			var inst = AchievementStateComponent.Instance;
			if (built != 0 && inst != null) {
				inst.BuildingsBuilt = built;
				built = 0;
			}
			return inst != null && inst.BuildingsBuilt >= required;
		}
	}
}
