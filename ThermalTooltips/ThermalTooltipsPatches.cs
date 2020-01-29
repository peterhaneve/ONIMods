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

using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Patches which will be applied via annotations for Thermal Tooltips.
	/// </summary>
	public static class ThermalTooltipsPatches {
		/// <summary>
		/// The current mod options.
		/// </summary>
		internal static ExtendedThermalTooltip TooltipInstance { get; private set; }

		/// <summary>
		/// Reports whether the element is a valid state transition.
		/// </summary>
		/// <param name="element">The element to check.</param>
		/// <param name="original">The original element.</param>
		/// <returns>true if it is a valid element, or false if it is null, Vacuum, the
		/// original element, or Void.</returns>
		public static bool IsValidTransition(this Element element, Element original) {
			bool valid = element != null;
			if (valid) {
				var id = element.id;
				valid = id != SimHashes.Void && id != SimHashes.Vacuum && (original == null ||
					id != original.id);
			}
			return valid;
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			POptions.RegisterOptions(typeof(ThermalTooltipsOptions));
			PLocalization.Register();
			TooltipInstance = null;
		}

		/// <summary>
		/// Applied to Game to clean up the tooltips on close.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DestroyInstances")]
		public static class Game_DestroyInstances_Patch {
			/// <summary>
			/// Applied after DestroyInstances runs.
			/// </summary>
			internal static void Postfix() {
				PUtil.LogDebug("Destroying ExtendedThermalTooltip");
				TooltipInstance = null;
			}
		}

		/// <summary>
		/// Applied to Game to load mod options when the game starts.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix() {
				var options = POptions.ReadSettings<ThermalTooltipsOptions>();
				// Check for DisplayAllTemps
				try {
					if (Type.GetType("DisplayAllTemps.State, DisplayAllTemps", false) != null) {
						// Let Display All Temps take over display (ironically setting AllUnits
						// to FALSE) since it patches GetFormattedTemperature
						PUtil.LogDebug("DisplayAllTemps compatibility activated");
						options.AllUnits = false;
					}
				} catch { }
				TooltipInstance = new ExtendedThermalTooltip(options);
				PUtil.LogDebug("Created ExtendedThermalTooltip");
			}
		}

		/// <summary>
		/// Applied to SelectToolHoverTextCard to display our tool tip in the dialog.
		/// </summary>
		[HarmonyPatch(typeof(SelectToolHoverTextCard), "UpdateHoverElements")]
		public static class SelectToolHoverTextCard_UpdateHoverElements_Patch {
			/// <summary>
			/// Transpiles UpdateHoverElements to display more thermal information.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return new ThermalTranspilerPatch().DoTranspile(method);
			}
		}
	}
}
