/*
 * Copyright 2026 Peter Han
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
using System.Collections.Generic;
using UnityEngine;

using CircuitInfo = CircuitManager.CircuitInfo;
using ConnectionStatus = CircuitManager.ConnectionStatus;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to CircuitManager to speed up how electrical grids are calculated.
	/// </summary>
	public static class FastElectricalNetworkCalculator {
		/// <summary>
		/// Whether colony reports are to be generated.
		/// </summary>
		private static bool report = true;

		/// <summary>
		/// Applies all electrical network patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			report = !FastTrackOptions.Instance.NoReports;
			harmony.Patch(typeof(CircuitManager), nameof(CircuitManager.Refresh), prefix:
				new HarmonyMethod(typeof(FastElectricalNetworkCalculator),
				nameof(Refresh_Prefix)));
			harmony.Patch(typeof(CircuitManager), nameof(CircuitManager.Sim200msLast), prefix:
				new HarmonyMethod(typeof(FastElectricalNetworkCalculator),
				nameof(Sim200msLast_Prefix)));
		}

		/// <summary>
		/// Charges as many batteries as possible with the energy from this source.
		/// </summary>
		/// <param name="source">The energy source for charging.</param>
		/// <param name="sinks">The batteries to charge.</param>
		/// <param name="first">The first valid battery index.</param>
		/// <returns>The energy remaining in the generator after charging.</returns>
		private static float ChargeBatteriesFrom(Generator source, IList<Battery> sinks,
				ref int first) {
			int firstBattery = first, n = sinks.Count;
			float energyLeft = source.JoulesAvailable, energyAdded;
			var sourceGO = source.gameObject;
			do {
				float energyPerBattery = energyLeft / (n - firstBattery);
				energyAdded = 0.0f;
				for (int i = firstBattery; i < n; i++) {
					var battery = sinks[i];
					float space;
					// No self-charging on looped transformers
					if (battery != null && (space = battery.Capacity - battery.
							JoulesAvailable) > 0.0f) {
						if (battery.gameObject != sourceGO) {
							float energy = Mathf.Min(energyPerBattery, space);
							battery.AddEnergy(energy);
							energyLeft -= energy;
							energyAdded += energy;
						}
					} else
						firstBattery = i + 1;
				}
				if (energyAdded > 0.0f)
					source.ApplyDeltaJoules(-energyAdded);
			} while (energyLeft > 0.0f && energyAdded > 0.0f);
			first = firstBattery;
			return energyLeft;
		}

		/// <summary>
		/// Charges all batteries with the remaining energy available from sources.
		/// </summary>
		/// <param name="sinks">The batteries to charge.</param>
		/// <param name="sources">The energy sources available for charging.</param>
		/// <param name="firstSource">The first valid source index.</param>
		/// <param name="firstBattery">The first valid battery index.</param>
		private static void ChargeBatteries(IList<Battery> sinks, IList<Generator> sources,
				int firstSource, ref int firstBattery) {
			int n = sources.Count;
			for (int i = firstSource; i < n; i++) {
				var source = sources[i];
				// If energy remains in the source after all batteries charge, end loop, as all
				// batteries must be charged
				if (source != null && source.JoulesAvailable > 0.0f && ChargeBatteriesFrom(
						source, sinks, ref firstBattery) > 0.0f)
					break;
			}
		}

		/// <summary>
		/// Transfers energy from the circuit to transformer inputs. The sources are drained
		/// evenly.
		/// 
		/// The sources must be in lowest energy first order.
		/// </summary>
		/// <param name="sinks">The transformer inputs to charge.</param>
		/// <param name="sources">The sources to drain.</param>
		/// <returns>The total energy transferred.</returns>
		private static float ChargeDrainEvenly(IList<Battery> sinks, IList<Battery> sources) {
			int ni = sinks.Count, ns = sources.Count, first = 0;
			float usage = 0.0f;
			for (int i = 0; i < ni; i++) {
				var sink = sinks[i];
				if (sink != null) {
					float energyNeeded = Mathf.Min(sink.Capacity - sink.JoulesAvailable, sink.
						ChargeCapacity);
					if (energyNeeded > 0.0f && first < ns) {
						float energyAdded = energyNeeded - DrainEvenly(energyNeeded, sources,
							ref first);
						sink.AddEnergy(energyAdded);
						usage += energyAdded;
					}
				}
			}
			return usage;
		}

		/// <summary>
		/// Transfers energy from the circuit to transformer inputs. The sources are drained
		/// in first available order.
		/// 
		/// The sources must be in lowest energy first order.
		/// </summary>
		/// <param name="sinks">The transformer inputs to charge.</param>
		/// <param name="sources">The sources to drain.</param>
		/// <param name="first">The first valid source index to use.</param>
		/// <returns>The total energy transferred.</returns>
		private static float ChargeDrainFirst(IList<Battery> sinks, IList<Generator> sources,
				ref int first) {
			int ni = sinks.Count, ns = sources.Count, firstSource = first;
			float usage = 0.0f;
			for (int i = 0; i < ni; i++) {
				var sink = sinks[i];
				if (sink != null) {
					float energyNeeded = Mathf.Min(sink.Capacity - sink.JoulesAvailable, sink.
						ChargeCapacity);
					if (energyNeeded > 0.0f && firstSource < ns) {
						float energyAdded = energyNeeded - DrainFirstAvailable(energyNeeded,
							sources, ref firstSource);
						sink.AddEnergy(energyAdded);
						usage += energyAdded;
					}
				}
			}
			first = firstSource;
			return usage;
		}

		/// <summary>
		/// Transfers energy out of sources, draining evenly where possible.
		/// 
		/// The sources must be in lowest energy first order.
		/// </summary>
		/// <param name="energyNeeded">The energy required.</param>
		/// <param name="sources">The sources to use.</param>
		/// <param name="first">The first valid source index to use.</param>
		/// <returns>The number of joules remaining to supply.</returns>
		private static float DrainEvenly(float energyNeeded, IList<Battery> sources,
				ref int first) {
			int firstBattery = first, n = sources.Count, numLeft = n - firstBattery;
			do {
				var battery = sources[firstBattery];
				// Since the sources are sorted and drained evenly, all must have at least
				// the charge of the first sources
				float energyAcrossAll = Mathf.Min(numLeft * battery.JoulesAvailable,
					energyNeeded);
				if (energyAcrossAll > 0.0f) {
					float energyUsedPer = energyAcrossAll / numLeft;
					energyNeeded -= energyAcrossAll;
					for (int i = firstBattery; i < n; i++)
						sources[i].ConsumeEnergy(energyUsedPer);
				} else
					firstBattery++;
				numLeft = n - firstBattery;
			} while (energyNeeded > 0.0f && numLeft > 0);
			first = firstBattery;
			return energyNeeded;
		}

		/// <summary>
		/// Transfers energy out of sources, draining the first available.
		/// 
		/// The sources must be in lowest energy first order.
		/// </summary>
		/// <param name="energyNeeded">The energy required.</param>
		/// <param name="sources">The sources to use.</param>
		/// <param name="first">The first valid source index to use.</param>
		/// <returns>The number of joules remaining to supply.</returns>
		private static float DrainFirstAvailable(float energyNeeded, IList<Generator> sources,
				ref int first) {
			int firstGenerator = first, n = sources.Count;
			for (int i = firstGenerator; i < n && energyNeeded > 0.0f; i++) {
				var generator = sources[i];
				if (generator != null) {
					float energy = Mathf.Min(generator.JoulesAvailable, energyNeeded);
					if (energy > 0.0f) {
						energyNeeded -= energy;
						generator.ApplyDeltaJoules(-energy);
					} else
						// If generator is out of power, move to next generator
						firstGenerator = i + 1;
				}
			}
			first = firstGenerator;
			return energyNeeded;
		}

		/// <summary>
		/// Populates the list of generators that are generating power.
		/// </summary>
		/// <param name="circuit">The circuit to update.</param>
		/// <param name="activeGenerators">The location where the active generators will be stored.</param>
		/// <returns>true if any generator is active, or a power transformer is able to provide
		/// power; or false otherwise.</returns>
		private static bool GetActiveGenerators(ref CircuitInfo circuit,
				List<Generator> activeGenerators) {
			var generators = circuit.generators;
			var outputTransformers = circuit.outputTransformers;
			int n = generators.Count;
			activeGenerators.Clear();
			// Take from generators first
			for (int i = 0; i < n; i++) {
				var generator = generators[i];
				if (generator != null && generator.JoulesAvailable > 0.0f)
					activeGenerators.Add(generator);
			}
			bool hasEnergy = activeGenerators.Count > 0;
			if (hasEnergy)
				activeGenerators.Sort(GeneratorChargeComparer.INSTANCE);
			else {
				// Then from transformers that output onto this grid
				n = outputTransformers.Count;
				for (int i = 0; i < n && !hasEnergy; i++) {
					var transformer = outputTransformers[i];
					if (transformer != null && transformer.JoulesAvailable > 0.0f)
						hasEnergy = true;
				}
			}
			return hasEnergy;
		}

		/// <summary>
		/// Calculates the minimum battery charge across the circuit, used for manual
		/// generators to schedule the chore.
		/// </summary>
		/// <param name="circuit">The circuit to update.</param>
		/// <returns>true if any battery has charge, or false otherwise.</returns>
		private static bool GetMinimumBatteryCharge(ref CircuitInfo circuit) {
			float batteryLevel = 1.0f, charge;
			var batteries = circuit.batteries;
			var inputTransformers = circuit.inputTransformers;
			int n = batteries.Count;
			bool hasCharge = false;
			for (int i = 0; i < n; i++) {
				var battery = batteries[i];
				charge = battery.PercentFull;
				if (battery.JoulesAvailable > 0.0f)
					hasCharge = true;
				if (batteryLevel > charge)
					batteryLevel = charge;
			}
			n = inputTransformers.Count;
			for (int i = 0; i < n; i++) {
				charge = inputTransformers[i].PercentFull;
				if (batteryLevel > charge)
					batteryLevel = charge;
			}
			circuit.minBatteryPercentFull = batteryLevel;
			return hasCharge;
		}

		/// <summary>
		/// Adds electrical circuits if needed to match the number of wire networks.
		/// </summary>
		/// <param name="instance">The circuit manager to initialize.</param>
		/// <param name="electricalSystem">The current electrical network.</param>
		private static void InitNetworks(CircuitManager instance,
				UtilityNetworkManager<ElectricalUtilityNetwork, Wire> electricalSystem) {
			var networks = electricalSystem.GetNetworks();
			var circuits = instance.circuitInfo;
			int nGroups = (int)Wire.WattageRating.NumRatings, nNetworks = networks.Count;
			while (circuits.Count < nNetworks) {
				var newInfo = new CircuitInfo {
					generators = new List<Generator>(16),
					consumers = new List<IEnergyConsumer>(32),
					batteries = new List<Battery>(16),
					inputTransformers = new List<Battery>(8),
					outputTransformers = new List<Generator>(16)
				};
				var wireLinks = new List<WireUtilityNetworkLink>[nGroups];
				for (int i = 0; i < nGroups; i++)
					wireLinks[i] = new List<WireUtilityNetworkLink>(8);
				newInfo.bridgeGroups = wireLinks;
				circuits.Add(newInfo);
			}
		}

		/// <summary>
		/// Rebuilds the electrical networks.
		/// </summary>
		/// <param name="instance">The networks to rebuild.</param>
		private static void Rebuild(CircuitManager instance) {
			var circuits = instance.circuitInfo;
			var minBatteryFull = ListPool<float, CircuitManager>.Allocate();
			int n = circuits.Count, circuitID;
			for (int i = 0; i < n; i++) {
				var circuit = circuits[i];
				var bridges = circuit.bridgeGroups;
				int nGroups = bridges.Length;
				circuit.generators.Clear();
				circuit.consumers.Clear();
				circuit.batteries.Clear();
				circuit.inputTransformers.Clear();
				circuit.outputTransformers.Clear();
				minBatteryFull.Add(1.0f);
				for (int j = 0; j < nGroups; j++)
					bridges[j].Clear();
			}
			foreach (var consumer in instance.consumers)
				if (consumer != null && (circuitID = instance.GetCircuitID(consumer)) !=
						ushort.MaxValue) {
					var circuit = circuits[circuitID];
					if (consumer is Battery battery) {
						if (battery.powerTransformer != null)
							circuit.inputTransformers.Add(battery);
						else {
							circuit.batteries.Add(battery);
							minBatteryFull[circuitID] = Mathf.Min(minBatteryFull[circuitID],
								battery.PercentFull);
						}
					} else
						circuit.consumers.Add(consumer);
				}
			for (int i = 0; i < n; i++) {
				var circuit = circuits[i];
				circuit.consumers.Sort(ConsumerWattageComparer.INSTANCE);
				circuit.minBatteryPercentFull = minBatteryFull[i];
				circuits[i] = circuit;
			}
			minBatteryFull.Recycle();
			foreach (var generator in instance.generators)
				if (generator != null && (circuitID = instance.GetCircuitID(generator)) !=
						ushort.MaxValue) {
					var circuit = circuits[circuitID];
					if (generator is PowerTransformer)
						circuit.outputTransformers.Add(generator);
					else
						circuit.generators.Add(generator);
				}
			foreach (var bridge in instance.bridges)
				if (bridge != null && (circuitID = instance.GetCircuitID(bridge)) != ushort.
						MaxValue)
					circuits[circuitID].bridgeGroups[(int)bridge.GetMaxWattageRating()].Add(
						bridge);
			instance.dirty = false;
		}

		/// <summary>
		/// Applied before Refresh runs.
		/// </summary>
		internal static bool Refresh_Prefix(CircuitManager __instance) {
			var electricalSystem = Game.Instance.electricalConduitSystem;
			if (electricalSystem != null) {
				bool rebuild = electricalSystem.IsDirty;
				if (rebuild)
					electricalSystem.Update();
				if (rebuild || __instance.dirty) {
					InitNetworks(__instance, electricalSystem);
					Rebuild(__instance);
				}
			}
			return false;
		}

		/// <summary>
		/// Sets the power status of all consumers on the given circuit.
		/// </summary>
		/// <param name="consumers">The consumers to notify.</param>
		/// <param name="status">The status to which to set all consumers.</param>
		private static void SetAllConsumerStatus(IList<IEnergyConsumer> consumers,
				ConnectionStatus status) {
			int n = consumers.Count;
			for (int i = 0; i < n; i++)
				consumers[i].SetConnectionStatus(status);
		}

		/// <summary>
		/// Applied before Sim200msLast runs.
		/// </summary>
		private static bool Sim200msLast_Prefix(CircuitManager __instance, float dt) {
			var infoScreen = UIPatches.AdditionalDetailsPanelWrapper.Instance;
			float elapsedTime = __instance.elapsedTime + dt;
			if (elapsedTime >= UpdateManager.SecondsPerSimTick) {
				elapsedTime -= UpdateManager.SecondsPerSimTick;
				Update(__instance);
				if (infoScreen != null)
					infoScreen.enetDirty = true;
			}
			__instance.elapsedTime = elapsedTime;
			return false;
		}

		/// <summary>
		/// Supplies as many consumers on the circuit as there is energy to do so.
		/// </summary>
		/// <param name="circuit">The circuit to update.</param>
		/// <param name="activeGenerators">The list of generators which are actively producing power.</param>
		/// <returns>The total wattage used for overloading purposes.</returns>
		private static float SupplyConsumers(ref CircuitInfo circuit,
				IList<Generator> activeGenerators) {
			var consumers = circuit.consumers;
			var batteries = circuit.batteries;
			var batteryStatus = new ConsumerRun(batteries);
			var outputTransformers = circuit.outputTransformers;
			int nc = consumers.Count, firstGenerator = 0, firstTransformer = 0;
			float usage = 0.0f;
			for (int i = 0; i < nc; i++) {
				var consumer = consumers[i];
				float energy = consumer.WattsUsed * UpdateManager.SecondsPerSimTick;
				if (energy > 0.0f) {
					float e0 = energy;
					energy = DrainFirstAvailable(energy, activeGenerators, ref firstGenerator);
					if (energy > 0.0f)
						energy = DrainFirstAvailable(energy, outputTransformers,
							ref firstTransformer);
					if (energy > 0.0f)
						energy = batteryStatus.Power(energy);
					if (report && energy < e0)
						ReportManager.Instance.ReportValue(ReportManager.ReportType.
							EnergyCreated, energy - e0, consumer.Name);
					usage += e0 - energy;
					consumer.SetConnectionStatus(energy == 0.0f ? ConnectionStatus.Powered :
						ConnectionStatus.Unpowered);
				} else
					// Base game had a condition that was always true when reached
					consumer.SetConnectionStatus(ConnectionStatus.Powered);
			}
			batteryStatus.Finish();
			return usage / UpdateManager.SecondsPerSimTick;
		}

		/// <summary>
		/// Fills battery and transformer storage on the circuit.
		/// </summary>
		/// <param name="circuit">The circuit to update.</param>
		/// <returns>The total wattage used for overloading purposes.</returns>
		private static float SupplyStorage(ref CircuitInfo circuit) {
			var batteries = circuit.batteries;
			var generators = circuit.generators;
			var inputTransformers = circuit.inputTransformers;
			var outputTransformers = circuit.outputTransformers;
			int firstGenerator = 0, firstTransformer = 0, firstBattery = 0;
			batteries.Sort(BatterySpaceComparer.INSTANCE);
			inputTransformers.Sort(BatterySpaceComparer.INSTANCE);
			generators.Sort(GeneratorChargeComparer.INSTANCE);
			float usage = ChargeDrainFirst(inputTransformers, generators, ref firstGenerator);
			usage += ChargeDrainFirst(inputTransformers, outputTransformers,
				ref firstTransformer);
			if (batteries.Count > 0) {
				ChargeBatteries(batteries, generators, firstGenerator, ref firstBattery);
				ChargeBatteries(batteries, outputTransformers, firstTransformer,
					ref firstBattery);
			}
			return usage / UpdateManager.SecondsPerSimTick;
		}

		/// <summary>
		/// Fills the transformers with battery charge if necessary, then finishes up the
		/// update by reporting any wasted energy and checking for overloading.
		/// </summary>
		/// <param name="instance">The circuit manager to update.</param>
		/// <param name="circuitID">The circuit ID to update.</param>
		/// <param name="used">The wattage used so far by consumers.</param>
		private static void SupplyTransformers(CircuitManager instance, int circuitID,
				float used) {
			var circuits = instance.circuitInfo;
			var circuit = circuits[circuitID];
			var batteries = circuit.batteries;
			var generators = circuit.generators;
			var inputTransformers = circuit.inputTransformers;
			int nb = batteries.Count, ng = generators.Count;
			bool hasSources = ng > 0 || circuit.outputTransformers.Count > 0, isUseful =
				hasSources || circuit.consumers.Count > 0;
			if (nb > 0) {
				// Batteries were in space-order, need to resort to charge-order
				batteries.Sort(BatteryChargeComparer.INSTANCE);
				used += ChargeDrainEvenly(inputTransformers, batteries) / UpdateManager.
					SecondsPerSimTick;
				hasSources |= GetMinimumBatteryCharge(ref circuit);
				UpdateConnectionStatus(instance, circuitID, batteries, isUseful);
			} else
				GetMinimumBatteryCharge(ref circuit);
			UpdateConnectionStatus(instance, circuitID, inputTransformers, hasSources);
			// Report wasted energy
			for (int i = 0; i < ng; i++) {
				var generator = generators[i];
				if (generator != null && report)
					ReportManager.Instance.ReportValue(ReportManager.ReportType.EnergyWasted,
						-generator.JoulesAvailable, StringFormatter.Replace(STRINGS.BUILDINGS.
						PREFABS.GENERATOR.OVERPRODUCTION, "{Generator}", generator.
						GetProperName()));
			}
			circuit.wattsUsed = used;
			circuits[circuitID] = circuit;
			// Check for overloading
			var network = Game.Instance.electricalConduitSystem.GetNetworkByID(circuitID);
			if (network is ElectricalUtilityNetwork enet)
				enet.UpdateOverloadTime(UpdateManager.SecondsPerSimTick, used, circuit.
					bridgeGroups);
		}

		/// <summary>
		/// Updates the electrical circuits.
		/// </summary>
		/// <param name="instance">The circuits to update.</param>
		private static void Update(CircuitManager instance) {
			var circuits = instance.circuitInfo;
			var active = instance.activeGenerators;
			int n = circuits.Count;
			var usage = ListPool<float, CircuitManager>.Allocate();
			// Running in parallel would be really nice, but unfortunately transformers are
			// on multiple enet grids at once and are not very threadsafe, and setting
			// consumer status triggers a bunch of get components and UI updates
			for (int i = 0; i < n; i++) {
				var circuit = circuits[i];
				var consumers = circuit.consumers;
				var batteries = circuit.batteries;
				int nb = batteries.Count;
				float used = 0.0f;
				batteries.Sort(BatteryChargeComparer.INSTANCE);
				// IFF the battery with the most charge has no power, then no batteries do
				if (GetActiveGenerators(ref circuit, active) || (nb > 0 && batteries[nb - 1].
						JoulesAvailable > 0.0f))
					used = SupplyConsumers(ref circuit, active);
				else if (circuit.generators.Count > 0)
					SetAllConsumerStatus(consumers, ConnectionStatus.Unpowered);
				else
					SetAllConsumerStatus(consumers, ConnectionStatus.NotConnected);
				usage.Add(used);
			}
			// Second pass to charge batteries, maybe using energy from the transformers in
			// the first pass
			for (int i = 0; i < n; i++) {
				var circuit = circuits[i];
				usage[i] += SupplyStorage(ref circuit);
			}
			for (int i = 0; i < n; i++)
				SupplyTransformers(instance, i, usage[i]);
			usage.Recycle();
		}

		/// <summary>
		/// Updates the plugged in status of batteries or transformers.
		/// </summary>
		/// <param name="instance">The circuit manager to update.</param>
		/// <param name="circuitID">The circuit index to update.</param>
		/// <param name="batteries">The batteries or transformers to update.</param>
		/// <param name="hasSources">Whether the batteries have a potential energy source.</param>
		private static void UpdateConnectionStatus(CircuitManager instance, int circuitID,
				IList<Battery> batteries, bool hasSources) {
			int ni = batteries.Count;
			for (int i = 0; i < ni; i++) {
				var battery = batteries[i];
				if (battery != null) {
					if (battery.powerTransformer == null)
						battery.SetConnectionStatus(hasSources ? ConnectionStatus.Powered :
							ConnectionStatus.NotConnected);
					else if (instance.GetCircuitID(battery) == circuitID)
						battery.SetConnectionStatus(hasSources ? ConnectionStatus.Powered :
							ConnectionStatus.Unpowered);
				}
			}
		}

		/// <summary>
		/// Allows run-on powering many small consumers from one scan of the battery list.
		/// </summary>
		private struct ConsumerRun {
			/// <summary>
			/// The batteries to drain.
			/// </summary>
			private readonly IList<Battery> batteries;

			/// <summary>
			/// The first valid battery index with charge.
			/// </summary>
			private int firstValid;

			/// <summary>
			/// The energy that is pending consume across all batteries evenly.
			/// </summary>
			private float pendingWithdraw;

			internal ConsumerRun(IList<Battery> batteries) {
				Battery battery;
				int n = batteries.Count, first = 0;
				this.batteries = batteries;
				firstValid = 0;
				pendingWithdraw = 0.0f;
				while (first < n && ((battery = batteries[first]) == null || battery.
						JoulesAvailable <= 0.0f))
					first++;
				firstValid = first;
			}

			/// <summary>
			/// If all consumers are finished, withdraws the cached energy evenly from
			/// batteries.
			/// </summary>
			public void Finish() {
				float pending = pendingWithdraw;
				if (pending > 0.0f) {
					int n = batteries.Count, first = firstValid;
					float energyUsedPer = pending / (n - first);
					for (int i = first; i < n; i++) {
						var battery = batteries[i];
						if (battery != null) {
							battery.ConsumeEnergy(energyUsedPer);
							if (battery.JoulesAvailable <= 0.0f)
								first = i + 1;
						}
					}
					firstValid = first;
					pendingWithdraw = 0.0f;
				}
			}

			/// <summary>
			/// Attempts to power a consumer. If the energy required can be provably satisfied
			/// instantly without scanning batteries, the energy is added to the pending
			/// withdraw without removing any charge. But if the sum total of pending withdraw
			/// exceeds the amount provably available, the list is scanned and updated.
			/// </summary>
			/// <param name="demand">The energy required.</param>
			/// <returns>The energy remaining to supply.</returns>
			public float Power(float demand) {
				int n = batteries.Count, first;
				// Since the batteries are sorted and drained evenly, all must have at least
				// the charge of the first one
				while ((first = firstValid) < n) {
					var battery = batteries[first];
					float energyAcrossAll = (n - first) * battery.JoulesAvailable;
					if (demand < energyAcrossAll) {
						pendingWithdraw += demand;
						demand = 0.0f;
						break;
					} else if (energyAcrossAll > 0.0f) {
						// Large demands end up here
						if (pendingWithdraw == 0.0f) {
							pendingWithdraw = energyAcrossAll;
							demand -= energyAcrossAll;
						}
						Finish();
					} else
						// No energy left
						break;
				}
				return demand;
			}
		}
	}

	/// <summary>
	/// Sorts batteries ascending by available power. The lowest power batteries should go
	/// first, so they get drained quickly and then removed from the iteration order.
	/// </summary>
	internal sealed class BatteryChargeComparer : IComparer<Battery> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly BatteryChargeComparer INSTANCE = new BatteryChargeComparer();

		private BatteryChargeComparer() { }

		public int Compare(Battery a, Battery b) {
			return a.JoulesAvailable.CompareTo(b.JoulesAvailable);
		}
	}

	/// <summary>
	/// Sorts batteries ascending by the amount of energy storage available.
	/// </summary>
	internal sealed class BatterySpaceComparer : IComparer<Battery> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly BatterySpaceComparer INSTANCE = new BatterySpaceComparer();

		private BatterySpaceComparer() { }

		public int Compare(Battery a, Battery b) {
			return (a.Capacity - a.JoulesAvailable).CompareTo(b.Capacity - b.JoulesAvailable);
		}
	}

	/// <summary>
	/// Sorts power consumers ascending by maximum wattage when active.
	/// </summary>
	internal sealed class ConsumerWattageComparer : IComparer<IEnergyConsumer> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly ConsumerWattageComparer INSTANCE =
			new ConsumerWattageComparer();

		private ConsumerWattageComparer() { }

		public int Compare(IEnergyConsumer a, IEnergyConsumer b) {
			return a.WattsNeededWhenActive.CompareTo(b.WattsNeededWhenActive);
		}
	}

	/// <summary>
	/// Sorts generators ascending by available power. The lowest power generators should go
	/// first, so that they get emptied quicker and thus kicked out of the iteration order
	/// first.
	/// </summary>
	internal sealed class GeneratorChargeComparer : IComparer<Generator> {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static readonly GeneratorChargeComparer INSTANCE =
			new GeneratorChargeComparer();

		private GeneratorChargeComparer() { }

		public int Compare(Generator a, Generator b) {
			return a.JoulesAvailable.CompareTo(b.JoulesAvailable);
		}
	}
}
