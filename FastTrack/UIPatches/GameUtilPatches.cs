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
using System.Text;
using UnityEngine;

using TimeSlice = GameUtil.TimeSlice;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to GameUtil to reduce the memory allocations of this method.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedMass))]
	public static class GameUtil_GetFormattedMass_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

		/// <summary>
		/// Applied before GetFormattedMass runs.
		/// </summary>
		internal static bool Prefix(float mass, TimeSlice timeSlice,
				GameUtil.MetricMassFormat massFormat, bool includeSuffix, string floatFormat,
				ref string __result) {
			if (float.IsInfinity(mass) || float.IsNaN(mass) || mass == float.MaxValue)
				// Handle inf and NaN
				__result = STRINGS.UI.CALCULATING;
			else {
				// Divide by cycle length if /cycle
				string suffix;
				float absMass = Mathf.Abs(mass);
				mass = GameUtil.ApplyTimeSlice(mass, timeSlice);
				if (includeSuffix) {
					if (GameUtil.massUnit == GameUtil.MassUnit.Kilograms) {
						if (massFormat == GameUtil.MetricMassFormat.UseThreshold) {
							if (absMass > 0.0f) {
								if (absMass < 5E-06f) {
									suffix = STRINGS.UI.UNITSUFFIXES.MASS.MICROGRAM;
									mass = Mathf.Floor(mass * 1.0E+09f);
								} else if (absMass < 0.005f) {
									mass *= 1000000.0f;
									suffix = STRINGS.UI.UNITSUFFIXES.MASS.MILLIGRAM;
								} else if (absMass < 5.0f) {
									mass *= 1000.0f;
									suffix = STRINGS.UI.UNITSUFFIXES.MASS.GRAM;
								} else if (absMass < 5000.0f)
									suffix = STRINGS.UI.UNITSUFFIXES.MASS.KILOGRAM;
								else {
									mass /= 1000.0f;
									suffix = STRINGS.UI.UNITSUFFIXES.MASS.TONNE;
								}
							} else
								suffix = STRINGS.UI.UNITSUFFIXES.MASS.KILOGRAM;
						} else if (massFormat == GameUtil.MetricMassFormat.Kilogram)
							suffix = STRINGS.UI.UNITSUFFIXES.MASS.KILOGRAM;
						else if (massFormat == GameUtil.MetricMassFormat.Gram) {
							mass *= 1000f;
							suffix = STRINGS.UI.UNITSUFFIXES.MASS.GRAM;
						} else if (massFormat == GameUtil.MetricMassFormat.Tonne) {
							mass /= 1000f;
							suffix = STRINGS.UI.UNITSUFFIXES.MASS.TONNE;
						} else
							suffix = STRINGS.UI.UNITSUFFIXES.MASS.TONNE;
					} else {
						mass /= 2.2f;
						if (massFormat == GameUtil.MetricMassFormat.UseThreshold)
							if (absMass < 5.0f && absMass > 0.001f) {
								mass *= 256.0f;
								suffix = STRINGS.UI.UNITSUFFIXES.MASS.DRACHMA;
							} else {
								mass *= 7000.0f;
								suffix = STRINGS.UI.UNITSUFFIXES.MASS.GRAIN;
							}
						else
							suffix = STRINGS.UI.UNITSUFFIXES.MASS.POUND;
					}
				} else {
					suffix = "";
					timeSlice = TimeSlice.None;
				}
				var buffer = new StringBuilder(16);
				if (floatFormat == "{0:0.#}")
					buffer.AppendFormat("{0:F1}", mass);
				else
					buffer.AppendFormat(floatFormat, mass);
				__result = buffer.Append(suffix).AppendTimeSlice(timeSlice).ToString();
			}
			return false;
		}
	}

	/// <summary>
	/// Applied to GameUtil to reduce the memory allocations of this method.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedTemperature))]
	public static class GameUtil_GetFormattedTemperature_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

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

		/// <summary>
		/// Applied before GetFormattedTemperature runs.
		/// </summary>
		internal static bool Prefix(float temp, TimeSlice timeSlice,
				GameUtil.TemperatureInterpretation interpretation, bool displayUnits,
				bool roundInDestinationFormat, ref string __result) {
			var text = new StringBuilder(16);
			if (interpretation == GameUtil.TemperatureInterpretation.Absolute)
				temp = GameUtil.GetConvertedTemperature(temp, roundInDestinationFormat);
			else
				temp = GetConvertedTemperatureDelta(temp);
			temp = GameUtil.ApplyTimeSlice(temp, timeSlice);
			// Handle inf
			if (float.IsPositiveInfinity(temp) || float.IsNaN(temp))
				text.Append(STRINGS.UI.POS_INFINITY);
			else if (float.IsNegativeInfinity(temp))
				text.Append(STRINGS.UI.NEG_INFINITY);
			else if (Mathf.Abs(temp) < 0.1f)
				text.AppendFormat("{0:F4}", temp);
			else
				text.AppendFormat("{0:F1}", temp);
			if (displayUnits)
				text.Append(GameUtil.GetTemperatureUnitSuffix());
			__result = text.AppendTimeSlice(timeSlice).ToString();
			return false;
		}
	}

	/// <summary>
	///  Applied to GameUtil to reduce the memory allocations of this method.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedTime))]
	public static class GameUtil_GetFormattedTime_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

		/// <summary>
		/// Applied before GetFormattedTime runs.
		/// </summary>
		internal static bool Prefix(float seconds, string floatFormat, ref string __result) {
			__result = seconds.ToString(floatFormat) + "s";
			return false;
		}
	}

	/// <summary>
	/// Applied to GameUtil to reduce the memory allocations of this method.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedUnits))]
	public static class GameUtil_GetFormattedUnits_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

		/// <summary>
		/// Applied before GetFormattedUnits runs.
		/// </summary>
		internal static bool Prefix(float units, TimeSlice timeSlice, bool displaySuffix,
				string floatFormatOverride, ref string __result) {
			var text = new StringBuilder(16);
			units = GameUtil.ApplyTimeSlice(units, timeSlice);
			if (!floatFormatOverride.IsNullOrWhiteSpace())
				text.AppendFormat(floatFormatOverride, units);
			else
				text.Append(units.ToStandardString());
			if (displaySuffix)
				text.Append(Mathf.Approximately(units, 1.0f) ? STRINGS.UI.UNITSUFFIXES.UNIT :
					STRINGS.UI.UNITSUFFIXES.UNITS);
			__result = text.AppendTimeSlice(timeSlice).ToString();
			return false;
		}
	}

	/// <summary>
	/// Applied to GameUtil to reduce the memory allocations of this method.
	/// </summary>
	[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetStandardFloat))]
	public static class GameUtil_GetStandardFloat_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

		/// <summary>
		/// Applied before GetStandardFloat runs.
		/// </summary>
		internal static bool Prefix(float f, ref string __result) {
			__result = f.ToStandardString();
			return false;
		}
	}
}
