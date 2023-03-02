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

using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using PreContext = Chore.Precondition.Context;
using SortedClearable = ClearableManager.SortedClearable;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Groups patches used to optimize chore selection.
	/// </summary>
	internal static class ChorePatches {
		/// <summary>
		/// A much faster version of (extension) ClsuterUtil.GetMyParentWorldId.
		/// </summary>
		/// <param name="go">The game object to look up.</param>
		/// <returns>The top level world ID of that game object.</returns>
		private static int GetMyParentWorldID(GameObject go) {
			int cell = Grid.PosToCell(go.transform.position), id;
			int invalid = ClusterManager.INVALID_WORLD_IDX;
			if (Grid.IsValidCell(cell)) {
				WorldContainer world;
				int index = Grid.WorldIdx[cell];
				if (index != invalid && (world = ClusterManager.Instance.GetWorld(index)) !=
						null)
					id = world.ParentWorldId;
				else
					id = index;
			} else
				id = invalid;
			return id;
		}

		/// <summary>
		/// Merges common preconditions between the two patches to see if the fast chore
		/// optimizations can be run.
		/// </summary>
		/// <param name="consumerState">The current chore consumer's state.</param>
		/// <param name="parentWorldID">Returns the world ID to use for checking chores.</param>
		/// <returns>true to run the patch, or false not to.</returns>
		private static bool CanUseFastChores(ChoreConsumerState consumerState,
				out int parentWorldID) {
			var inst = RootMenu.Instance;
			GameObject go;
			bool result = false;
			if ((inst != null && inst.IsBuildingChorePanelActive()) || consumerState.
					selectable.IsSelected || (go = consumerState.gameObject) == null)
				parentWorldID = 0;
			else {
				parentWorldID = GetMyParentWorldID(go);
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Applied to ChoreProvider to more efficiently check for chores.
		/// </summary>
		[HarmonyPatch(typeof(ChoreProvider), nameof(ChoreProvider.CollectChores))]
		internal static class ChoreProvider_CollectChores_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ChoreOpts;

			/// <summary>
			/// Applied before CollectChores runs.
			/// </summary>
			[HarmonyPriority(Priority.LowerThanNormal)]
			internal static bool Prefix(ChoreConsumerState consumer_state,
					ChoreProvider __instance, List<PreContext> succeeded) {
				bool run = false;
				// Avoid doing double the work on the patch that GCP already has
				if (__instance.GetType() != typeof(GlobalChoreProvider) && CanUseFastChores(
						consumer_state, out int worldID) && __instance.choreWorldMap.
						TryGetValue(worldID, out var chores) && chores != null) {
					var ci = ChoreComparator.Instance;
					run = ci.Setup(consumer_state, succeeded);
					if (run) {
						ci.CollectNonFetch(chores);
						ci.Cleanup();
					}
				}
				return !run;
			}
		}

		/// <summary>
		/// Applied to GlobalChoreProvider to more efficiently check for chores.
		/// </summary>
		[HarmonyPatch(typeof(GlobalChoreProvider), nameof(GlobalChoreProvider.CollectChores))]
		internal static class GlobalChoreProvider_CollectChores_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.ChoreOpts;

			/// <summary>
			/// Applied before CollectChores runs.
			/// </summary>
			internal static bool Prefix(ChoreConsumerState consumer_state,
					GlobalChoreProvider __instance, List<PreContext> succeeded) {
				bool run = false;
				if (CanUseFastChores(consumer_state, out int worldID)) {
					var ci = ChoreComparator.Instance;
					run = ci.Setup(consumer_state, succeeded);
					if (run) {
						var fetches = __instance.fetches;
						if (__instance.choreWorldMap.TryGetValue(worldID, out var chores))
							ci.CollectNonFetch(chores);
						ci.CollectSweep(__instance.clearableManager);
						int n = fetches.Count;
						for (int i = 0; i < n; i++)
							ci.Collect(fetches[i].chore);
						ci.Cleanup();
					}
				}
				return !run;
			}
		}
	}

	/// <summary>
	/// Compares chores the fast way!
	/// 
	/// Some chores rely on "static" state (fetch chores in particular), so this class is not
	/// thread safe anyways.
	/// </summary>
	internal sealed class ChoreComparator {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly ChoreComparator Instance = new ChoreComparator();

		/// <summary>
		/// The chore type used for Sweep errands.
		/// </summary>
		private static ChoreType transport;

		/// <summary>
		/// The interrupt priority of yellow alert chores.
		/// </summary>
		private static int yellowPriority;

		/// <summary>
		/// Trivially compares two chore contexts, establishing whether one is clearly better
		/// than the others.
		/// </summary>
		/// <param name="current">The current leading chore.</param>
		/// <param name="other">The chore that might be better.</param>
		/// <returns>positive if the new chore is clearly worse and should be skipped, 0 if the
		/// chores are somewhat equal and need closer comparison, or negative if the new chore
		/// is clearly better and should become the new leader.</returns>
		private static int CompareChores(ref PreContext current, ref PreContext other) {
			return CompareChores(ref current, other.personalPriority, ref other.masterPriority,
				other.priority, other.priorityMod);
		}
		
		/// <summary>
		/// Trivially compares two chore contexts, establishing whether one is clearly better
		/// than the others.
		/// </summary>
		/// <param name="current">The current leading chore.</param>
		/// <param name="personalPriority">The Duplicant's priority modifier of the candidate chore.</param>
		/// <param name="userPriority">The user's priority setting of the candidate chore.</param>
		/// <param name="proxPriority">The proximity priority modifier of the candidate chore.</param>
		/// <param name="chorePriority">The priority modifier of the chore.</param>
		/// <returns>positive if the new chore is clearly worse and should be skipped, 0 if the
		/// chores are somewhat equal and need closer comparison, or negative if the new chore
		/// is clearly better and should become the new leader.</returns>
		private static int CompareChores(ref PreContext current, int personalPriority,
				ref PrioritySetting userPriority, int proxPriority, int chorePriority) {
			ref var cp = ref current.masterPriority;
			// IsMoreSatisfyingEarly
			int result = cp.priority_class - userPriority.priority_class;
			if (result != 0)
				return result;
			result = current.personalPriority - personalPriority;
			if (result != 0)
				return result;
			result = cp.priority_value - userPriority.priority_value;
			if (result != 0)
				return result;
			result = current.priority - proxPriority;
			return result != 0 ? result : current.priorityMod - chorePriority;
		}

		/// <summary>
		/// Gets the interrupt priority of a chore.
		/// </summary>
		/// <param name="chore">The chore that could be interrupting.</param>
		/// <returns>The interrupt priority of this chore.</returns>
		private static int GetInterruptPriority(Chore chore) {
			return chore.masterPriority.priority_class == PriorityScreen.PriorityClass.
				topPriority ? yellowPriority : chore.choreType.interruptPriority;
		}
		
		/// <summary>
		/// Initializes static variables that only depend on the Db.
		/// </summary>
		internal static void Init() {
			var types = Db.Get().ChoreTypes;
			transport = types.Transport;
			yellowPriority = types.TopPriority.interruptPriority;
		}

		/// <summary>
		/// Runs selected chore preconditions that we did not already exclude.
		/// </summary>
		/// <param name="context">The chore context to evaluate.</param>
		private static void RunSomePreconditions(ref PreContext context) {
			var chore = context.chore;
			var preconditions = chore.preconditions;
			int n = preconditions.Count;
			if (chore.arePreconditionsDirty) {
				preconditions.Sort(ChorePreconditionComparer.Instance);
				chore.arePreconditionsDirty = false;
			}
			// i = 0 is "IsValid"
			for (int i = 1; i < n; i++) {
				// It would be faster to just remove the preconditions completely from all
				// chores, but they still need to be run when the tasks panel is open
				var pc = preconditions[i];
				string id = pc.id;
				if (id != "IsMoreSatisfyingEarly" && id != "IsPermitted" && id != "HasUrge" &&
						id != "IsOverrideTargetNullOrMe" && !pc.fn(ref context, pc.data)) {
					context.failedPreconditionId = i;
					break;
				}
			}
		}

		/// <summary>
		/// Whether advanced (i.e. proximity) priorities are enabled.
		/// </summary>
		private bool advPriority;

		/// <summary>
		/// The best chore precondition so far.
		/// </summary>
		private PreContext best;

		/// <summary>
		/// The consumer of this chore.
		/// </summary>
		private ChoreConsumerState consumerState;

		/// <summary>
		/// The currently active chore.
		/// </summary>
		private Chore currentChore;

		/// <summary>
		/// The personal priority of the current chore.
		/// </summary>
		private int currentPersonal;

		/// <summary>
		/// The chore tags which cannot interrupt the current chore.
		/// </summary>
		private ISet<Tag> exclusions;

		/// <summary>
		/// The minimum interrupt priority required to consider a chore.
		/// </summary>
		private int interruptPriority;
		
		/// <summary>
		/// The location where successful chores will be stored.
		/// </summary>
		private ICollection<PreContext> succeeded;

		/// <summary>
		/// The priority to use for sweep chores.
		/// </summary>
		private int transportPriority;
		
		/// <summary>
		/// Attempts to match up a Sweep errand with a Fetch destination.
		/// </summary>
		/// <param name="precondition">The preconditions of the sweep chore.</param>
		/// <param name="chore">The fetch chore to satisfy with the sweep.</param>
		/// <param name="sortedClearable">The candidate item to sweep.</param>
		/// <returns>true if the sweep chore can satisfy this fetch errand, or false otherwise.</returns>
		private bool CheckFetchChore(ref PreContext precondition, FetchChore chore,
				ref SortedClearable sortedClearable) {
			var masterPriority = sortedClearable.masterPriority;
			var pickupable = sortedClearable.pickupable;
			var prefabID = pickupable.KPrefabID;
			bool found = false;
			int diff = best.chore == null ? -1 : CompareChores(ref best, precondition.
				personalPriority, ref masterPriority, transportPriority, chore.priorityMod);
			if (diff <= 0 && ((chore.criteria == FetchChore.MatchCriteria.MatchID && chore.
					tags.Contains(prefabID.PrefabTag)) || (chore.criteria == FetchChore.
					MatchCriteria.MatchTags && prefabID.HasTag(chore.tagsFirst))) &&
					FastCheckPreconditions(chore, transport)) {
				precondition.Set(chore, consumerState, false, pickupable);
				RunSomePreconditions(ref precondition);
				found = precondition.IsSuccess();
				if (found) {
					precondition.masterPriority = masterPriority;
					precondition.priority = transportPriority;
					// Implement Stock Bug Fix's priority inheritance here
					precondition.interruptPriority = masterPriority.priority_class ==
						PriorityScreen.PriorityClass.topPriority ? yellowPriority :
						transport.interruptPriority;
					succeeded.Add(precondition);
					if (diff < 0)
						best = precondition;
				}
			}
			return found;
		}

		/// <summary>
		/// Cleans up after one pass of comparisons.
		/// </summary>
		internal void Cleanup() {
			best = default;
			consumerState = null;
			currentChore = null;
			exclusions = null;
			interruptPriority = int.MinValue;
			succeeded = null;
		}

		/// <summary>
		/// Collects available chores when the Duplicant is currently doing something else.
		/// </summary>
		/// <param name="chore">The chore which could be chosen.</param>
		internal void Collect(Chore chore) {
			// Fetch chores are handled separately
			if (FastCheckPreconditions(chore, chore.choreType)) {
				var newContext = new PreContext(chore, consumerState, false);
				int diff = best.chore == null ? -1 : CompareChores(ref best, ref newContext);
				if (diff <= 0) {
					RunSomePreconditions(ref newContext);
					if (newContext.IsSuccess()) {
						succeeded.Add(newContext);
						if (diff < 0)
							best = newContext;
					}
				}
			}
		}

		/// <summary>
		/// Collects all non-fetch chores from the specified list into the comparator.
		/// </summary>
		/// <param name="chores">The chores to collect.</param>
		internal void CollectNonFetch(IList<Chore> chores) {
			int nc = chores.Count;
			for (int i = 0; i < nc; i++) {
				var chore = chores[i];
				if (!(chore is FetchChore))
					Collect(chore);
			}
		}

		/// <summary>
		/// Collects Sweep errands as they do not generate chores, but still need a
		/// destination that is a valid Fetch errand.
		/// </summary>
		/// <param name="manager">The current sweep manager.</param>
		internal void CollectSweep(ClearableManager manager) {
			int personalPriority = consumerState.consumer.GetPersonalPriority(transport);
			bool found = false;
			var fetches = GlobalChoreProvider.Instance.fetches;
			var sortedClearables = manager.sortedClearables;
			int n = sortedClearables.Count, fn = fetches.Count;
			var precondition = default(PreContext);
			precondition.personalPriority = personalPriority;
			for (int i = 0; i < n && !found; i++) {
				var sortedClearable = sortedClearables[i];
				for (int j = 0; j < fn && !found; j++)
					found = CheckFetchChore(ref precondition, fetches[j].chore,
						ref sortedClearable);
			}
		}
		
		/// <summary>
		/// Checks the universal Chore preconditions very quickly with no failure reason.
		///
		/// The current chore should *not* be returned as a potential candidate as this would
		/// cause a chore-to-chore switch.
		/// </summary>
		/// <param name="chore">The chore to check.</param>
		/// <param name="typeForPermission">The chore type to use for IsPermitted.</param>
		/// <returns>true if the universal Chore preconditions are satisfied, or false otherwise.</returns>
		private bool FastCheckPreconditions(Chore chore, ChoreType typeForPermission) {
			// IsValid
			if (!chore.IsValid() || chore.isNull)
				return false;
			var consumer = consumerState.consumer;
			var type = chore.choreType;
			var overrideTarget = chore.overrideTarget;
			// Do not even consider chores that cannot interrupt the current chore
			return (currentChore == null || IsMoreSatisfyingEarly(chore)) &&
				// IsPermitted
				consumer.IsPermittedOrEnabled(typeForPermission, chore) &&
				// IsOverrideTargetNullOrMe
				(overrideTarget == null || overrideTarget == consumer) &&
				// HasUrge
				(type.urge == null || consumer.GetUrges().Contains(type.urge));
			// IsInMyParentWorld is no longer used!
		}

		/// <summary>
		/// A faster version of the IsMoreSatisfyingEarly precondition. Only to be used if
		/// there is a current chore.
		///
		/// The base game does this INCORRECTLY: it compares the modified priority (including
		/// proximity) to the unmodified priority (never explicit) in IsMoreSatisfyingEarly/
		/// IsMoreSatisfyingLate no matter the proximity priority setting. To properly mimic
		/// this bug, false will be returned for all chores that would fail this check even if
		/// they rightfully should be run.
		/// </summary>
		/// <param name="chore">The chore to check.</param>
		/// <returns>true to allow the chore to be considered, or false to exclude it.</returns>
		private bool IsMoreSatisfyingEarly(Chore chore) {
			var type = chore.choreType;
			var cp = currentChore.masterPriority;
			var mp = chore.masterPriority;
			bool result;
			int d;
			if (GetInterruptPriority(chore) > interruptPriority && (exclusions == null ||
					!exclusions.Overlaps(type.tags)))
				result = true;
			else if ((d = mp.priority_class - cp.priority_class) != 0)
				result = d > 0;
			else if ((d = consumerState.consumer.GetPersonalPriority(type) -
					currentPersonal) != 0)
				result = d > 0;
			else {
				d = mp.priority_value - cp.priority_value;
				result = d > 0 || (d == 0 && (advPriority ? type.explicitPriority : type.
					priority) > currentChore.choreType.priority);
			}
			return result;
		}

		/// <summary>
		/// Sets up the initial state for chore comparison.
		/// </summary>
		/// <param name="state">The consumer of this chore.</param>
		/// <param name="contexts">The location where successful chores will be stored.</param>
		/// <returns>true if some chores could match, or false if all chores will fail.</returns>
		internal bool Setup(ChoreConsumerState state, ICollection<PreContext> contexts) {
			int cell = Grid.PosToCell(state.gameObject.transform.position);
			bool valid = Grid.IsValidCell(cell);
			// If false, all vanilla chores would fail preconditions
			if (valid) {
				currentChore = state.choreDriver.GetCurrentChore();
				if (currentChore != null) {
					exclusions = currentChore.choreType.interruptExclusion;
					interruptPriority = GetInterruptPriority(currentChore);
					currentPersonal = state.consumer.GetPersonalPriority(currentChore.
						choreType);
				}
				advPriority = Game.Instance.advancedPersonalPriorities;
				transportPriority = advPriority ? transport.explicitPriority : transport.
					priority;
				best = default;
				consumerState = state;
				succeeded = contexts;
			} else
				currentChore = null;
			return valid;
		}
	}

	/// <summary>
	/// Sorts two chore precondition instances by sort order.
	/// </summary>
	internal sealed class ChorePreconditionComparer : IComparer<Chore.PreconditionInstance> {
		internal static readonly ChorePreconditionComparer Instance =
			new ChorePreconditionComparer();

		public int Compare(Chore.PreconditionInstance x, Chore.PreconditionInstance y) {
			return x.sortOrder.CompareTo(y.sortOrder);
		}
	}
}
