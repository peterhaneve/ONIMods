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

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using PeterHan.PLib.Core;
using TMPro;
using UnityEngine;

using ENERGYGENERATOR = STRINGS.UI.DETAILTABS.ENERGYGENERATOR;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Stores state information about the energy info screen to avoid recalculating so much
	/// every frame.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class EnergyInfoScreenWrapper : KMonoBehaviour {
		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(64);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		internal static EnergyInfoScreenWrapper Instance { get; private set; }

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
		/// Fixes GetWattsNeededWhenActive to include transformers in the maximum wattage
		/// consumed rating.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		/// <returns>The maximum wattage that circuit needs when all loads are active.</returns>
		public static float GetWattsNeededWhenActive(CircuitManager manager, ushort circuitID)
		{
			float wattage = 0.0f;
			if (circuitID != ushort.MaxValue) {
				var circuit = manager.circuitInfo[circuitID];
				var consumers = circuit.consumers;
				var transformers = circuit.inputTransformers;
				int n = consumers.Count;
				for (int i = 0; i < n; i++) {
					var consumer = consumers[i];
					if (consumer != null)
						wattage += consumer.WattsNeededWhenActive;
				}
				n = transformers.Count;
				for (int i = 0; i < n; i++) {
					var transformer = transformers[i];
					if (transformer != null)
						wattage += transformer.powerTransformer.BaseWattageRating;
				}
			}
			return wattage;
		}

		private readonly ISet<EnergyInfoLabel> batteryLabels;

		private GameObject batteryParent;

		/// <summary>
		/// Caches energy labels to use them again.
		/// </summary>
		private readonly IDictionary<string, EnergyInfoLabel> cache;

		private readonly ISet<EnergyInfoLabel> consumerLabels;

		private GameObject consumerParent;

		/// <summary>
		/// Whether energy networks were simulated since the last update.
		/// </summary>
		internal bool dirty;

#pragma warning disable IDE0044
#pragma warning disable CS0649
		// These fields are automatically populated by KMonoBehaviour
		[MyCmpReq]
		private EnergyInfoScreen es;
#pragma warning restore CS0649
#pragma warning restore IDE0044

		private readonly ISet<EnergyInfoLabel> generatorLabels;

		private GameObject generatorParent;

		private EnergyInfoLabel joulesAvailable;

		private LastSelectionDetails lastSelected;

		private EnergyInfoLabel maxSafeWattage;

		private EnergyInfoLabel noCircuit;

		private EnergyInfoLabel potentialWattageConsumed;

		private readonly ISet<EnergyInfoLabel> setInactive;

		private EnergyInfoLabel wattageConsumed;

		private EnergyInfoLabel wattageGenerated;

		private bool wasValid;

		internal EnergyInfoScreenWrapper() {
			batteryLabels = new HashSet<EnergyInfoLabel>();
			consumerLabels = new HashSet<EnergyInfoLabel>();
			cache = new Dictionary<string, EnergyInfoLabel>(64);
			generatorLabels = new HashSet<EnergyInfoLabel>();
			setInactive = new HashSet<EnergyInfoLabel>();
			dirty = true;
			wasValid = false;
		}

		/// <summary>
		/// Adds an energy consumer to the consumers list.
		/// </summary>
		/// <param name="iConsumer">The consumer to add.</param>
		/// <param name="index">The consumer's index.</param>
		/// <param name="required">The wattage needed when the consumer is active.</param>
		/// <param name="selected">The currently selected object.</param>
		private void AddConsumer(IEnergyConsumer iConsumer, int index, float required,
				GameObject selected) {
			var text = CACHED_BUILDER;
			if (iConsumer is KMonoBehaviour consumer && consumer != null) {
				var label = AddOrGetLabel(es.labelTemplate, consumerParent,
					"consumer" + index);
				var go = consumer.gameObject;
				var fontStyle = (go == selected) ? FontStyles.Bold : FontStyles.Normal;
				var title = label.text;
				float used = iConsumer.WattsUsed;
				text.Clear().Append(iConsumer.Name).Append(": ");
				FormatStringPatches.GetFormattedWattage(text, used);
				if (!Mathf.Approximately(used, required)) {
					text.Append(" / ");
					FormatStringPatches.GetFormattedWattage(text, required);
				}
				title.fontStyle = fontStyle;
				title.SetText(text);
				consumerLabels.Add(label);
			}
		}

		/// <summary>
		/// Retrieves a label for displaying energy information.
		/// </summary>
		/// <param name="prefab">The prefab to copy if a new label is needed.</param>
		/// <param name="parent">The parent of the label.</param>
		/// <param name="id">The label's unique ID.</param>
		/// <returns>A label with that ID, possibly cached if the ID was used before.</returns>
		private EnergyInfoLabel AddOrGetLabel(GameObject prefab, GameObject parent, string id) {
			if (cache.TryGetValue(id, out EnergyInfoLabel label)) {
				label.SetActive(true);
			} else {
				label = new EnergyInfoLabel(prefab, parent, id);
				cache[id] = label;
			}
			return label;
		}

		public override void OnCleanUp() {
			joulesAvailable?.Dispose();
			wattageGenerated?.Dispose();
			wattageConsumed?.Dispose();
			potentialWattageConsumed?.Dispose();
			maxSafeWattage?.Dispose();
			noCircuit?.Dispose();
			noCircuit = null;
			consumerParent = null;
			batteryParent = null;
			generatorParent = null;
			lastSelected = default;
			foreach (var pair in cache)
				pair.Value.Dispose();
			cache.Clear();
			batteryLabels.Clear();
			consumerLabels.Clear();
			generatorLabels.Clear();
			wasValid = false;
			base.OnCleanUp();
			Instance = null;
		}

		public override void OnPrefabInit() {
			base.OnPrefabInit();
			Instance = this;
		}

		/// <summary>
		/// Initializes labels that need only be updated once.
		/// </summary>
		public override void OnSpawn() {
			var op = es.overviewPanel;
			base.OnSpawn();
			op.SetActive(true);
			if (op.TryGetComponent(out CollapsibleDetailContentPanel panel)) {
				var overviewParent = panel.Content.gameObject;
				joulesAvailable = new EnergyInfoLabel(es.labelTemplate, overviewParent,
					nameof(joulesAvailable));
				joulesAvailable.tooltip.toolTip = ENERGYGENERATOR.AVAILABLE_JOULES_TOOLTIP;
				joulesAvailable.SetActive(false);
				wattageGenerated = new EnergyInfoLabel(es.labelTemplate, overviewParent,
					nameof(wattageGenerated));
				wattageGenerated.tooltip.toolTip = ENERGYGENERATOR.WATTAGE_GENERATED_TOOLTIP;
				wattageGenerated.SetActive(false);
				wattageConsumed = new EnergyInfoLabel(es.labelTemplate, overviewParent, nameof(
					wattageConsumed));
				wattageConsumed.tooltip.toolTip = ENERGYGENERATOR.WATTAGE_CONSUMED_TOOLTIP;
				wattageConsumed.SetActive(false);
				potentialWattageConsumed = new EnergyInfoLabel(es.labelTemplate,
					overviewParent, nameof(potentialWattageConsumed));
				potentialWattageConsumed.tooltip.toolTip = ENERGYGENERATOR.
					POTENTIAL_WATTAGE_CONSUMED_TOOLTIP;
				potentialWattageConsumed.SetActive(false);
				maxSafeWattage = new EnergyInfoLabel(es.labelTemplate, overviewParent, nameof(
					maxSafeWattage));
				maxSafeWattage.tooltip.toolTip = ENERGYGENERATOR.MAX_SAFE_WATTAGE_TOOLTIP;
				maxSafeWattage.SetActive(false);
				noCircuit = new EnergyInfoLabel(es.labelTemplate, overviewParent, nameof(
					noCircuit));
				noCircuit.text.SetText(ENERGYGENERATOR.DISCONNECTED);
				noCircuit.tooltip.toolTip = ENERGYGENERATOR.DISCONNECTED;
				noCircuit.SetActive(true);
				wasValid = false;
				es.generatorsPanel.SetActive(false);
				es.consumersPanel.SetActive(false);
				es.batteriesPanel.SetActive(false);
			} else {
				noCircuit = null;
				PUtil.LogWarning("Unable to find electrical overview panel");
			}
			generatorParent = es.generatorsPanel.TryGetComponent(out panel) ? panel.Content.
				gameObject : null;
			batteryParent = es.batteriesPanel.TryGetComponent(out panel) ? panel.Content.
				gameObject : null;
			consumerParent = es.consumersPanel.TryGetComponent(out panel) ? panel.Content.
				gameObject : null;
			lastSelected = default;
			dirty = true;
		}

		/// <summary>
		/// Refreshes the energy info screen.
		/// </summary>
		internal void Refresh() {
			var manager = Game.Instance.circuitManager;
			var target = es.selectedTarget;
			bool update = dirty;
			ushort circuitID = ushort.MaxValue;
			if (target != null) {
				int cell;
				if (target != lastSelected.lastTarget) {
					lastSelected = new LastSelectionDetails(target);
					update = true;
				}
				var conn = lastSelected.connected;
				if (conn != null)
					circuitID = manager.GetCircuitID(conn);
				else if (Grid.IsValidCell(cell = lastSelected.cell))
					circuitID = manager.GetCircuitID(cell);
			}
			if (update) {
				if (circuitID != ushort.MaxValue) {
					RefreshSummary(manager, circuitID);
					if (!wasValid) {
						es.generatorsPanel.SetActive(true);
						es.consumersPanel.SetActive(true);
						es.batteriesPanel.SetActive(true);
						joulesAvailable.SetActive(true);
						wattageGenerated.SetActive(true);
						wattageConsumed.SetActive(true);
						potentialWattageConsumed.SetActive(true);
						maxSafeWattage.SetActive(true);
						noCircuit.SetActive(false);
						wasValid = true;
					}
					RefreshGenerators(manager, circuitID);
					RefreshConsumers(manager, circuitID);
					RefreshBatteries(manager, circuitID);
				} else if (wasValid) {
					es.generatorsPanel.SetActive(false);
					es.consumersPanel.SetActive(false);
					es.batteriesPanel.SetActive(false);
					joulesAvailable.SetActive(false);
					wattageGenerated.SetActive(false);
					wattageConsumed.SetActive(false);
					potentialWattageConsumed.SetActive(false);
					maxSafeWattage.SetActive(false);
					noCircuit.SetActive(true);
					wasValid = false;
				}
				dirty = false;
			}
		}

		/// <summary>
		/// Updates the list of batteries on the circuit.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		private void RefreshBatteries(CircuitManager manager, ushort circuitID) {
			var text = CACHED_BUILDER;
			var batteries = manager.circuitInfo[circuitID].batteries;
			var target = lastSelected.lastTarget;
			int n = batteries.Count;
			setInactive.UnionWith(batteryLabels);
			batteryLabels.Clear();
			for (int i = 0; i < n; i++) {
				var battery = batteries[i];
				if (battery != null) {
					var label = AddOrGetLabel(es.labelTemplate, batteryParent,
						"battery" + i);
					var go = battery.gameObject;
					var fontStyle = (go == target) ? FontStyles.Bold : FontStyles.Normal;
					var title = label.text;
					text.Clear().Append(go.GetProperName()).Append(": ").Append(GameUtil.
						GetFormattedJoules(battery.JoulesAvailable));
					title.fontStyle = fontStyle;
					title.SetText(text);
					batteryLabels.Add(label);
				}
			}
			if (n <= 0) {
				var label = AddOrGetLabel(es.labelTemplate, batteryParent, "nobatteries");
				label.text.SetText(ENERGYGENERATOR.NOBATTERIES);
				batteryLabels.Add(label);
			}
			setInactive.ExceptWith(batteryLabels);
			foreach (var label in setInactive)
				label.SetActive(false);
			setInactive.Clear();
		}

		/// <summary>
		/// Updates the list of energy consumers on the circuit.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		private void RefreshConsumers(CircuitManager manager, ushort circuitID) {
			var info = manager.circuitInfo[circuitID];
			var consumers = info.consumers;
			var transformers = info.inputTransformers;
			var target = lastSelected.lastTarget;
			int nc = consumers.Count, nt = transformers.Count;
			setInactive.UnionWith(consumerLabels);
			consumerLabels.Clear();
			for (int i = 0; i < nc; i++) {
				var consumer = consumers[i];
				AddConsumer(consumer, i, consumer.WattsNeededWhenActive, target);
			}
			for (int i = 0; i < nt; i++) {
				var transformer = transformers[i];
				AddConsumer(transformer, i + nc, transformer.powerTransformer.WattageRating,
					target);
			}
			if (nc + nt <= 0) {
				var label = AddOrGetLabel(es.labelTemplate, consumerParent, "noconsumers");
				label.text.SetText(ENERGYGENERATOR.NOCONSUMERS);
				consumerLabels.Add(label);
			}
			setInactive.ExceptWith(consumerLabels);
			foreach (var label in setInactive)
				label.SetActive(false);
			setInactive.Clear();
		}

		/// <summary>
		/// Updates the list of generators on the circuit.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		private void RefreshGenerators(CircuitManager manager, ushort circuitID) {
			var text = CACHED_BUILDER;
			var generators = manager.circuitInfo[circuitID].generators;
			var target = lastSelected.lastTarget;
			int n = generators.Count;
			bool hasGenerator = false;
			setInactive.UnionWith(generatorLabels);
			generatorLabels.Clear();
			for (int i = 0; i < n; i++) {
				var generator = generators[i];
				if (generator != null && !generator.TryGetComponent(out Battery _)) {
					var label = AddOrGetLabel(es.labelTemplate, generatorParent,
						"generator" + i);
					var go = generator.gameObject;
					var fontStyle = (go == target) ? FontStyles.Bold : FontStyles.Normal;
					var title = label.text;
					text.Clear().Append(go.GetProperName()).Append(": ");
					if (!generator.IsProducingPower()) {
						FormatStringPatches.GetFormattedWattage(text, 0.0f);
						text.Append(" / ");
					}
					FormatStringPatches.GetFormattedWattage(text, generator.WattageRating);
					title.fontStyle = fontStyle;
					title.SetText(text);
					generatorLabels.Add(label);
					hasGenerator = true;
				}
			}
			if (!hasGenerator) {
				var label = AddOrGetLabel(es.labelTemplate, generatorParent, "nogenerators");
				label.text.SetText(ENERGYGENERATOR.NOGENERATORS);
				generatorLabels.Add(label);
			}
			setInactive.ExceptWith(generatorLabels);
			foreach (var label in setInactive)
				label.SetActive(false);
			setInactive.Clear();
		}

		/// <summary>
		/// Updates the circuit summary information.
		/// </summary>
		/// <param name="manager">The circuit manager to query.</param>
		/// <param name="circuitID">The circuit to look up.</param>
		private void RefreshSummary(CircuitManager manager, ushort circuitID) {
			var text = CACHED_BUILDER;
			// Available
			text.Clear().Append(ENERGYGENERATOR.AVAILABLE_JOULES).Replace("{0}", GameUtil.
				GetFormattedJoules(manager.GetJoulesAvailableOnCircuit(circuitID)));
			joulesAvailable.text.SetText(text);
			// Generated
			float generated = GetWattageGenerated(manager, circuitID, out float potential);
			text.Clear();
			if (Mathf.Approximately(generated, potential)) {
				FormatStringPatches.GetFormattedWattage(text, generated);
			} else {
				FormatStringPatches.GetFormattedWattage(text, generated);
				text.Append(" / ");
				FormatStringPatches.GetFormattedWattage(text, potential);
			}
			string ratio = text.ToString();
			text.Clear().Append(ENERGYGENERATOR.WATTAGE_GENERATED).Replace("{0}", ratio);
			wattageGenerated.text.SetText(text);
			// Consumed
			text.Clear().Append(ENERGYGENERATOR.WATTAGE_CONSUMED).Replace("{0}",
				GameUtil.GetFormattedWattage(manager.GetWattsUsedByCircuit(circuitID)));
			wattageConsumed.text.SetText(text);
			// Max consumed
			text.Clear().Append(ENERGYGENERATOR.POTENTIAL_WATTAGE_CONSUMED).Replace("{0}",
				GameUtil.GetFormattedWattage(GetWattsNeededWhenActive(manager, circuitID)));
			potentialWattageConsumed.text.SetText(text);
			// Max safe
			text.Clear().Append(ENERGYGENERATOR.MAX_SAFE_WATTAGE).Replace("{0}",
				GameUtil.GetFormattedWattage(manager.GetMaxSafeWattageForCircuit(circuitID)));
			maxSafeWattage.text.SetText(text);
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// </summary>
		private readonly struct LastSelectionDetails {
			internal readonly int cell;

			internal readonly ICircuitConnected connected;

			/// <summary>
			/// The last selected object.
			/// </summary>
			internal readonly GameObject lastTarget;

			internal LastSelectionDetails(GameObject target) {
				lastTarget = target;
				target.TryGetComponent(out connected);
				cell = target.TryGetComponent(out Wire _) ? Grid.PosToCell(target.transform.
					position) : Grid.InvalidCell;
			}
		}

		/// <summary>
		/// A label shown in the Energy Info screen.
		/// </summary>
		private sealed class EnergyInfoLabel : IDisposable {
			/// <summary>
			/// The "unique" ID of this entry.
			/// </summary>
			private readonly string id;

			/// <summary>
			/// The root game object.
			/// </summary>
			private readonly GameObject root;

			internal readonly LocText text;

			internal readonly ToolTip tooltip;

			public EnergyInfoLabel(GameObject prefab, GameObject parent, string id) {
				root = Util.KInstantiate(prefab, parent, id);
				root.TryGetComponent(out text);
				root.TryGetComponent(out tooltip);
				var rt = root.transform;
				rt.localScale = Vector3.one;
				this.id = id;
				root.SetActive(true);
			}

			public void Dispose() {
				if (root != null)
					Destroy(root);
			}

			public override bool Equals(object obj) {
				return obj is EnergyInfoLabel other && other.id == id;
			}

			public override int GetHashCode() {
				return id.GetHashCode();
			}

			/// <summary>
			/// Shows or hides this label.
			/// </summary>
			/// <param name="active">true to show the label, or false to hide it.</param>
			internal void SetActive(bool active) {
				root.SetActive(active);
			}

			public override string ToString() {
				return id;
			}
		}

		/// <summary>
		/// Applied to CircuitManager to fix the watts needed display to include transformers.
		/// 
		/// Always runs as it is a bug fix patch.
		/// </summary>
		[HarmonyPatch(typeof(CircuitManager), nameof(CircuitManager.GetWattsNeededWhenActive))]
		internal static class GetWattsNeededWhenActive_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before GetWattsNeededWhenActive runs.
			/// </summary>
			internal static bool Prefix(CircuitManager __instance, ushort circuitID,
					ref float __result) {
				__result = GetWattsNeededWhenActive(__instance, circuitID);
				return false;
			}
		}

		/// <summary>
		/// Applied to EnergyInfoScreen to add our component when it initializes.
		/// </summary>
		[HarmonyPatch(typeof(EnergyInfoScreen), nameof(EnergyInfoScreen.OnPrefabInit))]
		internal static class OnPrefabInit_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(EnergyInfoScreen __instance) {
				if (__instance != null)
					__instance.gameObject.AddOrGet<EnergyInfoScreenWrapper>();
			}
		}

		/// <summary>
		/// Applied to EnergyInfoScreen to replace the refresh code with much faster code.
		/// </summary>
		[HarmonyPatch(typeof(EnergyInfoScreen), nameof(EnergyInfoScreen.Refresh))]
		internal static class Refresh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.SideScreenOpts;

			/// <summary>
			/// Applied before Refresh runs.
			/// </summary>
			internal static bool Prefix() {
				var inst = Instance;
				bool run = inst == null;
				if (!run)
					inst.Refresh();
				return run;
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
					inst.dirty = true;
			}
		}
	}
}
