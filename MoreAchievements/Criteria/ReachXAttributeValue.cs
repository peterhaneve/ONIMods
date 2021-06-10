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
using PeterHan.PLib;
using System;
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires a Duplicant to reach the specified value in the specified attribute.
	/// </summary>
	public class ReachXAttributeValue : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// The attribute ID which is checked.
		/// </summary>
		protected string attribute;

		/// <summary>
		/// The maximum attribute value currently present in the colony.
		/// </summary>
		protected float maxValue;

		/// <summary>
		/// The attribute value required.
		/// </summary>
		protected float required;

		public ReachXAttributeValue(string attribute, float required) {
			if (required.IsNaNOrInfinity())
				throw new ArgumentOutOfRangeException("required");
			this.attribute = attribute ?? throw new ArgumentNullException("attribute");
			maxValue = 0;
			this.required = Math.Max(0.0f, required);
		}

		public void Deserialize(IReader reader) {
			attribute = reader.ReadKleiString();
			maxValue = 0;
			required = Math.Max(0.0f, reader.ReadSingle());
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.DESTROYEROFWORLDS.PROGRESS, complete ?
				required : maxValue, required);
		}

		public override void Serialize(BinaryWriter writer) {
			writer.WriteKleiString(attribute);
			writer.Write(required);
		}

		public override bool Success() {
			return maxValue >= required;
		}

		public override void Update() {
			// Check each duplicant for the best value
			float best = 0.0f;
			var attr = Db.Get().Attributes.Get(attribute);
			foreach (var duplicant in Components.LiveMinionIdentities.Items)
				if (duplicant != null)
					best = Math.Max(best, attr.Lookup(duplicant).GetTotalValue());
			maxValue = best;
		}
	}
}
