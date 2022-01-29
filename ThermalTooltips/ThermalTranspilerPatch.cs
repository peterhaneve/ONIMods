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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Applied to SelectToolHoverTextCard to display our tool tip in the dialog.
	/// </summary>
	internal sealed class ThermalTranspilerPatch {
		/// <summary>
		/// The current runtime value of the buildings layer.
		/// </summary>
		private static readonly int LAYER_BUILDINGS = (int)PGameUtils.GetObjectLayer(nameof(
			ObjectLayer.Building), ObjectLayer.Building);

		/// <summary>
		/// Called when thermal information needs to be displayed for buildings and other
		/// items in game (like debris).
		/// </summary>
		private static void AddThermalInfoEntities(HoverTextDrawer drawer, string _,
				TextStyleSetting style) {
			var instance = ThermalTooltipsPatches.TooltipInstance;
			var primaryElement = instance?.PrimaryElement;
			if (primaryElement != null) {
				float insulation = 1.0f;
				// Check for insulation
				var building = primaryElement.GetComponent<Building>();
				if (building != null)
					insulation = building.Def.ThermalConductivity;
				float mass = GetAdjustedMass(primaryElement.gameObject, primaryElement.Mass);
				instance.Drawer = drawer;
				instance.Style = style;
				instance.DisplayThermalInfo(primaryElement.Element, primaryElement.Temperature,
					mass, insulation);
				instance.PrimaryElement = null;
			}
		}

		/// <summary>
		/// Called when thermal information needs to be displayed for elements.
		/// </summary>
		private static void AddThermalInfoElements(HoverTextDrawer drawer, string _,
				TextStyleSetting style) {
			var instance = ThermalTooltipsPatches.TooltipInstance;
			int cell;
			if (instance != null && Grid.IsValidCell(cell = instance.Cell)) {
				var element = Grid.Element[cell];
				float mass = Grid.Mass[cell];
				if (element != null && mass > 0.0f) {
					instance.Drawer = drawer;
					instance.Style = style;
					instance.DisplayThermalInfo(element, Grid.Temperature[cell], mass);
				}
				instance.Cell = 0;
			}
		}

		/// <summary>
		/// Returns the adjusted mass for an entity.
		/// </summary>
		/// <param name="entity">The entity.</param>
		/// <param name="originalMass">The original entity mass.</param>
		/// <returns>The mass used for that entity in temperature calculations.</returns>
		public static float GetAdjustedMass(GameObject entity, float originalMass) {
			var sco = entity.GetComponentSafe<SimCellOccupier>();
			var def = entity.GetComponentSafe<Building>()?.Def;
			// Buildings have that insidious /5 multiplier... if they do not use tile
			// temperature instead (with doors only being /5 if open)
			//  isSolidTile almost works but it is false on FarmTile
			//  IsFoundation almost works but it is true on Mesh and Airflow tiles
			return (def == null || (sco != null && sco.IsVisuallySolid)) ? originalMass : def.
				MassForTemperatureModification;
		}

		/// <summary>
		/// Called after Grid.PosToCell; stores the cell to be used for the element based
		/// thermal tooltips.
		/// </summary>
		private static int SetCell(int cell) {
			var instance = ThermalTooltipsPatches.TooltipInstance;
			if (instance != null)
				instance.Cell = cell;
			return cell;
		}

		/// <summary>
		/// Called after GetComponent&lt;PrimaryElement&gt;; stores the element to be used for
		/// the item based thermal tooltips.
		/// </summary>
		private static PrimaryElement SetElement(PrimaryElement element) {
			var instance = ThermalTooltipsPatches.TooltipInstance;
			if (instance != null && element != null)
				instance.PrimaryElement = element;
			return element;
		}

		/// <summary>
		/// HoverTextDrawer.DrawText(string, TextStyleSetting)
		/// </summary>
		private readonly MethodInfo drawText;

		/// <summary>
		/// GameObject.GetComponent&lt;PrimaryElement&gt;()
		/// </summary>
		private readonly MethodInfo getComponent;

		/// <summary>
		/// GameUtil.GetFormattedTemperature(...)
		/// </summary>
		private readonly MethodInfo marker;

		/// <summary>
		/// Grid.PosToCell(Point)
		/// </summary>
		private readonly MethodInfo posToCell;

		internal ThermalTranspilerPatch() {
			drawText = typeof(HoverTextDrawer).GetMethodSafe(nameof(HoverTextDrawer.DrawText),
				false, typeof(string), typeof(TextStyleSetting));
			getComponent = typeof(Component).GetMethodSafe(nameof(Component.GetComponent),
				false)?.MakeGenericMethod(typeof(PrimaryElement));
			marker = typeof(GameUtil).GetMethodSafe(nameof(GameUtil.GetFormattedTemperature),
				true, typeof(float), typeof(GameUtil.TimeSlice), typeof(GameUtil.
				TemperatureInterpretation), typeof(bool), typeof(bool));
			posToCell = typeof(Grid).GetMethodSafe(nameof(Grid.PosToCell), true, typeof(
				Vector3));
#if DEBUG
			PUtil.LogDebug("PATCH: Looking for method " + marker);
			PUtil.LogDebug("PATCH: Replacing call to " + drawText);
			PUtil.LogDebug("PATCH: With element accessor " + getComponent);
			PUtil.LogDebug("PATCH: With cell accessor " + posToCell);
#endif
		}

		/// <summary>
		/// Transpiles UpdateHoverElements to display more thermal information.
		/// </summary>
		internal IEnumerable<CodeInstruction> DoTranspile(IEnumerable<CodeInstruction> method)
		{
			var newMethod = new List<CodeInstruction>(method);
			int n = newMethod.Count, i;
			bool patchOne = false, patchTwo = false;
			// Replace the first PosToCell
			for (i = 0; i < n && (!patchOne || !patchTwo); i++) {
				var instruction = newMethod[i];
				if (IsCallTo(instruction, posToCell) && !patchOne) {
#if DEBUG
					PUtil.LogDebug("PATCH: PosToCell found at {0:D}".F(i));
#endif
					newMethod.Insert(++i, new CodeInstruction(OpCodes.Call, typeof(
						ThermalTranspilerPatch).GetMethodSafe(nameof(SetCell), true,
						PPatchTools.AnyArguments)));
					patchOne = true;
					n++;
					i++;
				}
				if (IsCallTo(instruction, getComponent) && !patchTwo) {
#if DEBUG
					PUtil.LogDebug("PATCH: GetComponent found at {0:D}".F(i));
#endif
					// For BIC compatibility call our stuff after it
					newMethod.Insert(++i, new CodeInstruction(OpCodes.Call, typeof(
						ThermalTranspilerPatch).GetMethodSafe(nameof(SetElement), true,
						PPatchTools.AnyArguments)));
					patchTwo = true;
					n++;
					i++;
				}
			}
			// Find first instance of GetFormattedTemperature
			for (i++; i < n && !IsCallTo(newMethod[i], marker); i++) ;
#if DEBUG
			if (i < n)
				PUtil.LogDebug("PATCH: GetFormattedTemperature(1) at {0:D}".F(i));
#endif
			// Replace next instance of DrawText with the building/item handler
			for (i++; i < n; i++) {
				var instruction = newMethod[i];
				if (IsCallTo(instruction, drawText)) {
#if DEBUG
					PUtil.LogDebug("PATCH: DrawText found at {0:D}".F(i));
#endif
					instruction.opcode = OpCodes.Call;
					instruction.operand = typeof(ThermalTranspilerPatch).GetMethodSafe(nameof(
						AddThermalInfoEntities), true, PPatchTools.AnyArguments);
					break;
				}
			}
			// Find second instance of GetFormattedTemperature
			for (i++; i < n && !IsCallTo(newMethod[i], marker); i++) ;
#if DEBUG
			if (i < n)
				PUtil.LogDebug("PATCH: GetFormattedTemperature(2) at {0:D}".F(i));
#endif
			// Replace next instance of DrawText with the element handler
			for (i++; i < n; i++) {
				var instruction = newMethod[i];
				if (IsCallTo(instruction, drawText)) {
#if DEBUG
					PUtil.LogDebug("PATCH: DrawText found at {0:D}".F(i));
#endif
					instruction.opcode = OpCodes.Call;
					instruction.operand = typeof(ThermalTranspilerPatch).GetMethodSafe(nameof(
						AddThermalInfoElements), true, PPatchTools.AnyArguments);
					break;
				}
			}
			PUtil.LogDebug("UpdateHoverElements patch complete");
			return newMethod;
		}

		/// <summary>
		/// Checks to see if the instruction is a call instruction to the specified method.
		/// </summary>
		/// <param name="instruction">The IL instruction.</param>
		/// <param name="method">The method which should be called.</param>
		/// <returns>true if it is a call or callvirt instruction to the specified method, or
		/// false otherwise.</returns>
		private bool IsCallTo(CodeInstruction instruction, MethodInfo method) {
			return instruction != null && (instruction.opcode == OpCodes.Call || instruction.
				opcode == OpCodes.Callvirt) && method != null && method == (instruction.
				operand as MethodInfo);
		}
	}
}
