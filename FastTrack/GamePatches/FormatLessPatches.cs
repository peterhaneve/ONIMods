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

using HarmonyLib;
using Klei.AI;
using UnityEngine;

using ELEMENTAL = STRINGS.UI.ELEMENTAL;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to AdditionalDetailsPanel to optimize this memory hungry method that runs every
	/// frame.
	/// </summary>
	[HarmonyPatch(typeof(AdditionalDetailsPanel), nameof(AdditionalDetailsPanel.
		RefreshDetails))]
	public static class AdditionalDetailsPanelPatch {
		/// <summary>
		/// The last object selected in the additional details pane.
		/// </summary>
		private static LastSelectionDetails lastSelection;

		/// <summary>
		/// The number of cycles to display uptime.
		/// </summary>
		private const int NUM_CYCLES = Tracker.defaultCyclesTracked;

		/// <summary>
		/// The ID of the overheat temperature attribute.
		/// </summary>
		private static string overheatID;

		/// <summary>
		/// A cached version of the uptime.
		/// </summary>
		private static string UPTIME_STR;

		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Called at shutdown to avoid leaking references.
		/// </summary>
		internal static void Cleanup() {
			lastSelection = default;
		}

		/// <summary>
		/// Initializes and resets the last selected item.
		/// </summary>
		internal static void Init() {
			Cleanup();
			overheatID = Db.Get().BuildingAttributes.OverheatTemperature.Id;
			UPTIME_STR = string.Format(ELEMENTAL.UPTIME.NAME, "    • ",
				ELEMENTAL.UPTIME.THIS_CYCLE, "{0}", ELEMENTAL.UPTIME.LAST_CYCLE, "{1}",
				ELEMENTAL.UPTIME.LAST_X_CYCLES.Replace("{0}", NUM_CYCLES.ToString()), "{2}");
		}

		/// <summary>
		/// Draws the building's creation time, or nothing if the data is not available.
		/// </summary>
		/// <param name="drawer">The renderer for the details.</param>
		/// <param name="changed">true if the target changed, or false otherwise.</param>
		private static void AddCreationTime(DetailsPanelDrawer drawer, bool changed) {
			var bc = lastSelection.buildingComplete;
			float creationTime;
			if (bc != null && (creationTime = bc.creationTime) > 0.0f) {
				string time = lastSelection.creationTimeCached;
				if (changed || time == null) {
					time = Util.FormatTwoDecimalPlace((GameClock.Instance.GetTime() -
						creationTime) / Constants.SECONDS_PER_CYCLE);
					lastSelection.creationTimeCached = time;
				}
				drawer.NewLabel(drawer.Format(ELEMENTAL.AGE.NAME, time)).Tooltip(
					drawer.Format(ELEMENTAL.AGE.TOOLTIP, time));
			}
		}

		/// <summary>
		/// Draws the thermal properties of the constituent element.
		/// </summary>
		/// <param name="drawer">The renderer for the details.</param>
		private static void AddElementInfo(DetailsPanelDrawer drawer) {
			string tempStr = GameUtil.GetFormattedTemperature(lastSelection.Temperature);
			byte diseaseIdx = lastSelection.DiseaseIndex;
			int diseaseCount = lastSelection.DiseaseCount;
			drawer.NewLabel(drawer.Format(ELEMENTAL.TEMPERATURE.NAME, tempStr)).
				Tooltip(drawer.Format(ELEMENTAL.TEMPERATURE.TOOLTIP, tempStr)).
				NewLabel(drawer.Format(ELEMENTAL.DISEASE.NAME, GameUtil.
					GetFormattedDisease(diseaseIdx, diseaseCount, false))).
				Tooltip(drawer.Format(ELEMENTAL.DISEASE.TOOLTIP, GameUtil.GetFormattedDisease(
					diseaseIdx, diseaseCount, true)));
			lastSelection.specificHeat.AddLine(drawer);
			lastSelection.thermalConductivity.AddLine(drawer);
			if (lastSelection.insulator)
				drawer.NewLabel(STRINGS.UI.GAMEOBJECTEFFECTS.INSULATED.NAME).Tooltip(
					STRINGS.UI.GAMEOBJECTEFFECTS.INSULATED.TOOLTIP);
		}

		/// <summary>
		/// Draws the state change and radiation (if enabled) information of an element.
		/// </summary>
		/// <param name="drawer">The renderer for the details.</param>
		/// <param name="element">The element to display.</param>
		private static void AddPhaseChangeInfo(DetailsPanelDrawer drawer, Element element) {
			// Phase change points
			if (element.IsSolid) {
				var overheat = lastSelection.overheat;
				lastSelection.boil.AddLine(drawer);
				if (!string.IsNullOrEmpty(overheat.text))
					overheat.AddLine(drawer);
			} else if (element.IsLiquid) {
				lastSelection.freeze.AddLine(drawer);
				lastSelection.boil.AddLine(drawer);
			} else if (element.IsGas)
				lastSelection.freeze.AddLine(drawer);
			// Radiation absorption
			if (DlcManager.FeatureRadiationEnabled()) {
				string radAbsorb = lastSelection.radiationAbsorption;
				drawer.NewLabel(drawer.Format(STRINGS.UI.DETAILTABS.DETAILS.
					RADIATIONABSORPTIONFACTOR.NAME, radAbsorb)).Tooltip(drawer.Format(STRINGS.
					UI.DETAILTABS.DETAILS.RADIATIONABSORPTIONFACTOR.TOOLTIP, radAbsorb));
			}
		}

		/// <summary>
		/// Draws the uptime statistics of the building if available.
		/// </summary>
		/// <param name="drawer">The renderer for the details.</param>
		/// <param name="changed">true if the target changed, or false otherwise.</param>
		private static void AddUptimeStats(DetailsPanelDrawer drawer, bool changed) {
			var operational = lastSelection.operational;
			float thisCycle;
			if (operational != null && lastSelection.showUptime && (thisCycle = operational.
					GetCurrentCycleUptime()) >= 0.0f) {
				float lastCycle = operational.GetLastCycleUptime(), prevCycles =
					operational.GetUptimeOverCycles(NUM_CYCLES);
				string label = lastSelection.uptimeCached;
				if (changed || label == null) {
					label = string.Format(UPTIME_STR, GameUtil.GetFormattedPercent(
						thisCycle * 100.0f), GameUtil.GetFormattedPercent(lastCycle * 100.0f),
						GameUtil.GetFormattedPercent(prevCycles * 100.0f));
					lastSelection.uptimeCached = label;
				}
				drawer.NewLabel(label);
			}
		}

		/// <summary>
		/// Draws the extra descriptors (attributes and details) for the selected object.
		/// </summary>
		/// <param name="drawer">The renderer for the details.</param>
		private static void DetailDescriptors(DetailsPanelDrawer drawer) {
			var target = lastSelection.target;
			var attributes = target.GetAttributes();
			var descriptors = GameUtil.GetAllDescriptors(target);
			int n;
			if (attributes != null) {
				n = attributes.Count;
				for (int i = 0; i < n; i++) {
					var instance = attributes.AttributeTable[i];
					var attr = instance.Attribute;
					if (DlcManager.IsDlcListValidForCurrentContent(attr.DLCIds) && (attr.
							ShowInUI == Attribute.Display.Details || attr.ShowInUI ==
							Attribute.Display.Expectation))
						drawer.NewLabel(instance.modifier.Name + ": " + instance.
							GetFormattedValue()).Tooltip(instance.GetAttributeValueTooltip());
				}
			}
			n = descriptors.Count;
			for (int i = 0; i < n; i++) {
				var descriptor = descriptors[i];
				if (descriptor.type == Descriptor.DescriptorType.Detail)
					drawer.NewLabel(descriptor.text).Tooltip(descriptor.tooltipText);
			}
		}

		/// <summary>
		/// Gets the overheat temperature modifier of an element.
		/// </summary>
		/// <param name="element">The selected element.</param>
		/// <returns>The amount that the element changes the overheat temperature, if any.</returns>
		private static string GetOverheatModifier(Element element) {
			string overheatStr = null;
			// Modify the overheat temperature accordingly
			var modifiers = element.attributeModifiers;
			int n = modifiers.Count;
			for (int i = 0; i < n; i++) {
				var modifier = modifiers[i];
				if (modifier.AttributeId == overheatID) {
					overheatStr = modifier.GetFormattedString();
					break;
				}
			}
			return overheatStr;
		}

		/// <summary>
		/// Gets the specific heat text for the details screen.
		/// </summary>
		/// <param name="element">The selected element.</param>
		/// <param name="tempUnits">The temperature units to use for formatting.</param>
		/// <param name="shcInfo">The specific heat information.</returns>
		private static void GetSHCText(Element element, string tempUnits, out InfoLine shcInfo)
		{
			string shcText = ELEMENTAL.SHC.NAME.Format(GameUtil.GetDisplaySHC(element.
				specificHeatCapacity).ToString("0.000"));
			shcInfo = new InfoLine(shcText, ELEMENTAL.SHC.TOOLTIP.Replace(
				"{SPECIFIC_HEAT_CAPACITY}", shcText + GameUtil.GetSHCSuffix()).Replace(
				"{TEMPERATURE_UNIT}", tempUnits));
		}

		/// <summary>
		/// Gets the thermal conductivity text for the details screen.
		/// </summary>
		/// <param name="element">The selected element.</param>
		/// <param name="building">The building use to build this element, if any.</param>
		/// <param name="tempUnits">The temperature units to use for formatting.</param>
		/// <param name="tcInfo">The thermal conductivity information.</returns>
		/// <returns>Whether the insulated tooltip should appear.</param>
		private static bool GetTCText(Element element, Building building,
				string tempUnits, out InfoLine tcInfo) {
			float tc = element.thermalConductivity;
			bool insulator = false;
			if (building != null) {
				float tcModifier = building.Def.ThermalConductivity;
				tc *= tcModifier;
				insulator = tcModifier < 1.0f;
			}
			// TC
			string tcText = ELEMENTAL.THERMALCONDUCTIVITY.NAME.Format(GameUtil.
				GetDisplayThermalConductivity(tc).ToString("0.000"));
			tcInfo = new InfoLine(tcText, ELEMENTAL.THERMALCONDUCTIVITY.TOOLTIP.
				Replace("{THERMAL_CONDUCTIVITY}", tcText + GameUtil.
				GetThermalConductivitySuffix()).Replace("{TEMPERATURE_UNIT}", tempUnits));
			return insulator;
		}

		/// <summary>
		/// Populates the element phase change information.
		/// </summary>
		/// <param name="element">The selected element.</param>
		/// <param name="boil">The boiling/melting point information.</param>
		/// <param name="freeze">The freezing/condensation point information.</param>
		/// <param name="overheat">The overheat temperature information.</param>
		private static void PopulatePhase(Element element, out InfoLine boil,
				out InfoLine freeze, out InfoLine overheat) {
			string htc = GameUtil.GetFormattedTemperature(element.highTemp),
				ltc = GameUtil.GetFormattedTemperature(element.lowTemp);
			if (element.IsSolid) {
				string oh = GetOverheatModifier(element);
				boil = new InfoLine(ELEMENTAL.MELTINGPOINT.NAME.Format(htc),
					ELEMENTAL.MELTINGPOINT.TOOLTIP.Format(htc));
				freeze = default;
				if (oh != null)
					overheat = new InfoLine(ELEMENTAL.OVERHEATPOINT.NAME.Format(oh),
						ELEMENTAL.OVERHEATPOINT.TOOLTIP.Format(oh));
				else
					overheat = default;
			} else if (element.IsLiquid) {
				freeze = new InfoLine(ELEMENTAL.FREEZEPOINT.NAME.Format(ltc),
					ELEMENTAL.FREEZEPOINT.TOOLTIP.Format(ltc));
				boil = new InfoLine(ELEMENTAL.VAPOURIZATIONPOINT.NAME.Format(htc),
					ELEMENTAL.VAPOURIZATIONPOINT.TOOLTIP.Format(htc));
				overheat = default;
			} else if (element.IsGas) {
				boil = default;
				freeze = new InfoLine(ELEMENTAL.DEWPOINT.NAME.Format(ltc),
					ELEMENTAL.DEWPOINT.TOOLTIP.Format(ltc));
				overheat = default;
			} else {
				boil = default;
				freeze = default;
				overheat = default;
			}
		}

		/// <summary>
		/// Applied before RefreshDetails runs.
		/// </summary>
		internal static bool Prefix(AdditionalDetailsPanel __instance) {
			GameObject target;
			Element element;
			if ((target = __instance.selectedTarget) != null) {
				var drawer = __instance.drawer;
				bool changed = target != lastSelection.target;
				if (changed) {
					var detailsPanel = __instance.detailsPanel;
					lastSelection = new LastSelectionDetails(target);
					detailsPanel.SetActive(true);
					detailsPanel.GetComponent<CollapsibleDetailContentPanel>().HeaderLabel.
						text = STRINGS.UI.DETAILTABS.DETAILS.GROUPNAME_DETAILS;
				}
				element = lastSelection.element;
				if (element != null) {
					string massStr = GameUtil.GetFormattedMass(lastSelection.Mass);
					var id = element.id;
					changed |= !SpeedControlScreen.Instance.IsPaused;
					lastSelection.elementName.AddLine(drawer);
					drawer.NewLabel(drawer.Format(ELEMENTAL.MASS.NAME, massStr)).
						Tooltip(drawer.Format(ELEMENTAL.MASS.TOOLTIP, massStr));
					AddCreationTime(drawer, changed);
					AddUptimeStats(drawer, changed);
					if (id != SimHashes.Vacuum && id != SimHashes.Void)
						AddElementInfo(drawer);
					AddPhaseChangeInfo(drawer, element);
					DetailDescriptors(drawer);
				}
			}
			return false;
		}

		/// <summary>
		/// Stores component references to the last selected object.
		/// 
		/// Big structs are normally a no-no because copying them is expensive. However, this
		/// is mostly a container, and should never be copied.
		/// </summary>
		private struct LastSelectionDetails {
			/// <summary>
			/// The number of germs on this item.
			/// </summary>
			internal int DiseaseCount {
				get {
					int dc;
					var pe = primaryElement;
					if (pe != null)
						dc = pe.DiseaseCount;
					else
						dc = cso.diseaseCount;
					return dc;
				}
			}
			
			/// <summary>
			/// The current disease index on this item, or -1 if none is.
			/// </summary>
			internal byte DiseaseIndex {
				get {
					byte di;
					var pe = primaryElement;
					if (pe != null)
						di = pe.DiseaseIdx;
					else
						di = cso.diseaseIdx;
					return di;
				}
			}

			/// <summary>
			/// The item's current mass.
			/// </summary>
			internal float Mass {
				get {
					float mass;
					var pe = primaryElement;
					if (pe != null)
						mass = pe.Mass;
					else
						mass = cso.Mass;
					return mass;
				}
			}

			/// <summary>
			/// The item's current temperature.
			/// </summary>
			internal float Temperature {
				get {
					float tmp;
					var pe = primaryElement;
					if (pe != null)
						tmp = pe.Temperature;
					else
						tmp = cso.temperature;
					return tmp;
				}
			}

			internal readonly InfoLine boil;

			internal readonly Building building;

			internal readonly BuildingComplete buildingComplete;

			internal string creationTimeCached;

			internal readonly CellSelectionObject cso;

			internal readonly Element element;

			internal readonly InfoLine elementName;

			internal readonly InfoLine freeze;

			internal readonly bool insulator;

			internal readonly Operational operational;

			internal readonly InfoLine overheat;

			internal readonly PrimaryElement primaryElement;

			internal readonly string radiationAbsorption;

			internal readonly InfoLine specificHeat;

			internal readonly bool showUptime;

			/// <summary>
			/// The selected target.
			/// </summary>
			public readonly GameObject target;

			internal readonly InfoLine thermalConductivity;

			internal string uptimeCached;

			public LastSelectionDetails(GameObject go) {
				int cell = Grid.PosToCell(go);
				string tempUnits = GameUtil.GetTemperatureUnitSuffix();
				var bc = go.GetComponent<BuildingComplete>();
				PrimaryElement pe;
				buildingComplete = bc;
				if (bc != null)
					building = bc;
				else
					building = go.GetComponent<Building>();
				creationTimeCached = null;
				operational = go.GetComponent<Operational>();
				// Use primary element by default, but allow CellSelectionObject to stand in
				primaryElement = pe = go.GetComponent<PrimaryElement>();
				if (pe != null) {
					element = pe.Element;
					cso = null;
				} else {
					cso = go.GetComponent<CellSelectionObject>();
					element = (cso == null) ? null : cso.element;
				}
				if (DlcManager.FeatureRadiationEnabled())
					radiationAbsorption = GameUtil.GetFormattedPercent(GameUtil.
						GetRadiationAbsorptionPercentage(cell) * 100.0f);
				else
					radiationAbsorption = null;
				// Why these in particular? Clay please
				showUptime = go.GetComponent<LogicPorts>() != null ||
					go.GetComponent<EnergyConsumer>() != null ||
					go.GetComponent<Battery>() != null;
				target = go;
				uptimeCached = null;
				if (element != null) {
					string name = element.name;
					elementName = new InfoLine(ELEMENTAL.PRIMARYELEMENT.NAME.Format(name),
						ELEMENTAL.PRIMARYELEMENT.TOOLTIP.Format(name));
					insulator = GetTCText(element, building, tempUnits,
						out thermalConductivity);
					GetSHCText(element, tempUnits, out specificHeat);
					PopulatePhase(element, out boil, out freeze, out overheat);
				} else {
					boil = default;
					elementName = default;
					freeze = default;
					overheat = default;
					insulator = false;
					specificHeat = default;
					thermalConductivity = default;
				}
			}
		}

		/// <summary>
		/// Stores precomputed info descriptors in the additional details panel.
		/// </summary>
		private struct InfoLine {
			/// <summary>
			/// The text to display in the panel.
			/// </summary>
			public readonly string text;

			/// <summary>
			/// The text to display on mouse over.
			/// </summary>
			public readonly string tooltip;

			public InfoLine(string text, string tooltip) {
				this.text = text;
				this.tooltip = tooltip;
			}

			/// <summary>
			/// Adds this info descriptor to the details screen.
			/// </summary>
			/// <param name="drawer">The renderer for the details.</param>
			public void AddLine(DetailsPanelDrawer drawer) {
				drawer.NewLabel(text).Tooltip(tooltip);
			}

			public override string ToString() {
				return text;
			}
		}
	}
}
