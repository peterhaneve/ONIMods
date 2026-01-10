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
	/// Requires a Duplicant death from the specified cause.
	/// </summary>
	public class DeathFromCause : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The trigger cause used for deaths.
		/// </summary>
		public const string PREFIX = "DeathFrom_";

		/// <summary>
		/// The cause of death which must occur.
		/// </summary>
		private string cause;

		/// <summary>
		/// Whether a death from this cause occurred, when deserialized from a legacy save.
		/// </summary>
		private bool? triggered;

		public DeathFromCause(string cause) {
			if (string.IsNullOrEmpty(cause))
				throw new ArgumentNullException(nameof(cause));
			this.cause = cause;
			triggered = null;
		}

		public void Deserialize(IReader reader) {
			// Needs special case deserialization for save file migration
			cause = reader.ReadKleiString();
			triggered = reader.ReadInt32() != 0;
		}

		public override string GetProgress(bool complete) {
			var death = Db.Get().Deaths.Get(cause);
			return string.Format(AchievementStrings.MASTEROFDISASTER.PROGRESS, (death ==
				null) ? cause : death.Name);
		}

		public override bool Success() {
			var te = AchievementStateComponent.Instance?.TriggerEvents;
			if (this.triggered != null && te != null) {
				te[PREFIX + cause] = this.triggered ?? false;
				this.triggered = null;
			}
			return te != null && te.TryGetValue(PREFIX + cause, out bool triggered) &&
				triggered;
		}
	}
}
