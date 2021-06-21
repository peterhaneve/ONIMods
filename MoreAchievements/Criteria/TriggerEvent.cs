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
	/// Requires an event to be triggered through the achievement state component.
	/// </summary>
	public sealed class TriggerEvent : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The ID of the event to trigger. Should be the achievement ID for simple
		/// achievements.
		/// </summary>
		public string ID { get; private set; }

		/// <summary>
		/// The description (non serialized) of this event.
		/// </summary>
		private string description;

		/// <summary>
		/// Whether this event has been triggered.
		/// </summary>
		private bool triggered;

		public TriggerEvent(string id) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException("id");
			ID = id;
			triggered = false;
			UpdateDescription();
		}

#if VANILLA
		public override void Deserialize(IReader reader) {
#else
		public void Deserialize(IReader reader) {
#endif
			ID = reader.ReadKleiString();
			triggered = reader.ReadInt32() != 0;
			UpdateDescription();
		}

		public override string GetProgress(bool complete) {
			return description;
		}

		public override void Serialize(BinaryWriter writer) {
			writer.WriteKleiString(ID);
			writer.Write(triggered ? 1 : 0);
		}

		public override bool Success() {
			return triggered;
		}

		/// <summary>
		/// Triggers the event for this achievement.
		/// </summary>
		public void Trigger() {
			triggered = true;
		}

		/// <summary>
		/// Updates the description of this achievement.
		/// </summary>
		private void UpdateDescription() {
			// Determine text from the achievement strings
			var type = AD.GetAchievementData(ID);
			if (type == null)
				description = AchievementStrings.DEFAULT_PROGRESS;
			else
				description = AD.GetStringValue(type, "PROGRESS") ?? AchievementStrings.
					DEFAULT_PROGRESS.text;
		}
	}
}
