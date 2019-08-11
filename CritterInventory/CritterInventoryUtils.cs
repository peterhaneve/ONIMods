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

using Harmony;
using PeterHan.PLib;
using System;

namespace PeterHan.CritterInventory {
	/// <summary>
	/// Utility functions used for critter inventory creation.
	/// </summary>
	static class CritterInventoryUtils {
		/// <summary>
		/// Retrieves the UI description of a critter type.
		/// </summary>
		/// <param name="type">The critter type.</param>
		/// <returns>The (translated?) name as it should appear in the UI.</returns>
		public static string GetDescription(this CritterType type) {
			string desc;
			switch (type) {
			case CritterType.Tame:
				desc = STRINGS.CREATURES.MODIFIERS.TAME.NAME;
				break;
			case CritterType.Wild:
				desc = STRINGS.CREATURES.MODIFIERS.WILD.NAME;
				break;
			default:
				throw new InvalidOperationException("Unsupported critter type " + type);
			}
			return desc;
		}
		
		/// <summary>
		/// Iterates all active creatures and invokes the delegate for each.
		/// </summary>
		/// <param name="action">The method to call for each creature.</param>
		public static void IterateCreatures(Action<CreatureBrain> action) {
			var enumerator = Components.Brains.GetEnumerator();
			try {
				while (enumerator.MoveNext()) {
					var creatureBrain = enumerator.Current as CreatureBrain;
					// Must be spawned, do not check "Unreachable" because most flying critters
					// are indeed unreachable
					if ((creatureBrain?.isSpawned ?? false) && !creatureBrain.HasTag(
							GameTags.Dead))
						action.Invoke(creatureBrain);
				}
			} finally {
				(enumerator as IDisposable)?.Dispose();
			}
		}

		/// <summary>
		/// Checks to see if a creature matches the critter type specified.
		/// </summary>
		/// <param name="type">The critter type.</param>
		/// <param name="creature">The creature to compare against.</param>
		/// <returns>true if the creature matches the type, or false otherwise.</returns>
		public static bool Matches(this CritterType type, CreatureBrain creature) {
			bool match;
			switch (type) {
			case CritterType.Tame:
				match = !creature.HasTag(GameTags.Creatures.Wild);
				break;
			case CritterType.Wild:
				match = creature.HasTag(GameTags.Creatures.Wild);
				break;
			default:
				throw new InvalidOperationException("Unsupported critter type " + type);
			}
			return match;
		}

		/// <summary>
		/// Totals up the number of creatures, creating resource entries if needed.
		/// </summary>
		/// <param name="wild">true to count wild creatures, or false to count tamed ones.</param>
		public static void TotalCreatures(CritterResourceHeader header) {
			var totals = DictionaryPool<Tag, CritterTotals, ResourceCategoryHeader>.Allocate();
			CritterType type = header.Type;
			IterateCreatures((creature) => {
				var species = creature.PrefabID();
				var go = creature.gameObject;
				if (type.Matches(creature)) {
					var alignment = go.GetComponent<FactionAlignment>();
					// Create critter totals if not present
					if (!totals.TryGetValue(species, out CritterTotals total)) {
						total = new CritterTotals();
						totals.Add(species, total);
					}
					total.Total++;
					// Reserve wrangled, marked for attack, and trussed/bagged creatures
					if ((go.GetComponent<Capturable>()?.IsMarkedForCapture ?? false) ||
						((alignment?.targeted ?? false) && alignment.targetable) ||
						creature.HasTag(GameTags.Creatures.Bagged))
						total.Reserved++;
				}
			});
			header.Update(totals);
			totals.Recycle();
		}
	}
}
