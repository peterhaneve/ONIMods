/*
 * Copyright 2024 Peter Han
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

using TTS = PeterHan.ThermalTooltips.ThermalTooltipsStrings.UI.THERMALTOOLTIPS;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Handles the tooltips shown when planning a building.
	/// </summary>
	public sealed class BuildThermalTooltip {
		/// <summary>
		/// The building to be constructed.
		/// </summary>
		public BuildingDef Def { get; set; }

		public BuildThermalTooltip() {
			Def = null;
		}

		/// <summary>
		/// Adds thermal tooltips to the descriptors shown in the product information pane
		/// (the menu when selecting a build material).
		/// </summary>
		/// <param name="effectsPane">The pane to modify.</param>
		/// <param name="elementTag">The element that is selected.</param>
		public void AddThermalInfo(DescriptorPanel effectsPane, Tag elementTag) {
			var element = ElementLoader.GetElement(elementTag);
			var desc = default(Descriptor);
			if (element != null && Def != null) {
				var descriptors = GameUtil.GetMaterialDescriptors(element);
				// Get building mass from its def (primary element is in slot 0)
				var masses = Def.Mass;
				float tc = element.thermalConductivity * Def.ThermalConductivity, shc =
					element.specificHeatCapacity, mass = ThermalTranspilerPatch.
					GetAdjustedMass(Def.BuildingComplete, masses != null && masses.Length >
					0 ? masses[0] : 0.0f), tMass = GameUtil.GetDisplaySHC(mass * shc);
				string deg = GameUtil.GetTemperatureUnitSuffix()?.Trim(), kDTU = STRINGS.UI.
					UNITSUFFIXES.HEAT.KDTU.text.Trim();
				// GetMaterialDescriptors returns a fresh list
				desc.SetupDescriptor(STRINGS.ELEMENTS.MATERIAL_MODIFIERS.EFFECTS_HEADER,
					STRINGS.ELEMENTS.MATERIAL_MODIFIERS.TOOLTIP.EFFECTS_HEADER);
				descriptors.Insert(0, desc);
				// Thermal Conductivity
				desc.SetupDescriptor(string.Format(TTS.EFFECT_CONDUCTIVITY, tc),
					string.Format(TTS.BUILDING_CONDUCTIVITY, Def.Name, GameUtil.
					GetFormattedThermalConductivity(tc), tc, deg,
					STRINGS.UI.UNITSUFFIXES.HEAT.DTU_S.text.Trim()));
				desc.IncreaseIndent();
				descriptors.Add(desc);
				// Thermal Mass
				desc.SetupDescriptor(string.Format(TTS.EFFECT_THERMAL_MASS, tMass, kDTU, deg),
					string.Format(TTS.BUILDING_THERMAL_MASS, Def.Name, tMass, kDTU, deg));
				descriptors.Add(desc);
				// Melt Temperature
				var hotElement = element.highTempTransition;
				if (hotElement.IsValidTransition(element)) {
					string meltTemp = GameUtil.GetFormattedTemperature(element.highTemp +
						ExtendedThermalTooltip.TRANSITION_HYSTERESIS);
					desc.SetupDescriptor(string.Format(TTS.EFFECT_MELT_TEMPERATURE, meltTemp),
						string.Format(TTS.BUILDING_MELT_TEMPERATURE, Def.Name, meltTemp,
						hotElement.FormatName(STRINGS.UI.StripLinkFormatting(element.name))));
					descriptors.Add(desc);
				}
				effectsPane.SetDescriptors(descriptors);
				effectsPane.gameObject.SetActive(true);
			}
		}

		/// <summary>
		/// Removes the cached building def so it can be garbage collected if needed.
		/// </summary>
		public void ClearDef() {
			Def = null;
		}
	}
}
