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
using KSerialization;
using System;
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires a Duplicant death from the specified cause.
	/// </summary>
	public class DeathFromCause : ColonyAchievementRequirement, IDeathRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The cause of death which must occur.
		/// </summary>
		private string cause;

		/// <summary>
		/// Whether this death has occurred.
		/// </summary>
		private bool triggered;

		public DeathFromCause(string cause) {
			if (string.IsNullOrEmpty(cause))
				throw new ArgumentNullException("cause");
			this.cause = cause;
			triggered = false;
		}

#if VANILLA
		public override void Deserialize(IReader reader) {
#else
		public void Deserialize(IReader reader) {
#endif
			cause = reader.ReadKleiString();
			triggered = reader.ReadInt32() != 0;
		}

		public override string GetProgress(bool complete) {
			var death = Db.Get().Deaths.Get(cause);
			return string.Format(AchievementStrings.MASTEROFDISASTER.PROGRESS, (death ==
				null) ? cause : death.Name);
		}

		public void OnDeath(Death cause) {
			if (cause.Id == this.cause)
				triggered = true;
		}

		public override void Serialize(BinaryWriter writer) {
			writer.WriteKleiString(cause);
			writer.Write(triggered ? 1 : 0);
		}

		public override bool Success() {
			return triggered;
		}
	}
}
