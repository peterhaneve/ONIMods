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

using PeterHan.PLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.Claustrophobia {
	/// <summary>
	/// Checks for trapped or confined Duplicants and spawns notifications for them.
	/// </summary>
	internal sealed class ClaustrophobiaChecker : KMonoBehaviour, ISim1000ms {
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
		/// The singleton instance of this class.
		/// </summary>
		internal static ClaustrophobiaChecker Instance { get; private set; }

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
		private static EntrapmentQuery CheckEntrapment(GameObject victim) {
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
					// The query will always return null, just keep the navigable cell total
					result = new EntrapmentQuery(victim, bedCell, messCell, toiletCells);
					navigator.RunQuery(result);
				}
				cells.Recycle();
			}
			return result;
		}

		/// <summary>
		/// These Duplicants will be immediately rechecked on the next pass.
		/// </summary>
		private readonly ICollection<GameObject> checkNextTime;

		/// <summary>
		/// Limits performance impact by cycling through duplicants.
		/// </summary>
		private volatile int minionPacer;

		/// <summary>
		/// The number of pathable tiles the most free Duplicant can currently reach.
		/// </summary>
		private volatile int mostReachable;

		/// <summary>
		/// Cached Duplicants refreshed on every recycle of the pacer.
		/// </summary>
		private readonly IList<GameObject> minionCache;

		/// <summary>
		/// The value of mostReachable which is still being calculated.
		/// </summary>
		private volatile int pendingReachable;

		internal ClaustrophobiaChecker() {
			checkNextTime = new HashSet<GameObject>();
			minionCache = new List<GameObject>(64);
			minionPacer = 0;
			mostReachable = pendingReachable = 0;
		}

		/// <summary>
		/// Calculates the Duplicants which will be checked this time for confinement.
		/// </summary>
		/// <param name="toDo">The location where the Duplicants to check will be stored.</param>
		private void FillDuplicantList(ICollection<GameObject> toDo) {
			lock (checkNextTime) {
				int pacer = minionPacer;
				// Refresh Duplicant cache if necessary
				if (pacer < 0 || pacer >= minionCache.Count) {
					minionCache.Clear();
					mostReachable = pendingReachable;
					pendingReachable = 0;
					// First iterate living duplicants and add valid entries to the list
					GameObject obj;
					foreach (var dupe in Components.LiveMinionIdentities.Items)
						// Do not replace with ?. since Unity overloads "=="
						if (dupe != null && (obj = dupe.gameObject) != null && dupe.isSpawned)
							minionCache.Add(obj);
					pacer = 0;
				}
				int len = minionCache.Count, step = 1 + Math.Max(0, len - 1) / PACE_CYCLE_TIME;
				// Add periodic duplicants to refresh
				for (int i = 0; i < step && pacer < len; i++) {
					var dupe = minionCache[pacer++];
					if (dupe != null && !checkNextTime.Contains(dupe))
						toDo.Add(dupe);
				}
				// Add forced duplicants
				foreach (var dupe in checkNextTime)
					toDo.Add(dupe);
				checkNextTime.Clear();
				minionPacer = pacer;
			}
		}

		/// <summary>
		/// Forces a Duplicant to be checked on the next iteration. Used when a Duplicant
		/// enters a trapped or confined state and is pending a recheck.
		/// </summary>
		/// <param name="duplicant">The Duplicant to recheck.</param>
		internal void ForceCheckDuplicant(GameObject duplicant) {
			if (duplicant != null)
				lock (checkNextTime) {
#if DEBUG
					PUtil.LogDebug("Force check " + duplicant?.name);
#endif
					checkNextTime.Add(duplicant);
				}
		}

		protected override void OnCleanUp() {
			Instance = null;
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			Instance = this;
		}

		/// <summary>
		/// Checks for trapped Duplicants.
		/// </summary>
		/// <param name="delta">The actual time since the last check.</param>
		public void Sim1000ms(float delta) {
			var results = ListPool<EntrapmentQuery, ClaustrophobiaChecker>.Allocate();
			var toDo = ListPool<GameObject, ClaustrophobiaChecker>.Allocate();
			FillDuplicantList(toDo);
			foreach (var dupe in toDo)
				// Do not replace with ?. since Unity overloads "=="
				// Exclude falling Duplicants, they have no pathing
				if (dupe != null && dupe.activeInHierarchy && !dupe.IsFalling())
					results.Add(CheckEntrapment(dupe));
			int threshold = (mostReachable + 5) / 10, pend = pendingReachable;
			bool strict = ClaustrophobiaPatches.Options.StrictConfined;
			foreach (var result in results) {
				var obj = result.Victim;
				int reachable = result.ReachableCells;
				pend = Math.Max(reachable, pend);
				var smi = obj.GetSMI<ClaustrophobiaMonitor.Instance>();
				if (smi != null) {
					if (((mostReachable > MIN_CONFINED && reachable < MIN_CONFINED) ||
							reachable < threshold) && (!strict || result.TrappedScore > 1)) {
						// Confined
						smi.sm.IsTrapped.Set(false, smi);
						smi.sm.IsConfined.Set(true, smi);
#if DEBUG
						PUtil.LogDebug("{0} is confined: reaches {1:D}, best reach {2:D}".F(
							obj?.name, reachable, mostReachable));
#endif
					} else if (result.TrappedScore > 1) {
						// Trapped
						smi.sm.IsConfined.Set(false, smi);
						smi.sm.IsTrapped.Set(true, smi);
#if DEBUG
						PUtil.LogDebug("{0} is trapped: bed? {1}, mess? {2}, toilet? {3}".F(
							obj?.name, result.CanReachBed, result.CanReachMess, result.
							CanReachToilet));
#endif
					} else {
						// Neither
						smi.sm.IsConfined.Set(false, smi);
						smi.sm.IsTrapped.Set(false, smi);
					}
				}
			}
			pendingReachable = pend;
			toDo.Recycle();
			results.Recycle();
		}
	}
}
