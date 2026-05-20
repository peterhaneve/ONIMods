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
using UnityEngine;

using ENERGYGENERATOR = STRINGS.UI.DETAILTABS.ENERGYGENERATOR;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the energy info screen to avoid recalculating so much
	/// every frame.
	/// </summary>
	public sealed partial class AdditionalDetailsPanelWrapper {
		/// <summary>
		/// Adds an energy consumer to the consumers list.
		/// </summary>
		/// <param name="panel">The panel where the details should be populated.</param>
		/// <param name="ic">The consumer to add.</param>
		/// <param name="required">The wattage needed when the consumer is active.</param>
		/// <param name="selected">The currently selected object.</param>
		private static void AddConsumer(CollapsibleDetailContentPanel panel, IEnergyConsumer ic,
				float required, GameObject selected) {
			var text = CACHED_BUILDER;
			if (ic is KMonoBehaviour consumer && consumer != null) {
				var go = consumer.gameObject;
				bool isSelected = go == selected;
				float used = ic.WattsUsed;
				text.Clear();
				if (isSelected)
					text.Append("<b>");
				text.Append(ic.Name).Append(": ");
				FormatStringPatches.GetFormattedWattage(text, used);
				if (!Mathf.Approximately(used, required)) {
					text.Append(" / ");
					FormatStringPatches.GetFormattedWattage(text, required);
				}
				if (isSelected)
					text.Append("</b>");
				panel.SetLabel(go.GetInstanceID().ToString(), text.ToString(), "");
			}
		}
		
		/// <summary>
		/// Gets both the current wattage generated and the potential wattage generated.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		/// <param name="potential">The location where the potential power generation will be stored.</param>
		/// <returns>The power currently being generated.</returns>
		private static float GetWattageGenerated(CircuitManager manager, ushort circuitID,
				out float potential) {
			float generated = 0.0f, total = 0.0f;
			if (circuitID != ushort.MaxValue) {
				var generators = manager.circuitInfo[circuitID].generators;
				int n = generators.Count;
				for (int i = 0; i < n; i++) {
					var generator = generators[i];
					if (generator != null) {
						float watts = generator.WattageRating;
						total += watts;
						if (generator.IsProducingPower())
							generated += watts;
					}
				}
			}
			potential = total;
			return generated;
		}

		/// <summary>
		/// Whether energy networks were simulated since the last update.
		/// </summary>
		internal bool enetDirty;

		/// <summary>
		/// Updates the list of batteries on the circuit.
		/// </summary>
		/// <param name="panel">The panel to update.</param>
		internal void UpdateBatteries(CollapsibleDetailContentPanel panel) {
			var text = CACHED_BUILDER;
			var target = lastSelection.target;
			if (target != null) {
				ushort id = lastSelection.CircuitID;
				if (id != ushort.MaxValue) {
					var batteries = Game.Instance.circuitManager.circuitInfo[id].batteries;
					int n = batteries.Count;
					for (int i = 0; i < n; i++) {
						var battery = batteries[i];
						if (battery != null) {
							var go = battery.gameObject;
							bool isSelected = go == target;
							text.Clear();
							if (isSelected)
								text.Append("<b>");
							text.Append(go.GetProperName()).Append(": ").Append(GameUtil.
								GetFormattedJoules(battery.JoulesAvailable));
							if (isSelected)
								text.Append("</b>");
							panel.SetLabel(go.GetInstanceID().ToString(), text.ToString(), "");
						}
					}
					if (n <= 0)
						panel.SetLabel("nobatteries", ENERGYGENERATOR.NOBATTERIES, "");
				}
				panel.Commit();
			}
		}

		/// <summary>
		/// Updates the list of energy consumers on the circuit.
		/// </summary>
		/// <param name="panel">The panel to update.</param>
		internal void UpdateConsumers(CollapsibleDetailContentPanel panel) {
			var target = lastSelection.target;
			if (target != null) {
				ushort id = lastSelection.CircuitID;
				if (id != ushort.MaxValue) {
					var info = Game.Instance.circuitManager.circuitInfo[id];
					var consumers = info.consumers;
					var transformers = info.inputTransformers;
					int nc = consumers.Count, nt = transformers.Count;
					for (int i = 0; i < nc; i++) {
						var consumer = consumers[i];
						AddConsumer(panel, consumer, consumer.WattsNeededWhenActive, target);
					}
					for (int i = 0; i < nt; i++) {
						var transformer = transformers[i];
						AddConsumer(panel, transformer, transformer.powerTransformer.
							WattageRating, target);
					}
					if (nc + nt <= 0)
						panel.SetLabel("noconsumers", ENERGYGENERATOR.NOCONSUMERS, "");
				}
				panel.Commit();
			}
		}

		/// <summary>
		/// Updates the list of generators on the circuit.
		/// </summary>
		/// <param name="panel">The panel to update.</param>
		internal void UpdateGenerators(CollapsibleDetailContentPanel panel) {
			var text = CACHED_BUILDER;
			var target = lastSelection.target;
			if (target != null) {
				ushort id = lastSelection.CircuitID;
				if (id != ushort.MaxValue) {
					var generators = Game.Instance.circuitManager.circuitInfo[id].generators;
					int n = generators.Count;
					bool hasGenerator = false;
					for (int i = 0; i < n; i++) {
						var generator = generators[i];
						if (generator != null && !generator.TryGetComponent(out Battery _)) {
							var go = generator.gameObject;
							text.Clear().Append(go.GetProperName()).Append(": ");
							if (!generator.IsProducingPower()) {
								FormatStringPatches.GetFormattedWattage(text, 0.0f);
								text.Append(" / ");
							}
							FormatStringPatches.GetFormattedWattage(text, generator.
								WattageRating);
							panel.SetLabel(go.GetInstanceID().ToString(), text.ToString(), "");
							hasGenerator = true;
						}
					}
					if (!hasGenerator)
						panel.SetLabel("nogenerators", ENERGYGENERATOR.NOGENERATORS, "");
				}
				panel.Commit();
			}
		}

		/// <summary>
		/// Updates the circuit summary information.
		/// </summary>
		/// <param name="panel">The panel to update.</param>
		internal void UpdateSummary(CollapsibleDetailContentPanel panel) {
			var text = CACHED_BUILDER;
			var target = lastSelection.target;
			if (target != null) {
				ushort id = lastSelection.CircuitID;
				var manager = Game.Instance.circuitManager;
				if (id != ushort.MaxValue) {
					// Available
					panel.SetLabel("joulesAvailable", ENERGYGENERATOR.AVAILABLE_JOULES.
						Format(GameUtil.GetFormattedJoules(manager.
						GetJoulesAvailableOnCircuit(id))), ENERGYGENERATOR.
						AVAILABLE_JOULES_TOOLTIP);
					// Generated
					float generated = GetWattageGenerated(manager, id, out float potential);
					text.Clear();
					if (Mathf.Approximately(generated, potential)) {
						FormatStringPatches.GetFormattedWattage(text, generated);
					} else {
						FormatStringPatches.GetFormattedWattage(text, generated);
						text.Append(" / ");
						FormatStringPatches.GetFormattedWattage(text, potential);
					}
					panel.SetLabel("wattageGenerated", ENERGYGENERATOR.WATTAGE_GENERATED.
						Format(text.ToString()), ENERGYGENERATOR.WATTAGE_GENERATED_TOOLTIP);
					// Consumed
					FormatStringPatches.GetFormattedWattage(text.Clear(), manager.
						GetWattsUsedByCircuit(id));
					panel.SetLabel("wattageConsumed", ENERGYGENERATOR.WATTAGE_CONSUMED.
						Format(text.ToString()), ENERGYGENERATOR.WATTAGE_CONSUMED_TOOLTIP);
					// Max consumed
					FormatStringPatches.GetFormattedWattage(text.Clear(), manager.
						GetWattsNeededWhenActive(id));
					panel.SetLabel("potentialWattageConsumed", ENERGYGENERATOR.
						POTENTIAL_WATTAGE_CONSUMED.Format(text.ToString()), ENERGYGENERATOR.
						POTENTIAL_WATTAGE_CONSUMED_TOOLTIP);
					// Max safe
					FormatStringPatches.GetFormattedWattage(text.Clear(), manager.
						GetMaxSafeWattageForCircuit(id));
					panel.SetLabel("maxSafeWattage", ENERGYGENERATOR.MAX_SAFE_WATTAGE.Format(
						text.ToString()), ENERGYGENERATOR.MAX_SAFE_WATTAGE_TOOLTIP);
				} else
					panel.SetLabel("nocircuit", ENERGYGENERATOR.DISCONNECTED, ENERGYGENERATOR.
						DISCONNECTED);
				panel.Commit();
			}
		}

		/// <summary>
		/// Applied to CircuitManager to flag when electrical networks are dirty.
		/// </summary>
		[HarmonyPatch(typeof(CircuitManager), nameof(CircuitManager.Sim200msLast))]
		internal static class Sim200msLast_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.SideScreenOpts && !options.ENetOpts;
			}

			/// <summary>
			/// Applied before Sim200msLast runs.
			/// </summary>
			internal static void Prefix(CircuitManager __instance, float dt) {
				float elapsedTime = __instance.elapsedTime;
				var inst = Instance;
				if (elapsedTime + dt >= UpdateManager.SecondsPerSimTick && inst != null)
					inst.enetDirty = true;
			}
		}
	}
}
