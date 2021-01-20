/*
 * Copyright 2021 Peter Han
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
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using UnityEngine;

using BuildingTechGroup = System.Collections.Generic.IDictionary<string, string[]>;
using PlanInfo = PlanScreen.PlanInfo;
using Techs = Database.Techs;

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// Utility methods for creating new buildings.
	/// </summary>
	public sealed partial class PBuilding {
		// TODO Vanilla/DLC code
		private delegate Tech TryGetTech(Techs techs, string name);

		/// <summary>
		/// The default building category.
		/// </summary>
		private static readonly HashedString DEFAULT_CATEGORY = new HashedString("Base");

		// The tech tree in the DLC.
		private static readonly DetouredMethod<TryGetTech> DLC_TECHS = typeof(Techs).
			DetourLazy<TryGetTech>("TryGet");

		private static readonly IDetouredField<object, object> PLAN_ORDER_FIELD = PDetours.
			DetourStructField<object>(typeof(PlanInfo), nameof(PlanInfo.data));

		private static readonly IDetouredField<Tech, List<string>> UNLOCKED_ITEMS =
			PDetours.DetourFieldLazy<Tech, List<string>>("unlockedItemIDs");

		// The tech tree in vanilla.
		private static readonly System.Reflection.FieldInfo VANILLA_TECHS = typeof(Techs).
			GetFieldSafe("TECH_GROUPING", true);

		/// <summary>
		/// If true, the DB must be initialized before building techs are added.
		/// </summary>
		internal static bool RequiresDBInit {
			get {
				return VANILLA_TECHS == null;
			}
		}

		/// <summary>
		/// The building table.
		/// </summary>
		private static ICollection<object> buildingTable;

		/// <summary>
		/// Adds the strings for every registered building to the database.
		/// </summary>
		internal static void AddAllStrings() {
			if (buildingTable == null)
				throw new InvalidOperationException("Building table not loaded");
			lock (PSharedData.GetLock(PRegistry.KEY_BUILDING_LOCK)) {
				PRegistry.LogPatchDebug("Register strings for {0:D} buildings".F(
					buildingTable.Count));
				foreach (var building in buildingTable)
					if (building != null)
						try {
							var trBuilding = Traverse.Create(building);
							// Building is of type object because it is in another assembly
							var addStr = trBuilding.Method(nameof(AddStrings));
							if (addStr.MethodExists())
								addStr.GetValue();
							else
								PRegistry.LogPatchWarning("Invalid building strings!");
							var addMenu = trBuilding.Method(nameof(AddPlan));
							if (addMenu.MethodExists())
								addMenu.GetValue();
							else
								PRegistry.LogPatchWarning("Invalid building plan!");
						} catch (System.Reflection.TargetInvocationException e) {
							// Log errors when registering building from another mod
							PUtil.LogError("Unable to add building strings for " +
								building.GetType().Assembly?.GetNameSafe() + ":");
							PUtil.LogException(e.GetBaseException());
						}
			}
		}

		/// <summary>
		/// Adds the techs for every registered building to the database.
		/// </summary>
		internal static void AddAllTechs() {
			if (buildingTable == null)
				throw new InvalidOperationException("Building table not loaded");
			lock (PSharedData.GetLock(PRegistry.KEY_BUILDING_LOCK)) {
				PRegistry.LogPatchDebug("Register techs for {0:D} buildings".F(
					buildingTable.Count));
				foreach (var building in buildingTable)
					if (building != null)
						try {
							var trBuilding = Traverse.Create(building);
							// Building is of type object because it is in another assembly
							var addTech = Traverse.Create(building).Method(nameof(AddTech));
							if (addTech.MethodExists())
								addTech.GetValue();
							else
								PRegistry.LogPatchWarning("Invalid building technology!");
						} catch (System.Reflection.TargetInvocationException e) {
							// Log errors when registering building from another mod
							PUtil.LogError("Unable to add building tech for " +
								building.GetType().Assembly?.GetNameSafe() + ":");
							PUtil.LogException(e.GetBaseException());
						}
			}
		}

		/// <summary>
		/// Makes the building always operational without triggering a warning in both the
		/// new Automation Update and before.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		private void ApplyAlwaysOperational(GameObject go) {
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
		/// Checks for globally registered buildings and puts them into this assembly's
		/// building cache if present.
		/// </summary>
		/// <returns>true if buildings must be patched in, or false otherwise</returns>
		internal static bool CheckBuildings() {
			bool any = buildingTable != null;
			if (!any)
				lock (PSharedData.GetLock(PRegistry.KEY_BUILDING_LOCK)) {
					var table = PSharedData.GetData<ICollection<object>>(PRegistry.
						KEY_BUILDING_TABLE);
					if (table != null && table.Count > 0) {
						buildingTable = table;
						any = true;
					}
				}
			return any;
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
		/// Retrieves an object layer by its name, resolving the value at runtime to handle
		/// differences in the layer enum. This method is slower than a direct lookup -
		/// consider caching the result.
		/// </summary>
		/// <param name="name">The name of the layer (use nameof()!)</param>
		/// <param name="defValue">The default value (use the value at compile time)</param>
		/// <returns>The value to use for this object layer.</returns>
		public static ObjectLayer GetObjectLayer(string name, ObjectLayer defValue) {
			if (!Enum.TryParse(name, out ObjectLayer value))
				value = defValue;
			return value;
		}

		/// <summary>
		/// Registers a building to properly display its name, description, and tech tree
		/// entry. PLib must be initialized using InitLibrary before using this method. Each
		/// building should only be registered once, either in OnLoad or a post-load patch.
		/// </summary>
		/// <param name="building">The building to register.</param>
		public static void Register(PBuilding building) {
			if (building == null)
				throw new ArgumentNullException("building");
			// In case this call is used before the library was initialized
			if (!PUtil.PLibInit) {
				PUtil.InitLibrary(false);
				PUtil.LogWarning("PUtil.InitLibrary was not called before using " +
					"PBuilding.Register!");
			}
			// Must use object as the building table type
			lock (PSharedData.GetLock(PRegistry.KEY_BUILDING_LOCK)) {
				var table = PSharedData.GetData<ICollection<object>>(PRegistry.
					KEY_BUILDING_TABLE);
				if (table == null)
					PSharedData.PutData(PRegistry.KEY_BUILDING_TABLE, table = new
						List<object>(64));
#if DEBUG
				PUtil.LogDebug("Registered building: {0}".F(building.ID));
#endif
				table.Add(building);
			}
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
			if (PLAN_ORDER_FIELD.Get(menu) is IList<string> data) {
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
			// TODO Vanilla/DLC code
			if (!addedTech && Tech != null) {
				if (VANILLA_TECHS?.GetValue(null) is BuildingTechGroup groups)
					AddTechVanilla(groups);
				else
					AddTechDLC();
				addedTech = true;
			}
		}

		/// <summary>
		/// Adds the building tech to the tech tree - DLC implementation.
		/// </summary>
		private void AddTechDLC() {
			var dbTech = DLC_TECHS.Invoke(Db.Get().Techs, Tech);
			if (dbTech != null)
				UNLOCKED_ITEMS.Get(dbTech)?.Add(ID);
			else
				PUtil.LogWarning("Unknown technology " + Tech);
		}

		/// <summary>
		/// Adds the building tech to the tech tree - Vanilla implementation.
		/// </summary>
		/// <param name="groups">The tech grouping to modify.</param>
		private void AddTechVanilla(BuildingTechGroup groups) {
			if (groups.TryGetValue(Tech, out string[] values)) {
				int n = values.Length;
				// Expand by 1 and add building ID
				string[] newValues = new string[n + 1];
				Array.Copy(values, newValues, n);
				newValues[n] = ID;
				groups[Tech] = newValues;
			} else
				PUtil.LogWarning("Unknown technology " + Tech);
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
