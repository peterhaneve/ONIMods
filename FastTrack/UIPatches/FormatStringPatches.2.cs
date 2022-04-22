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
using Ryu;
using UnityEngine;

using SUFFIXES = STRINGS.UI.UNITSUFFIXES;
using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to improve format strings for the UI.
	/// </summary>
	public static partial class FormatStringPatches {
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetBreathableString))]
		internal static class GetBreathableString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetBreathableString runs.
			/// </summary>
			internal static bool Prefix(Element element, float Mass, ref string __result) {
				__result = GetBreathableString(element, Mass);
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedCalories))]
		internal static class GetFormattedCalories_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedCalories runs.
			/// </summary>
			internal static bool Prefix(float calories, TimeSlice timeSlice, bool forceKcal,
					ref string __result) {
				string unit;
				var text = CACHED_BUILDER;
				text.Clear();
				if (Mathf.Abs(calories) >= 1000.0f || forceKcal) {
					calories *= 0.001f;
					unit = SUFFIXES.CALORIES.KILOCALORIE;
				} else
					unit = SUFFIXES.CALORIES.CALORIE;
				GameUtil.ApplyTimeSlice(calories, timeSlice).ToStandardString(text);
				text.Append(unit);
				text.AppendTimeSlice(timeSlice);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedCycles))]
		internal static class GetFormattedCycles_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedCycles runs.
			/// </summary>
			internal static bool Prefix(float seconds, string formatString, bool forceCycles,
					ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				if (text.AppendIfInfinite(seconds))
					text.Append("s");
				else if (forceCycles || Mathf.Abs(seconds) > 100.0f) {
					text.AppendSimpleFormat(formatString, seconds / Constants.
						SECONDS_PER_CYCLE);
					string tmp = text.ToString();
					text.Clear();
					text.Append(STRINGS.UI.FORMATDAY);
					text.Replace("{0}", tmp);
				} else {
					seconds.ToRyuHardString(text, 0);
					text.Append("s");
				}
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedDistance))]
		internal class GetFormattedDistance_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedDistance runs.
			/// </summary>
			internal static bool Prefix(float meters, ref string __result) {
				var text = CACHED_BUILDER;
				float absMeters = Mathf.Abs(meters);
				text.Clear();
				if (absMeters < 1.0f) {
					(meters * 100.0f).ToRyuSoftString(text, 2);
					// Hardcoded in original ONI source and no LocString for it :(
					text.Append(" cm");
				} else if (absMeters < 1000.0f) {
					meters.ToRyuSoftString(text, 3);
					text.Append(SUFFIXES.DISTANCE.METER);
				} else if (!text.AppendIfInfinite(meters)) {
					(meters * 0.001f).ToRyuSoftString(text, 1);
					text.Append(SUFFIXES.DISTANCE.KILOMETER);
				}
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedInt))]
		internal static class GetFormattedInt_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedInt runs.
			/// </summary>
			internal static bool Prefix(float num, TimeSlice timeSlice, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				num = GameUtil.ApplyTimeSlice(num, timeSlice);
				if (!text.AppendIfInfinite(num))
					RyuFormat.ToString(text, (double)num, 0, RyuFormatOptions.FixedMode |
						RyuFormatOptions.ThousandsSeparators);
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedJoules))]
		internal static class GetFormattedJoules_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedJoules runs.
			/// </summary>
			internal static bool Prefix(float joules, string floatFormat, TimeSlice timeSlice,
					ref string __result) {
				var text = CACHED_BUILDER;
				if (timeSlice == TimeSlice.PerSecond)
					__result = GetFormattedWattage(joules, GameUtil.WattageFormatterUnit.
						Automatic, true);
				else {
					var legend = JOULE_LEGEND;
					float absJ = Mathf.Abs(joules);
					text.Clear();
					joules = GameUtil.ApplyTimeSlice(joules, timeSlice);
					if (text.AppendIfInfinite(joules))
						text.Append(legend[0]);
					else if (absJ > 1000000.0f) {
						text.AppendSimpleFormat(floatFormat, joules * 0.000001f);
						text.Append(legend[2]);
					} else if (absJ > 1000.0f) {
						text.AppendSimpleFormat(floatFormat, joules * 0.001f);
						text.Append(legend[1]);
					} else {
						text.AppendSimpleFormat(floatFormat, joules);
						text.Append(legend[0]);
					}
					__result = text.AppendTimeSlice(timeSlice).ToString();
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedMass))]
		internal static class GetFormattedMass_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedMass runs.
			/// </summary>
			internal static bool Prefix(float mass, TimeSlice timeSlice, string floatFormat,
					GameUtil.MetricMassFormat massFormat, bool includeSuffix,
					ref string __result) {
				if (float.IsInfinity(mass) || float.IsNaN(mass) || mass == float.MaxValue)
					// Handle inf and NaN
					__result = STRINGS.UI.CALCULATING;
				else {
					// Divide by cycle length if /cycle
					LocString suffix;
					float absMass = Mathf.Abs(mass);
					var legend = MASS_LEGEND;
					mass = GameUtil.ApplyTimeSlice(mass, timeSlice);
					if (GameUtil.massUnit == GameUtil.MassUnit.Kilograms) {
						if (massFormat == GameUtil.MetricMassFormat.UseThreshold) {
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
						} else if (massFormat == GameUtil.MetricMassFormat.Kilogram)
							// kg
							suffix = legend[1];
						else if (massFormat == GameUtil.MetricMassFormat.Gram) {
							mass *= 1000f;
							// g
							suffix = legend[2];
						} else if (massFormat == GameUtil.MetricMassFormat.Tonne) {
							mass /= 1000f;
							// t
							suffix = legend[0];
						} else
							// t
							suffix = legend[0];
					} else {
						mass /= 2.2f;
						if (massFormat == GameUtil.MetricMassFormat.UseThreshold)
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
					var sb = CACHED_BUILDER;
					sb.Clear();
					// Hardcodes for the most common cases in ONI
					if (floatFormat == "{0:0.#}")
						mass.ToRyuSoftString(sb, 1);
					else if (floatFormat == "{0:0.##}")
						mass.ToRyuSoftString(sb, 2);
					else if (floatFormat == "{0:0.###}")
						mass.ToRyuSoftString(sb, 3);
					else
						sb.AppendFormat(floatFormat, mass);
					if (includeSuffix)
						sb.Append(suffix).AppendTimeSlice(timeSlice);
					__result = sb.ToString();
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedPercent))]
		internal static class GetFormattedPercent_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedPercent runs.
			/// </summary>
			internal static bool Prefix(float percent, TimeSlice timeSlice,
					ref string __result) {
				float absP = Mathf.Abs(percent);
				int precision;
				var text = CACHED_BUILDER;
				text.Clear();
				if (!text.AppendIfInfinite(percent)) {
					percent = GameUtil.ApplyTimeSlice(percent, timeSlice);
					if (absP < 0.1f)
						precision = 2;
					else if (absP < 1.0f)
						precision = 1;
					else
						precision = 0;
					percent.ToRyuSoftString(text, precision);
				}
				text.Append(SUFFIXES.PERCENT);
				text.AppendTimeSlice(timeSlice);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedRoundedJoules))]
		internal static class GetFormattedRoundedJoules_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedRoundedJoules runs.
			/// </summary>
			internal static bool Prefix(float joules, ref string __result) {
				var text = CACHED_BUILDER;
				var legend = JOULE_LEGEND;
				text.Clear();
				if (text.AppendIfInfinite(joules))
					text.Append(legend[0]);
				else if (Mathf.Abs(joules) > 1000.0f) {
					(joules * 0.001f).ToRyuHardString(text, 1);
					text.Append(legend[1]);
				} else {
					joules.ToRyuHardString(text, 1);
					text.Append(legend[0]);
				}
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedSimple))]
		internal static class GetFormattedSimple_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedSimple runs.
			/// </summary>
			internal static bool Prefix(float num, TimeSlice timeSlice, string formatString,
					ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				num = GameUtil.ApplyTimeSlice(num, timeSlice);
				if (!text.AppendIfInfinite(num)) {
					if (formatString != null)
						text.AppendFormat(formatString, num);
					else if (num == 0.0f)
						text.Append('0');
					else
						RyuFormat.ToString(text, (double)num, 2, RyuFormatOptions.FixedMode |
							RyuFormatOptions.SoftPrecision | RyuFormatOptions.
							ThousandsSeparators);
				}
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedTemperature))]
		internal static class GetFormattedTemperature_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedTemperature runs.
			/// </summary>
			internal static bool Prefix(float temp, TimeSlice timeSlice,
					GameUtil.TemperatureInterpretation interpretation, bool displayUnits,
					bool roundInDestinationFormat, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				GetFormattedTemperature(text, temp, timeSlice, interpretation, displayUnits,
					roundInDestinationFormat);
				__result = text.ToString();
				return false;
			}
		}

		/// <summary>
		///  Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedTime))]
		internal static class GetFormattedTime_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedTime runs.
			/// </summary>
			internal static bool Prefix(float seconds, string floatFormat, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				if (!text.AppendIfInfinite(seconds))
					text.AppendSimpleFormat(floatFormat, seconds);
				text.Append("s");
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedUnits))]
		internal static class GetFormattedUnits_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedUnits runs.
			/// </summary>
			internal static bool Prefix(float units, TimeSlice timeSlice, bool displaySuffix,
					string floatFormatOverride, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				units = GameUtil.ApplyTimeSlice(units, timeSlice);
				if (!text.AppendIfInfinite(units)) {
					if (!floatFormatOverride.IsNullOrWhiteSpace())
						text.AppendFormat(floatFormatOverride, units);
					else
						units.ToStandardString(text);
				}
				if (displaySuffix)
					text.Append(Mathf.Approximately(units, 1.0f) ? UNIT : UNITS);
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedWattage))]
		internal static class GetFormattedWattage_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedWattage runs.
			/// </summary>
			internal static bool Prefix(float watts, GameUtil.WattageFormatterUnit unit,
					bool displayUnits, ref string __result) {
				__result = GetFormattedWattage(watts, unit, displayUnits);
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetHardnessString))]
		internal static class GetHardnessString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetHardnessString runs.
			/// </summary>
			internal static bool Prefix(Element element, bool addColor, ref string __result) {
				__result = GetHardnessString(element, addColor);
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetStandardFloat))]
		internal static class GetStandardFloat_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetStandardFloat runs.
			/// </summary>
			internal static bool Prefix(float f, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				if (!text.AppendIfInfinite(f))
					f.ToStandardString(text);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetThermalConductivityString))]
		internal static class GetThermalConductivityString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetThermalConductivityString runs.
			/// </summary>
			internal static bool Prefix(Element element, bool addColor, bool addValue,
					ref string __result) {
				int index;
				if (element.thermalConductivity >= 50f)
					index = 0;
				else if (element.thermalConductivity >= 10f)
					index = 1;
				else if (element.thermalConductivity >= 2f)
					index = 2;
				else if (element.thermalConductivity >= 1f)
					index = 3;
				else
					index = 4;
				string tcText = addColor ? TC_LEGEND_COLOR[index] : TC_LEGEND[index];
				if (addValue) {
					var text = CACHED_BUILDER;
					float tc = element.thermalConductivity;
					text.Clear();
					// This is technically a LocString but we hope that it is the same...
					if (!text.AppendIfInfinite(tc))
						tc.ToRyuSoftString(text, 7);
					text.Append(" (");
					text.Append(tcText);
					text.Append(')');
					tcText = text.ToString();
				}
				__result = tcText;
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetUnitFormattedName), typeof(string),
			typeof(float), typeof(bool))]
		internal static class GetUnitFormattedName_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetUnitFormattedName runs.
			/// </summary>
			internal static bool Prefix(string name, float count, bool upperName,
					ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				if (!text.AppendIfInfinite(count))
					count.ToRyuSoftString(text, 2);
				string units = text.ToString();
				text.Clear();
				text.Append(STRINGS.UI.NAME_WITH_UNITS);
				text.Replace("{0}", upperName ? StringFormatter.ToUpper(name) : name);
				text.Replace("{1}", units);
				__result = text.ToString();
				return false;
			}
		}
	}
}
