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

using Database;
using System;
using System.Collections.Generic;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires all destinations on the starmap to be visited by a rocket.
	/// </summary>
	public sealed class VisitAllPlanets : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The planets already visited, when deserialized from a legacy save.
		/// </summary>
		private ICollection<int> planetsVisited;

		public VisitAllPlanets() {
			planetsVisited = null;
		}

		public void Deserialize(IReader reader) {
			int visited = Math.Max(0, reader.ReadInt32());
			if (visited > 0) {
				planetsVisited = new HashSet<int>();
				for (int i = 0; i < visited; i++)
					planetsVisited.Add(reader.ReadInt32());
			}
		}

		public override string GetProgress(bool complete) {
			var inst = AchievementStateComponent.Instance;
			return string.Format(AchievementStrings.WHOLENEWWORLDS.PROGRESS, complete ?
				inst.PlanetsRequired : inst.PlanetsVisited.Count, inst.PlanetsRequired);
		}

		public override bool Success() {
			var inst = AchievementStateComponent.Instance;
			if (inst != null && planetsVisited != null) {
				foreach (int id in planetsVisited)
					inst.PlanetsVisited.Add(id);
				planetsVisited = null;
			}
			return inst != null && inst.PlanetsVisited.Count >= inst.PlanetsRequired;
		}
	}
}
