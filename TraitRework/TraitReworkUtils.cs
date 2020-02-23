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

using Harmony;
using Klei.AI;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.TraitRework {
	/// <summary>
	/// Utility functions for Trait Rework.
	/// </summary>
	internal static class TraitReworkUtils {
		/// <summary>
		/// Cached Traverse for the static AcousticDisturbance method.
		/// </summary>
		private static readonly Traverse ACOUSTIC_TRAVERSE = Traverse.Create(typeof(
			AcousticDisturbance));

		/// <summary>
		/// Bans foods if necessary from a Duplicant.
		/// </summary>
		/// <param name="consumer">The food consumer to update.</param>
		/// <returns>true if foods were banned, or false if no change was made.</returns>
		internal static bool ApplyBannedFoods(ConsumableConsumer consumer) {
			var obj = consumer.gameObject;
			Traits traits;
			bool changed = false;
			if (obj != null && (traits = obj.GetComponent<Traits>()) != null) {
				if (traits.HasTrait("ScaredyCat")) {
#if DEBUG
					PUtil.LogDebug("Removing Pacifist foods from " + obj.name);
#endif
					// Pacifist
					BanFoods(TraitTuning.MEAT_FOODS, consumer);
					changed = true;
				} else if (traits.HasTrait("CantCook")) {
					// Gastrophobia
#if DEBUG
					PUtil.LogDebug("Removing Gas Range foods from " + obj.name);
#endif
					BanFoods(TraitTuning.GASRANGE_FOODS, consumer);
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>
		/// Bans foods disallowed by traits from all Duplicants.
		/// </summary>
		internal static void ApplyAllBannedFoods(Tag _) {
			foreach (var dupe in Components.LiveMinionIdentities.Items) {
				var cc = dupe.gameObject?.GetComponent<ConsumableConsumer>();
				if (cc != null && ApplyBannedFoods(cc))
					cc.consumableRulesChanged.Signal();
			}
#if DEBUG
			PUtil.LogDebug("Applied food permissions rules on {0:D} duplicants".F(Components.
				LiveMinionIdentities.Count));
#endif
		}

		/// <summary>
		/// Bans food items from a duplicant.
		/// </summary>
		/// <param name="banned">The foods to ban.</param>
		/// <param name="instance">The consumer who cannot eat these items.</param>
		private static void BanFoods(IEnumerable<string> banned, ConsumableConsumer instance) {
			var set = HashSetPool<Tag, ConsumableConsumer>.Allocate();
			foreach (var tag in instance.forbiddenTags)
				set.Add(tag);
			foreach (var bannedTag in banned)
				set.Add(bannedTag);
			// Create new tag list
			var newTags = new Tag[set.Count];
			set.CopyTo(newTags, 0);
			set.Recycle();
			instance.forbiddenTags = newTags;
		}

		/// <summary>
		/// Disturbs all Duplicants in range.
		/// </summary>
		/// <param name="source">The source of the disturbance.</param>
		/// <param name="radius">The disturbance radius.</param>
		internal static void DisturbInRange(GameObject source, float radius) {
			var effects = Db.Get().effects;
			Vector2 loc = source.transform.GetPosition();
			// Radius is 3 in the base game
			float radSq = radius * radius;
			var cells = HashSetPool<int, TraitTemplate>.Allocate();
			// Determine who gets disturbed (ouch private method)
			// Disable cast warning, cast is to ensure correct method selection
#pragma warning disable IDE0004
			ACOUSTIC_TRAVERSE.CallMethod("DetermineCellsInRadius", Grid.PosToCell(source), 0,
				Mathf.CeilToInt(radius), (HashSet<int>)cells);
#pragma warning restore IDE0004
			foreach (var dupe in Components.LiveMinionIdentities.Items) {
				var newObj = dupe.gameObject;
				if (newObj != null && newObj != source) {
					// Is this dupe in range?
					Vector2 newLoc = dupe.transform.GetPosition();
					if (Vector2.SqrMagnitude(loc - newLoc) <= radSq) {
						int cell = Grid.PosToCell(newLoc);
						var sleepMonitor = dupe.GetSMI<StaminaMonitor.Instance>();
						if (cells.Contains(cell) && (sleepMonitor == null || !sleepMonitor.
								IsSleeping())) {
#if DEBUG
							PUtil.LogDebug("Disturbing " + newObj.name);
#endif
							// Not happy at hearing snoring
							newObj.GetSMI<ThoughtGraph.Instance>()?.AddThought(Db.Get().
								Thoughts.Unhappy);
							// Inflict disturbed effect
							newObj.GetComponent<Effects>()?.Add(effects.Get(TraitTuning.
								DISTURBED_EFFECT), true);
						}
					}
				}
			}
			cells.Recycle();
		}

		/// <summary>
		/// Alters the specified traits.
		/// </summary>
		/// <param name="modifiers">The modifiers containing the source traits.</param>
		internal static void FixTraits(EntityModifierSet modifiers) {
			var source = modifiers.traits;
			var newTraits = TraitTuning.Instance.Traits;
			if (source != null) {
				var unusedTraits = HashSetPool<string, TraitTemplate>.Allocate();
				// Track which traits did not get used
				foreach (var trait in newTraits)
					unusedTraits.Add(trait.Key);
				foreach (var trait in source.resources)
					// Do we have a replacement?
					if (newTraits.TryGetValue(trait.Id, out TraitTemplate newTrait)) {
						PUtil.LogDebug("Patched trait: " + trait.Id);
						var disabled = newTrait.DisabledChores;
						trait.PositiveTrait = newTrait.IsPositive;
						if (disabled != null) {
							// Add disabled chores
							int n = disabled.Count;
							var groups = new ChoreGroup[n];
							for (int i = 0; i < n; i++)
								groups[i] = modifiers.ChoreGroups.Get(disabled[i]);
							trait.disabledChoreGroups = groups;
						} else
							// Remove all disabled chores
							trait.disabledChoreGroups = null;
						trait.ValidStarterTrait = newTrait.ValidStartingTrait;
						// Leave OnAddTrait, replace the modifiers
						var mods = trait.SelfModifiers;
						mods.Clear();
						mods.AddRange(newTrait.Modifiers);
						unusedTraits.Remove(newTrait.ID);
						// Replace ignored effects if present
						var effects = newTrait.IgnoredEffects;
						if (effects != null) {
							var arr = new string[effects.Count];
							effects.CopyTo(arr, 0);
							trait.ignoredEffects = arr;
						}
						// Add extended trait tooltip
						var extendedFn = newTrait.ExtendedTooltip;
						if (extendedFn != null)
							trait.ExtendedTooltip = (Func<string>)Delegate.Combine(trait.
								ExtendedTooltip, extendedFn);
					}
				unusedTraits.Recycle();
			}
		}

		/// <summary>
		/// Removes the "Sore Back" debuff if the Duplicant is Narcoleptic.
		/// </summary>
		/// <param name="monitor">The sleep chore monitor of that Duplicant.</param>
		/// <param name="locator">The locator for the pending sleep chore.</param>
		internal static void RemoveSoreBack(SleepChoreMonitor.Instance monitor,
				GameObject locator) {
			var sleepable = locator.GetComponentSafe<Sleepable>();
			var traits = monitor.gameObject.GetComponentSafe<Traits>();
			if (traits != null && sleepable != null && traits.HasTrait("Narcolepsy"))
				sleepable.wakeEffects = null;
		}

		/// <summary>
		/// Updates the "Eating in Lit Area" modifier when a Duplicant eats.
		/// </summary>
		/// <param name="worker">The Duplicant that is eating.</param>
		internal static void UpdateLitEatingModifier(Worker worker) {
			var modifier = TraitReworkPatches.EAT_LIT_MODIFIER;
			var litWorkspace = Db.Get().DuplicantStatusItems.LightWorkEfficiencyBonus;
			var attrs = worker.GetAttributes();
			var selectable = worker.GetComponent<KSelectable>();
			if (modifier != null && attrs != null && selectable != null) {
				var calAttribute = attrs.Get(modifier.AttributeId);
				if (calAttribute != null) {
					// If lit workspace, add the eating in lit area modifier to boost
					// calorie gain rate by 15%
					bool hasModifier = false;
					var modifiers = calAttribute.Modifiers;
					for (int i = 0; i < modifiers.Count && !hasModifier; i++)
						// Only one instance, == is fine
						if (modifiers[i] == modifier)
							hasModifier = true;
					// Set as needed
					if (selectable.HasStatusItem(litWorkspace)) {
						if (!hasModifier)
							attrs.Add(modifier);
					} else if (hasModifier)
						attrs.Remove(modifier);
				}
			}
		}
	}
}
