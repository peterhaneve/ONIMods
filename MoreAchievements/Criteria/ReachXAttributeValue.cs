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
using PeterHan.PLib.Core;
using System;

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
		/// The attribute value required.
		/// </summary>
		protected float required;

		public ReachXAttributeValue(string attribute, float required) {
			if (required.IsNaNOrInfinity())
				throw new ArgumentOutOfRangeException("required");
			this.attribute = attribute ?? throw new ArgumentNullException("attribute");
			this.required = Math.Max(0.0f, required);
		}

		public void Deserialize(IReader reader) {
			attribute = reader.ReadKleiString();
			required = Math.Max(0.0f, reader.ReadSingle());
		}

		/// <summary>
		/// Gets the best value of this attribute ever seen in the colony, registering to
		/// track it if not already tracked.
		/// </summary>
		/// <returns>The best attribute value seen.</returns>
		protected float GetBestValue() {
			var bv = AchievementStateComponent.Instance?.BestAttributeValue;
			float bestValue = 0.0f;
			if (bv != null && !bv.TryGetValue(attribute, out bestValue)) {
				bestValue = 0.0f;
				bv.Add(attribute, 0.0f);
			}
			return bestValue;
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.DESTROYEROFWORLDS.PROGRESS, complete ?
				required : GetBestValue(), required);
		}

		public override bool Success() {
			return GetBestValue() >= required;
		}
	}
}
