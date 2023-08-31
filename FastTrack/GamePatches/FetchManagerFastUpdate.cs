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

using System;
using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

using FMPickup = FetchManager.Pickup;

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
				POOL.Push(new PickupTagDict());
		}

		/// <summary>
		/// Applied before UpdatePickups runs. A more optimized UpdatePickups whose aggregate
		/// runtime on a test world dropped from ~60 ms/1000 ms to ~45 ms/1000 ms.
		/// </summary>
		internal static bool BeforeUpdatePickups(FetchManager.FetchablesByPrefabId __instance,
				Navigator worker_navigator, GameObject worker_go) {
			var pathCosts = __instance.cellCosts;
			var finalPickups = __instance.finalPickups;
			// Will reflect the changes from Waste Not, Want Not and No Manual Delivery
			var fetchables = __instance.fetchables.GetDataList();
			int n = fetchables.Count;
			if (!POOL.TryPop(out var canBePickedUp))
				canBePickedUp = new PickupTagDict();
			for (int i = 0; i < n; i++) {
				var fetchable = fetchables[i];
				var target = fetchable.pickupable;
				int cell = target.cachedCell;
				if (!pathCosts.TryGetValue(cell, out int cost))
					pathCosts.Add(cell, cost = target.GetNavigationCost(worker_navigator,
						cell));
				// Exclude unreachable items
				if (target.CouldBePickedUpByMinion(worker_go) && cost >= 0)
					canBePickedUp.AddItem(ref fetchable, cost);
			}
			pathCosts.Clear();
			// Copy the remaining pickups to the list, there are now way fewer because only
			// one was kept per possible tag bits (with the best path cost etc)
			finalPickups.Clear();
			canBePickedUp.CollectAll(finalPickups);
			canBePickedUp.Clear();
			POOL.Push(canBePickedUp);
			// Prevent the original method from running
			return false;
		}

		/// <summary>
		/// Wraps a prefab and its tag bit hash in a key structure that can be very quickly and
		/// properly hashed and compared for a dictionary key.
		/// </summary>
		internal readonly struct PickupTagKey : IEquatable<PickupTagKey> {
			/// <summary>
			/// The tag bits' hash.
			/// </summary>
			internal readonly int Hash;

			/// <summary>
			/// The prefab ID of the tagged object.
			/// </summary>
			internal readonly KPrefabID ID;

			public PickupTagKey(int hash, KPrefabID id) {
				Hash = hash;
				ID = id;
			}

			public override bool Equals(object obj) {
				return obj is PickupTagKey other && Hash == other.Hash;
			}

			public bool Equals(PickupTagKey other) {
				return Hash == other.Hash;
			}

			public override int GetHashCode() {
				return Hash;
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
				return x.Hash == y.Hash;
			}

			public int GetHashCode(PickupTagKey obj) {
				return obj.Hash;
			}
		}

		/// <summary>
		/// Maps pickup tags to a list (by priority) of items.
		/// </summary>
		private sealed class PickupTagDict {
			/// <summary>
			/// Priority is usually from 1-9, Priority Zero adds p0, Stock Bug Fix can add p10
			/// </summary>
			private const int MAX_PRIORITY = 10;

			/// <summary>
			/// Pools items list to reduce memory allocations. As each UpdatePickups runs on
			/// its own thread in the worst case and this class is already pooled, no
			/// contention can occur here.
			/// </summary>
			private readonly Queue<FMPickup[]> itemPool;

			/// <summary>
			/// Maps pickup tags to a list by priority.
			/// </summary>
			private readonly IDictionary<PickupTagKey, FMPickup[]> pickups;

			internal PickupTagDict() {
				itemPool = new Queue<FMPickup[]>(64);
				pickups = new Dictionary<PickupTagKey, FMPickup[]>(256,
					PickupTagEqualityComparer.Instance);
			}

			/// <summary>
			/// Adds an item to the list of fetchable items.
			/// </summary>
			/// <param name="fetchable">The item that can be fetched.</param>
			/// <param name="cost">The path cost to the item.</param>
			public void AddItem(ref FetchManager.Fetchable fetchable, int cost) {
				int hash = fetchable.tagBitsHash, mp = fetchable.masterPriority, result;
				var target = fetchable.pickupable;
				var key = new PickupTagKey(hash, target.KPrefabID);
				var candidate = new FMPickup {
					pickupable = target, tagBitsHash = hash, PathCost = (ushort)Math.Min(cost,
					ushort.MaxValue), masterPriority = mp, freshness = fetchable.freshness,
					foodQuality = fetchable.foodQuality
				};
				if (!pickups.TryGetValue(key, out var slots)) {
					slots = itemPool.Count > 0 ? itemPool.Dequeue() : new FMPickup[1 +
						MAX_PRIORITY];
					pickups.Add(key, slots);
				}
				if (mp < 0)
					// Priority Zero uses -200 priority
					mp = 0;
				else if (mp >= slots.Length) {
#if DEBUG
					PUtil.LogDebug("Item priority is outside bounds: " + mp);
#endif
					slots = new FMPickup[mp + 1];
					pickups[key] = slots;
				}
				ref var current = ref slots[mp];
				var pu = current.pickupable;
				// Is the new one better?
				if (pu == null || (result = FetchManager.ComparerIncludingPriority.Compare(
						candidate, current)) < 0 || (result == 0 && target.UnreservedAmount >
						pu.UnreservedAmount))
					current = candidate;
			}

			/// <summary>
			/// Removes and recycles all items from the dictionary.
			/// </summary>
			public void Clear() {
				foreach (var pair in pickups) {
					var items = pair.Value;
					int n = items.Length;
					// Avoid leaking a ref to a pickupable
					for (int i = 0; i < n; i++)
						items[i].pickupable = null;
					itemPool.Enqueue(items);
				}
				pickups.Clear();
			}
			
			/// <summary>
			/// Adds all items to the final item list.
			/// </summary>
			/// <param name="finalPickups">The location where items will be stored.</param>
			public void CollectAll(ICollection<FMPickup> finalPickups) {
				foreach (var pair in pickups) {
					var priList = pair.Value;
					// Reverse order = higher priority first
					for (int i = priList.Length - 1; i >= 0; i--) {
						ref var item = ref priList[i];
						if (item.pickupable != null)
							finalPickups.Add(item);
					}
				}
			}
		}
	}
}
