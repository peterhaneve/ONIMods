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
using System;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to FetchManager.FetchablesByPrefabId to optimize UpdatePickups. It already
	/// runs in a Job Manager for parallelism, so it cannot access components, but for now
	/// it stores stateful information required for chore selection and other sensors.
	/// </summary>
	internal static class FetchManagerFastUpdate {
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
			var comparer = FetchManager.ComparerIncludingPriority;
			bool needThreadSafe = FastTrackOptions.Instance.PickupOpts;
			foreach (var fetchable in __instance.fetchables.GetDataList()) {
				var target = fetchable.pickupable;
				int cell = target.cachedCell;
				if (target.CouldBePickedUpByMinion(worker_go)) {
					// Look for cell cost, share costs across multiple queries to a cell
					// If this is being run synchronous, no issue, otherwise the GSP patch will
					// avoid races on the scene partitioner
					if (!pathCosts.TryGetValue(cell, out int cost)) {
						if (needThreadSafe)
							worker_navigator.GetNavigationCostNU(target, cell, out cost);
						else
							cost = worker_navigator.GetNavigationCost(target);
						pathCosts.Add(cell, cost);
					}
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
							if (result < 0 || (result == 0 && candidate.pickupable.
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
		/// Wraps a prefab and its tag bit hash in a key structure that can be very quickly and
		/// properly hashed and compared for a dictionary key.
		/// </summary>
		private struct PickupTagKey : IEquatable<PickupTagKey> {
			/// <summary>
			/// The precomputed tag bits mask against the disallowed tags list. This field is
			/// actually not mutable, but cannot be readonly to pass the tag mask.
			/// </summary>
			private TagBits bits;

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
				bits = new TagBits(ref FetchManager.disallowedTagMask);
				id.AndTagBits(ref bits);
			}

			public override bool Equals(object obj) {
				return obj is PickupTagKey other && Equals(other);
			}

			// IEquatable prevents ObjectEqualityComparer from boxing the struct
			public bool Equals(PickupTagKey other) {
				bool ret = false;
				if (hash == other.hash)
					ret = other.bits.AreEqual(ref bits);
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
