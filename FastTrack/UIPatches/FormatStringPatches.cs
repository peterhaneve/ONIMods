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
using PeterHan.PLib.Core;
using Ryu;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using BreathableValues = GameUtil.BreathableValues;
using HARDNESS = STRINGS.ELEMENTS.HARDNESS;
using HardnessValues = GameUtil.Hardness;
using OXYGEN = STRINGS.UI.OVERLAYS.OXYGEN;
using SUFFIXES = STRINGS.UI.UNITSUFFIXES;
using TCADJ = STRINGS.UI.ELEMENTAL.THERMALCONDUCTIVITY.ADJECTIVES;
using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to improve format strings for the UI.
	/// </summary>
	public static partial class FormatStringPatches {
		/// <summary>
		/// Avoid reallocating a new StringBuilder every frame.
		/// </summary>
		private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

		private static readonly string[] BREATHABLE_LEGEND = new string[4];

		private static string EP;

		private static readonly string[] JOULE_LEGEND = new string[3];

		private static readonly string[] MASS_LEGEND = new string[5];

		private static string PER_CYCLE;

		private static string PER_SECOND;

		private static readonly Color[] TC_COLORS = new Color[] {
			GameUtil.ThermalConductivityValues.veryHighConductivityColor,
			GameUtil.ThermalConductivityValues.highConductivityColor,
			GameUtil.ThermalConductivityValues.mediumConductivityColor,
			GameUtil.ThermalConductivityValues.lowConductivityColor,
			GameUtil.ThermalConductivityValues.veryLowConductivityColor,
		};

		private static readonly string[] TC_LEGEND = new string[5];

		private static readonly string[] TC_LEGEND_COLOR = new string[5];

		private static string UNIT;

		private static string UNITS;

		private static readonly string[] WATT_LEGEND = new string[2];

		/// <summary>
		/// Appends a standard infinity string to the buffer if the value is infinite.
		/// </summary>
		/// <param name="buffer">The string builder to append.</param>
		/// <param name="value">The value to append.</param>
		/// <returns>true if the value was infinite, or false otherwise.</returns>
		internal static bool AppendIfInfinite(this StringBuilder buffer, float value) {
			bool infinite = false;
			if (float.IsPositiveInfinity(value)) {
				buffer.Append(STRINGS.UI.POS_INFINITY);
				infinite = true;
			} else if (float.IsNegativeInfinity(value)) {
				buffer.Append(STRINGS.UI.NEG_INFINITY);
				infinite = true;
			}
			return infinite;
		}

		/// <summary>
		/// Appends a formatted float to the buffer, with a no alloc fast case for all of the
		/// "Fx" and "Ex" strings.
		/// </summary>
		/// <param name="buffer">The string builder to append.</param>
		/// <param name="floatFormat">The format to use for the number.</param>
		/// <param name="num">The value to append.</param>
		/// <returns>The string builder.</returns>
		private static StringBuilder AppendSimpleFormat(this StringBuilder buffer,
				string floatFormat, float num) {
			if (floatFormat.Length == 2) {
				char type = floatFormat[0];
				int precision = floatFormat[1] - '0';
				if (type == 'F')
					num.ToRyuHardString(buffer, precision);
				else if (type == 'E')
					RyuFormat.ToString(buffer, (double)num, precision, RyuFormatOptions.
						ExponentialMode);
			} else
				buffer.Append(num.ToString(floatFormat));
			return buffer;
		}

		/// <summary>
		/// Appends the time slice unit (like "/s") to the string buffer. Allocates less than
		/// a string concatenation.
		/// </summary>
		/// <param name="buffer">The string builder to append.</param>
		/// <param name="timeSlice">The time slice unit to use.</param>
		/// <returns>The string builder.</returns>
		internal static StringBuilder AppendTimeSlice(this StringBuilder buffer,
				TimeSlice timeSlice) {
			switch (timeSlice) {
			case TimeSlice.PerSecond:
				buffer.Append(PER_SECOND);
				break;
			case TimeSlice.PerCycle:
				buffer.Append(PER_CYCLE);
				break;
			}
			return buffer;
		}

		/// <summary>
		/// Applies the MassStringsReadOnly patch, removing the Stock Bug Fix patch if present.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void ApplyPatch(Harmony harmony) {
			var targetMethod = typeof(HoverTextHelper).GetMethodSafe(nameof(HoverTextHelper.
				MassStringsReadOnly), true, typeof(int));
			if (targetMethod != null) {
				var patches = Harmony.GetPatchInfo(targetMethod)?.Postfixes;
				if (patches != null)
					// Remove Stock Bug Fix's patch, it cannot be allowed to run
					foreach (var patch in patches)
						if (patch.owner == "PeterHan.StockBugFix") {
							PUtil.LogDebug("Removing Stock Bug Fix patch for mass strings");
							harmony.Unpatch(targetMethod, patch.PatchMethod);
						}
				// Add our patch
				harmony.Patch(targetMethod, prefix: new HarmonyMethod(typeof(
					FormatStringPatches), nameof(MassStringsPrefix)));
			}
		}

		/// <summary>
		/// Clears the static StringFormatter caches that otherwise leak memory after reload.
		/// </summary>
		internal static void DumpStringFormatterCaches() {
			StringFormatter.cachedCombines.Clear();
			StringFormatter.cachedReplacements.Clear();
			StringFormatter.cachedToUppers.Clear();
		}

		/// <summary>
		/// Gets text describing the breathability of an element.
		/// </summary>
		/// <param name="element">The element present.</param>
		/// <param name="mass">The mass of that element.</param>
		/// <returns>A string describing how breathable it is.</returns>
		private static string GetBreathableString(Element element, float mass) {
			string result;
			if (element.IsGas || element.IsVacuum) {
				string[] legends = BREATHABLE_LEGEND;
				if (element.HasTag(GameTags.Breathable)) {
					float optimal = SimDebugView.optimallyBreathable, minimum =
						SimDebugView.minimumBreathable;
					if (mass >= optimal)
						// Very breathable
						result = legends[0];
					else if (mass >= (optimal + minimum) * 0.5f)
						// Breathable
						result = legends[1];
					else if (mass >= minimum)
						// Barely breathable
						result = legends[2];
					else
						// Unbreathable
						result = legends[3];
				} else
					// All other gases: Unbreathable
					result = legends[3];
			} else
				result = "";
			return result;
		}

		/// <summary>
		/// Formats the temperature into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="temperature">The temperature in K.</param>
		/// <param name="timeSlice">The time unit, if any.</param>
		/// <param name="interpretation">Whether the temperature is a delta, or an absolute value.</param>
		/// <param name="displayUnits">Whether to display the units.</param>
		/// <param name="roundOff">Whether to round off the temperature to the nearest degree.</param>
		private static void GetFormattedTemperature(StringBuilder text, float temperature,
				TimeSlice timeSlice = TimeSlice.None, GameUtil.TemperatureInterpretation
				interpretation = GameUtil.TemperatureInterpretation.Absolute,
				bool displayUnits = true, bool roundOff = false) {
			if (interpretation == GameUtil.TemperatureInterpretation.Absolute)
				temperature = GameUtil.GetConvertedTemperature(temperature, roundOff);
			else
				temperature = GetConvertedTemperatureDelta(temperature);
			temperature = GameUtil.ApplyTimeSlice(temperature, timeSlice);
			// Handle inf - rare code path, so no precaching
			if (!text.AppendIfInfinite(temperature))
				temperature.ToRyuSoftString(text, Mathf.Abs(temperature) < 0.1f ? 4 : 1);
			if (displayUnits)
				text.Append(GameUtil.GetTemperatureUnitSuffix());
			text.AppendTimeSlice(timeSlice);
		}

		/// <summary>
		/// Formats the wattage as a string.
		/// </summary>
		/// <param name="watts">The raw wattage in watts.</param>
		/// <param name="unit">The unit to use.</param>
		/// <param name="displayUnits">Whether to display the units.</param>
		/// <returns>The resulting wattage formatted for display.</returns>
		private static string GetFormattedWattage(float watts,
				GameUtil.WattageFormatterUnit unit, bool displayUnits) {
			string unitStr;
			var legend = WATT_LEGEND;
			var text = CACHED_BUILDER;
			text.Clear();
			if (text.AppendIfInfinite(watts))
				// POWER OVERWHELMING
				text.Append(legend[0]);
			else {
				switch (unit) {
				case GameUtil.WattageFormatterUnit.Watts:
					unitStr = legend[0];
					break;
				case GameUtil.WattageFormatterUnit.Kilowatts:
					watts *= 0.001f;
					unitStr = legend[1];
					break;
				case GameUtil.WattageFormatterUnit.Automatic:
				default:
					if (Mathf.Abs(watts) > 1000.0f) {
						watts *= 0.001f;
						unitStr = legend[1];
					} else
						unitStr = legend[0];
					break;
				}
				watts.ToRyuSoftString(text, 2);
				if (displayUnits)
					text.Append(unitStr);
			}
			return text.ToString();
		}

		/// <summary>
		/// Converts the temperature difference from Kelvin to the current display unit.
		/// </summary>
		/// <param name="dt">The change in temperature.</param>
		/// <returns>The change in temperature to display.</returns>
		private static float GetConvertedTemperatureDelta(float dt) {
			float dtConv = dt;
			if (GameUtil.temperatureUnit == GameUtil.TemperatureUnit.Fahrenheit)
				dtConv = dt * 1.8f;
			return dtConv;
		}

		private static string GetHardnessString(Element element, bool addColor) {
			string text;
			if (element.IsSolid) {
				Color c;
				int hardness = element.hardness;
				string hs = hardness.ToString();
				if (hardness >= HardnessValues.IMPENETRABLE) {
					c = HardnessValues.ImpenetrableColor;
					text = HARDNESS.IMPENETRABLE.Format(hs);
				} else if (hardness >= HardnessValues.NEARLY_IMPENETRABLE) {
					c = HardnessValues.nearlyImpenetrableColor;
					text = HARDNESS.NEARLYIMPENETRABLE.Format(hs);
				} else if (hardness >= HardnessValues.VERY_FIRM) {
					c = HardnessValues.veryFirmColor;
					text = HARDNESS.VERYFIRM.Format(hs);
				} else if (hardness >= HardnessValues.FIRM) {
					c = HardnessValues.firmColor;
					text = HARDNESS.FIRM.Format(hs);
				} else if (hardness >= HardnessValues.SOFT) {
					c = HardnessValues.softColor;
					text = HARDNESS.SOFT.Format(hs);
				} else {
					c = HardnessValues.verySoftColor;
					text = HARDNESS.VERYSOFT.Format(hs);
				}
				if (addColor)
					text = "<color=#" + c.ToHexString() + ">" + text + "</color>";
			} else
				text = HARDNESS.NA;
			return text;
		}

		/// <summary>
		/// Initializes the strings, which when resolving LocString.ToString at runtime would
		/// require a relatively expensive Strings.Get.
		/// </summary>
		internal static void Init() {
			string[] legend = BREATHABLE_LEGEND, legendColor = TC_LEGEND_COLOR;
			string fmt = STRINGS.ELEMENTS.BREATHABLEDESC;
			// Breathable legends
			legend[0] = string.Format(fmt, BreathableValues.positiveColor.
				ToHexString(), OXYGEN.LEGEND1);
			legend[1] = string.Format(fmt, BreathableValues.positiveColor.
				ToHexString(), OXYGEN.LEGEND2);
			legend[2] = string.Format(fmt, BreathableValues.warningColor.
				ToHexString(), OXYGEN.LEGEND3);
			legend[3] = string.Format(fmt, BreathableValues.negativeColor.
				ToHexString(), OXYGEN.LEGEND4);
			// Mass legends
			legend = MASS_LEGEND;
			legend[0] = SUFFIXES.MASS.TONNE;
			legend[1] = SUFFIXES.MASS.KILOGRAM;
			legend[2] = SUFFIXES.MASS.GRAM;
			legend[3] = SUFFIXES.MASS.MILLIGRAM;
			legend[4] = SUFFIXES.MASS.MICROGRAM;
			// TC legends
			legend = TC_LEGEND;
			legend[0] = TCADJ.VERY_HIGH_CONDUCTIVITY;
			legend[1] = TCADJ.HIGH_CONDUCTIVITY;
			legend[2] = TCADJ.MEDIUM_CONDUCTIVITY;
			legend[3] = TCADJ.LOW_CONDUCTIVITY;
			legend[4] = TCADJ.VERY_LOW_CONDUCTIVITY;
			for (int i = 0; i < 5; i++) {
				ref var color = ref TC_COLORS[i];
				legendColor[i] = "<color=#" + color.ToHexString() + ">" + legend[i] +
					"</color>";
			}
			// J and W legends
			legend = WATT_LEGEND;
			legend[0] = SUFFIXES.ELECTRICAL.WATT;
			legend[1] = SUFFIXES.ELECTRICAL.KILOWATT;
			legend = JOULE_LEGEND;
			legend[0] = SUFFIXES.ELECTRICAL.JOULE;
			legend[1] = SUFFIXES.ELECTRICAL.KILOJOULE;
			legend[2] = SUFFIXES.ELECTRICAL.MEGAJOULE;
			// All other cached strings
			EP = STRINGS.ELEMENTS.ELEMENTPROPERTIES.Format("");
			PER_CYCLE = SUFFIXES.PERCYCLE;
			PER_SECOND = SUFFIXES.PERSECOND;
			UNIT = SUFFIXES.UNIT;
			UNITS = SUFFIXES.UNITS;
		}

		/// <summary>
		/// Applied before MassStringsReadOnly runs.
		/// </summary>
		internal static void MassStringsPrefix(int cell, ref string[] __result) {
			var strings = HoverTextHelper.massStrings;
			if (!Grid.IsValidCell(cell))
				__result = HoverTextHelper.invalidCellMassStrings;
			else
				__result = strings;
			var element = Grid.Element[cell];
			float mass = Grid.Mass[cell];
			// If element or mass has changed
			if (element != HoverTextHelper.cachedElement || mass != HoverTextHelper.
					cachedMass) {
				HoverTextHelper.cachedElement = element;
				HoverTextHelper.cachedMass = mass;
				strings[3] = " " + GetBreathableString(element, mass);
				if (element.id == SimHashes.Vacuum) {
					strings[0] = STRINGS.UI.NA;
					strings[1] = "";
					strings[2] = "";
				} else if (element.id == SimHashes.Unobtanium) {
					strings[0] = STRINGS.UI.NEUTRONIUMMASS;
					strings[1] = "";
					strings[2] = "";
				} else
					UpdateMassStrings(mass, strings);
			}
		}

		/// <summary>
		/// Updates the mass strings in the hover card.
		/// </summary>
		/// <param name="mass">The current mass in kg.</param>
		/// <param name="strings">The strings to update.</param>
		private static void UpdateMassStrings(float mass, string[] strings) {
			var text = CACHED_BUILDER;
			// kg
			int index = 1, n;
			bool found = false;
			if (mass < 5.0f) {
				// kg => g
				mass *= 1000.0f;
				index = 2;
			}
			if (mass < 5.0f) {
				// g => mg
				mass *= 1000.0f;
				index = 3;
			}
			if (mass < 5.0f) {
				mass = Mathf.Floor(1000.0f * mass);
				index = 4;
			}
			strings[2] = MASS_LEGEND[index];
			text.Clear();
			// Base game hardcodes dots so we will too
			RyuFormat.ToString(text, (double)mass, 1, RyuFormatOptions.FixedMode,
				System.Globalization.CultureInfo.InvariantCulture);
			n = text.Length;
			for (index = 0; index < n && !found; index++)
				if (text[index] == '.') {
					strings[0] = text.ToString(0, index);
					strings[1] = text.ToString(index, n - index);
					found = true;
				}
			if (!found) {
				// Should be unreachable
				strings[0] = text.ToString();
				strings[1] = "";
			}
		}

		/// <summary>
		/// Applied to Util to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(Util), nameof(Util.FormatOneDecimalPlace))]
		internal static class FormatOneDecimalPlace_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before FormatOneDecimalPlace runs.
			/// </summary>
			internal static bool Prefix(float value, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				value.ToRyuHardString(text, 1);
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to Util to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(Util), nameof(Util.FormatTwoDecimalPlace))]
		internal static class FormatTwoDecimalPlace_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before FormatTwoDecimalPlace runs.
			/// </summary>
			internal static bool Prefix(float value, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				value.ToRyuHardString(text, 2);
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to Element to thoroughly reduce the memory consumption of the generated
		/// description.
		/// </summary>
		[HarmonyPatch(typeof(Element), nameof(Element.FullDescription))]
		internal static class FullDescription_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every call.
			/// </summary>
			private static readonly StringBuilder OUTER_BUILDER = new StringBuilder(256);

			private static readonly StringBuilder PART_BUILDER = new StringBuilder(64);

			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before FullDescription runs.
			/// </summary>
			internal static bool Prefix(Element __instance, bool addHardnessColor,
					ref string __result) {
				StringBuilder text = OUTER_BUILDER, part = PART_BUILDER;
				float shc = GameUtil.GetDisplaySHC(__instance.specificHeatCapacity),
					tc = GameUtil.GetDisplayThermalConductivity(__instance.
					thermalConductivity);
				bool vacuum = __instance.IsVacuum;
				var tags = __instance.oreTags;
				var modifiers = __instance.attributeModifiers;
				int n = tags.Length;
				text.Clear();
				text.Append(__instance.Description());
				if (!vacuum) {
					string ht, lt;
					text.AppendLine().AppendLine();
					part.Clear();
					if (__instance.IsSolid) {
						GetFormattedTemperature(part, __instance.highTemp);
						ht = part.ToString();
						part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCSOLID).Replace("{1}",
							ht).Replace("{2}", GetHardnessString(__instance, addHardnessColor));
					} else if (__instance.IsLiquid) {
						GetFormattedTemperature(part, __instance.highTemp);
						ht = part.ToString();
						part.Clear();
						GetFormattedTemperature(part, __instance.lowTemp);
						lt = part.ToString();
						part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCLIQUID).Replace("{1}",
							lt).Replace("{2}", ht);
					} else {
						GetFormattedTemperature(part, __instance.lowTemp);
						lt = part.ToString();
						part.Clear().Append(STRINGS.ELEMENTS.ELEMENTDESCGAS).Replace("{1}",
							lt);
					}
					part.Replace("{0}", __instance.GetMaterialCategoryTag().ProperName());
				}
				// SHC
				part.Clear();
				shc.ToRyuHardString(part, 3);
				string shcText = part.Append(" (DTU/g)/").Append(GameUtil.
					GetTemperatureUnitSuffix()).ToString();
				// TC
				part.Clear();
				tc.ToRyuHardString(part, 3);
				string tcText = part.Append(" (DTU/(m*s))/").Append(GameUtil.
					GetTemperatureUnitSuffix()).ToString();
				part.Clear().Append(STRINGS.ELEMENTS.THERMALPROPERTIES).
					Replace("{SPECIFIC_HEAT_CAPACITY}", shcText).
					Replace("{THERMAL_CONDUCTIVITY}", tcText);
				text.AppendLine().Append(part);
				if (DlcManager.FeatureRadiationEnabled()) {
					part.Clear();
					__instance.radiationAbsorptionFactor.ToRyuSoftString(part, 4);
					string radAbsorb = part.ToString();
					part.Clear();
					(__instance.radiationPer1000Mass * 1.1f).ToRyuSoftString(part, 3);
					string radEmit = part.Append(SUFFIXES.RADIATION.RADS).Append(PER_CYCLE).
						ToString();
					// Could not find this constant in Klei source
					part.Clear().Append(STRINGS.ELEMENTS.RADIATIONPROPERTIES).Replace("{0}",
						radAbsorb).Replace("{1}", radEmit);
					text.AppendLine().Append(part);
				}
				if (n > 0 && !vacuum) {
					text.AppendLine().AppendLine().Append(EP);
					for (int i = 0; i < n; i++) {
						var tag = tags[i];
						text.Append(tag.ProperName());
						if (i < n - 1)
							text.Append(", ");
					}
				}
				n = modifiers.Count;
				for (int i = 0; i < n; i++) {
					var modifier = modifiers[i];
					string name = Db.Get().BuildingAttributes.Get(modifier.AttributeId).Name;
					text.AppendLine().Append(STRINGS.UI.PRE_KEYWORD).Append(name).Append(
						STRINGS.UI.PST_KEYWORD).Append(": ").Append(modifier.
						GetFormattedString());
				}
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to Util to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(Util), nameof(Util.FormatWholeNumber))]
		internal static class FormatWholeNumber_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before FormatWholeNumber runs.
			/// </summary>
			internal static bool Prefix(float value, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				value.ToRyuHardString(text, 0);
				__result = text.ToString();
				return false;
			}
		}
	}

	/// <summary>
	/// Applied to StringFormatter to reduce wasted memory on 3-way sound event combines
	/// where the second parameter is always a "_".
	/// </summary>
	[HarmonyPatch(typeof(StringFormatter), nameof(StringFormatter.Combine), typeof(string),
		typeof(string), typeof(string))]
	public static class StringFormatter_Combine3_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

		/// <summary>
		/// Applied before Combine runs.
		/// </summary>
		internal static bool Prefix(string a, string b, string c, ref string __result) {
			bool cont = b != "_";
			if (!cont) {
				var cached = StringFormatter.cachedCombines;
				if (!cached.TryGetValue(a, out Dictionary<string, string> dictionary))
					cached[a] = dictionary = new Dictionary<string, string>(8);
				if (!dictionary.TryGetValue(c, out string text))
					dictionary[c] = text = a + "_" + b;
				__result = text;
			}
			return cont;
		}
	}

	/// <summary>
	/// Applied to StringFormatter to reduce wasted memory on 4-way sound event combines
	/// where the third parameter is always a "_" and first is always "DupVoc_".
	/// </summary>
	[HarmonyPatch(typeof(StringFormatter), nameof(StringFormatter.Combine), typeof(string),
		typeof(string), typeof(string), typeof(string))]
	public static class StringFormatter_Combine4_Patch {
		private const string PREFIX = "DupVoc_";

		internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

		/// <summary>
		/// Applied before Combine runs.
		/// </summary>
		internal static bool Prefix(string a, string b, string c, string d,
				ref string __result) {
			bool cont = c != "_" || a != PREFIX;
			if (!cont) {
				var cached = StringFormatter.cachedCombines;
				if (!cached.TryGetValue(b, out Dictionary<string, string> dictionary))
					cached[b] = dictionary = new Dictionary<string, string>(64);
				if (!dictionary.TryGetValue(d, out string text))
					dictionary[d] = text = PREFIX + b + "_" + d;
				__result = text;
			}
			return cont;
		}
	}
}
