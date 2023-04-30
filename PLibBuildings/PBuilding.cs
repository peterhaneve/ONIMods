/*
 * Copyright 2023 Peter Han
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
	/// A class used for creating new buildings. Abstracts many of the details to allow them
	/// to be used across different game versions.
	/// </summary>
	public sealed partial class PBuilding {
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
		/// The subcategory for this building.
		/// 
		/// The base game currently defines the following:
		/// Base:
		/// ladders, tiles, printing pods, doors, storage, tubes, default
		/// Oxygen:
		/// producers, scrubbers
		/// Power:
		/// generators, wires, batteries, transformers, switches
		/// Food:
		/// cooking, farming, ranching
		/// Plumbing:
		/// bathroom, pipes, pumps, valves, sensors
		/// HVAC:
		/// pipes, pumps, valves, sensors
		/// Refining:
		/// materials, oil, advanced
		/// Medical:
		/// cleaning, hospital, wellness
		/// Furniture:
		/// bed, lights, dining, recreation, pots, sculpture, electronic decor, moulding,
		/// canvas, dispaly, monument, signs
		/// Equipment:
		/// research, exploration, work stations, suits general, oxygen masks, atmo suits,
		/// jet suits, lead suits
		/// Utilities:
		/// temperature, other utilities, special
		/// Automation:
		/// wires, sensors, logic gates, utilities
		/// Solid Transport:
		/// conduit, valves, utilities
		/// Rocketry:
		/// telescopes, launch pad, railguns, engines, fuel and oxidizer, cargo, utility,
		/// command, fittings
		/// Radiation:
		/// HEP, uranium, radiation
		/// </summary>
		public string SubCategory { get; set; }

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
				throw new ArgumentNullException(nameof(id));
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
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
			ObjectLayer = PGameUtils.GetObjectLayer(nameof(ObjectLayer.Building), ObjectLayer.
				Building);
			OutputConduits = new List<ConduitConnection>(4);
			OverheatTemperature = null;
			Placement = BuildLocationRule.OnFloor;
			PowerInput = null;
			PowerOutput = null;
			RotateMode = PermittedRotations.Unrotatable;
			SceneLayer = Grid.SceneLayer.Building;
			// Hard coded strings in base game, no const to reference
			SubCategory = "default";
			Tech = null;
			ViewMode = OverlayModes.None.ID;
			Width = 1;

			addedPlan = false;
			addedStrings = false;
			addedTech = false;
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
					throw new ArgumentNullException(nameof(ingredient));
				quantity[i] = ingredient.Quantity;
				tag[i] = ingredient.Material;
			}
			// Melting point is not currently used
			var def = BuildingTemplates.CreateBuildingDef(ID, Width, Height, Animation, HP,
				Math.Max(0.1f, ConstructionTime), quantity, tag, 2400.0f, Placement, Decor,
				Noise);
			// Solid tile?
			if (IsSolidTile) {
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
			def.ExhaustKilowattsWhenActive = ExhaustHeatGeneration;
			def.SelfHeatKilowattsWhenActive = HeatGeneration;
			def.Floodable = Floods;
			def.Entombable = Entombs;
			def.ObjectLayer = ObjectLayer;
			def.SceneLayer = SceneLayer;
			def.ViewMode = ViewMode;
			// Conduits (multiple per building are hard but will be added someday...)
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
			// Add to the massive sub category dictionary to silence a warning
			var subcategory = TUNING.BUILDINGS.PLANSUBCATEGORYSORTING;
			subcategory[ID] = SubCategory;
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
			if (IndustrialMachine && go.TryGetComponent(out KPrefabID id))
				id.AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
			if (PowerInput != null)
				go.AddOrGet<EnergyConsumer>();
			if (PowerOutput != null)
				go.AddOrGet<EnergyGenerator>();
			// Set a default priority
			if (DefaultPriority != null && go.TryGetComponent(out Prioritizable pr)) {
				Prioritizable.AddRef(go);
				pr.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic,
					DefaultPriority ?? 5));
			}
		}

		public override string ToString() {
			return "PBuilding[ID={0}]".F(ID);
		}
	}
}
