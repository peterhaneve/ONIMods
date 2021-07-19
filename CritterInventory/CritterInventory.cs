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

using KSerialization;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;

using CritterInventoryPerType = System.Collections.Generic.Dictionary<Tag, PeterHan.
	CritterInventory.CritterTotals>;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Stores the inventory of each critter type. One is created per world.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class CritterInventory : KMonoBehaviour {
		// EX1-452242 made these fields private
		private static readonly IDetouredField<FactionAlignment, bool> FACTION_TARGETABLE =
			PDetours.DetourField<FactionAlignment, bool>("targetable");

		private static readonly IDetouredField<FactionAlignment, bool> FACTION_TARGETED =
			PDetours.DetourField<FactionAlignment, bool>("targeted");

		/// <summary>
		/// The total quantity of creatures.
		/// </summary>
		private readonly IDictionary<CritterType, CritterInventoryPerType> counts;

		/// <summary>
		/// Flags whether new critter types have been discovered.
		/// </summary>
		private bool discovered;

		/// <summary>
		/// The critter types that are currently pinned.
		/// </summary>
		[Serialize]
		private Dictionary<CritterType, HashSet<Tag>> pinned;

#pragma warning disable CS0649
#pragma warning disable IDE0044
		// This field is automatically populated by KMonoBehaviour
		[MyCmpReq]
		private WorldContainer worldContainer;
#pragma warning restore IDE0044
#pragma warning restore CS0649

		public CritterInventory() {
			counts = new Dictionary<CritterType, CritterInventoryPerType>(4);
			foreach (var type in Enum.GetValues(typeof(CritterType)))
				if (type is CritterType ct)
					counts.Add(ct, new CritterInventoryPerType(32));
		}

		/// <summary>
		/// Adds a critter in the current world to the inventory.
		/// </summary>
		/// <param name="creature">The creature to add.</param>
		private void AddCritter(CreatureBrain creature) {
			if (counts.TryGetValue(creature.GetCritterType(), out CritterInventoryPerType
					byType)) {
				var species = creature.PrefabID();
				var alignment = creature.GetComponent<FactionAlignment>();
				bool targeted = false, targetable = false;
				// Create critter totals if not present
				if (!byType.TryGetValue(species, out CritterTotals totals)) {
					byType.Add(species, totals = new CritterTotals());
					discovered = true;
				}
				totals.Total++;
				if (alignment != null) {
					targeted = FACTION_TARGETED.Get(alignment);
					targetable = FACTION_TARGETABLE.Get(alignment);
				}
				// Reserve wrangled, marked for attack, and trussed/bagged creatures
				if ((creature.GetComponent<Capturable>()?.IsMarkedForCapture ?? false) ||
						(targeted && targetable) || creature.HasTag(GameTags.Creatures.Bagged))
					totals.Reserved++;
			}
		}

		/// <summary>
		/// Gets the total quantity of critters of a specific type.
		/// </summary>
		/// <param name="type">The critter type, wild or tame.</param>
		/// <param name="species">The critter species to examine.</param>
		/// <returns>The total quantity of critters of that type and species.</returns>
		internal CritterTotals GetBySpecies(CritterType type, Tag species) {
			if (!counts.TryGetValue(type, out CritterInventoryPerType byType))
				throw new ArgumentOutOfRangeException(nameof(type));
			if (!byType.TryGetValue(species, out CritterTotals totals))
				totals = new CritterTotals();
			return totals;
		}

		/// <summary>
		/// Gets the species that are pinned for a given critter type.
		/// </summary>
		/// <param name="type">The critter type to look up.</param>
		/// <returns>The pinned species, or null if pins are not yet initialized.</returns>
		public ISet<Tag> GetPinnedSpecies(CritterType type) {
			if (pinned == null || !pinned.TryGetValue(type, out HashSet<Tag> result))
				result = null;
			return result;
		}

		/// <summary>
		/// Gets the world ID of this inventory. It is parented to the same component as
		/// WorldInventory, so the same method is used.
		/// </summary>
		/// <returns>The world ID to use for inventory.</returns>
		private int GetWorldID() {
			return (worldContainer != null) ? worldContainer.id : -1;
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			// Initialize, if not deserialized from the file
			if (pinned == null) {
				pinned = new Dictionary<CritterType, HashSet<Tag>>(4);
				foreach (var pair in counts)
					pinned.Add(pair.Key, new HashSet<Tag>());
			}
		}

		/// <summary>
		/// Gets the total quantity of each critter of a specific type.
		/// </summary>
		/// <param name="type">The critter type, wild or tame.</param>
		/// <param name="results">The location to populate the results per species.</param>
		/// <returns>The total quantity of critters of that type.</returns>
		internal CritterTotals PopulateTotals(CritterType type,
				IDictionary<Tag, CritterTotals> results) {
			if (!counts.TryGetValue(type, out CritterInventoryPerType byType))
				throw new ArgumentOutOfRangeException(nameof(type));
			var all = new CritterTotals();
			foreach (var pair in byType) {
				var totals = pair.Value;
				var species = pair.Key;
				if (results != null && !results.ContainsKey(species))
					results.Add(species, totals);
				all.Total += totals.Total;
				all.Reserved += totals.Reserved;
			}
			return all;
		}

		/// <summary>
		/// Updates the contents of the critter inventory.
		/// </summary>
		public void Update() {
			// Reset existing count to zero
			foreach (var typePair in counts)
				foreach (var speciesPair in typePair.Value) {
					var species = speciesPair.Value;
					species.Reserved = 0;
					species.Total = 0;
				}
			discovered = false;
			CritterInventoryUtils.GetCritters(GetWorldID(), AddCritter);
			var inst = AllResourcesScreen.Instance;
			if (discovered && inst != null)
				inst.Populate(null);
		}
	}
}
