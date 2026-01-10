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

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires no Duplicants to die for the specified number of consecutive cycles.
	/// </summary>
	public sealed class NoDeathsForNCycles : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// How many cycles is required for no deaths.
		/// </summary>
		private int cycles;

		/// <summary>
		/// The cycle of the last death, when deserialized from a legacy save.
		/// </summary>
		private int lastDeath;

		public NoDeathsForNCycles(int cycles) {
			this.cycles = Math.Max(cycles, 1);
			lastDeath = -1;
		}

#if VANILLA
		public override void Deserialize(IReader reader) {
#else
		public void Deserialize(IReader reader) {
#endif
			cycles = Math.Max(1, reader.ReadInt32());
			lastDeath = Math.Max(-1, reader.ReadInt32());
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.SAFESPACE.PROGRESS, Math.Max(0,
				AchievementStateComponent.Instance.LastDeath));
		}

		public override bool Success() {
			var inst = AchievementStateComponent.Instance;
			int cycle = GameClock.Instance?.GetCycle() ?? 0;
			if (inst != null && lastDeath >= 0) {
				inst.LastDeath = lastDeath;
				lastDeath = -1;
			}
			return inst != null && inst.LastDeath >= 0 && cycle - inst.LastDeath >= cycles;
		}
	}
}
