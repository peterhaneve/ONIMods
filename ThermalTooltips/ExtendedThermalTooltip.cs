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

using PeterHan.PLib;
using System;
using UnityEngine;

using TEMP_SUFFIXES = STRINGS.UI.UNITSUFFIXES.TEMPERATURE;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Displays extended thermal information tooltips on objects in-game.
	/// </summary>
	public sealed class ExtendedThermalTooltip {
		/// <summary>
		/// The hysteresis in each direction when changing states.
		/// </summary>
		private const float TRANSITION_HYSTERESIS = 3.0f;

		/// <summary>
		/// The cell to use for element-based thermal tooltips.
		/// </summary>
		public int Cell { get; set; }

		/// <summary>
		/// The hover text drawer where the information will be written.
		/// </summary>
		public HoverTextDrawer Drawer { get; set; }

		/// <summary>
		/// The element to use for item-based thermal tooltips.
		/// </summary>
		public PrimaryElement PrimaryElement { get; set; }

		/// <summary>
		/// The text style to use.
		/// </summary>
		public TextStyleSetting Style { get; set; }

		/// <summary>
		/// The options to use.
		/// </summary>
		private readonly ThermalTooltipsOptions options;

		/// <summary>
		/// The "cold" sprite for transition temperature.
		/// </summary>
		private readonly Sprite spriteCold;

		/// <summary>
		/// The "-" sprite.
		/// </summary>
		private readonly Sprite spriteDash;

		/// <summary>
		/// The "hot" sprite for transition temperature.
		/// </summary>
		private readonly Sprite spriteHot;

		internal ExtendedThermalTooltip(ThermalTooltipsOptions options) {
			this.options = options;
			Cell = 0;
			PrimaryElement = null;
			Drawer = null;
			Style = null;
			spriteDash = Assets.GetSprite("dash");
			spriteCold = Assets.GetSprite("crew_state_temp_down");
			spriteHot = Assets.GetSprite("crew_state_temp_up");
		}

		/// <summary>
		/// Displays an element and its icon.
		/// </summary>
		/// <param name="element">The element to display.</param>
		/// <param name="oldElementName">The name of the base element. If it is the same as
		/// the provided element, the state of matter will be displayed.</param>
		private void DisplayElement(Element element, string oldElementName = null) {
			var prefab = Assets.GetPrefab(element.tag);
			string name = STRINGS.UI.StripLinkFormatting(element.name);
			Tuple<Sprite, Color> pair;
			// Extract the UI preview image
			if (prefab != null && (pair = Def.GetUISprite(prefab)) != null)
				Drawer.DrawIcon(pair.first, pair.second, 22);
			if (name == oldElementName) {
				// Do not switch case on State, it is a bit field
				if (element.IsLiquid)
					name = STRINGS.ELEMENTS.STATE.LIQUID + " " + name;
				else if (element.IsSolid)
					name = STRINGS.ELEMENTS.STATE.SOLID + " " + name;
				else if (element.IsGas)
					name = STRINGS.ELEMENTS.STATE.GAS + " " + name;
			}
			Drawer.DrawText(name, Style);
		}

		/// <summary>
		/// Called when thermal information needs to be displayed.
		/// </summary>
		/// <param name="defaultText">The default temperature text.</param>
		/// <param name="element">The element that is being displayed.</param>
		/// <param name="temperature">The element's temperature in K.</param>
		/// <param name="mass">The element's mass in kg.</param>
		public void DisplayThermalInfo(Element element, float temperature, float mass) {
			if (Drawer != null && Style != null) {
				// Ignore SHC <= 0: vacuum, void, neutronium
				if (element != null && (SimDebugView.Instance.GetMode() == OverlayModes.
						Temperature.ID || options?.OnlyOnThermalOverlay == false) &&
						element.specificHeatCapacity > 0.0f) {
					string name = STRINGS.UI.StripLinkFormatting(element.name);
					DisplayThermalStats(element, temperature, mass);
					var coldElement = element.lowTempTransition;
					// Freeze to
					if (coldElement.IsValidTransition(element)) {
						DisplayTransitionSprite(spriteCold);
						DisplayTransition(coldElement, Math.Max(0.1f, element.lowTemp -
							TRANSITION_HYSTERESIS), element.lowTempTransitionOreID,
							element.lowTempTransitionOreMassConversion, name);
					}
					var hotElement = element.highTempTransition;
					// Boil to
					if (hotElement.IsValidTransition(element)) {
						DisplayTransitionSprite(spriteHot);
						DisplayTransition(hotElement, element.highTemp + TRANSITION_HYSTERESIS,
							element.highTempTransitionOreID,
							element.highTempTransitionOreMassConversion, name);
					}
				} else
					// Not displayed in this mode
					Drawer.DrawText(GetTemperatureString(temperature), Style);
			}
		}

		/// <summary>
		/// Displays the thermal statistics of the item.
		/// </summary>
		/// <param name="element">The element's material.</param>
		/// <param name="temp">The temperature in K.</param>
		/// <param name="mass">The element's mass.</param>
		private void DisplayThermalStats(Element element, float temp, float mass) {
			float tc = element.thermalConductivity, shc = element.specificHeatCapacity;
			string kDTU = STRINGS.UI.UNITSUFFIXES.HEAT.KDTU.text.Trim();
			// Temperature
			Drawer.DrawText(GetTemperatureString(temp), Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Thermal conductivity
			Drawer.DrawText(string.Format(STRINGS.UI.ELEMENTAL.THERMALCONDUCTIVITY.NAME,
				GameUtil.GetFormattedThermalConductivity(tc)), Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Thermal mass (mass is in kg so result is in kDTU/C)
			Drawer.DrawText(string.Format(ThermalTooltipsStrings.THERMAL_MASS, GameUtil.
				GetDisplaySHC(mass * shc), kDTU, GameUtil.GetTemperatureUnitSuffix()?.Trim()),
				Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Heat energy in kDTU
			Drawer.DrawText(string.Format(ThermalTooltipsStrings.HEAT_ENERGY, mass * shc *
				temp, kDTU), Style);
		}

		/// <summary>
		/// Displays the information about the element transition temperature.
		/// </summary>
		/// <param name="newElement">The element to which it transitions.</param>
		/// <param name="temp">The temperature when it occurs.</param>
		/// <param name="secondary">The secondary element produced as a byproduct.</param>
		/// <param name="ratio">The ratio of the secondary element by mass.</param>
		/// <param name="oldName">The old element's name.</param>
		private void DisplayTransition(Element newElement, float temp, SimHashes secondary,
				float ratio, string oldName) {
			// Primary element
			DisplayElement(newElement, oldName);
			if (secondary != SimHashes.Vacuum && secondary != SimHashes.Void && ratio > 0.0f) {
				var altElement = ElementLoader.FindElementByHash(secondary);
				ratio *= 100.0f;
				if (altElement != null) {
					// "and <other element>"
					Drawer.DrawText(string.Format(ThermalTooltipsStrings.AND_JOIN, GameUtil.
						GetFormattedPercent(100.0f - ratio)), Style);
					DisplayElement(altElement, oldName);
					Drawer.DrawText(string.Format("[{0}]", GameUtil.GetFormattedPercent(
						ratio)), Style);
				}
			}
			Drawer.DrawText(" ({0:##0.#})".F(GetTemperatureString(temp)), Style);
		}

		/// <summary>
		/// Displays the hot or cold sprite (or the fallback text if not found) and the
		/// required lead-in text for an element transition.
		/// </summary>
		/// <param name="sprite">The sprite to display.</param>
		private void DisplayTransitionSprite(Sprite sprite) {
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Fallback if sprite is missing
			if (sprite != null)
				Drawer.DrawIcon(sprite, Color.white, 22);
			else
				Drawer.DrawText(ThermalTooltipsStrings.CHANGES, Style);
			Drawer.DrawText(ThermalTooltipsStrings.TO_JOIN, Style);
		}

		/// <summary>
		/// Retrieves the string value of the temperature, in all units if needed.
		/// </summary>
		/// <param name="temp">The temperature to display.</param>
		/// <returns>The display value of that temperature.</returns>
		private string GetTemperatureString(float temp) {
			string result;
			if (options?.AllUnits == true) {
				string c = ThermalTooltipsStrings.TEMP_FORMAT.F(GameUtil.
					GetTemperatureConvertedFromKelvin(temp, GameUtil.TemperatureUnit.
					Celsius), TEMP_SUFFIXES.CELSIUS);
				string f = ThermalTooltipsStrings.TEMP_FORMAT.F(GameUtil.
					GetTemperatureConvertedFromKelvin(temp, GameUtil.TemperatureUnit.
					Fahrenheit), TEMP_SUFFIXES.FAHRENHEIT);
				string k = ThermalTooltipsStrings.TEMP_FORMAT.F(temp, TEMP_SUFFIXES.KELVIN);
				// Put the user preferred temperature first
				switch (GameUtil.temperatureUnit) {
				case GameUtil.TemperatureUnit.Celsius:
					result = ThermalTooltipsStrings.ALL_TEMPS.F(c, f, k);
					break;
				case GameUtil.TemperatureUnit.Fahrenheit:
					result = ThermalTooltipsStrings.ALL_TEMPS.F(f, c, k);
					break;
				case GameUtil.TemperatureUnit.Kelvin:
				default:
					// No k, f, c for you!
					result = ThermalTooltipsStrings.ALL_TEMPS.F(k, c, f);
					break;
				}
			} else
				// Single unit OR DisplayAllTemps
				result = GameUtil.GetFormattedTemperature(temp);
			return result;
		}
	}
}
