/*
 * Copyright 2022 Peter Han
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

using System.Collections.Generic;

using TraitVal = TUNING.DUPLICANTSTATS.TraitVal;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Fixes traits that should be mutually exclusive. Mostly DLC specific.
	/// </summary>
	internal static class TraitsExclusionPatches {
		/// <summary>
		/// Excludes a trait from occurring on a particular interest.
		/// </summary>
		/// <param name="traitID">The trait ID to exclude.</param>
		/// <param name="interestID">The interest ID that should not have this trait.</param>
		/// <returns>true if the trait was excluded, or false if not found.</returns>
		private static bool ExcludeTraitFromInterest(string traitID, string interestID) {
			return ExcludeTraitFromInterest(traitID, interestID, TUNING.DUPLICANTSTATS.
				BADTRAITS) || ExcludeTraitFromInterest(traitID, interestID, TUNING.
				DUPLICANTSTATS.GOODTRAITS);
		}

		/// <summary>
		/// Excludes a trait from occurring on a particular interest.
		/// </summary>
		/// <param name="traitID">The trait ID to exclude.</param>
		/// <param name="interestID">The interest ID that should not have this trait.</param>
		/// <param name="toSearch">The trait values to search.</param>
		/// <returns>true if the trait was excluded, or false if not found.</returns>
		private static bool ExcludeTraitFromInterest(string traitID, string interestID,
				List<TraitVal> toSearch) {
			int n = toSearch.Count;
			bool found = false;
			for (int i = 0; i < n; i++) {
				var val = toSearch[i];
				if (val.id == traitID) {
					var exc = val.mutuallyExclusiveAptitudes;
					if (exc != null)
						exc.Add(interestID);
					else
						val.mutuallyExclusiveAptitudes = new List<HashedString>() { 
							interestID
						};
					toSearch[i] = val;
					found = true;
					break;
				}
			}
			return found;
		}

		/// <summary>
		/// Excludes a trait from occurring with another trait.
		/// </summary>
		/// <param name="traitID1">The trait ID to exclude.</param>
		/// <param name="traitID2">The trait ID that should not occur together.</param>
		/// <returns>true if the trait was excluded, or false if not found.</returns>
		private static bool ExcludeTraitsMutually(string traitID1, string traitID2) {
			return ExcludeTraitsMutually(traitID1, traitID2, TUNING.DUPLICANTSTATS.
				BADTRAITS) || ExcludeTraitsMutually(traitID1, traitID2, TUNING.
				DUPLICANTSTATS.GOODTRAITS);
		}

		/// <summary>
		/// Excludes a trait from occurring with another trait.
		/// </summary>
		/// <param name="traitID1">The trait ID to exclude.</param>
		/// <param name="traitID2">The trait ID that should not occur together.</param>
		/// <param name="toSearch">The trait values to search.</param>
		/// <returns>true if the trait was excluded, or false if not found.</returns>
		private static bool ExcludeTraitsMutually(string traitID1, string traitID2,
				List<TraitVal> toSearch) {
			int n = toSearch.Count;
			bool found = false;
			for (int i = 0; i < n; i++) {
				var val = toSearch[i];
				if (val.id == traitID1) {
					var exc = val.mutuallyExclusiveTraits;
					if (exc != null)
						exc.Add(traitID2);
					else
						val.mutuallyExclusiveTraits = new List<string>() {
							traitID2
						};
					toSearch[i] = val;
					found = true;
					break;
				}
			}
			return found;
		}

		/// <summary>
		/// Fixes those traits to actually make sense.
		/// </summary>
		internal static void FixTraits() {
			var sg = Db.Get().SkillGroups;
			// Undigging on dig interest
			ExcludeTraitFromInterest("DiggingDown", sg.Mining.Id);
			// Undigging and all of the Skilled: Mining
			ExcludeTraitsMutually("GrantSkill_Mining1", "DiggingDown");
			ExcludeTraitsMutually("GrantSkill_Mining2", "DiggingDown");
			ExcludeTraitsMutually("GrantSkill_Mining3", "DiggingDown");
			// Building Impaired on build interest
			ExcludeTraitFromInterest("ConstructionDown", sg.Building.Id);
			// Critter Aversion on ranch interest
			ExcludeTraitFromInterest("RanchingDown", sg.Ranching.Id);
			// Plant Murderer on farm interest
			ExcludeTraitFromInterest("BotanistDown", sg.Farming.Id);
			// Unpracticed Artist on decorate interest
			ExcludeTraitFromInterest("ArtDown", sg.Art.Id);
			// Uncultured and Unpracticed Artist
			ExcludeTraitsMutually("Uncultured", "ArtDown");
			// Unpracticed Artist and all of the Skilled: Decorating
			ExcludeTraitsMutually("GrantSkill_Arting1", "ArtDown");
			ExcludeTraitsMutually("GrantSkill_Arting2", "ArtDown");
			ExcludeTraitsMutually("GrantSkill_Arting3", "ArtDown");
			// Kitchen Menace on cook interest
			ExcludeTraitFromInterest("CookingDown", sg.Cooking.Id);
			// Unempathetic on doctoring interest
			ExcludeTraitFromInterest("CaringDown", sg.MedicalAid.Id);
			// Luddite on operate interest
			ExcludeTraitFromInterest("MachineryDown", sg.Technicals.Id);
			// Luddite and Skilled: Electrical Engineering
			ExcludeTraitsMutually("GrantSkill_Technicals2", "MachineryDown");
			// Anemic and Skilled: Exosuit Training
			ExcludeTraitsMutually("GrantSkill_Suits1", "Anemic");
			// Noodle Arms and Skilled: Plumbing
			ExcludeTraitsMutually("GrantSkill_Basekeeping2", "NoodleArms");
			// Building Impaired and Mechatronics Engineering
			ExcludeTraitsMutually("GrantSkill_Engineering1", "ConstructionDown");
		}
	}
}
