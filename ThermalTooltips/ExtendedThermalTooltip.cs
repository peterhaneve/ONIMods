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
using System.Collections.Generic;
using UnityEngine;

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
		/// The hover text drawer where the information will be written.
		/// </summary>
		public HoverTextDrawer Drawer { get; set; }

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
			Drawer = null;
			Style = null;
			spriteDash = Assets.GetSprite("dash");
			spriteCold = Assets.GetSprite("crew_state_temp_down");
			spriteHot = Assets.GetSprite("crew_state_temp_up");
		}

		/// <summary>
		/// Called when thermal information needs to be displayed.
		/// </summary>
		/// <param name="defaultText">The default temperature text.</param>
		/// <param name="element">The element that is being displayed.</param>
		/// <param name="temperature">The element's temperature in K.</param>
		/// <param name="mass">The element's mass in kg.</param>
		public void AddThermalInfo(Element element, float temperature, float mass) {
			if (Drawer != null && Style != null) {
				// Ignore SHC <= 0: vacuum, void, neutronium
				if (element != null && (SimDebugView.Instance.GetMode() == OverlayModes.
						Temperature.ID || options?.OnlyOnThermalOverlay == false) &&
						element.specificHeatCapacity > 0.0f) {
					DisplayThermalStats(element, temperature, mass);
					var coldElement = element.lowTempTransition;
					// Freeze to
					if (coldElement.IsValidTransition(element)) {
						PrepareStateChange(spriteCold);
						DisplayElement(coldElement, Math.Max(0.1f, element.lowTemp -
							TRANSITION_HYSTERESIS));
					}
					var hotElement = element.highTempTransition;
					// Boil to
					if (hotElement.IsValidTransition(element)) {
						PrepareStateChange(spriteHot);
						DisplayElement(hotElement, element.highTemp + TRANSITION_HYSTERESIS);
					}
				} else
					// Not displayed in this mode
					DisplayTemperature(temperature);
			}
		}

		/// <summary>
		/// Displays the information about the element transition temperature.
		/// </summary>
		/// <param name="newElement">The element to which it transitions.</param>
		/// <param name="temp">The temperature when it occurs.</param>
		private void DisplayElement(Element newElement, float temp) {
			var prefab = Assets.GetPrefab(newElement.tag);
			Tuple<Sprite, Color> pair;
			// Extract the UI preview image
			if (prefab != null && (pair = Def.GetUISprite(prefab)) != null)
				Drawer.DrawIcon(pair.first, pair.second);
			Drawer.DrawText("{0:##0.#} ({1})".F(newElement.name, GameUtil.
				GetFormattedTemperature(temp)), Style);
		}

		/// <summary>
		/// Displays the temperature, in all units if needed.
		/// </summary>
		/// <param name="temp">The temperature to display.</param>
		private void DisplayTemperature(float temp) {
			if (options?.AllUnits == true) {
				float f = GameUtil.GetTemperatureConvertedFromKelvin(temp, GameUtil.
					TemperatureUnit.Fahrenheit);
				float c = GameUtil.GetTemperatureConvertedFromKelvin(temp, GameUtil.
					TemperatureUnit.Celsius);
				Drawer.DrawText("{0:##0.#}{3} / {1:##0.#}{4} / {2:##0.#}{5}".F(c, f, temp,
					STRINGS.UI.UNITSUFFIXES.TEMPERATURE.CELSIUS,
					STRINGS.UI.UNITSUFFIXES.TEMPERATURE.FAHRENHEIT,
					STRINGS.UI.UNITSUFFIXES.TEMPERATURE.KELVIN), Style);
			} else
				Drawer.DrawText(GameUtil.GetFormattedTemperature(temp), Style);
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
			DisplayTemperature(temp);
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
		/// Displays the tooltip text for phase change.
		/// </summary>
		/// <param name="sprite">The sprite to display for the phase change.</param>
		private void PrepareStateChange(Sprite sprite) {
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			if (sprite != null)
				Drawer.DrawIcon(sprite);
			else
				Drawer.DrawText(ThermalTooltipsStrings.CHANGES, Style);
			Drawer.DrawText(ThermalTooltipsStrings.TO_JOIN, Style);
		}
	}
}
