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

using HarmonyLib;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using UnityEngine;

using PickupComparer = System.Collections.Generic.IComparer<FetchManager.Pickup>;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Applied to FetchManager.FetchablesByPrefabId to optimize UpdatePickups. It already
	/// runs in a Job Manager for parallelism, so it cannot access components, but for now
	/// it stores stateful information required for chore selection and other sensors.
	/// </summary>
	internal static class FetchManagerFastUpdate {
		/// <summary>
		/// A delegate to trigger OffsetTracker updates off the main thread.
		/// </summary>
		private delegate void UpdateOffsets(OffsetTracker table, int current_cell);

		/// <summary>
		/// Retrieves the offset tracker from a workable (like Pickupable or Storage).
		/// </summary>
		private static readonly IDetouredField<Workable, OffsetTracker> OFFSET_TRACKER =
			PDetours.DetourField<Workable, OffsetTracker>("offsetTracker");

		/// <summary>
		/// Access the priority-based comparator of FetchManager.
		/// </summary>
		private static readonly IDetouredField<FetchManager, PickupComparer>
			PICKUP_COMPARER = PDetours.DetourField<FetchManager, PickupComparer>(
			"ComparerIncludingPriority");

		/// <summary>
		/// Gets the offsets from an offset tracker, without updating them which could trigger
		/// nasty things like scene partitioner rebuilds.
		/// </summary>
		private static readonly IDetouredField<OffsetTracker, CellOffset[]> RAW_OFFSETS =
			PDetours.DetourField<OffsetTracker, CellOffset[]>("offsets");

		/// <summary>
		/// A delegate to call the UpdateOffsets method manually of OffsetTracker.
		/// </summary>
		private static readonly UpdateOffsets UPDATE_OFFSETS = typeof(OffsetTracker).
			Detour<UpdateOffsets>();

		static FetchManagerFastUpdate() {
			if (OFFSET_TRACKER == null || RAW_OFFSETS == null || UPDATE_OFFSETS == null)
				PUtil.LogWarning("Unable to patch Navigator.GetNavigationCost");
		}

		/// <summary>
		/// Applied before UpdatePickups runs. A more optimized UpdatePickups whose aggregate
		/// runtime on a test world dropped from ~60 ms/1000 ms to ~45 ms/1000 ms.
		/// </summary>
		internal static bool BeforeUpdatePickups(FetchManager.FetchablesByPrefabId __instance,
				Navigator worker_navigator, GameObject worker_go) {
			var canBePickedUp = DictionaryPool<PickupTagKey, FetchManager.Pickup,
				FetchManager>.Allocate();
			var pathCosts = DictionaryPool<int, int, FetchManager>.Allocate();
			var finalPickups = __instance.finalPickups;
			// Will reflect the changes from Waste Not, Want Not and No Manual Delivery
			var comparer = PICKUP_COMPARER.Get(null);
			foreach (var fetchable in __instance.fetchables.GetDataList()) {
				var target = fetchable.pickupable;
				int cell = target.cachedCell;
				if (target.CouldBePickedUpByMinion(worker_go)) {
					// Look for cell cost, share costs across multiple queries to a cell
					if (!pathCosts.TryGetValue(cell, out int cost))
						pathCosts.Add(cell, cost = GetNavigationCost(worker_navigator,
							target, cell));
					// Exclude unreachable items
					if (cost >= 0) {
						int hash = fetchable.tagBitsHash;
						var key = new PickupTagKey(hash, target.KPrefabID);
						var candidate = new FetchManager.Pickup {
							pickupable = target, tagBitsHash = hash, PathCost = (ushort)cost,
							masterPriority = fetchable.masterPriority, freshness = fetchable.
							freshness, foodQuality = fetchable.foodQuality
						};
						if (canBePickedUp.TryGetValue(key, out FetchManager.Pickup current)) {
							// Is the new one better?
							int result = comparer.Compare(candidate, current);
							if (result > 0 || (result == 0 && candidate.pickupable.
									UnreservedAmount > current.pickupable.UnreservedAmount))
								canBePickedUp[key] = candidate;
						} else
							canBePickedUp.Add(key, candidate);
					}
				}
			}
			// Copy the remaining pickups to the list, there are now way fewer because only
			// one was kept per possible tag bits (with the highest priority, best path cost,
			// etc)
			finalPickups.Clear();
			foreach (var pair in canBePickedUp)
				finalPickups.Add(pair.Value);
			pathCosts.Recycle();
			canBePickedUp.Recycle();
			// Prevent the original method from running
			return false;
		}

		/// <summary>
		/// A non-mutating version of Navigator.GetNavigationCost that can be run on
		/// background threads.
		/// </summary>
		/// <param name="navigator">The navigator to calculate.</param>
		/// <param name="destination">The destination to find the cost.</param>
		/// <param name="cell">The workable's current cell.</param>
		/// <returns>The navigation cost to the destination.</returns>
		internal static int GetNavigationCost(Navigator navigator, Workable destination,
				int cell) {
			CellOffset[] offsets = null;
			if (OFFSET_TRACKER != null && RAW_OFFSETS != null && UPDATE_OFFSETS != null) {
				var offsetTracker = OFFSET_TRACKER.Get(destination);
				if (offsetTracker != null && (offsets = RAW_OFFSETS.Get(offsetTracker)) ==
						null) {
#if DEBUG
					PUtil.LogWarning("Updating Pickupable offsets!");
#endif
					UPDATE_OFFSETS.Invoke(offsetTracker, cell);
					offsets = RAW_OFFSETS.Get(offsetTracker);
				}
			}
			return (offsets == null) ? navigator.GetNavigationCost(cell) : navigator.
				GetNavigationCost(cell, offsets);
		}

		/// <summary>
		/// Wraps a prefab and its tag bit hash in a key structure that can be very quickly and
		/// properly hashed and compared for a dictionary key.
		/// </summary>
		private sealed class PickupTagKey {
			/// <summary>
			/// The prefab ID of the tagged object.
			/// </summary>
			private readonly KPrefabID id;

			/// <summary>
			/// The tag bits' hash.
			/// </summary>
			private readonly int hash;

			public PickupTagKey(int hash, KPrefabID id) {
				this.hash = hash;
				this.id = id;
			}

			public override bool Equals(object obj) {
				bool ret = false;
				if (obj is PickupTagKey other && hash == other.hash) {
					var bits = new TagBits(ref FetchManager.disallowedTagMask);
					// AndTagBits updates the argument!
					id.AndTagBits(ref bits);
					var otherBits = new TagBits(ref FetchManager.disallowedTagMask);
					other.id.AndTagBits(ref otherBits);
					ret = otherBits.AreEqual(ref bits);
				}
				return ret;
			}

			public override int GetHashCode() {
				return hash;
			}

			public override string ToString() {
				return "PickupTagKey[Hash={0:D},Tags=[{1}]]".F(hash, id.Tags.Join());
			}
		}
	}
}
