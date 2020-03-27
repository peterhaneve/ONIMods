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
			if (element != null && Def != null) {
				var descriptors = GameUtil.GetMaterialDescriptors(element);
				var item = default(Descriptor);
				// Get building mass from its def (primary element is in slot 0)
				var masses = Def.Mass;
				float tc = element.thermalConductivity, shc = element.specificHeatCapacity;
				float mass = (masses != null && masses.Length > 0) ? masses[0] : 0.0f;
				string deg = GameUtil.GetTemperatureUnitSuffix()?.Trim(), kDTU = STRINGS.UI.
					UNITSUFFIXES.HEAT.KDTU.text.Trim();
				float tMass = GameUtil.GetDisplaySHC(mass * shc) * ThermalTranspilerPatch.
					GetSHCAdjustment(Def?.BuildingComplete);
				// GetMaterialDescriptors returns a fresh list
				item.SetupDescriptor(STRINGS.ELEMENTS.MATERIAL_MODIFIERS.EFFECTS_HEADER,
					STRINGS.ELEMENTS.MATERIAL_MODIFIERS.TOOLTIP.EFFECTS_HEADER);
				descriptors.Insert(0, item);
				// Thermal Conductivity
				item.SetupDescriptor(string.Format(STRINGS.UI.ELEMENTAL.THERMALCONDUCTIVITY.
					NAME, tc), string.Format(ThermalTooltipsStrings.BUILDING_CONDUCTIVITY,
					Def.Name, GameUtil.GetFormattedThermalConductivity(tc), tc, deg,
					STRINGS.UI.UNITSUFFIXES.HEAT.DTU_S.text.Trim()));
				item.IncreaseIndent();
				descriptors.Add(item);
				// Thermal Mass
				item.SetupDescriptor(string.Format(ThermalTooltipsStrings.THERMAL_MASS, tMass,
					kDTU, deg), string.Format(ThermalTooltipsStrings.BUILDING_THERMAL_MASS,
					Def.Name, tMass, kDTU, deg));
				descriptors.Add(item);
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
