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
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Checks for trapped or confined Duplicants and spawns notifications for them.
	/// </summary>
	sealed class ClaustrophobiaChecker : KMonoBehaviour, ISim1000ms {
		/// <summary>
		/// Duplicants are considered confined if they can reach less than 10% of the cell
		/// count that the most mobile Duplicant can, or are confined to this many cells and
		/// the most mobile Duplicant can reach more than this quantity
		/// </summary>
		private const int MIN_CONFINED = 8;

		/// <summary>
		/// Every duplicant is checked for confined / trapped within approximately this many
		/// seconds in in-game time. The duplicants are cycled through in groups, checking a
		/// few every second.
		/// </summary>
		private const int PACE_CYCLE_TIME = 15;

		/// <summary>
		/// Checks assigned objects and determines their locations.
		/// </summary>
		/// <param name="owner">The owner of the objects.</param>
		/// <param name="type">The type of object to search.</param>
		/// <param name="cells">Valid ownables will have their cell locations placed here.</param>
		private static void CheckAssignedCell(Ownables owner, AssignableSlot type,
				IList<int> cells) {
			var items = Game.Instance.assignmentManager.GetPreferredAssignables(owner, type);
			cells.Clear();
			if (items != null)
				// All usable items are added
				foreach (Assignable item in items)
					if (item.gameObject.IsUsable())
						cells.Add(Grid.PosToCell(item));
		}

		/// <summary>
		/// Checks for a trapped or confined Duplicant.
		/// </summary>
		/// <param name="victim">The Duplicant to check.</param>
		/// <returns>The results of the entrapment query, or null if they could not be determined.</returns>
		internal static EntrapmentQuery CheckEntrapment(GameObject victim) {
			EntrapmentQuery result = null;
			MinionIdentity mi;
			if (victim != null && (mi = victim.GetComponent<MinionIdentity>()) != null) {
				var cells = ListPool<int, ClaustrophobiaChecker>.Allocate();
				var soleOwner = mi.GetSoleOwner();
				var slots = Db.Get().AssignableSlots;
				// Check beds
				CheckAssignedCell(soleOwner, slots.Bed, cells);
				int bedCell = cells.Count > 0 ? cells[0] : 0;
				// Check mess tables
				CheckAssignedCell(soleOwner, slots.MessStation, cells);
				int messCell = cells.Count > 0 ? cells[0] : 0;
				// Check toilets
				CheckAssignedCell(soleOwner, slots.Toilet, cells);
				int[] toiletCells = cells.ToArray();
				var navigator = victim.GetComponent<Navigator>();
				if (navigator != null) {
					// The result will always be null, just keep the navigable cell total
					result = new EntrapmentQuery(bedCell, messCell, toiletCells);
					navigator.RunQuery(result);
				}
				cells.Recycle();
			}
			return result;
		}

		/// <summary>
		/// These Duplicants will be immediately rechecked on the next pass.
		/// </summary>
		private readonly IList<GameObject> checkNextFrame;

		/// <summary>
		/// Limits performance impact by cycling through duplicants.
		/// </summary>
		private int minionPacer;

		/// <summary>
		/// Cached Duplicants refreshed on every recycle of the pacer.
		/// </summary>
		private readonly IList<GameObject> minionCache;

		/// <summary>
		/// Stores the status of each living Duplicant.
		/// </summary>
		private readonly IDictionary<GameObject, EntrapmentStatus> statusCache;

		public ClaustrophobiaChecker() {
			checkNextFrame = new List<GameObject>(8);
			minionCache = new List<GameObject>(64);
			minionPacer = 0;
			// PooledDictionary is useless since this dictionary is created once per load
			statusCache = new Dictionary<GameObject, EntrapmentStatus>(64);
		}

		/// <summary>
		/// Determines the approximate reachable colony size by finding the Duplicant which can
		/// reach the most locations.
		/// </summary>
		/// <returns>The number of locations reachable by the most free Duplicant.</returns>
		private int CalculateColonySize() {
			int mostReachable = 0;
			// Find most free dupe
			foreach (var pair in statusCache) {
				int reachable = pair.Value.ReachableCells;
				if (reachable > mostReachable)
					mostReachable = reachable;
			}
			return mostReachable;
		}

		/// <summary>
		/// Checks the specified entrapment status and creates the notifications if Duplicants
		/// are trapped or confined.
		/// </summary>
		/// <param name="toCheck">The entrapment status to check.</param>
		private void CheckNotifications(ICollection<EntrapmentStatus> toCheck) {
			int mostReachable = CalculateColonySize(), threshold = (mostReachable + 5) / 10;
			// Using summary stats, check all dupes
			foreach (var status in toCheck) {
				var victim = status.Victim;
				var obj = victim.gameObject;
				int reachable = status.ReachableCells;
				var lastStatus = status.LastStatus;
				// Create notifications if not yet present
				var confined = obj.AddOrGet<ConfinedNotification>();
				var trapped = obj.AddOrGet<TrappedNotification>();
				if ((mostReachable > MIN_CONFINED && reachable < MIN_CONFINED) ||
						(reachable < threshold)) {
					// Confined
					PUtil.LogDebug(("{0} is confined ({3} last), reaches {1:D}, " +
						"best reach {2:D}").F(status.VictimName, reachable, mostReachable,
						lastStatus));
					if (lastStatus == EntrapmentState.Confined) {
						confined.Show();
						trapped.Hide();
					} else
						// Preserve current notification state, check next time
						checkNextFrame.Add(victim);
					status.LastStatus = EntrapmentState.Confined;
				} else if (status.TrappedScore > 1) {
					// Trapped
					PUtil.LogDebug(("{0} is trapped ({4} last), bed? {1}, mess? {2}, " +
						"toilet? {3}").F(status.VictimName, status.CanReachBed,
						status.CanReachMess, status.CanReachToilet, lastStatus));
					if (lastStatus == EntrapmentState.Trapped) {
						confined.Hide();
						trapped.Show();
					} else
						// Preserve current notification state, check next time
						checkNextFrame.Add(victim);
					status.LastStatus = EntrapmentState.Trapped;
				} else {
					// Neither
					trapped.Hide();
					confined.Hide();
					status.LastStatus = EntrapmentState.None;
				}
			}
		}

		/// <summary>
		/// At the start of an entrapment cycle check, refreshes the cached list of living
		/// Duplicants and clears old entries.
		/// </summary>
		private void FillDuplicantList() {
			var enumerator = Components.LiveMinionIdentities.GetEnumerator();
			minionCache.Clear();
			// Invalidate all entries
			foreach (var pair in statusCache)
				pair.Value.StillLiving = false;
			// First iterate living duplicants and add valid entries to the list
			try {
				GameObject obj;
				while (enumerator.MoveNext()) {
					var dupe = enumerator.Current as MinionIdentity;
					// Do not replace with ?. since Unity overloads "=="
					if (dupe != null && (obj = dupe.gameObject) != null && dupe.isSpawned) {
						minionCache.Add(obj);
						// Mark entry as valid
						if (statusCache.TryGetValue(obj, out EntrapmentStatus entry))
							entry.StillLiving = true;
					}
				}
			} finally {
				(enumerator as IDisposable)?.Dispose();
			}
			int living = statusCache.Count;
			// Clear entries from the cache of deleted / deceased dupes
			if (living > 0) {
				var oldDupes = new GameObject[living];
				statusCache.Keys.CopyTo(oldDupes, 0);
				foreach (var oldDupe in oldDupes)
					if (statusCache.TryGetValue(oldDupe, out EntrapmentStatus entry) && !entry.
							StillLiving) {
						statusCache.Remove(oldDupe);
#if DEBUG
						PUtil.LogDebug("Removing {0} from cache".F(entry.VictimName));
#endif
					}
			}
		}

		/// <summary>
		/// Checks Duplicants who need to be rechecked from the previous frame.
		/// </summary>
		/// <param name="checkThisFrame">The Duplicants to check for this frame.</param>
		private void RecheckLastFrame(IList<EntrapmentStatus> checkThisFrame) {
			foreach (var dupe in checkNextFrame)
				// Do not replace with ?. since Unity overloads "=="
				if (dupe != null && dupe.activeInHierarchy) {
#if DEBUG
					PUtil.LogDebug("Rechecking " + dupe.name);
#endif
					checkThisFrame.Add(UpdateStatus(dupe));
				}
			checkNextFrame.Clear();
		}

		/// <summary>
		/// Checks for trapped Duplicants.
		/// </summary>
		/// <param name="delta">The actual time since the last check.</param>
		public void Sim1000ms(float delta) {
			var checkThisFrame = ListPool<EntrapmentStatus, ClaustrophobiaChecker>.Allocate();
			int pacer = minionPacer;
			// Refresh Duplicant cache if necessary
			if (pacer < 0 || pacer >= minionCache.Count) {
				FillDuplicantList();
				pacer = 0;
			}
			int len = minionCache.Count, step = 1 + Math.Max(0, len - 1) / PACE_CYCLE_TIME;
			RecheckLastFrame(checkThisFrame);
			// Add periodic duplicants to check this time
			for (int i = 0; i < step && pacer < len; i++) {
				var dupe = minionCache[pacer++];
				// Do not replace with ?. since Unity overloads "=="
				if (dupe != null && dupe.activeInHierarchy && !dupe.IsFalling())
					// Exclude falling Duplicants, they have no pathing
					checkThisFrame.Add(UpdateStatus(dupe));
			}
			CheckNotifications(checkThisFrame);
			checkThisFrame.Recycle();
			minionPacer = pacer;
		}

		/// <summary>
		/// Updates a Duplicant's entrapment status.
		/// </summary>
		/// <param name="dupe">The Duplicant to check.</param>
		/// <returns>The status calculated for this Duplicant.</returns>
		private EntrapmentStatus UpdateStatus(GameObject dupe) {
			var status = new EntrapmentStatus(dupe);
			if (statusCache.TryGetValue(dupe, out EntrapmentStatus oldStatus)) {
				// Copy status from previous entry
				status.LastStatus = oldStatus.LastStatus;
				statusCache[dupe] = status;
			} else {
				// Add to cache if missing
				statusCache.Add(dupe, status);
				PUtil.LogDebug("Adding " + status);
			}
			return status;
		}
	}
}
