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

using System;
using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using PickupTagDict = System.Collections.Generic.Dictionary<PeterHan.FastTrack.GamePatches.
	FetchManagerFastUpdate.PickupTagKey, FetchManager.Pickup>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to FetchManager.FetchablesByPrefabId to optimize UpdatePickups. It already
	/// runs in a Job Manager for parallelism, so it cannot access components, but for now
	/// it stores stateful information required for chore selection and other sensors.
	/// </summary>
	internal static class FetchManagerFastUpdate {
		/// <summary>
		/// The pool of available temporary dictionaries for UpdatePickups. Must be concurrent
		/// as it is called in parallel by async path optimizations.
		/// </summary>
		private static readonly ConcurrentStack<PickupTagDict> POOL;

		private const int PRESEED = 4;

		static FetchManagerFastUpdate() {
			POOL = new ConcurrentStack<PickupTagDict>();
			for (int i = 0; i < PRESEED; i++)
				POOL.Push(new PickupTagDict(256, PickupTagEqualityComparer.Instance));
		}

		/// <summary>
		/// Gets a temporary pickup dictionary, retrieving one from the pool if available to
		/// avoid allocating memory.
		/// </summary>
		/// <returns>A pooled instance (if available) with the pickup tags.</returns>
		private static PickupTagDict Allocate() {
			if (!POOL.TryPop(out var dict))
				dict = new PickupTagDict(256, PickupTagEqualityComparer.Instance);
			return dict;
		}

		/// <summary>
		/// Applied before UpdatePickups runs. A more optimized UpdatePickups whose aggregate
		/// runtime on a test world dropped from ~60 ms/1000 ms to ~45 ms/1000 ms.
		/// </summary>
		internal static bool BeforeUpdatePickups(FetchManager.FetchablesByPrefabId __instance,
				Navigator worker_navigator, GameObject worker_go) {
			var canBePickedUp = Allocate();
			var pathCosts = __instance.cellCosts;
			var finalPickups = __instance.finalPickups;
			// Will reflect the changes from Waste Not, Want Not and No Manual Delivery
			var comparer = FetchManager.ComparerIncludingPriority;
			var fetchables = __instance.fetchables.GetDataList();
			int n = fetchables.Count;
			for (int i = 0; i < n; i++) {
				var fetchable = fetchables[i];
				var target = fetchable.pickupable;
				int cost;
				// Exclude unreachable items
				if (target.CouldBePickedUpByMinion(worker_go) && (cost = GetPathCost(target,
						worker_navigator, pathCosts)) >= 0) {
					int hash = fetchable.tagBitsHash, mp = fetchable.masterPriority;
					var key = new PickupTagKey(hash, target.KPrefabID, mp);
					var candidate = new FetchManager.Pickup {
						pickupable = target, tagBitsHash = hash, PathCost = (ushort)Math.
						Min(cost, ushort.MaxValue), masterPriority = mp,
						freshness = fetchable.freshness, foodQuality = fetchable.foodQuality
					};
					if (canBePickedUp.TryGetValue(key, out var current)) {
						// Is the new one better?
						int result = comparer.Compare(candidate, current);
						if (result < 0 || (result == 0 && target.UnreservedAmount > current.
								pickupable.UnreservedAmount))
							canBePickedUp[key] = candidate;
					} else
						canBePickedUp.Add(key, candidate);
				}
			}
			pathCosts.Clear();
			// Copy the remaining pickups to the list, there are now way fewer because only
			// one was kept per possible tag bits (with the highest priority, best path cost,
			// etc)
			finalPickups.Clear();
			foreach (var pickup in canBePickedUp.Values)
				finalPickups.Add(pickup);
			Recycle(canBePickedUp);
			// Prevent the original method from running
			return false;
		}

		/// <summary>
		/// Gets the path cost to a target item.
		/// </summary>
		/// <param name="target">The item to pick up.</param>
		/// <param name="navigator">The navigator attempting to find the item.</param>
		/// <param name="pathCosts">The cached path costs from previous queries.</param>
		/// <returns>The cost to navigate to the item's current cell.</returns>
		private static int GetPathCost(Pickupable target, Navigator navigator,
				IDictionary<int, int> pathCosts) {
			int cell = target.cachedCell;
			// Look for cell cost, share costs across multiple queries to a cell
			// If this is being run synchronous, no issue, otherwise the GSP patch will
			// avoid races on the scene partitioner
			if (!pathCosts.TryGetValue(cell, out int cost))
				pathCosts.Add(cell, cost = target.GetNavigationCost(navigator, cell));
			return cost;
		}

		/// <summary>
		/// Returns a temporary pickup dictionary to the pool.
		/// </summary>
		/// <param name="dict">The dictionary to clear and recycle.</param>
		private static void Recycle(PickupTagDict dict) {
			dict.Clear();
			POOL.Push(dict);
		}

		/// <summary>
		/// Wraps a prefab and its tag bit hash in a key structure that can be very quickly and
		/// properly hashed and compared for a dictionary key.
		/// </summary>
		internal readonly struct PickupTagKey : IEquatable<PickupTagKey> {
			/// <summary>
			/// The prefab ID of the tagged object.
			/// </summary>
			internal readonly KPrefabID ID;

			/// <summary>
			/// The tag bits' hash.
			/// </summary>
			internal readonly int Hash;

			/// <summary>
			/// The priority of the item.
			/// </summary>
			internal readonly int MasterPriority;

			public PickupTagKey(int hash, KPrefabID id, int priority) {
				Hash = hash;
				ID = id;
				MasterPriority = priority;
			}

			public override bool Equals(object obj) {
				return obj is PickupTagKey other && PickupTagEqualityComparer.Instance.Equals(
					this, other);
			}

			public bool Equals(PickupTagKey other) {
				return PickupTagEqualityComparer.Instance.Equals(this, other);
			}

			public override int GetHashCode() {
				return (Hash << 4) | MasterPriority;
			}

			public override string ToString() {
				return "PickupTagKey[Hash={0:D},Tags=[{1}]]".F(Hash, ID.Tags.Join());
			}
		}

		/// <summary>
		/// Compares and hashes PickupTagKey without any boxing.
		/// </summary>
		private sealed class PickupTagEqualityComparer : IEqualityComparer<PickupTagKey> {
			/// <summary>
			/// The singleton instance of this class.
			/// </summary>
			public static readonly PickupTagEqualityComparer Instance = new
				PickupTagEqualityComparer();

			private PickupTagEqualityComparer() { }

			public bool Equals(PickupTagKey x, PickupTagKey y) {
				bool ret = false;
				if (x.Hash == y.Hash && x.MasterPriority == y.MasterPriority) {
					var bitsA = new TagBits(ref FetchManager.disallowedTagMask);
					var bitsB = new TagBits(ref FetchManager.disallowedTagMask);
					x.ID.AndTagBits(ref bitsA);
					y.ID.AndTagBits(ref bitsB);
					ret = bitsA.AreEqual(ref bitsB);
				}
				return ret;
			}

			public int GetHashCode(PickupTagKey obj) {
				// Master priority is from 0 to 10
				return (obj.Hash << 4) | obj.MasterPriority;
			}
		}
	}
}
