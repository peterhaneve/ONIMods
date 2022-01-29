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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// Utility methods for creating new buildings.
	/// </summary>
	public sealed partial class PBuilding {
		/// <summary>
		/// The default building category.
		/// </summary>
		private static readonly HashedString DEFAULT_CATEGORY = new HashedString("Base");

		/// <summary>
		/// Makes the building always operational.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		private static void ApplyAlwaysOperational(GameObject go) {
			Component comp;
			// Remove default components that could make a building non-operational
			comp = go.GetComponent<BuildingEnabledButton>();
			if (comp != null)
				UnityEngine.Object.DestroyImmediate(comp);
			comp = go.GetComponent<Operational>();
			if (comp != null)
				UnityEngine.Object.DestroyImmediate(comp);
			comp = go.GetComponent<LogicPorts>();
			if (comp != null)
				UnityEngine.Object.DestroyImmediate(comp);
		}

		/// <summary>
		/// Creates a logic port, in a method compatible with both the new and old Automation
		/// updates. The port will have the default strings which fit well with the
		/// LogicOperationalController.
		/// </summary>
		/// <returns>A logic port compatible with both editions.</returns>
		public static LogicPorts.Port CompatLogicPort(LogicPortSpriteType type,
				CellOffset offset) {
			return new LogicPorts.Port(LogicOperationalController.PORT_ID, offset,
				STRINGS.UI.LOGIC_PORTS.CONTROL_OPERATIONAL,
				STRINGS.UI.LOGIC_PORTS.CONTROL_OPERATIONAL_ACTIVE,
				STRINGS.UI.LOGIC_PORTS.CONTROL_OPERATIONAL_INACTIVE, false, type);
		}

		/// <summary>
		/// Adds the building to the plan menu.
		/// </summary>
		public void AddPlan() {
			if (!addedPlan && Category.IsValid) {
				bool add = false;
				foreach (var menu in TUNING.BUILDINGS.PLANORDER)
					if (menu.category == Category) {
						AddPlanToCategory(menu);
						add = true;
						break;
					}
				if (!add)
					PUtil.LogWarning("Unable to find build menu: " + Category);
				addedPlan = true;
			}
		}

		/// <summary>
		/// Adds a building to a specific plan menu.
		/// </summary>
		/// <param name="menu">The menu to which to add the building.</param>
		private void AddPlanToCategory(PlanScreen.PlanInfo menu) {
			// Found category
			var data = menu.data;
			if (data != null) {
				string addID = AddAfter;
				bool add = false;
				if (addID != null) {
					// Optionally choose the position
					int n = data.Count;
					for (int i = 0; i < n - 1 && !add; i++)
						if (data[i] == addID) {
							data.Insert(i + 1, ID);
							add = true;
						}
				}
				if (!add)
					data.Add(ID);
			} else
				PUtil.LogWarning("Build menu " + Category + " has invalid entries!");
		}

		/// <summary>
		/// Adds the building strings to the strings list.
		/// </summary>
		public void AddStrings() {
			if (!addedStrings) {
				string prefix = "STRINGS.BUILDINGS.PREFABS." + ID.ToUpperInvariant() + ".";
				string nameStr = prefix + "NAME";
				if (Strings.TryGet(nameStr, out StringEntry localized))
					Name = localized.String;
				else
					Strings.Add(nameStr, Name);
				// Allow null values to be defined in LocString class adds / etc
				if (Description != null)
					Strings.Add(prefix + "DESC", Description);
				if (EffectText != null)
					Strings.Add(prefix + "EFFECT", EffectText);
				addedStrings = true;
			}
		}

		/// <summary>
		/// Adds the building tech to the tech tree.
		/// </summary>
		public void AddTech() {
			if (!addedTech && Tech != null) {
				var technology = Db.Get().Techs?.TryGet(Tech);
				if (technology != null)
					technology.unlockedItemIDs?.Add(ID);
				addedTech = true;
			}
		}

		/// <summary>
		/// Splits up logic input/output ports and configures the game object with them.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		private void SplitLogicPorts(GameObject go) {
			int n = LogicIO.Count;
			var inputs = new List<LogicPorts.Port>(n);
			var outputs = new List<LogicPorts.Port>(n);
			foreach (var port in LogicIO)
				if (port.spriteType == LogicPortSpriteType.Output)
					outputs.Add(port);
				else
					inputs.Add(port);
			// This works in both the old and new versions
			var ports = go.AddOrGet<LogicPorts>();
			if (inputs.Count > 0)
				ports.inputPortInfo = inputs.ToArray();
			if (outputs.Count > 0)
				ports.outputPortInfo = outputs.ToArray();
		}
	}
}
