/*
 * Copyright 2023 Peter Han
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
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires a wire of a specific type to take overload damage.
	/// </summary>
	public sealed class OverloadWire : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The trigger cause used for overloads.
		/// </summary>
		public const string PREFIX = "OverloadWire_";

		/// <summary>
		/// The wire type that must be overloaded.
		/// </summary>
		private Wire.WattageRating type;

		public OverloadWire(Wire.WattageRating type) {
			this.type = type;
		}

		public void Deserialize(IReader reader) {
			// Needs special case deserialization for save file migration
			type = (Wire.WattageRating)reader.ReadInt32();
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.POWEROVERWHELMING.PROGRESS, GameUtil.
				GetFormattedWattage(Wire.GetMaxWattageAsFloat(type)));
		}

		public override bool Success() {
			var te = AchievementStateComponent.Instance?.TriggerEvents;
			return te != null && te.TryGetValue(PREFIX + type.ToString(), out bool
				overloaded) && overloaded;
		}
	}
}
