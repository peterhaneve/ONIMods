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
using Ryu;
using System.Text;
using UnityEngine;

using BreathableValues = GameUtil.BreathableValues;
using HARDNESS = STRINGS.ELEMENTS.HARDNESS;
using HardnessValues = GameUtil.Hardness;
using MetricMassFormat = GameUtil.MetricMassFormat;
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

		private static string PCT;

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

		private static string UNIT_FORMATTED;

		private static string UNITS;

		private static readonly string[] WATT_LEGEND = new string[2];

		/// <summary>
		/// For compatibility with the High Precision Temperature mod, add precision to all
		/// temperature values in the Ryu stage.
		/// </summary>
		public static bool ForceHighPrecisionTemperature = false;

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
				else
					buffer.Append(num.ToString(floatFormat));
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
		/// Formats the time into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the time will be stored.</param>
		/// <param name="seconds">The time in seconds.</param>
		/// <param name="format">The string format to use.</param>
		/// <param name="forceCycles">true to always use cycles, or false to use seconds for
		/// short intervals.</param>
		internal static void GetFormattedCycles(StringBuilder text, float seconds,
				string format, bool forceCycles = false) {
			if (text.Clear().AppendIfInfinite(seconds))
				text.Append("s");
			else if (forceCycles || Mathf.Abs(seconds) > 100.0f) {
				// The format is always F1 now in the base game apparently
				string tmp = text.AppendSimpleFormat(format, seconds / Constants.
					SECONDS_PER_CYCLE).ToString();
				text.Clear().Append(STRINGS.UI.FORMATDAY).Replace("{0:F1}", tmp);
			} else {
				seconds.ToRyuHardString(text, 0);
				text.Append("s");
			}
		}

		/// <summary>
		/// Formats the germ quantity into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the germs will be stored.</param>
		/// <param name="germs">The number of germs.</param>
		/// <param name="timeSlice">The time unit, if any.</param>
		internal static void GetFormattedDiseaseAmount(StringBuilder text, long germs,
				TimeSlice timeSlice = TimeSlice.None) {
			// /cycle is broken in vanilla, clay please
			GameUtil.ApplyTimeSlice(germs, timeSlice).ToStandardString(text);
			text.Append(SUFFIXES.DISEASE.UNITS).AppendTimeSlice(timeSlice);
		}

		/// <summary>
		/// Formats the mass into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the mass will be stored.</param>
		/// <param name="mass">The mass in kilograms.</param>
		/// <param name="timeSlice">The time unit, if any.</param>
		/// <param name="massFormat">The mass units to use.</param>
		/// <param name="displaySuffix">Whether to display the units.</param>
		/// <param name="format">The string format to use, or null for the ONI default
		/// (1 decimal place soft).</param>
		internal static void GetFormattedMass(StringBuilder text, float mass,
				TimeSlice timeSlice = TimeSlice.None, MetricMassFormat massFormat =
				MetricMassFormat.UseThreshold, bool displaySuffix = true,
				string format = null) {
			if (float.IsInfinity(mass) || float.IsNaN(mass))
				// Handle inf and NaN
				text.Append(STRINGS.UI.CALCULATING);
			else {
				// Divide by cycle length if /cycle
				LocString suffix;
				var legend = MASS_LEGEND;
				mass = GameUtil.ApplyTimeSlice(mass, timeSlice);
				float absMass = Mathf.Abs(mass);
				if (GameUtil.massUnit == GameUtil.MassUnit.Kilograms) {
					switch (massFormat) {
					case MetricMassFormat.UseThreshold:
						if (absMass > 0.0f) {
							if (absMass < 5E-06f) {
								// ug
								suffix = legend[4];
								mass = Mathf.Floor(mass * 1.0E+09f);
							} else if (absMass < 0.005f) {
								mass *= 1000000.0f;
								// mg
								suffix = legend[3];
							} else if (absMass < 5.0f) {
								mass *= 1000.0f;
								// g
								suffix = legend[2];
							} else if (absMass < 5000.0f)
								// kg
								suffix = legend[1];
							else {
								mass /= 1000.0f;
								// t
								suffix = legend[0];
							}
						} else
							// kg
							suffix = legend[1];
						break;
					case MetricMassFormat.Gram:
						mass *= 1000f;
						// g
						suffix = legend[2];
						break;
					case MetricMassFormat.Tonne:
						mass /= 1000f;
						// t
						suffix = legend[0];
						break;
					case MetricMassFormat.Kilogram:
					default:
						// kg
						suffix = legend[1];
						break;
					}
				} else {
					mass /= 2.2f;
					if (massFormat == MetricMassFormat.UseThreshold)
						if (absMass < 5.0f && absMass > 0.001f) {
							mass *= 256.0f;
							suffix = SUFFIXES.MASS.DRACHMA;
						} else {
							mass *= 7000.0f;
							suffix = SUFFIXES.MASS.GRAIN;
						}
					else
						suffix = SUFFIXES.MASS.POUND;
				}
				// Hardcodes for the most common cases in ONI
				if (format == null || format == "{0:0.#}")
					mass.ToRyuSoftString(text, 1);
				else if (format == "{0:0.##}")
					mass.ToRyuSoftString(text, 2);
				else if (format == "{0:0.###}")
					mass.ToRyuSoftString(text, 3);
				else
					text.AppendFormat(format, mass);
				if (displaySuffix)
					text.Append(suffix).AppendTimeSlice(timeSlice);
			}
		}

		/// <summary>
		/// Formats the percentage into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the percentage will be stored.</param>
		/// <param name="percent">The percentage from 0 to 100 (NOT 0 to 1!)</param>
		/// <param name="timeSlice">The time unit, if any.</param>
		internal static void GetFormattedPercent(StringBuilder text, float percent,
				TimeSlice timeSlice = TimeSlice.None) {
			if (percent == 0.0f)
				// Pretty common, make it fast
				text.Append('0');
			else if (!text.AppendIfInfinite(percent)) {
				int precision;
				percent = GameUtil.ApplyTimeSlice(percent, timeSlice);
				float absP = Mathf.Abs(percent);
				// The base game uses 2dp for anything under 0.1%, 1dp for 0.1-1%, and 0dp
				// for anything higher. Improve UX a bit here by showing an extra place
				if (absP < 0.1f)
					precision = 3;
				else if (absP < 1.0f)
					precision = 2;
				else if (absP < 100.0f)
					precision = 1;
				else
					precision = 0;
				percent.ToRyuSoftString(text, precision);
			}
			text.Append(PCT);
			text.AppendTimeSlice(timeSlice);
		}

		/// <summary>
		/// Formats the rocket range into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the rocket range will be stored.</param>
		/// <param name="range">The rocket range in tiles.</param>
		/// <param name="displaySuffix">Whether to display the units.</param>
		internal static void GetFormattedRocketRange(StringBuilder text, float range,
				bool displaySuffix) {
			// Range cannot be over 999 tiles right now ;)
			range.ToRyuHardString(text, 1);
			if (displaySuffix)
				text.Append(' ').Append(STRINGS.UI.CLUSTERMAP.TILES_PER_CYCLE);
		}

		/// <summary>
		/// Formats the temperature into a string buffer to save on allocations.
		/// </summary>
		/// <param name="text">The location where the temperature will be stored.</param>
		/// <param name="temperature">The temperature in K.</param>
		/// <param name="timeSlice">The time unit, if any.</param>
		/// <param name="interpretation">Whether the temperature is a delta, or an absolute value.</param>
		/// <param name="displayUnits">Whether to display the units.</param>
		/// <param name="roundOff">Whether to round off the temperature to the nearest degree.</param>
		internal static void GetFormattedTemperature(StringBuilder text, float temperature,
				TimeSlice timeSlice = TimeSlice.None, GameUtil.TemperatureInterpretation
				interpretation = GameUtil.TemperatureInterpretation.Absolute,
				bool displayUnits = true, bool roundOff = false) {
			if (interpretation == GameUtil.TemperatureInterpretation.Absolute)
				temperature = GameUtil.GetConvertedTemperature(temperature, roundOff);
			else if (GameUtil.temperatureUnit == GameUtil.TemperatureUnit.Fahrenheit)
				temperature *= 1.8f;
			temperature = GameUtil.ApplyTimeSlice(temperature, timeSlice);
			if (!text.AppendIfInfinite(temperature))
				temperature.ToRyuSoftString(text, Mathf.Abs(temperature) < 0.1f ||
					ForceHighPrecisionTemperature ? 4 : 1);
			if (displayUnits)
				text.Append(GameUtil.GetTemperatureUnitSuffix());
			text.AppendTimeSlice(timeSlice);
		}

		/// <summary>
		/// Formats the wattage as a string.
		/// </summary>
		/// <param name="text">The location where the wattage will be stored.</param>
		/// <param name="watts">The raw wattage in watts.</param>
		/// <param name="unit">The unit to use.</param>
		/// <param name="displayUnits">Whether to display the units.</param>
		/// <returns>The resulting wattage formatted for display.</returns>
		internal static void GetFormattedWattage(StringBuilder text, float watts,
				GameUtil.WattageFormatterUnit unit = GameUtil.WattageFormatterUnit.Automatic,
				bool displayUnits = true) {
			var legend = WATT_LEGEND;
			if (text.AppendIfInfinite(watts))
				// POWER OVERWHELMING
				text.Append(legend[0]);
			else {
				string unitStr;
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
		}

		/// <summary>
		/// Gets a string describing the element's difficulty to dig.
		/// </summary>
		/// <param name="element">The element to display.</param>
		/// <param name="addColor">Whether the color should be added to the displayed hardness.</param>
		/// <returns>A tooltip describing the difficulty to dig.</returns>
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
		/// Gets a string describing the quantity of an item.
		/// 
		/// Warning: This method requires starting with a clean buffer!
		/// </summary>
		/// <param name="text">The location where the quantity will be stored.</param>
		/// <param name="name">The item name.</param>
		/// <param name="count">The quantity of the item.</param>
		/// <param name="upperName">If true, the item name is converted to uppercase.</param>
		internal static void GetUnitFormattedName(StringBuilder text, string name, float count,
				bool upperName = false) {
			string uName = upperName ? StringFormatter.ToUpper(name) : name;
			if (UNIT_FORMATTED == null) {
				if (!text.AppendIfInfinite(count))
					count.ToRyuSoftString(text, 2);
				string units = text.ToString();
				text.Clear().Append(STRINGS.UI.NAME_WITH_UNITS).Replace("{0}", uName).
					Replace("{1}", units);
			} else {
				// Fast path
				text.Append(uName).Append(UNIT_FORMATTED);
				if (!text.AppendIfInfinite(count))
					count.ToRyuSoftString(text, 2);
			}
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
			fmt = STRINGS.UI.NAME_WITH_UNITS;
			if (fmt.StartsWith("{0}") && fmt.EndsWith("{1}"))
				UNIT_FORMATTED = fmt.Substring(3, fmt.Length - 6);
			else
				UNIT_FORMATTED = null;
			EP = STRINGS.ELEMENTS.ELEMENTPROPERTIES.Format("");
			PCT = SUFFIXES.PERCENT;
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
			if (Grid.IsValidCell(cell)) {
				var element = Grid.Element[cell];
				float mass = Grid.Mass[cell];
				__result = strings;
				// If element or mass has changed
				if (element != HoverTextHelper.cachedElement || !Mathf.Approximately(mass,
						HoverTextHelper.cachedMass)) {
					HoverTextHelper.cachedElement = element;
					HoverTextHelper.cachedMass = mass;
					strings[3] = " " + GetBreathableString(element, mass);
					switch (element.id) {
					case SimHashes.Vacuum:
						strings[0] = STRINGS.UI.NA;
						strings[1] = "";
						strings[2] = "";
						break;
					case SimHashes.Unobtanium:
						strings[0] = STRINGS.UI.NEUTRONIUMMASS;
						strings[1] = "";
						strings[2] = "";
						break;
					default:
						UpdateMassStrings(mass, strings);
						break;
					}
				}
			} else
				__result = HoverTextHelper.invalidCellMassStrings;
		}

		/// <summary>
		/// Updates the mass strings in the hover card.
		/// </summary>
		/// <param name="mass">The current mass in kg.</param>
		/// <param name="strings">The strings to update.</param>
		private static void UpdateMassStrings(float mass, string[] strings) {
			var text = CACHED_BUILDER;
			// kg
			int index = 1;
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
			int n = text.Length;
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
			[HarmonyPriority(Priority.Low)]
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float value, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				value.ToRyuHardString(text, 2);
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float value, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				value.ToRyuHardString(text, 0);
				__result = text.ToString();
				return false;
			}
		}
	}
}
