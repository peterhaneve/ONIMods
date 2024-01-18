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
			[HarmonyPriority(Priority.Low)]
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float calories, TimeSlice timeSlice, bool forceKcal,
					ref string __result) {
				string unit;
				var text = CACHED_BUILDER;
				if (Mathf.Abs(calories) >= 1000.0f || forceKcal) {
					calories *= 0.001f;
					unit = SUFFIXES.CALORIES.KILOCALORIE;
				} else
					unit = SUFFIXES.CALORIES.CALORIE;
				GameUtil.ApplyTimeSlice(calories, timeSlice).ToStandardString(text.Clear());
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float seconds, string formatString, bool forceCycles,
					ref string __result) {
				var text = CACHED_BUILDER;
				if (text.Clear().AppendIfInfinite(seconds))
					text.Append("s");
				else if (forceCycles || Mathf.Abs(seconds) > 100.0f) {
					string tmp = text.AppendSimpleFormat(formatString, seconds / Constants.
						SECONDS_PER_CYCLE).ToString();
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

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedDisease))]
		internal static class GetFormattedDisease_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedDisease runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(byte idx, int units, ref string __result) {
				var text = CACHED_BUILDER;
				if (idx == Sim.InvalidDiseaseIdx || units <= 0)
					__result = STRINGS.UI.OVERLAYS.DISEASE.NO_DISEASE;
				else {
					var disease = Db.Get().Diseases[idx];
					// The color tag does nothing in vanilla...
					text.Clear().Append(disease.Name).Append(" [");
					GetFormattedDiseaseAmount(text, units);
					__result = text.Append(']').ToString();
				}
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedDiseaseAmount),
			typeof(int), typeof(TimeSlice))]
		internal static class GetFormattedDiseaseAmount_Int_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedDiseaseAmount runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(int units, TimeSlice timeSlice, ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedDiseaseAmount(text.Clear(), units, timeSlice);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedDiseaseAmount),
			typeof(long), typeof(TimeSlice))]
		internal static class GetFormattedDiseaseAmount_Long_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedDiseaseAmount runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(long units, TimeSlice timeSlice, ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedDiseaseAmount(text.Clear(), units, timeSlice);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedDistance))]
		internal static class GetFormattedDistance_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedDistance runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float meters, ref string __result) {
				var text = CACHED_BUILDER.Clear();
				float absMeters = Mathf.Abs(meters);
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

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedHeatEnergy))]
		internal static class GetFormattedHeatEnergy_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedHeatEnergy runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float dtu, GameUtil.HeatEnergyFormatterUnit unit,
					ref string __result) {
				var text = CACHED_BUILDER.Clear();
				switch (unit) {
				case GameUtil.HeatEnergyFormatterUnit.DTU_S:
					dtu.ToRyuHardString(text, 0);
					text.Append(SUFFIXES.HEAT.DTU);
					break;
				case GameUtil.HeatEnergyFormatterUnit.KDTU_S:
					(dtu * 0.001f).ToRyuSoftString(text, 2);
					text.Append(SUFFIXES.HEAT.KDTU);
					break;
				default:
					if (Mathf.Abs(dtu) > 1000.0f) {
						(dtu * 0.001f).ToRyuSoftString(text, 2);
						text.Append(SUFFIXES.HEAT.KDTU);
					} else {
						dtu.ToRyuHardString(text, 0);
						text.Append(SUFFIXES.HEAT.DTU);
					}
					break;
				}
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedHeatEnergyRate))]
		internal static class GetFormattedHeatEnergyRate_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedHeatEnergyRate runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float dtu_s, GameUtil.HeatEnergyFormatterUnit unit,
					ref string __result) {
				var text = CACHED_BUILDER;
				string unitText;
				switch (unit) {
				case GameUtil.HeatEnergyFormatterUnit.DTU_S:
					unitText = SUFFIXES.HEAT.DTU_S;
					break;
				case GameUtil.HeatEnergyFormatterUnit.KDTU_S:
					unitText = SUFFIXES.HEAT.KDTU_S;
					break;
				default:
					if (Mathf.Abs(dtu_s) > 1000.0f) {
						dtu_s *= 0.001f;
						unitText = SUFFIXES.HEAT.KDTU_S;
					} else
						unitText = SUFFIXES.HEAT.DTU_S;
					break;
				}
				dtu_s.ToRyuSoftString(text.Clear(), 2);
				__result = text.Append(unitText).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedHighEnergyParticles))]
		internal static class GetFormattedHighEnergyParticles_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedHighEnergyParticles runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float units, TimeSlice timeSlice, bool displayUnits,
					ref string __result) {
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(units))
					GameUtil.ApplyTimeSlice(units, timeSlice).ToRyuSoftString(text, 1);
				if (displayUnits)
					// That is not how particle is spelled
					text.Append(Mathf.Approximately(units, 1.0f) ? SUFFIXES.
						HIGHENERGYPARTICLES.PARTRICLE : SUFFIXES.
						HIGHENERGYPARTICLES.PARTRICLES);
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedInt))]
		internal static class GetFormattedInt_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedInt runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float num, TimeSlice timeSlice, ref string __result) {
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(num))
					RyuFormat.ToString(text, (double)GameUtil.ApplyTimeSlice(num, timeSlice),
						0, RyuFormatOptions.FixedMode | RyuFormatOptions.ThousandsSeparators);
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float joules, string floatFormat, TimeSlice timeSlice,
					ref string __result) {
				var text = CACHED_BUILDER.Clear();
				if (timeSlice == TimeSlice.PerSecond)
					GetFormattedWattage(text, joules);
				else {
					var legend = JOULE_LEGEND;
					joules = GameUtil.ApplyTimeSlice(joules, timeSlice);
					float absJ = Mathf.Abs(joules);
					if (text.AppendIfInfinite(joules))
						text.Append(legend[0]);
					else if (absJ > 1000000.0f)
						text.AppendSimpleFormat(floatFormat, joules * 0.000001f).Append(
							legend[2]);
					else if (absJ > 1000.0f)
						text.AppendSimpleFormat(floatFormat, joules * 0.001f).Append(
							legend[1]);
					else
						text.AppendSimpleFormat(floatFormat, joules).Append(legend[0]);
					text.AppendTimeSlice(timeSlice);
				}
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedMass))]
		internal static class GetFormattedMass_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedMass runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float mass, TimeSlice timeSlice, string floatFormat,
					GameUtil.MetricMassFormat massFormat, bool includeSuffix,
					ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedMass(text.Clear(), mass, timeSlice, massFormat, includeSuffix,
					floatFormat);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedPercent))]
		internal static class GetFormattedPercent_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedPercent runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float percent, TimeSlice timeSlice,
					ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedPercent(text.Clear(), percent, timeSlice);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedPlantGrowth))]
		internal static class GetFormattedPlantGrowth_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedPlantGrowth runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float percent, TimeSlice timeSlice,
					ref string __result) {
				float absP = Mathf.Abs(percent);
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(percent)) {
					int precision;
					percent = GameUtil.ApplyTimeSlice(percent, timeSlice);
					if (absP < 0.1f)
						precision = 2;
					else if (absP < 1.0f)
						precision = 1;
					else
						precision = 0;
					percent.ToRyuSoftString(text, precision);
				}
				__result = text.Append(PCT).Append(' ').Append(SUFFIXES.GROWTH).
					AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedRocketRange))]
		internal static class GetFormattedRocketRange_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedRocketRange runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float range, TimeSlice timeSlice, bool displaySuffix,
					ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedRocketRange(text.Clear(), range, timeSlice, displaySuffix);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedRads))]
		internal static class GetFormattedRads_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedRads runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float rads, TimeSlice timeSlice, ref string __result) {
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(rads))
					GameUtil.ApplyTimeSlice(rads, timeSlice).ToStandardString(text);
				text.Append(SUFFIXES.RADIATION.RADS);
				__result = text.AppendTimeSlice(timeSlice).ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedRoundedJoules))]
		internal static class GetFormattedRoundedJoules_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedRoundedJoules runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float joules, ref string __result) {
				var text = CACHED_BUILDER;
				var legend = JOULE_LEGEND;
				if (text.Clear().AppendIfInfinite(joules))
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float num, TimeSlice timeSlice, string formatString,
					ref string __result) {
				var text = CACHED_BUILDER;
				num = GameUtil.ApplyTimeSlice(num, timeSlice);
				if (!text.Clear().AppendIfInfinite(num)) {
					if (formatString != null)
						AppendSimpleFormat(text, formatString, num);
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float temp, TimeSlice timeSlice,
					GameUtil.TemperatureInterpretation interpretation, bool displayUnits,
					bool roundInDestinationFormat, ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedTemperature(text.Clear(), temp, timeSlice, interpretation,
					displayUnits, roundInDestinationFormat);
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float seconds, string floatFormat, ref string __result) {
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(seconds))
					text.AppendSimpleFormat(floatFormat, seconds);
				__result = text.Append("s").ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetFormattedUnits))]
		internal static class GetFormattedUnits_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetFormattedUnits runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float units, TimeSlice timeSlice, bool displaySuffix,
					string floatFormatOverride, ref string __result) {
				var text = CACHED_BUILDER;
				units = GameUtil.ApplyTimeSlice(units, timeSlice);
				if (!text.Clear().AppendIfInfinite(units)) {
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float watts, GameUtil.WattageFormatterUnit unit,
					bool displayUnits, ref string __result) {
				var text = CACHED_BUILDER;
				GetFormattedWattage(text.Clear(), watts, unit, displayUnits);
				__result = text.ToString();
				return false;
			}
		}

		[HarmonyPatch(typeof(GameUtil), nameof(GameUtil.GetHardnessString))]
		internal static class GetHardnessString_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.CustomStringFormat;

			/// <summary>
			/// Applied before GetHardnessString runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(float f, ref string __result) {
				var text = CACHED_BUILDER;
				if (!text.Clear().AppendIfInfinite(f))
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
			[HarmonyPriority(Priority.Low)]
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
					tcText = text.Append(" (").Append(tcText).Append(')').ToString();
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
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(string name, float count, bool upperName,
					ref string __result) {
				var text = CACHED_BUILDER;
				GetUnitFormattedName(text.Clear(), name, count, upperName);
				__result = text.ToString();
				return false;
			}
		}
	}
}
