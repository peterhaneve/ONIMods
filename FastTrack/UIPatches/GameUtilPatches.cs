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

using BreathableValues = GameUtil.BreathableValues;
using HardnessValues = GameUtil.Hardness;
using HARDNESS = STRINGS.ELEMENTS.HARDNESS;
using OXYGEN = STRINGS.UI.OVERLAYS.OXYGEN;
using SUFFIXES = STRINGS.UI.UNITSUFFIXES;
using TimeSlice = GameUtil.TimeSlice;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Groups patches to the GameUtil class which formats strings for the UI.
	/// </summary>
	public static class GameUtilPatches {
		private static readonly string[] BREATHABLE_LEGEND = new string[4];

		private static string PER_CYCLE;

		private static string PER_SECOND;

		private static string UNIT;

		private static string UNITS;

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
		/// Initializes the strings, which when resolving LocString.ToString at runtime would
		/// require a relatively expensive Strings.Get.
		/// </summary>
		internal static void Init() {
			string[] legend = BREATHABLE_LEGEND;
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
			PER_CYCLE = SUFFIXES.PERCYCLE;
			PER_SECOND = SUFFIXES.PERSECOND;
			UNIT = SUFFIXES.UNIT;
			UNITS = SUFFIXES.UNITS;
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetBreathableString))]
		internal static class GetBreathableString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Applied before GetBreathableString runs.
			/// </summary>
			internal static bool Prefix(Element element, float Mass, ref string __result) {
				string result;
				if (element.IsGas || element.IsVacuum) {
					string[] legends = BREATHABLE_LEGEND;
					if (element.HasTag(GameTags.Breathable)) {
						float optimal = SimDebugView.optimallyBreathable, minimum =
							SimDebugView.minimumBreathable;
						if (Mass >= optimal)
							// Very breathable
							result = legends[0];
						else if (Mass >= (optimal + minimum) * 0.5f)
							// Breathable
							result = legends[1];
						else if (Mass >= minimum)
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
				__result = result;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedMass))]
		internal static class GetFormattedMass_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every frame. GetFormattedMass should
			/// only be used on the foreground thread anyways.
			/// </summary>
			private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

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
					mass = GameUtil.ApplyTimeSlice(mass, timeSlice);
					if (GameUtil.massUnit == GameUtil.MassUnit.Kilograms) {
						if (massFormat == GameUtil.MetricMassFormat.UseThreshold) {
							if (absMass > 0.0f) {
								if (absMass < 5E-06f) {
									suffix = SUFFIXES.MASS.MICROGRAM;
									mass = Mathf.Floor(mass * 1.0E+09f);
								} else if (absMass < 0.005f) {
									mass *= 1000000.0f;
									suffix = SUFFIXES.MASS.MILLIGRAM;
								} else if (absMass < 5.0f) {
									mass *= 1000.0f;
									suffix = SUFFIXES.MASS.GRAM;
								} else if (absMass < 5000.0f)
									suffix = SUFFIXES.MASS.KILOGRAM;
								else {
									mass /= 1000.0f;
									suffix = SUFFIXES.MASS.TONNE;
								}
							} else
								suffix = SUFFIXES.MASS.KILOGRAM;
						} else if (massFormat == GameUtil.MetricMassFormat.Kilogram)
							suffix = SUFFIXES.MASS.KILOGRAM;
						else if (massFormat == GameUtil.MetricMassFormat.Gram) {
							mass *= 1000f;
							suffix = SUFFIXES.MASS.GRAM;
						} else if (massFormat == GameUtil.MetricMassFormat.Tonne) {
							mass /= 1000f;
							suffix = SUFFIXES.MASS.TONNE;
						} else
							suffix = SUFFIXES.MASS.TONNE;
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
					sb.AppendFormat(floatFormat, mass);
					if (includeSuffix)
						sb.Append(suffix).AppendTimeSlice(timeSlice);
					__result = sb.ToString();
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedTemperature))]
		internal static class GetFormattedTemperature_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every frame. GetFormattedTemperature
			/// should only be used on the foreground thread anyways.
			/// </summary>
			private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

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
				var text = CACHED_BUILDER;
				text.Clear();
				if (interpretation == GameUtil.TemperatureInterpretation.Absolute)
					temp = GameUtil.GetConvertedTemperature(temp, roundInDestinationFormat);
				else
					temp = GetConvertedTemperatureDelta(temp);
				temp = GameUtil.ApplyTimeSlice(temp, timeSlice);
				// Handle inf - rare code path, so no precaching
				if (float.IsPositiveInfinity(temp) || float.IsNaN(temp))
					text.Append(STRINGS.UI.POS_INFINITY);
				else if (float.IsNegativeInfinity(temp))
					text.Append(STRINGS.UI.NEG_INFINITY);
				else if (Mathf.Abs(temp) < 0.1f)
					text.AppendFormat("{0:##0.####}", temp);
				else
					text.AppendFormat("{0:##0.#}", temp);
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
		internal static class GetFormattedTime_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Applied before GetFormattedTime runs.
			/// </summary>
			internal static bool Prefix(float seconds, string floatFormat, ref string __result)
			{
				__result = seconds.ToString(floatFormat) + "s";
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedUnits))]
		internal static class GetFormattedUnits_Patch {
			/// <summary>
			/// Avoid reallocating a new StringBuilder every frame. GetFormattedUnits should
			/// only be used on the foreground thread anyways.
			/// </summary>
			private static readonly StringBuilder CACHED_BUILDER = new StringBuilder(32);

			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Applied before GetFormattedUnits runs.
			/// </summary>
			internal static bool Prefix(float units, TimeSlice timeSlice, bool displaySuffix,
					string floatFormatOverride, ref string __result) {
				var text = CACHED_BUILDER;
				text.Clear();
				units = GameUtil.ApplyTimeSlice(units, timeSlice);
				if (!floatFormatOverride.IsNullOrWhiteSpace())
					text.AppendFormat(floatFormatOverride, units);
				else
					text.Append(units.ToStandardString());
				if (displaySuffix)
					text.Append(Mathf.Approximately(units, 1.0f) ? UNIT : UNITS);
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetHardnessString))]
		internal static class GetHardnessString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.InfoCardOpts;

			/// <summary>
			/// Applied before GetHardnessString runs.
			/// </summary>
			internal static bool Prefix(Element element, bool addColor, ref string __result) {
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
				__result = text;
				return false;
			}
		}

		/// <summary>
		/// Applied to GameUtil to reduce the memory allocations of this method.
		/// </summary>
		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetStandardFloat))]
		internal static class GetStandardFloat_Patch {
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
}
