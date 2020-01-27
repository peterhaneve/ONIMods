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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.ThermalTooltips {
	/// <summary>
	/// Applied to SelectToolHoverTextCard to display our tool tip in the dialog.
	/// </summary>
	internal sealed class ThermalTooltipTranspilerPatch {
		/// <summary>
		/// Called when thermal information needs to be displayed for buildings.
		/// </summary>
		private static void AddThermalInfoBuildings(HoverTextDrawer drawer, string _,
				TextStyleSetting style, PrimaryElement primaryElement) {
			var instance = ThermalTooltipsPatches.TooltipInstance;
			if (instance != null && primaryElement != null) {
				instance.Drawer = drawer;
				instance.Style = style;
				instance.AddThermalInfo(primaryElement.Element, primaryElement.Temperature,
					primaryElement.Mass * 0.2f);
			}
		}

		/// <summary>
		/// Called when thermal information needs to be displayed for elements.
		/// </summary>
		private static void AddThermalInfoElements(HoverTextDrawer drawer, string _,
				TextStyleSetting style, int cell) {
			if (Grid.IsValidCell(cell)) {
				var element = Grid.Element[cell];
				float mass = Grid.Mass[cell];
				var instance = ThermalTooltipsPatches.TooltipInstance;
				if (instance != null && element != null && mass > 0.0f) {
					instance.Drawer = drawer;
					instance.Style = style;
					instance.AddThermalInfo(element, Grid.Temperature[cell], mass);
				}
			}
		}

		/// <summary>
		/// HoverTextDrawer.DrawText(string, TextStyleSetting)
		/// </summary>
		private readonly MethodInfo drawText;

		/// <summary>
		/// PrimaryElement.get_Temperature()
		/// </summary>
		private readonly MethodInfo elementTemp;

		/// <summary>
		/// Handles thermal tooltips on buildings.
		/// </summary>
		private readonly MethodInfo handlerBuild;

		/// <summary>
		/// Handles thermal tooltips on cells (elements in world).
		/// </summary>
		private readonly MethodInfo handlerElements;

		/// <summary>
		/// GameUtil.GetFormattedTemperature(...)
		/// </summary>
		private readonly MethodInfo marker;

		/// <summary>
		/// Grid.TemperatureIndexer[int]
		/// </summary>
		private readonly MethodInfo tempIndexer;

		internal ThermalTooltipTranspilerPatch() {
			drawText = typeof(HoverTextDrawer).GetMethodSafe(nameof(HoverTextDrawer.
				DrawText), false, typeof(string), typeof(TextStyleSetting));
			elementTemp = typeof(PrimaryElement).GetPropertySafe<float>(nameof(
				PrimaryElement.Temperature), false)?.GetGetMethod();
			handlerBuild = typeof(ThermalTooltipTranspilerPatch).GetMethodSafe(nameof(
				AddThermalInfoBuildings), true, PPatchTools.AnyArguments);
			handlerElements = typeof(ThermalTooltipTranspilerPatch).GetMethodSafe(nameof(
				AddThermalInfoElements), true, PPatchTools.AnyArguments);
			marker = typeof(GameUtil).GetMethodSafe(nameof(GameUtil.
				GetFormattedTemperature), true, PPatchTools.AnyArguments);
			tempIndexer = typeof(Grid.TemperatureIndexer).
				GetPropertyIndexedSafe<float>("Item", false, typeof(int))?.GetGetMethod();
#if DEBUG
			PUtil.LogDebug("PATCH: Looking for method " + marker);
			PUtil.LogDebug("PATCH: Replacing call to " + drawText);
			PUtil.LogDebug("PATCH: With temperature accessor " + elementTemp);
			PUtil.LogDebug("PATCH: Grid temperature indexer " + tempIndexer);
#endif
		}

		/// <summary>
		/// Transpiles UpdateHoverElements to display more thermal information.
		/// </summary>
		internal IEnumerable<CodeInstruction> DoTranspile(IEnumerable<CodeInstruction> method)
		{
			var state = State.NEED_FIRST;
			object peOperand = null, lastLdloc = null;
			CodeInstruction loadCell = null, lastInstruction = null;
			foreach (var instruction in method) {
				bool passThru = true;
				var opcode = instruction.opcode;
				var callee = instruction.operand as MethodInfo;
				// This is the PrimaryElement local variable which will be used
				if (opcode == OpCodes.Ldloc_S)
					lastLdloc = instruction.operand;
				if (opcode == OpCodes.Call && callee == tempIndexer && tempIndexer != null)
					loadCell = lastInstruction;
				switch (state) {
				case State.NEED_FIRST:
					// Look for the operand to PrimaryElement.Temperature
					if (opcode == OpCodes.Callvirt && elementTemp != null && callee ==
							elementTemp)
						peOperand = lastLdloc;
					// Looking for the first call to GameUtil.GetFormattedTemperature
					else if (opcode == OpCodes.Call && marker != null && callee == marker)
						state = (drawText == null) ? State.DONE : State.WAIT_FIRST;
					break;
				case State.WAIT_FIRST:
					// drawText must not be null
					if (opcode == OpCodes.Callvirt && callee == drawText) {
#if DEBUG
						PUtil.LogDebug("PATCH: PrimaryElement operand = " + peOperand);
#endif
						if (handlerBuild != null && peOperand != null) {
							// Push the PrimaryElement onto the stack
							yield return new CodeInstruction(OpCodes.Ldloc_S, peOperand);
							// Call our method
							PUtil.LogDebug("Patched UpdateHoverElements (Buildings)");
							yield return new CodeInstruction(OpCodes.Call, handlerBuild);
							passThru = false;
						}
						state = State.NEED_SECOND;
					}
					break;
				case State.NEED_SECOND:
					// Looking for the second call to GameUtil.GetFormattedTemperature
					if (opcode == OpCodes.Call && callee == marker)
						state = State.WAIT_SECOND;
					break;
				case State.WAIT_SECOND:
					if (opcode == OpCodes.Callvirt && callee == drawText) {
#if DEBUG
						PUtil.LogDebug("PATCH: Load cell = " + loadCell);
#endif
						if (handlerElements != null && loadCell != null) {
							// Push the cell onto the stack
							yield return new CodeInstruction(loadCell.opcode, loadCell.
								operand);
							// Call our method
							PUtil.LogDebug("Patched UpdateHoverElements (Elements)");
							yield return new CodeInstruction(OpCodes.Call, handlerElements);
							passThru = false;
						}
						state = State.DONE;
					}
					break;
				default:
					break;
				}
				if (passThru)
					yield return instruction;
				lastInstruction = instruction;
			}
		}

		/// <summary>
		/// The transpiler's state.
		/// </summary>
		private enum State {
			NEED_FIRST, WAIT_FIRST, NEED_SECOND, WAIT_SECOND, DONE
		}
	}
}
