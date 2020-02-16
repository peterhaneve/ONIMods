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
using System.Collections.Generic;
using System.IO;

namespace PeterHan.MoreAchievements.Criteria {
	/// <summary>
	/// Requires all destinations on the starmap to be visited by a rocket.
	/// </summary>
	public sealed class VisitAllPlanets : ColonyAchievementRequirement {
		/// <summary>
		/// The destination IDs already visited.
		/// </summary>
		private ICollection<int> beenTo;

		/// <summary>
		/// The number of planets which must be visited.
		/// </summary>
		private int required;

		public VisitAllPlanets() {
			required = int.MaxValue;
			beenTo = new HashSet<int>();
		}

		public override void Deserialize(IReader reader) {
			int visited = Math.Max(0, reader.ReadInt32());
			required = int.MaxValue;
			// Somehow this can be constructed without executing its constructor!!!
			if (beenTo == null)
				beenTo = new HashSet<int>();
			else
				beenTo.Clear();
			for (int i = 0; i < visited; i++)
				beenTo.Add(reader.ReadInt32());
		}

		public override string GetProgress(bool complete) {
			return string.Format(AchievementStrings.WHOLENEWWORLDS.PROGRESS, complete ?
				required : beenTo.Count, required);
		}

		/// <summary>
		/// Triggered when a space destination is visited.
		/// </summary>
		/// <param name="id">The destination ID that was visited.</param>
		public void OnVisit(int id) {
			beenTo.Add(id);
		}

		public override void Serialize(BinaryWriter writer) {
			writer.Write(beenTo.Count);
			foreach (int id in beenTo)
				writer.Write(id);
		}

		public override bool Success() {
			return beenTo.Count >= required;
		}

		public override void Update() {
			var dest = SpacecraftManager.instance?.destinations;
			if (dest != null) {
				int count = 0;
				// Exclude unreachable destinations (earth) but include temporal tear
				foreach (var destination in dest)
					if (destination.GetDestinationType()?.visitable == true)
						count++;
				if (count > 0)
					required = count;
			}
		}
	}
}
