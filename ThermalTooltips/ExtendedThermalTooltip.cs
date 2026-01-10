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

using PeterHan.PLib.Core;
using System;
using System.Globalization;
using UnityEngine;

using TEMP_SUFFIXES = STRINGS.UI.UNITSUFFIXES.TEMPERATURE;
using TTS = PeterHan.ThermalTooltips.ThermalTooltipsStrings.UI.THERMALTOOLTIPS;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Displays extended thermal information tooltips on objects in-game.
	/// </summary>
	public sealed class ExtendedThermalTooltip {
		/// <summary>
		/// Numbers over this value become scientific notation for Heat Energy and Thermal
		/// Mass.
		/// </summary>
		private const float SCI_NOTATION = 1E+6f;

		/// <summary>
		/// The hysteresis in each direction when changing states.
		/// </summary>
		internal const float TRANSITION_HYSTERESIS = 3.0f;

		/// <summary>
		/// Automatically reformats the number in TMP-compliant scientific notation if
		/// necessary.
		/// </summary>
		/// <param name="value">The value to format.</param>
		/// <returns>The formatted representation of that value.</returns>
		internal static string DoScientific(float value) {
			string formatted;
			if (value >= SCI_NOTATION || value <= -SCI_NOTATION) {
				string defSci = value.ToString("E3", CultureInfo.InvariantCulture).
					ToLowerInvariant();
				// Reformat by splitting over the "e"
				int index = defSci.IndexOf('e');
				if (index > 0 && int.TryParse(defSci.Substring(index + 1), out int exp))
					formatted = string.Format(TTS.NUM_FORMAT_BIG, defSci.Substring(0, index),
						exp);
				else
					formatted = defSci;
			} else
				formatted = value.ToString(TTS.NUM_FORMAT_SMALL);
			return formatted;
		}

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
		/// The text style to use in the tooltip.
		/// </summary>
		public TextStyleSetting Style { get; set; }

		/// <summary>
		/// Handles compatibility with Better Info Cards.
		/// </summary>
		private readonly BetterInfoCardsCompat bicCompat;

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

		internal ExtendedThermalTooltip(ThermalTooltipsOptions options,
				BetterInfoCardsCompat compat = null) {
			this.options = options ?? throw new ArgumentNullException("options");
			bicCompat = compat;
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
			Tuple<Sprite, Color> pair;
			// Extract the UI preview image
			if (prefab != null && (pair = Def.GetUISprite(prefab)) != null)
				Drawer.DrawIcon(pair.first, pair.second, 22);
			Drawer.DrawText(element.FormatName(oldElementName), Style);
		}

		/// <summary>
		/// Displays thermal information in the current tooltip.
		/// </summary>
		/// <param name="defaultText">The default temperature text.</param>
		/// <param name="element">The element that is being displayed.</param>
		/// <param name="temperature">The element's temperature in K.</param>
		/// <param name="mass">The element's mass in kg.</param>
		/// <param name="insulation">The insulation multiplier for thermal conductivity.</param>
		public void DisplayThermalInfo(Element element, float temperature, float mass,
				float insulation = 1.0f) {
			if (Drawer != null && Style != null) {
				// Ignore SHC <= 0: vacuum, void, neutronium
				if (element != null && (SimDebugView.Instance.GetMode() == OverlayModes.
						Temperature.ID || options.OnlyOnThermalOverlay == false) &&
						element.specificHeatCapacity > 0.0f) {
					string name = STRINGS.UI.StripLinkFormatting(element.name);
					DisplayThermalStats(element, temperature, mass, insulation);
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
		/// <param name="insulation">The insulation multiplier (1.0 is none, 0.01 is insulated tile)</param>
		private void DisplayThermalStats(Element element, float temp, float mass,
				float insulation) {
			float tc = element.thermalConductivity, shc = element.specificHeatCapacity;
			float tMass = GameUtil.GetDisplaySHC(mass * shc), tEnergy = mass * shc * temp;
			string kDTU = STRINGS.UI.UNITSUFFIXES.HEAT.KDTU.text.Trim();
			// Temperature
			Drawer.DrawText(GetTemperatureString(temp), Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Thermal conductivity
			Drawer.DrawText(string.Format(STRINGS.UI.ELEMENTAL.THERMALCONDUCTIVITY.NAME,
				GameUtil.GetFormattedThermalConductivity(tc * insulation)), Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Thermal mass (mass is in kg so result is in kDTU/C)
			bicCompat?.Export(BetterInfoCardsCompat.EXPORT_THERMAL_MASS, tMass);
			Drawer.DrawText(string.Format(TTS.THERMAL_MASS, DoScientific(tMass), kDTU,
				GameUtil.GetTemperatureUnitSuffix()?.Trim()), Style);
			Drawer.NewLine();
			Drawer.DrawIcon(spriteDash);
			// Heat energy in kDTU
			bicCompat?.Export(BetterInfoCardsCompat.EXPORT_HEAT_ENERGY, tEnergy);
			Drawer.DrawText(string.Format(TTS.HEAT_ENERGY, DoScientific(tEnergy), kDTU),
				Style);
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
					Drawer.DrawText(string.Format(TTS.AND_JOIN, GameUtil.GetFormattedPercent(
						100.0f - ratio)), Style);
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
				Drawer.DrawText(TTS.CHANGES, Style);
			Drawer.DrawText(TTS.TO_JOIN, Style);
		}

		/// <summary>
		/// Retrieves the string value of the temperature, in all units if needed.
		/// </summary>
		/// <param name="temp">The temperature to display.</param>
		/// <returns>The display value of that temperature.</returns>
		private string GetTemperatureString(float temp) {
			string result;
			if (options.AllUnits == true) {
				string c = TTS.TEMP_FORMAT.F(GameUtil.GetTemperatureConvertedFromKelvin(temp,
					GameUtil.TemperatureUnit.Celsius), TEMP_SUFFIXES.CELSIUS);
				string f = TTS.TEMP_FORMAT.F(GameUtil.GetTemperatureConvertedFromKelvin(temp,
					GameUtil.TemperatureUnit.Fahrenheit), TEMP_SUFFIXES.FAHRENHEIT);
				string k = TTS.TEMP_FORMAT.F(temp, TEMP_SUFFIXES.KELVIN);
				// Put the user preferred temperature first
				switch (GameUtil.temperatureUnit) {
				case GameUtil.TemperatureUnit.Celsius:
					result = TTS.ALL_TEMPS.F(c, f, k);
					break;
				case GameUtil.TemperatureUnit.Fahrenheit:
					result = TTS.ALL_TEMPS.F(f, c, k);
					break;
				case GameUtil.TemperatureUnit.Kelvin:
				default:
					// No k, f, c for you!
					result = TTS.ALL_TEMPS.F(k, c, f);
					break;
				}
			} else
				// Single unit OR DisplayAllTemps
				result = GameUtil.GetFormattedTemperature(temp);
			return result;
		}
	}
}
