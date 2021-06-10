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
using PeterHan.PLib;
using System;
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires a single Duplicant to reach the specified value in all attributes.
	/// </summary>
	public sealed class ReachXAllAttributes : ColonyAchievementRequirement, AchievementRequirementSerialization_Deprecated {
		/// <summary>
		/// Whether this requirement has been achieved.
		/// </summary>
		private bool achieved;

		/// <summary>
		/// The attribute which will be checked.
		/// </summary>
		private Klei.AI.Attribute[] check;

		/// <summary>
		/// The attribute value required.
		/// </summary>
		private float required;

		public ReachXAllAttributes(float required) {
			if (required.IsNaNOrInfinity())
				throw new ArgumentOutOfRangeException("required");
			achieved = false;
			this.required = Math.Max(0.0f, required);
		}

		public void Deserialize(IReader reader) {
			required = Math.Max(0.0f, reader.ReadSingle());
			achieved = false;
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.JACKOFALLTRADES.PROGRESS, required);
		}

		public override void Serialize(BinaryWriter writer) {
			writer.Write(required);
		}

		public override bool Success() {
			return achieved;
		}

		public override void Update() {
			var attributes = check;
			// Avoid recreating this every update
			if (attributes == null) {
				var dbAttr = Db.Get().Attributes;
				check = attributes = new Klei.AI.Attribute[] { dbAttr.Art, dbAttr.Athletics,
					dbAttr.Botanist, dbAttr.Caring, dbAttr.Construction, dbAttr.Cooking,
					dbAttr.Digging, dbAttr.Learning, dbAttr.Machinery, dbAttr.Ranching,
					dbAttr.Strength };
			}
			// Check each duplicant
			foreach (var duplicant in Components.LiveMinionIdentities.Items)
				if (duplicant != null) {
					bool ok = true;
					// All attributes must be over the threshold
					foreach (var attribute in attributes)
						if ((attribute.Lookup(duplicant)?.GetTotalValue() ?? 0.0f) < required)
						{
							ok = false;
							break;
						}
					if (ok) {
						achieved = true;
						break;
					}
				}
		}
	}
}
