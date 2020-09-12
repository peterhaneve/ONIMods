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
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.PLib.Buildings {
	/// <summary>
	/// Utility methods for creating new buildings. No I am not unconstructive any more!
	/// </summary>
	public sealed class PBuilding {
		/// <summary>
		/// The default building category.
		/// </summary>
		private static readonly HashedString DEFAULT_CATEGORY = new HashedString("Base");

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
					if (building != null) {
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
					if (building != null) {
						var trBuilding = Traverse.Create(building);
						// Building is of type object because it is in another assembly
						var addTech = Traverse.Create(building).Method(nameof(AddTech));
						if (addTech.MethodExists())
							addTech.GetValue();
						else
							PRegistry.LogPatchWarning("Invalid building technology!");
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
		/// The building ID which should precede this building ID in the plan menu.
		/// </summary>
		public string AddAfter { get; set; }

		/// <summary>
		/// Whether the building is always operational.
		/// </summary>
		public bool AlwaysOperational { get; set; }

		/// <summary>
		/// The building's animation.
		/// </summary>
		public string Animation { get; set; }

		/// <summary>
		/// The audio sounds used when placing/completing the building.
		/// </summary>
		public string AudioCategory { get; set; }

		/// <summary>
		/// The audio volume used when placing/completing the building.
		/// </summary>
		public string AudioSize { get; set; }

		/// <summary>
		/// Whether this building can break down.
		/// </summary>
		public bool Breaks { get; set; }

		/// <summary>
		/// The build menu category.
		/// </summary>
		public HashedString Category { get; set; }

		/// <summary>
		/// The construction time in seconds on x1 speed.
		/// </summary>
		public float ConstructionTime { get; set; }

		/// <summary>
		/// The decor of this building.
		/// </summary>
		public EffectorValues Decor { get; set; }

		/// <summary>
		/// The building description.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Text describing the building's effect.
		/// </summary>
		public string EffectText { get; set; }

		/// <summary>
		/// Whether this building can entomb.
		/// </summary>
		public bool Entombs { get; set; }

		/// <summary>
		/// The heat generation from the exhaust in kDTU/s.
		/// </summary>
		public float ExhaustHeatGeneration { get; set; }

		/// <summary>
		/// Whether this building can flood.
		/// </summary>
		public bool Floods { get; set; }

		/// <summary>
		/// The default priority of this building, with null to not add a priority.
		/// </summary>
		public int? DefaultPriority { get; set; }

		/// <summary>
		/// The self-heating when active in kDTU/s.
		/// </summary>
		public float HeatGeneration { get; set; }

		/// <summary>
		/// The building height.
		/// </summary>
		public int Height { get; set; }

		/// <summary>
		/// The building HP until it breaks down.
		/// </summary>
		public int HP { get; set; }

		/// <summary>
		/// The ingredients required for construction.
		/// </summary>
		public IList<BuildIngredient> Ingredients { get; }

		/// <summary>
		/// The building ID.
		/// </summary>
		public string ID { get; }

		/// <summary>
		/// Whether this building is an industrial machine.
		/// </summary>
		public bool IndustrialMachine { get; set; }

		/// <summary>
		/// The input conduits.
		/// </summary>
		public IList<ConduitConnection> InputConduits { get; }

		/// <summary>
		/// Whether this building is (or can be) a solid tile.
		/// </summary>
		public bool IsSolidTile { get; set; }

		/// <summary>
		/// The logic ports.
		/// </summary>
		public IList<LogicPorts.Port> LogicIO { get; }

		/// <summary>
		/// The building name.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// The noise of this building (not used by Klei).
		/// </summary>
		public EffectorValues Noise { get; set; }

		/// <summary>
		/// The layer for this building.
		/// </summary>
		public ObjectLayer ObjectLayer { get; set; }

		/// <summary>
		/// The output conduits.
		/// </summary>
		public IList<ConduitConnection> OutputConduits { get; }

		/// <summary>
		/// If null, the building does not overheat; otherwise, it overheats at this
		/// temperature in K.
		/// </summary>
		public float? OverheatTemperature { get; set; }

		/// <summary>
		/// The location where this building may be built.
		/// </summary>
		public BuildLocationRule Placement { get; set; }

		/// <summary>
		/// If null, the building has no power input; otherwise, it uses this much power.
		/// </summary>
		public PowerRequirement PowerInput { get; set; }

		/// <summary>
		/// If null, the building has no power output; otherwise, it provides this much power.
		/// </summary>
		public PowerRequirement PowerOutput { get; set; }

		/// <summary>
		/// The directions this building can face.
		/// </summary>
		public PermittedRotations RotateMode { get; set; }

		/// <summary>
		/// The scene layer for this building.
		/// </summary>
		public Grid.SceneLayer SceneLayer { get; set; }

		/// <summary>
		/// The technology name required to unlock the building.
		/// </summary>
		public string Tech { get; set; }

		/// <summary>
		/// The view mode used when placing this building.
		/// </summary>
		public HashedString ViewMode { get; set; }

		/// <summary>
		/// The building width.
		/// </summary>
		public int Width { get; set; }

		/// <summary>
		/// Whether the building was added to the plan menu.
		/// </summary>
		private bool addedPlan;

		/// <summary>
		/// Whether the strings were added.
		/// </summary>
		private bool addedStrings;

		/// <summary>
		/// Whether the technology wes added.
		/// </summary>
		private bool addedTech;

		/// <summary>
		/// Creates a new building. All buildings thus created must be registered using
		/// PBuilding.Register and have an appropriate IBuildingConfig class.
		/// 
		/// Building should be created in OnLoad or a post-load patch (not in static
		/// initializers) to give the localization framework time to patch the LocString
		/// containing the building name and description.
		/// </summary>
		/// <param name="id">The building ID.</param>
		/// <param name="name">The building name.</param>
		public PBuilding(string id, string name) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException("id");
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			AddAfter = null;
			AlwaysOperational = false;
			Animation = "";
			AudioCategory = "Metal";
			AudioSize = "medium";
			Breaks = true;
			Category = DEFAULT_CATEGORY;
			ConstructionTime = 10.0f;
			Decor = TUNING.BUILDINGS.DECOR.NONE;
			DefaultPriority = null;
			Description = "Default Building Description";
			EffectText = "Default Building Effect";
			Entombs = true;
			ExhaustHeatGeneration = 0.0f;
			Floods = true;
			HeatGeneration = 0.0f;
			Height = 1;
			Ingredients = new List<BuildIngredient>(4);
			IndustrialMachine = false;
			InputConduits = new List<ConduitConnection>(4);
			HP = 100;
			ID = id;
			LogicIO = new List<LogicPorts.Port>(4);
			Name = name;
			Noise = TUNING.NOISE_POLLUTION.NONE;
			ObjectLayer = ObjectLayer.Building;
			OutputConduits = new List<ConduitConnection>(4);
			OverheatTemperature = null;
			Placement = BuildLocationRule.OnFloor;
			PowerInput = null;
			PowerOutput = null;
			RotateMode = PermittedRotations.Unrotatable;
			SceneLayer = Grid.SceneLayer.Building;
			Tech = null;
			ViewMode = OverlayModes.None.ID;
			Width = 1;

			addedPlan = false;
			addedStrings = false;
			addedTech = false;
		}

		/// <summary>
		/// Adds the building to the plan menu.
		/// </summary>
		public void AddPlan() {
			if (!addedPlan && Category.IsValid) {
				bool add = false;
				foreach (var menu in TUNING.BUILDINGS.PLANORDER)
					if (menu.category == Category) {
						// Found category
						var data = menu.data as IList<string>;
						if (data == null)
							PUtil.LogWarning("Build menu " + Category +
								" has invalid entries!");
						else {
							string addID = AddAfter;
							if (addID != null) {
								// Optionally choose the position
								int n = data.Count;
								for (int i = 0; i < n - 1 && !add; i++)
									if (data[i] == addID) {
										data.Insert(i + 1, ID);
										add = true;
									}
							}
							if (!add) {
								data.Add(ID);
								add = true;
							}
						}
						break;
					}
				if (!add)
					PUtil.LogWarning("Unable to find build menu: " + Category);
				addedPlan = true;
			}
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
				var groups = Database.Techs.TECH_GROUPING;
				if (groups.TryGetValue(Tech, out string[] values)) {
					int n = values.Length;
					// Expand by 1 and add building ID
					string[] newValues = new string[n + 1];
					Array.Copy(values, newValues, n);
					newValues[n] = ID;
					groups[Tech] = newValues;
				} else
					PUtil.LogWarning("Unknown technology " + Tech);
				addedTech = true;
			}
		}

		/// <summary>
		/// Creates the building def from this class.
		/// </summary>
		/// <returns>The Klei building def.</returns>
		public BuildingDef CreateDef() {
			// The number of fields in BuildingDef makes it somewhat impractical to detour
			if (Width < 1)
				throw new InvalidOperationException("Building width: " + Width);
			if (Height < 1)
				throw new InvalidOperationException("Building height: " + Height);
			if (HP < 1)
				throw new InvalidOperationException("Building HP: " + HP);
			if (ConstructionTime.IsNaNOrInfinity())
				throw new InvalidOperationException("Construction time: " + ConstructionTime);
			// Build an ingredients list
			int n = Ingredients.Count;
			if (n < 1)
				throw new InvalidOperationException("No ingredients for build");
			float[] quantity = new float[n];
			string[] tag = new string[n];
			for (int i = 0; i < n; i++) {
				var ingredient = Ingredients[i];
				if (ingredient == null)
					throw new ArgumentNullException("ingredient");
				quantity[i] = ingredient.Quantity;
				tag[i] = ingredient.Material;
			}
			// Melting point is not currently used
			var def = BuildingTemplates.CreateBuildingDef(ID, Width, Height, Animation, HP,
				Math.Max(0.1f, ConstructionTime), quantity, tag, 2400.0f, Placement, Decor,
				Noise);
			// Solid tile
			if (IsSolidTile) {
				def.isSolidTile = true;
				def.BaseTimeUntilRepair = -1.0f;
				def.UseStructureTemperature = false;
				BuildingTemplates.CreateFoundationTileDef(def);
			}
			def.AudioCategory = AudioCategory;
			def.AudioSize = AudioSize;
			if (OverheatTemperature != null) {
				def.Overheatable = true;
				def.OverheatTemperature = OverheatTemperature ?? 348.15f;
			} else
				def.Overheatable = false;
			// Plug in
			if (PowerInput != null) {
				def.RequiresPowerInput = true;
				def.EnergyConsumptionWhenActive = PowerInput.MaxWattage;
				def.PowerInputOffset = PowerInput.PlugLocation;
			}
			// Plug out
			if (PowerOutput != null) {
				def.RequiresPowerOutput = true;
				def.GeneratorWattageRating = PowerOutput.MaxWattage;
				def.PowerOutputOffset = PowerOutput.PlugLocation;
			}
			def.Breakable = Breaks;
			def.PermittedRotations = RotateMode;
			def.ExhaustKilowattsWhenActive = Math.Max(0.0f, ExhaustHeatGeneration);
			def.SelfHeatKilowattsWhenActive = Math.Max(0.0f, HeatGeneration);
			def.Floodable = Floods;
			def.Entombable = Entombs;
			def.ObjectLayer = ObjectLayer;
			def.SceneLayer = SceneLayer;
			def.ViewMode = ViewMode;
			// Conduits
			if (InputConduits.Count > 1)
				throw new InvalidOperationException("Only supports one input conduit");
			foreach (var conduit in InputConduits) {
				def.UtilityInputOffset = conduit.Location;
				def.InputConduitType = conduit.Type;
			}
			if (OutputConduits.Count > 1)
				throw new InvalidOperationException("Only supports one output conduit");
			foreach (var conduit in OutputConduits) {
				def.UtilityOutputOffset = conduit.Location;
				def.OutputConduitType = conduit.Type;
			}
			return def;
		}

		/// <summary>
		/// Configures the building template of this building. Should be called in
		/// ConfigureBuildingTemplate.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		public void ConfigureBuildingTemplate(GameObject go) {
			if (AlwaysOperational)
				ApplyAlwaysOperational(go);
		}

		/// <summary>
		/// Populates the logic ports of this building. Must be used <b>after</b> the
		/// PBuilding.DoPostConfigureComplete method if logic ports are required.
		/// 
		/// Should be called in DoPostConfigureComplete, DoPostConfigurePreview, and
		/// DoPostConfigureUnderConstruction.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		public void CreateLogicPorts(GameObject go) {
			SplitLogicPorts(go);
		}

		/// <summary>
		/// Performs the post-configure complete steps that this building object can do.
		/// Not exhaustive! Other components must likely be added.
		/// 
		/// This method does NOT add the logic ports. Use CreateLogicPorts to do so,
		/// <b>after</b> this method has been invoked.
		/// </summary>
		/// <param name="go">The game object to configure.</param>
		public void DoPostConfigureComplete(GameObject go) {
			if (InputConduits.Count == 1) {
				var conduitConsumer = go.AddOrGet<ConduitConsumer>();
				foreach (var conduit in InputConduits) {
					conduitConsumer.alwaysConsume = true;
					conduitConsumer.conduitType = conduit.Type;
					conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.
						Store;
				}
			}
			if (OutputConduits.Count == 1) {
				var conduitDispenser = go.AddOrGet<ConduitDispenser>();
				foreach (var conduit in OutputConduits) {
					conduitDispenser.alwaysDispense = true;
					conduitDispenser.conduitType = conduit.Type;
					conduitDispenser.elementFilter = null;
				}
			}
			if (IndustrialMachine)
				go.GetComponent<KPrefabID>()?.AddTag(RoomConstraints.ConstraintTags.
					IndustrialMachinery, false);
			if (PowerInput != null)
				go.AddOrGet<EnergyConsumer>();
			if (PowerOutput != null)
				go.AddOrGet<EnergyGenerator>();
			// Set a default priority
			if (DefaultPriority != null) {
				Prioritizable.AddRef(go);
				go.GetComponent<Prioritizable>().SetMasterPriority(new PrioritySetting(
					PriorityScreen.PriorityClass.basic, DefaultPriority ?? 5));
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

		public override string ToString() {
			return "PBuilding[ID={0}]".F(ID);
		}
	}
}
