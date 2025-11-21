/*
 * Copyright 2025 Peter Han
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

using Klei.AI;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TemplateClasses;
using UnityEngine;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// A version of the base game's FertilityMonitor that produces live offspring directly
	/// instead of eggs.
	/// 
	/// For simplicity (and compatibility) sake, Moos and Husky Moos cannot crossbreed.
	/// </summary>
	public class LiveFertilityMonitor : GameStateMachine<LiveFertilityMonitor,
			LiveFertilityMonitor.Instance, IStateMachineTarget, LiveFertilityMonitor.Def> {
		/// <summary>
		/// A tag applied if a live birth creature has another copy penned in the same room.
		/// </summary>
		internal static readonly Tag HasNearbyCreature = new Tag("HasNearbyCreature");

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
		/// <summary>
		/// The creature can give birth.
		/// </summary>
		private State fertile;

		/// <summary>
		/// The creature cannot give birth.
		/// </summary>
		private State infertile;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		public override void InitializeStates(out BaseState default_state) {
			default_state = fertile;
			// Needs to be changed for vanilla
			serializable = SerializeType.ParamsOnly;
			root.DefaultState(fertile).
				Update("UpdateNearbyCreatures", (smi, dt) => smi.UpdateNearbyCreatures(), UpdateRate.SIM_1000ms);
			fertile.ToggleBehaviour(GameTags.Creatures.Fertile, (Instance smi) => smi.IsReadyToGiveBirth(), null).
				ToggleEffect((Instance smi) => smi.fertileEffect).
				Transition(infertile, Not(new Transition.ConditionCallback(IsFertile)), UpdateRate.SIM_1000ms);
			infertile.Transition(fertile, new Transition.ConditionCallback(IsFertile), UpdateRate.SIM_1000ms);
		}

		/// <summary>
		/// Checks to see if the creature can give birth. Since no egg is laid, check
		/// overcrowding as well as cramped.
		/// </summary>
		/// <param name="smi">The current state machine instance.</param>
		/// <returns>true if live birth is possible, or aflse otherwise</returns>
		public static bool IsFertile(Instance smi) {
			var kpid = smi.GetComponent<KPrefabID>();
			return !kpid.HasTag(GameTags.Creatures.Confined) && !kpid.HasTag(GameTags.
				Creatures.Overcrowded) && kpid.HasTag(GameTags.Creatures.Expecting) &&
				kpid.HasTag(HasNearbyCreature);
		}

		public class Def : BaseDef {
			/// <summary>
			/// The tag to spawn when a baby is born.
			/// </summary>
			public Tag babyPrefab;

			/// <summary>
			/// The number of cycles per live birth at full reproduction.
			/// </summary>
			public float baseFertileCycles;

			/// <summary>
			/// The weighted probability of each baby that could spawn.
			/// </summary>
			public List<FertilityMonitor.BreedingChance> initialBreedingWeights;

			public override void Configure(GameObject prefab) {
				prefab.AddOrGet<Modifiers>().initialAmounts.Add(Db.Get().Amounts.Fertility.Id);
			}
		}

		public new class Instance : GameInstance {
			/// <summary>
			/// The current weighted probability of each baby that could spawn.
			/// </summary>
			[KSerialization.Serialize]
			public List<FertilityMonitor.BreedingChance> breedingChances;

			/// <summary>
			/// The current reproduction progress.
			/// </summary>
			public readonly AmountInstance fertility;

			/// <summary>
			/// The effect to apply when able to reproduce.
			/// </summary>
			public readonly Effect fertileEffect;

			public Instance(IStateMachineTarget master, Def def) : base(master, def) {
				fertility = Db.Get().Amounts.Fertility.Lookup(gameObject);
				if (Klei.GenericGameSettings.instance.acceleratedLifecycle)
					// Debug setting
					fertility.deltaAttribute.Add(PDatabaseUtils.CreateAttributeModifier(
						fertility.deltaAttribute.Id, 33.3333333f, "Accelerated Lifecycle"));
				float baseFertileRate = 1.0f / (def.baseFertileCycles * 6.0f);
				fertileEffect = new Effect("Fertile", STRINGS.CREATURES.MODIFIERS.
					BASE_FERTILITY.NAME, STRINGS.CREATURES.MODIFIERS.BASE_FERTILITY.TOOLTIP,
					0f, false, false, false);
				fertileEffect.Add(PDatabaseUtils.CreateAttributeModifier(Db.Get().Amounts.
					Fertility.deltaAttribute.Id, baseFertileRate, STRINGS.CREATURES.
					MODIFIERS.BASE_FERTILITY.NAME));
				// Necessary to make OvercrowdingMonitor think that there is a difference
				if (master.gameObject.TryGetComponent(out KPrefabID id))
					id.SetTag(GameTags.Creatures.Expecting, true);
				InitializeBreedingChances();
			}

			/// <summary>
			/// Modifies the chance of giving birth to a specific baby type.
			/// </summary>
			/// <param name="type">The tag of the baby type to modify.</param>
			/// <param name="addedPercentChance">The chance to add.</param>
			public void AddBreedingChance(Tag type, float addedPercentChance) {
				foreach (var chance in breedingChances)
					if (chance.egg == type) {
						float added = Mathf.Min(1.0f - chance.weight, Mathf.Max(0.0f -
							chance.weight, addedPercentChance));
						chance.weight += added;
					}
				NormalizeBreedingChances();
				master.Trigger((int)GameHashes.BreedingChancesChanged, breedingChances);
			}

			/// <summary>
			/// Gets the chance of a baby morph spawning.
			/// </summary>
			/// <param name="type">The tag of the baby type to query.</param>
			/// <returns>The percentage chance (normalized, 0-1) of that baby currently being chosen.</returns>
			public float GetBreedingChance(Tag type) {
				foreach (var chance in breedingChances) {
					bool flag = chance.egg == type;
					if (flag)
						return chance.weight;
				}
				return -1.0f;
			}

			/// <summary>
			/// Gives birth to the young creature.
			/// </summary>
			public void GiveBirth() {
				var pos = smi.transform.GetPosition();
				float babyType = Random.value;
				var babyTag = Tag.Invalid;
				int n = breedingChances.Count;
				pos.z = Grid.GetLayerZ(Grid.SceneLayer.Ore);
				fertility.value = 0.0f;
				// Choose the morph to spawn
				if (Klei.GenericGameSettings.instance.acceleratedLifecycle) {
					// Always choose the most likely morph
					float maxChance = 0.0f;
					for (int i = 0; i < n; i++) {
						var chance = breedingChances[i];
						if (chance.weight > maxChance) {
							maxChance = chance.weight;
							babyTag = chance.egg;
						}
					}
				} else
					for (int i = 0; i < n; i++) {
						var chance = breedingChances[i];
						babyType -= chance.weight;
						if (babyType <= 0.0f) {
							babyTag = chance.egg;
							break;
						}
					}
				if (babyTag == Tag.Invalid)
					PUtil.LogWarning("No baby type chosen. Were weights normalized?");
				else {
					var babyPrefab = Assets.GetPrefab(babyTag);
					var baby = Util.KInstantiate(babyPrefab, pos);
					Trigger((int)GameHashes.LayEgg, baby);
					baby.SetActive(true);
					Db.Get().Amounts.Wildness.Copy(baby, gameObject);
				}
			}

			/// <summary>
			/// Initializes the chances of each morph to hatch. Does NOT call the modifier
			/// apply functions as those use the base FertilityMonitor and are not really
			/// applicable to live births anyways.
			/// </summary>
			private void InitializeBreedingChances() {
				var weights = def.initialBreedingWeights;
				breedingChances = new List<FertilityMonitor.BreedingChance>();
				if (weights != null) {
					foreach (var chance in weights)
						breedingChances.Add(new FertilityMonitor.BreedingChance {
							egg = chance.egg,
							weight = chance.weight
						});
					NormalizeBreedingChances();
				}
			}

			public bool IsReadyToGiveBirth() {
				return smi.fertility.value >= smi.fertility.GetMax();
			}

			/// <summary>
			/// Normalizes the breeding chances to add up to one.
			/// </summary>
			private void NormalizeBreedingChances() {
				float sum = 0f;
				foreach (var chance in breedingChances)
					sum += chance.weight;
				if (sum > 0.0f)
					foreach (var chance in breedingChances)
						chance.weight /= sum;
			}

			[OnDeserialized]
			private void OnDeserialized() {
				if (breedingChances == null || breedingChances.Count == 0)
					InitializeBreedingChances();
			}

			/// <summary>
			/// Updates the nearby creatures every second to see if breeding is possible.
			/// </summary>
			internal void UpdateNearbyCreatures() {
				var go = gameObject;
				if (go != null && go.TryGetComponent(out KPrefabID pid)) {
					var room = Game.Instance.roomProber.GetCavityForCell(Grid.PosToCell(go));
					if (room != null) {
						bool found = false;
						var tag = pid.PrefabTag;
						var creatures = room.creatures;
						int n = creatures.Count;
						// Search for a different critter in the same room
						for (int i = 0; i < n; i++) {
							var creature = creatures[i];
							if (creature != null && creature.PrefabTag == tag && go !=
									creature.gameObject) {
								found = true;
								break;
							}
						}
						// gameObject is not null
						if (found && !pid.HasTag(HasNearbyCreature))
							pid.AddTag(HasNearbyCreature);
						else if (!found && pid.HasTag(HasNearbyCreature))
							pid.RemoveTag(HasNearbyCreature);
					}
				}
			}
		}
	}
}
