/*
 * Copyright 2019 Peter Han
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

using PeterHan.PLib;
using System;
using UnityEngine;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Stores information determined about a duplicant's claustrophobia to see if it will
	/// trigger a confined or trapped notification.
	/// </summary>
	sealed class EntrapmentStatus {
		/// <summary>
		/// Whether this Duplicant can reach their bed. Also true if they have no bed or
		/// it is broken / disabled.
		/// </summary>
		public bool CanReachBed { get; }

		/// <summary>
		/// Whether this Duplicant can reach their mess table . Also true if they have no
		/// mess table or it is broken / disabled.
		/// </summary>
		public bool CanReachMess { get; }

		/// <summary>
		/// Whether this Duplicant can reach a toilet. Also true if there are no functional
		/// toilets in the colony.
		/// </summary>
		public bool CanReachToilet { get; }

		/// <summary>
		/// The entrapment status in the previous frame.
		/// </summary>
		public EntrapmentState LastStatus { get; set; }

		/// <summary>
		/// How many cells the Duplicant can reach.
		/// </summary>
		public int ReachableCells { get; }

		/// <summary>
		/// Used for bookkeeping to prune removed/dead Duplicants from the cache.
		/// </summary>
		public bool StillLiving { get; set; }

		/// <summary>
		/// Retrieves the "trapped score" of this Duplicant, gaining one point for each
		/// inaccessible essential colony item.
		/// </summary>
		public int TrappedScore {
			get {
				int score = 0;
				if (!CanReachBed) score++;
				if (!CanReachMess) score++;
				if (!CanReachToilet) score++;
				return score;
			}
		}

		/// <summary>
		/// The duplicant to which this status applies.
		/// </summary>
		public GameObject Victim { get; }

		/// <summary>
		/// The name of the victim duplicant. Deleted Duplicants crash on retrieving their
		/// name, so this is meant for the logs.
		/// </summary>
		public string VictimName { get; }

		public EntrapmentStatus(GameObject victim) {
			Victim = victim ?? throw new ArgumentNullException("victim");
			var trapQuery = ClaustrophobiaChecker.CheckEntrapment(victim);
			if (trapQuery != null) {
				ReachableCells = trapQuery.ReachableCells;
				CanReachBed = trapQuery.CanReachBed;
				CanReachMess = trapQuery.CanReachMess;
				CanReachToilet = trapQuery.CanReachToilet;
			} else {
				ReachableCells = 0;
				CanReachBed = false;
				CanReachMess = false;
				CanReachToilet = false;
			}
			LastStatus = EntrapmentState.None;
			StillLiving = true;
			VictimName = victim.name;
		}

		public override string ToString() {
			return "{0} ({5} last): {1:D} reachable, bed:{2}, mess:{3}, toilet:{4}".F(
				VictimName, ReachableCells, CanReachBed, CanReachMess, CanReachToilet,
				LastStatus);
		}
	}
}
