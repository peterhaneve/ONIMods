/*
 * Copyright 2020 Peter Han
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
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires the collection of the specified number of artifact types.
	/// </summary>
	public sealed class CollectNArtifacts : ColonyAchievementRequirement {
		/// <summary>
		/// The number of artifact types obtained.
		/// </summary>
		internal int Obtained { get; set; }

		/// <summary>
		/// The number of artifact types which must be collected.
		/// </summary>
		private int required;

		public CollectNArtifacts(int required) {
			Obtained = 0;
			this.required = Math.Max(1, required);
		}

		public override void Deserialize(IReader reader) {
			Obtained = 0;
			required = Math.Max(1, reader.ReadInt32());
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.BELONGSINAMUSEUM.PROGRESS, complete ?
				required : Obtained, required);
		}

		public override void Serialize(BinaryWriter writer) {
			writer.Write(required);
		}

		public override bool Success() {
			return Obtained >= required;
		}
	}
}
