/*
 * Copyright 2021 Peter Han
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

using PeterHan.PLib.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using FetchablesByPrefabId = FetchManager.FetchablesByPrefabId;
using Pickup = FetchManager.Pickup;

namespace PeterHan.EfficientFetch {
	/// <summary>
	/// Manages efficient fetching.
	/// </summary>
	internal sealed class EfficientFetchManager : IDisposable {
		/// <summary>
		/// The current instance of this class.
		/// </summary>
		public static EfficientFetchManager Instance { get; private set; }

		/// <summary>
		/// Creates the current instance.
		/// </summary>
		/// <param name="threshold">The threshold fraction for an efficient fetch.</param>
		public static void CreateInstance(float threshold) {
			DestroyInstance();
			Instance = new EfficientFetchManager(threshold);
		}

		/// <summary>
		/// Destroys the current instance.
		/// </summary>
		public static void DestroyInstance() {
			if (Instance != null)
				Instance.Dispose();
			Instance = null;
		}

		/// <summary>
		/// A reference to the shared chore type list.
		/// </summary>
		private readonly Database.ChoreTypes choreTypes;

		/// <summary>
		/// Outstanding requests for fetch information that were not efficiently satisfied
		/// go here.
		/// </summary>
		private readonly ConcurrentDictionary<Tag, FetchData> outstanding;

		/// <summary>
		/// The pickups field of the fetch manager.
		/// </summary>
		private readonly IList<Pickup> fmPickups;

		/// <summary>
		/// The threshold fraction which is considered an efficient fetch (0-1)
		/// </summary>
		private readonly float thresholdFraction;

		private EfficientFetchManager(float thresholdFraction) {
			if (thresholdFraction.IsNaNOrInfinity())
				throw new ArgumentException("thresholdFraction");
			choreTypes = Db.Get().ChoreTypes;
			outstanding = new ConcurrentDictionary<Tag, FetchData>(4, 512);
			// Reflect that field!
			IList<Pickup> fp = null;
			var fm = Game.Instance.fetchManager;
			try {
				var pickupsField = typeof(FetchManager).GetFieldSafe("pickups", false);
				if (pickupsField != null && fm != null)
					fp = pickupsField.GetValue(fm) as IList<Pickup>;
			} catch (FieldAccessException) {
			} catch (TargetException) { }
			if (fp == null)
				PUtil.LogWarning("Unable to find pickups field on FetchManager!");
			fmPickups = fp;
			this.thresholdFraction = thresholdFraction;
		}

		/// <summary>
		/// Condenses the pickups.
		/// </summary>
		/// <param name="pickups">The pickups to condense down.</param>
		private void CondensePickups(List<Pickup> pickups) {
			int n = pickups.Count;
			Pickup prevPickup = pickups[0];
			var tagBits = new TagBits(ref FetchManager.disallowedTagMask);
			prevPickup.pickupable.KPrefabID.AndTagBits(ref tagBits);
			int hash = prevPickup.tagBitsHash, last = n, next = 0;
			for (int i = 1; i < n; i++) {
				bool del = false;
				var pickup = pickups[i];
				var newTagBits = default(TagBits);
				if (prevPickup.masterPriority == pickup.masterPriority) {
					newTagBits = new TagBits(ref FetchManager.disallowedTagMask);
					pickup.pickupable.KPrefabID.AndTagBits(ref newTagBits);
					if (pickup.tagBitsHash == hash && newTagBits.AreEqual(ref tagBits))
						// Identical to the previous item
						del = true;
				}
				if (del)
					// Skip
					last--;
				else {
					// Keep and move down
					next++;
					prevPickup = pickup;
					tagBits = newTagBits;
					hash = pickup.tagBitsHash;
					if (i > next)
						pickups[next] = pickup;
				}
			}
			pickups.RemoveRange(last, n - last);
		}

		public void Dispose() {
			outstanding.Clear();
		}

		/// <summary>
		/// Uses the stock game logic to look for a usable item, but starts an efficient
		/// fetch search if it is too small.
		/// </summary>
		/// <param name="chore">The chore which is being completed.</param>
		/// <param name="state">The state of that chore.</param>
		/// <param name="result">The item to be picked up.</param>
		/// <returns>true to use the stock logic, or false to skip the stock method</returns>
		internal bool FindFetchTarget(FetchChore chore, ChoreConsumerState state,
				out Pickupable result) {
			bool cont = true;
			if (chore.destination != null && !state.hasSolidTransferArm && fmPickups != null) {
				// Only use if not a storage fetch (tidy)
				var id = chore.choreType?.Id ?? "";
				if (id != choreTypes.StorageFetch.Id && id != choreTypes.CreatureFetch.Id &&
						id != choreTypes.FoodFetch.Id) {
					result = FindFetchTarget(chore);
					cont = false;
				} else
					result = null;
			} else
				result = null;
			return cont;
		}
		
		/// <summary>
		/// Searches for fetchable pickups for the given chore.
		/// </summary>
		/// <param name="chore">The chore to complete.</param>
		/// <returns>The pickup to fetch, or null if none are currently available.</returns>
		internal Pickupable FindFetchTarget(FetchChore chore) {
			Pickupable bestMatch = null;
			var destination = chore.destination;
			float required = chore.originalAmount, target = required * thresholdFraction,
				canGet = 0.0f;
			foreach (var pickup in fmPickups) {
				var pickupable = pickup.pickupable;
				// Is this item accessible?
				if (FetchManager.IsFetchablePickup(pickupable, ref chore.tagBits, ref chore.
						requiredTagBits, ref chore.forbiddenTagBits, destination)) {
					float amount = pickupable.UnreservedAmount;
					if (bestMatch == null) {
						// Indicate if anything can be found at all
						bestMatch = pickupable;
						canGet = amount;
					}
					if (amount >= target) {
						// Already have the best one in our sights
						bestMatch = pickupable;
						canGet = amount;
						break;
					}
				}
			}
			// Do not start a fetch entry if nothing is available
			if (bestMatch != null) {
				Tag itemType = bestMatch.PrefabID();
				if (outstanding.TryGetValue(itemType, out FetchData current) && !current.
						NeedsScan) {
					// Retire it, with the best item we could do
					outstanding.TryRemove(itemType, out _);
#if DEBUG
					PUtil.LogDebug("{3} {0} ({2}) with {1:F2}".F(destination.name,
						canGet, itemType, (canGet >= target) ? "Complete" : "Retire"));
#endif
				} else if (canGet < target && outstanding.TryAdd(itemType, new FetchData(
						target))) {
					// Start searching for a better option
#if DEBUG
					PUtil.LogDebug("Find {0} ({3}): have {1:F2}, want {2:F2}".F(
						destination.name, canGet, target, itemType));
#endif
					bestMatch = null;
				}
			}
			return bestMatch;
		}

		/// <summary>
		/// Gets the list of fetchable pickups.
		/// </summary>
		/// <param name="fetch">The fetchables to update.</param>
		/// <param name="fetcher">The Duplicant gathering the items.</param>
		/// <param name="navigator">The navigator for that Duplicant.</param>
		/// <param name="cellCosts">A location to store the cell costs.</param>
		private void GetFetchList(FetchablesByPrefabId fetch, Navigator navigator,
				GameObject fetcher, IDictionary<int, int> cellCosts) {
			cellCosts.Clear();
			var pickups = fetch.finalPickups;
			foreach (var fetchable in fetch.fetchables.GetDataList()) {
				var pickupable = fetchable.pickupable;
				if (pickupable.CouldBePickedUpByMinion(fetcher)) {
					// Optimize if many pickupables are in the same cell
					int cell = pickupable.cachedCell;
					if (!cellCosts.TryGetValue(cell, out int cost)) {
						cost = pickupable.GetNavigationCost(navigator, cell);
						cellCosts.Add(cell, cost);
					}
					if (cost >= 0)
						// This pickup is reachable
						pickups.Add(new Pickup {
							pickupable = pickupable,
							tagBitsHash = fetchable.tagBitsHash,
							PathCost = (ushort)Math.Min(cost, ushort.MaxValue),
							masterPriority = fetchable.masterPriority,
							freshness = fetchable.freshness,
							foodQuality = fetchable.foodQuality
						});
				}
			}
		}

		/// <summary>
		/// Updates the available pickups.
		/// </summary>
		/// <param name="fetch">The fetchables to update.</param>
		/// <param name="fetcher">The Duplicant gathering the items.</param>
		/// <param name="navigator">The navigator for that Duplicant.</param>
		/// <param name="cellCosts">A location to store the cell costs.</param>
		internal void UpdatePickups(FetchablesByPrefabId fetch, Navigator navigator,
				GameObject fetcher, IDictionary<int, int> cellCosts) {
			var pickups = fetch.finalPickups;
			if (pickups != null) {
				if (!outstanding.TryGetValue(fetch.prefabId, out FetchData data))
					data = null;
				pickups.Clear();
				GetFetchList(fetch, navigator, fetcher, cellCosts);
				if (pickups.Count > 1) {
					if (data != null)
						pickups.Sort(data);
					else
						pickups.Sort(FetchData.Default);
					// Condense down using the stock game logic
					CondensePickups(pickups);
				}
				if (data != null)
					data.NeedsScan = false;
			}
		}

		/// <summary>
		/// Tracks the status of a particular fetch chore until it gets retired.
		/// </summary>
		internal sealed class FetchData : IComparer<Pickup> {
			/// <summary>
			/// Used if there is no request out for fetching, matches stock comparator exactly
			/// </summary>
			public static readonly FetchData Default = new FetchData(0.0f);

			/// <summary>
			/// true if this data must still be scanned, or false if it is ready to be used.
			/// </summary>
			public bool NeedsScan { get; set; }

			/// <summary>
			/// The threshold mass required to efficiently fetch.
			/// </summary>
			public float Threshold { get; }

			internal FetchData(float threshold) {
				Threshold = threshold;
				NeedsScan = true;
			}

			public int Compare(Pickup a, Pickup b) {
				int comp = a.tagBitsHash.CompareTo(b.tagBitsHash);
				if (comp != 0)
					return comp;
				comp = b.masterPriority.CompareTo(a.masterPriority);
				if (comp != 0)
					return comp;
				// Add comparison for threshold
				float aq = a.pickupable.UnreservedAmount, bq = b.pickupable.UnreservedAmount;
				if (aq >= Threshold && bq < Threshold)
					return -1;
				else if (aq < Threshold && bq >= Threshold)
					return 1;
				comp = a.PathCost.CompareTo(b.PathCost);
				if (comp != 0)
					return comp;
				comp = b.foodQuality.CompareTo(a.foodQuality);
				if (comp != 0)
					return comp;
				return b.freshness.CompareTo(a.freshness);
			}
		}
	}
}
