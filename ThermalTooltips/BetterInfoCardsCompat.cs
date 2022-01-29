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

using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;

using TTS = PeterHan.ThermalTooltips.ThermalTooltipsStrings.UI.THERMALTOOLTIPS;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Compatibility with Better Info Cards.
	/// </summary>
	internal sealed class BetterInfoCardsCompat {
		/// <summary>
		/// The assembly name for types in BetterInfoCards.
		/// </summary>
		private const string BIC_ASSEMBLY = "BetterInfoCards";

		/// <summary>
		/// The root namespace for types in BetterInfoCards.
		/// </summary>
		private const string BIC_NAMESPACE = "BetterInfoCards.";

		/// <summary>
		/// The export title for heat energy.
		/// </summary>
		internal const string EXPORT_HEAT_ENERGY = "PeterHan.ThermalTooltips.HeatEnergy";

		/// <summary>
		/// The export title for thermal mass.
		/// </summary>
		internal const string EXPORT_THERMAL_MASS = "PeterHan.ThermalTooltips.ThermalMass";

		/// <summary>
		/// Registers a converter. While this method seems useless, converting the arguments
		/// to the proper Func types for the parameter is required.
		/// </summary>
		/// <param name="registerMethod">The registration method to call.</param>
		/// <param name="title">The title to classify the data exported.</param>
		/// <param name="getValue">The function which converts the exported object to the data type.</param>
		/// <param name="getTextOverride">The function that accepts the original text and
		/// collapsed cards to convert them into the final text.</param>
		/// <typeparam name="T">The type of data that will be converted.</typeparam>
		private static void Register<T>(RegisterMethodFunc registerMethod, string title,
				Func<object, T> getValue, Func<string, List<T>, string> getTextOverride) {
			registerMethod.Invoke(title, getValue, getTextOverride, null);
		}

		/// <summary>
		/// Sums up heat energy and reports the total heat energy.
		/// </summary>
		/// <param name="values">The info cards to add up.</param>
		/// <returns>A string describing their total heat energy.</returns>
		private static string SumHeatEnergy(string _, List<float> values) {
			float sum = 0;
			foreach (var value in values)
				sum += value;
			return string.Format(TTS.HEAT_ENERGY, ExtendedThermalTooltip.DoScientific(sum),
				STRINGS.UI.UNITSUFFIXES.HEAT.KDTU.text.Trim()) + TTS.SUM;
		}

		/// <summary>
		/// Sums up thermal masses and reports the total thermal mass.
		/// </summary>
		/// <param name="values">The info cards to add up.</param>
		/// <returns>A string describing their total thermal mass.</returns>
		private static string SumThermalMass(string _, List<float> values) {
			float sum = 0;
			foreach (var value in values)
				sum += value;
			return string.Format(TTS.THERMAL_MASS, ExtendedThermalTooltip.DoScientific(sum),
				STRINGS.UI.UNITSUFFIXES.HEAT.KDTU.text.Trim(), GameUtil.
				GetTemperatureUnitSuffix()?.Trim()) + TTS.SUM;
		}

		/// <summary>
		/// Converts the exported object to a float.
		/// </summary>
		/// <param name="data">The exported float data.</param>
		/// <returns>The original result, or zero if it was not a float.</returns>
		private static float ObjectToFloat(object data) {
			float value = 0.0f;
			if (data is float newValue)
				value = newValue;
			return value;
		}

		private delegate void ExportMethodFunc(string title, object data);
		private delegate void RegisterMethodFunc(string name, object getValue,
			object getTextOverride, object splitListDefs);

		/// <summary>
		/// The method to call for exporting data.
		/// </summary>
		private readonly ExportMethodFunc exportMethod;

		internal BetterInfoCardsCompat() {
			RegisterMethodFunc addConv = null;
			exportMethod = null;
			try {
				addConv = PPatchTools.GetTypeSafe(BIC_NAMESPACE + "ConverterManager",
					BIC_ASSEMBLY)?.Detour<RegisterMethodFunc>("AddConverterReflect");
				var patchType = PPatchTools.GetTypeSafe(BIC_NAMESPACE + "CollectHoverInfo",
					BIC_ASSEMBLY)?.GetNestedType("GetSelectInfo_Patch", BindingFlags.Static |
					BindingFlags.Instance | PPatchTools.BASE_FLAGS);
				if (patchType != null)
					exportMethod = patchType.Detour<ExportMethodFunc>("Export");
			} catch (AmbiguousMatchException e) {
				PUtil.LogWarning("Exception when loading Better Info Cards compatibility:");
				PUtil.LogExcWarn(e);
			} catch (DetourException e) {
				PUtil.LogWarning("Exception when loading Better Info Cards compatibility:");
				PUtil.LogExcWarn(e);
			}
			if (addConv != null && exportMethod != null)
				try {
					Register(addConv, EXPORT_THERMAL_MASS, ObjectToFloat, SumThermalMass);
					Register(addConv, EXPORT_HEAT_ENERGY, ObjectToFloat, SumHeatEnergy);
					PUtil.LogDebug("Registered Better Info Cards status data handlers");
				} catch (Exception e) {
					PUtil.LogWarning("Exception when registering Better Info Cards compatibility:");
					PUtil.LogExcWarn(e.GetBaseException());
				}
		}

		/// <summary>
		/// Exports data to Better Info Cards. Use right before the DrawText that should be
		/// combined.
		/// </summary>
		/// <param name="title">The title to classify the data exported.</param>
		/// <param name="data">The value to export.</param>
		public void Export(string title, object data) {
			exportMethod?.Invoke(title, data);
		}
	}
}
