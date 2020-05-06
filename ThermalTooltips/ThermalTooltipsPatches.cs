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
		/// Handles compatibility with Better Info Cards.
		/// </summary>
		private static BetterInfoCardsCompat bicCompat;

		/// <summary>
		/// The current building handler.
		/// </summary>
		private static BuildThermalTooltip buildingInstance;

		/// <summary>
		/// The current tooltip handler.
		/// </summary>
		internal static ExtendedThermalTooltip TooltipInstance { get; private set; }

		/// <summary>
		/// Cleans up the tooltips on close.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void CleanupTooltips() {
			PUtil.LogDebug("Destroying ExtendedThermalTooltip");
			buildingInstance?.ClearDef();
			TooltipInstance = null;
		}

		/// <summary>
		/// Formats a transition element name, adding a prefix if it has the same name as the
		/// original element.
		/// </summary>
		/// <param name="element">The result of the state transition.</param>
		/// <param name="originalName">The original element name with link formatting removed.</param>
		/// <returns>The proper name for the result that is unambiguous from the original
		/// element.</returns>
		public static string FormatName(this Element element, string originalName) {
			string name = STRINGS.UI.StripLinkFormatting(element.name);
			if (name == originalName) {
				// Do not switch case on State, it is a bit field
				if (element.IsLiquid)
					name = STRINGS.ELEMENTS.STATE.LIQUID + " " + name;
				else if (element.IsSolid)
					name = STRINGS.ELEMENTS.STATE.SOLID + " " + name;
				else if (element.IsGas)
					name = STRINGS.ELEMENTS.STATE.GAS + " " + name;
			}
			return name;
		}

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
			bicCompat = null;
			buildingInstance = new BuildThermalTooltip();
			PUtil.InitLibrary();
			POptions.RegisterOptions(typeof(ThermalTooltipsOptions));
			PLocalization.Register();
			TooltipInstance = null;
			PUtil.RegisterPatchClass(typeof(ThermalTooltipsPatches));
		}

		/// <summary>
		/// Initializes Better Info Cards compatibility at a safe time.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void SetupCompat() {
			bicCompat = new BetterInfoCardsCompat();
		}

		/// <summary>
		/// Applied to Game to load mod options when the game starts.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void SetupTooltips() {
			var options = POptions.ReadSettings<ThermalTooltipsOptions>() ??
				new ThermalTooltipsOptions();
			// Check for DisplayAllTemps
			if (PPatchTools.GetTypeSafe("DisplayAllTemps.State", "DisplayAllTemps") !=
					null) {
				// Let Display All Temps take over display (ironically setting AllUnits
				// to FALSE) since it patches GetFormattedTemperature
				PUtil.LogDebug("DisplayAllTemps compatibility activated");
				options.AllUnits = false;
			}
			TooltipInstance = new ExtendedThermalTooltip(options, bicCompat);
			PUtil.LogDebug("Created ExtendedThermalTooltip");
		}

		/// <summary>
		/// Applied to MaterialSelector to display thermal information on buildings that are
		/// being planned.
		/// </summary>
		[HarmonyPatch(typeof(MaterialSelector), "SetEffects")]
		public static class MaterialSelector_SetEffects_Patch {
			/// <summary>
			/// Applied after SetEffects runs.
			/// </summary>
			internal static void Postfix(MaterialSelector __instance, Tag element) {
				if (__instance.selectorIndex == 0)
					// Primary element only
					buildingInstance?.AddThermalInfo(__instance.MaterialEffectsPane, element);
			}
		}

		/// <summary>
		/// Applied to ProductInfoScreen to clear the selected building when it is closed.
		/// </summary>
		[HarmonyPatch(typeof(ProductInfoScreen), "Close")]
		public static class ProductInfoScreen_Close_Patch {
			/// <summary>
			/// Applied after Close runs.
			/// </summary>
			internal static void Postfix() {
				buildingInstance?.ClearDef();
			}
		}

		/// <summary>
		/// Applied to ProductInfoScreen to store the building that will be constructed.
		/// </summary>
		[HarmonyPatch(typeof(ProductInfoScreen), "SetMaterials")]
		public static class ProductInfoScreen_SetMaterials_Patch {
			/// <summary>
			/// Applied before SetMaterials runs.
			/// </summary>
			internal static void Prefix(BuildingDef def) {
				if (buildingInstance != null)
					buildingInstance.Def = def;
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
			[HarmonyPriority(Priority.LowerThanNormal)]
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return new ThermalTranspilerPatch().DoTranspile(method);
			}
		}
	}
}
