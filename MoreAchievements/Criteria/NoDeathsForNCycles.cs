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
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires no Duplicants to die for the specified number of consecutive cycles.
	/// </summary>
	public sealed class NoDeathsForNCycles : ColonyAchievementRequirement, IDeathRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// How many cycles is required for no deaths.
		/// </summary>
		private int cycles;

		/// <summary>
		/// The cycle number of the last death.
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
			return string.Format(AchievementStrings.SAFESPACE.PROGRESS, Math.Max(0, lastDeath));
		}

		public override void Serialize(BinaryWriter writer) {
			writer.Write(cycles);
			// Try to update this before writing out
			if (lastDeath < 0)
				Update();
			writer.Write(lastDeath);
		}

		/// <summary>
		/// Triggered when any Duplicant dies.
		/// </summary>
		public void OnDeath(Death _) {
			var instance = GameClock.Instance;
			if (instance != null)
				lastDeath = instance.GetCycle();
		}

		public override bool Success() {
			int cycle = GameClock.Instance?.GetCycle() ?? 0;
			return lastDeath >= 0 && cycle - lastDeath >= cycles;
		}

		public override void Update() {
			if (lastDeath < 0) {
				// Look for the last dip in Duplicant count
				float lastValue = -1.0f;
				RetiredColonyData.RetiredColonyStatistic[] stats;
				var data = RetireColonyUtility.GetCurrentColonyRetiredColonyData();
				if ((stats = data?.Stats) != null && data.cycleCount > 0) {
					var liveDupes = new SortedList<int, float>(stats.Length);
					// Copy and sort the values
					foreach (var cycleData in stats)
						if (cycleData.id == RetiredColonyData.DataIDs.LiveDuplicants) {
							foreach (var entry in cycleData.value)
								liveDupes[Mathf.RoundToInt(entry.first)] = entry.second;
							break;
						}
					lastDeath = 0;
					// Sorted by cycle now
					foreach (var pair in liveDupes) {
						float dupes = pair.Value;
						if (dupes < lastValue)
							lastDeath = pair.Key;
						lastValue = dupes;
					}
					liveDupes.Clear();
				}
			}
		}
	}
}
