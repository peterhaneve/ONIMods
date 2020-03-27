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
using System.Reflection;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Compatibility with Better Info Cards.
	/// </summary>
	internal sealed class BetterInfoCardsCompat {
		/// <summary>
		/// The assembly qualified name for types in BetterInfoCards, including the root
		/// namespace.
		/// </summary>
		private static string BIC_ASSEMBLY = "BetterInfoCards.{0}, BetterInfoCards";

		/// <summary>
		/// The export title for heat energy.
		/// </summary>
		internal const string EXPORT_HEAT_ENERGY = "PeterHan.ThermalTooltips.HeatEnergy";

		/// <summary>
		/// The export title for thermal mass.
		/// </summary>
		internal const string EXPORT_THERMAL_MASS = "PeterHan.ThermalTooltips.ThermalMass";

		/// <summary>
		/// Sums up heat energy and reports the total heat energy.
		/// </summary>
		/// <param name="values">The info cards to add up.</param>
		/// <returns>A string describing their total heat energy.</returns>
		private static string SumHeatEnergy(string _, List<float> values) {
			float sum = 0;
			foreach (var value in values)
				sum += value;
			return string.Format(ThermalTooltipsStrings.HEAT_ENERGY, sum, STRINGS.UI.
				UNITSUFFIXES.HEAT.KDTU.text.Trim()) + ThermalTooltipsStrings.SUM;
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
			return string.Format(ThermalTooltipsStrings.THERMAL_MASS, sum, STRINGS.UI.
				UNITSUFFIXES.HEAT.KDTU.text.Trim(), GameUtil.GetTemperatureUnitSuffix()?.
				Trim()) + ThermalTooltipsStrings.SUM;
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

		/// <summary>
		/// The method to call for exporting data.
		/// </summary>
		private readonly MethodInfo exportMethod;

		/// <summary>
		/// The method to call for registering a converter.
		/// </summary>
		private readonly MethodInfo registerMethod;

		internal BetterInfoCardsCompat() {
			MethodInfo addConv = null, exportData = null;
			try {
				addConv = Type.GetType(BIC_ASSEMBLY.F("ConverterManager"))?.GetMethodSafe(
					"AddConverter", true, PPatchTools.AnyArguments);
				exportData = Type.GetType(BIC_ASSEMBLY.F("CollectHoverInfo"))?.
					GetNestedType("GetSelectInfo_Patch", BindingFlags.Static | BindingFlags.
					Instance | BindingFlags.NonPublic | BindingFlags.Public)?.
					GetMethodSafe("Export", true, typeof(string), typeof(object));
			} catch (TargetInvocationException e) {
				PUtil.LogWarning("Exception when loading Better Info Cards compatibility:");
				PUtil.LogExcWarn(e?.GetBaseException() ?? e);
			} catch (AmbiguousMatchException e) {
				PUtil.LogWarning("Exception when loading Better Info Cards compatibility:");
				PUtil.LogExcWarn(e);
			}
			if (addConv != null && exportData != null)
				PUtil.LogDebug("Registering Better Info Cards status data handlers");
			registerMethod = addConv;
			exportMethod = exportData;
			try {
				Register(EXPORT_THERMAL_MASS, ObjectToFloat, SumThermalMass);
				Register(EXPORT_HEAT_ENERGY, ObjectToFloat, SumHeatEnergy);
			} catch (TargetInvocationException e) {
				PUtil.LogWarning("Exception when registering Better Info Cards compatibility:");
				PUtil.LogExcWarn(e?.GetBaseException() ?? e);
			}
		}

		/// <summary>
		/// Exports data to Better Info Cards. Use right before the DrawText that should be
		/// combined.
		/// </summary>
		/// <param name="title">The title to classify the data exported.</param>
		/// <param name="data">The value to export.</param>
		public void Export(string title, object data) {
			exportMethod?.Invoke(null, new object[] { title, data });
		}

		/// <summary>
		/// Registers a converter.
		/// </summary>
		/// <param name="title">The title to classify the data exported.</param>
		/// <param name="getValue">The function which converts the exported object to the data type.</param>
		/// <param name="getTextOverride">The function that accepts the original text and collapsed cards to convert them into the final text.</param>
		/// <typeparam name="T">The type of data that will be converted.</typeparam>
		public void Register<T>(string title, Func<object, T> getValue, Func<string, List<T>,
				string> getTextOverride) {
			registerMethod?.MakeGenericMethod(typeof(T))?.Invoke(null, new object[] {
				title, getValue, getTextOverride, null
			});
		}
	}
}
