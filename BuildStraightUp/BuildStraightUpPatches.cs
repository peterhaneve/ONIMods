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
using UnityEngine;

namespace PeterHan.BuildStraightUp {
	/// <summary>
	/// Patches which will be applied via annotations for Build Straight Up.
	/// </summary>
	public static class BuildStraightUpPatches {
		/// <summary>
		/// The last building checked.
		/// </summary>
		private static LastBuilding lastChecked = new LastBuilding();

		public static void OnLoad() {
			PUtil.InitLibrary();
			lastChecked.Reset();
			PUtil.RegisterPatchClass(typeof(BuildStraightUpPatches));
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			lastChecked.Reset();
		}

		/// <summary>
		/// Checks to see if a building can be placed on attachment points.
		/// </summary>
		/// <param name="def">The building to be placed.</param>
		/// <returns>true if it can use attachment points, or false otherwise.</returns>
		private static bool CanUseAttachmentPoint(BuildingDef def) {
			var rule = def?.BuildLocationRule ?? BuildLocationRule.Anywhere;
			return rule == BuildLocationRule.BuildingAttachPoint || rule == BuildLocationRule.
				OnFloorOrBuildingAttachPoint;
		}

		/// <summary>
		/// Checks a particular building under construction for a valid attachment point.
		/// </summary>
		/// <param name="underCons">The planned building to check.</param>
		/// <param name="attachTag">The attachment hardpoint tag to check.</param>
		/// <param name="attachCell">The location of the attaching building.</param>
		/// <returns>true if an attachment point was found, or false otherwise.</returns>
		private static bool CheckBuilding(Building underCons, Tag attachTag, int attachCell) {
			var attach = underCons.Def?.BuildingComplete?.GetComponent<BuildingAttachPoint>();
			bool found = false;
			if (attach != null) {
				int origin = Grid.PosToCell(underCons);
				foreach (var point in attach.points)
					// Cannot use BuildingAttachPoint.AcceptsAttachment as that uses the
					// position of the template building (off screen!)
					if (Grid.OffsetCell(origin, point.position) == attachCell && point.
							attachableType == attachTag) {
						found = true;
						break;
					}
			}
			return found;
		}

		/// <summary>
		/// Checks for buildings in a cell that can attach to the specified type of point.
		/// </summary>
		/// <param name="attachTag">The attachment hardpoint tag to check.</param>
		/// <param name="attachCell">The location of the attaching building.</param>
		/// <param name="target">The cell to check for buildings.</param>
		/// <returns>true if an attachment point was found, or false otherwise.</returns>
		private static bool CheckForBuildings(Tag attachTag, int attachCell, int target) {
			// Search all buildings in the cell
			BuildingUnderConstruction underCons;
			bool found = false;
			for (int layer = 0; layer < (int)ObjectLayer.NumLayers && !found; layer++) {
				var building = Grid.Objects[target, layer];
				if (building != null && (underCons = building.GetComponent<
						BuildingUnderConstruction>()) != null) {
#if DEBUG
					PUtil.LogDebug("Checking cell {0:D}: found {1}".F(target, underCons.
						name));
#endif
					found = CheckBuilding(underCons, attachTag, attachCell);
				}
			}
			return found;
		}

		/// <summary>
		/// Checks for building attachment points on buildings still under construction.
		/// </summary>
		/// <param name="result">Whether the initial check failed.</param>
		/// <param name="def">The building to be placed.</param>
		/// <param name="cell">The cell where it would go.</param>
		/// <returns>true if it could go there eventually, or false otherwise.</returns>
		private static bool CheckVirtualAttachments(bool result, BuildingDef def, int cell) {
			if (!result && CanUseAttachmentPoint(def))
				result = IsAttachmentPointValid(def, cell);
			return result;
		}

		/// <summary>
		/// Checks to see if a building attachment point would be valid, after completion of
		/// all buildings planned or under construction.
		/// </summary>
		/// <param name="def">The building to be placed.</param>
		/// <param name="cell">The cell where it would go.</param>
		/// <returns>true if it could go there eventually, or false otherwise.</returns>
		private static bool IsAttachmentPointValid(BuildingDef def, int cell) {
			string prefabID = def.PrefabID;
			bool valid;
			if (lastChecked.IsSame(prefabID, cell))
				// Use cached value
				valid = lastChecked.WasValid;
			else {
				// Assumes that it uses attachment points
				var attachTag = def.AttachmentSlotTag;
				// Make a rectangle one bigger than the building in all dimensions
				int width = def.WidthInCells, halfWidth = width / 2 - width + 1, height = def.
					HeightInCells, attachCell = Grid.OffsetCell(cell, def.attachablePosition);
				Grid.CellToXY(cell, out int startX, out int startY);
				valid = false;
				for (int i = -1; i < width + 1 && !valid; i++)
					for (int j = -1; j < height + 1 && !valid; j++)
						valid = CheckForBuildings(attachTag, attachCell, Grid.XYToCell(
							startX + halfWidth + i, startY + j));
				lastChecked.LastCell = cell;
				lastChecked.LastPrefabID = prefabID;
				lastChecked.WasValid = valid;
			}
			return valid;
		}

		/// <summary>
		/// Transpiles the IsAreaClear method to modify the check for attachment points.
		/// </summary>
		/// <param name="name">The method patched.</param>
		/// <param name="method">The original method body.</param>
		/// <returns>The new transpiled method body.</returns>
		internal static IEnumerable<CodeInstruction> TranspileAreaClear(string name,
				IEnumerable<CodeInstruction> method) {
			var instructions = new List<CodeInstruction>(method);
			var powerMethod = typeof(BuildingDef).GetMethodSafe(
				"ArePowerPortsInValidPositions", false, PPatchTools.AnyArguments);
			var targetMethod = typeof(BuildStraightUpPatches).GetMethodSafe(
				nameof(CheckVirtualAttachments), true, typeof(bool), typeof(BuildingDef),
				typeof(int));
			bool hasAnchor = false;
			// Find call to power port check
			if (powerMethod == null)
				PUtil.LogWarning("Could not transpile {0} - no method found!".F(name));
			else {
				// Go back for branch instruction
				for (int i = instructions.Count - 1; i > 0; i--) {
					var instr = instructions[i];
					var code = instr.opcode;
					if ((code == OpCodes.Call || code == OpCodes.Callvirt) && powerMethod ==
							(instr.operand as MethodInfo)) {
						hasAnchor = true;
#if DEBUG
						PUtil.LogDebug("Power method offset: {0:D}".F(i));
#endif
					} else if (hasAnchor && code.IsConditionalBranchInstruction()) {
#if DEBUG
						PUtil.LogDebug("Branch offset: {0:D}".F(i));
#endif
						var store = PPatchTools.GetMatchingStoreInstruction(
							instructions[i - 1]);
						// Stack has "flag" on it, pass it to our method
						instructions.InsertRange(i, new List<CodeInstruction>(3) {
							// this
							new CodeInstruction(OpCodes.Ldarg_0),
							// cell
							new CodeInstruction(OpCodes.Ldarg_2),
							// Call
							new CodeInstruction(OpCodes.Call, targetMethod),
							// Duplicate
							new CodeInstruction(OpCodes.Dup),
							// Store
							store
						});
						// Stack now has altered flag value
						PUtil.LogDebug("Patched " + name);
						break;
					}
				}
				if (!hasAnchor)
					PUtil.LogWarning("Could not transpile {0} - no anchor found!".F(name));
			}
			return instructions;
		}

		/// <summary>
		/// Applied to BuildingDef to add logic for checking in-progress buildings when placing
		/// attachment points.
		/// </summary>
		[HarmonyPatch(typeof(BuildingDef), "IsAreaClear")]
		public static class BuildingDef_IsAreaClear_Patch {
			/// <summary>
			/// Transpiles IsAreaClear to insert our check in exactly the right place.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(MethodBase original,
					IEnumerable<CodeInstruction> method) {
				return TranspileAreaClear(original.Name, method);
			}
		}

		/// <summary>
		/// Applied to BuildingDef to add logic for checking in-progress buildings when placing
		/// attachment points.
		/// </summary>
		[HarmonyPatch]
		public static class BuildingDef_IsValidBuildLocation_Patch {
			/// <summary>
			/// To target "out string" we need MakeByRefType.
			/// </summary>
			internal static MethodBase TargetMethod() {
				return typeof(BuildingDef).GetMethodSafe(nameof(BuildingDef.
					IsValidBuildLocation), false, typeof(GameObject), typeof(int),
					typeof(Orientation), typeof(string).MakeByRefType());
			}

			/// <summary>
			/// Transpiles IsValidBuildLocation to insert our check in exactly the right place.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(MethodBase original,
					IEnumerable<CodeInstruction> method) {
				return TranspileAreaClear(original.Name, method);
			}
		}

		/// <summary>
		/// Avoids duplicate checks by caching building locations.
		/// </summary>
		private struct LastBuilding {
			/// <summary>
			/// The last cell checked.
			/// </summary>
			public int LastCell;

			/// <summary>
			/// The prefab ID of the last building checked.
			/// </summary>
			public string LastPrefabID;

			/// <summary>
			/// Whether it could be placed there.
			/// </summary>
			public bool WasValid;

			/// <summary>
			/// Checks to see if the building is the same one as the last test.
			/// </summary>
			/// <param name="prefabID">The building prefab ID.</param>
			/// <param name="cell">The cell which was checked.</param>
			/// <returns>true if it matches, or false if needs recalculation.</returns>
			internal bool IsSame(string prefabID, int cell) {
				return LastCell == cell && prefabID == LastPrefabID;
			}

			/// <summary>
			/// Resets the state when the build tool is cancelled.
			/// </summary>
			public void Reset() {
				LastCell = Grid.InvalidCell;
				LastPrefabID = string.Empty;
				WasValid = false;
			}
		}
	}
}
