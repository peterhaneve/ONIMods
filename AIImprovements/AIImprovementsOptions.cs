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

using Newtonsoft.Json;
using PeterHan.PLib.Options;
using System.Collections.Generic;

namespace PeterHan.AIImprovements {
	/// <summary>
	/// The options class used for AI Improvements.
	/// </summary>
	[ModInfo("https://github.com/peterhaneve/ONIMods", "preview.png")]
	[JsonObject(MemberSerialization.OptIn)]
	public sealed class AIImprovementsOptions {
		/// <summary>
		/// Base divisor is 10000, so 3000/10000 = 0.3 priority.
		/// </summary>
		public const int BLOCK_PRIORITY_MOD = 4000;

		/// <summary>
		/// Base divisor is 10000, so 3000/10000 = 0.3 priority.
		/// </summary>
		public const int BUILD_PRIORIY_MOD = 3000;

		/// <summary>
		/// Buildings of this type will get -0.3 priority when planned.
		/// </summary>
		[JsonProperty]
		public List<string> DeprioritizeBuildings { get; set; }

		/// <summary>
		/// Buildings of this type will get +0.3 priority when planned.
		/// </summary>
		[JsonProperty]
		public List<string> PrioritizeBuildings { get; set; }

		public AIImprovementsOptions() {
			DeprioritizeBuildings = new List<string>(1);
			PrioritizeBuildings = new List<string>() {
				// Can't build "PropLadder"
				LadderConfig.ID, FirePoleConfig.ID, LadderFastConfig.ID, "SteelLadder",
				"RibbedFirePole"
			};
		}
	}

	/// <summary>
	/// An instance of AI Improvements options after load. Speeds up null checks and sanitizes
	/// the list.
	/// </summary>
	internal sealed class AIImprovementsOptionsInstance {
		/// <summary>
		/// Creates a new instance of AI Improvements Options.
		/// </summary>
		/// <param name="options">The options read from the file.</param>
		/// <returns>A non-null instance to use for options lookup.</returns>
		public static AIImprovementsOptionsInstance Create(AIImprovementsOptions options) {
			var ret = new AIImprovementsOptionsInstance();
			if (options != null) {
				// Buildings that get +0.3
				var collection = options.PrioritizeBuildings;
				if (collection != null)
					foreach (var id in collection)
						if (!string.IsNullOrEmpty(id))
							ret.PrioritizeBuildings.Add(id);
				// Buildings that get -0.3
				collection = options.DeprioritizeBuildings;
				if (collection != null)
					foreach (var id in collection)
						if (!string.IsNullOrEmpty(id))
							ret.DeprioritizeBuildings.Add(id);
			}
			return ret;
		}

		/// <summary>
		/// Buildings of this type will get -0.3 priority when planned.
		/// </summary>
		public ICollection<string> DeprioritizeBuildings { get; }

		/// <summary>
		/// Buildings of this type will get +0.3 priority when planned.
		/// </summary>
		public ICollection<string> PrioritizeBuildings { get; }

		internal AIImprovementsOptionsInstance() {
			DeprioritizeBuildings = new HashSet<string>();
			PrioritizeBuildings = new HashSet<string>();
		}
	}
}
