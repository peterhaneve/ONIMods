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

using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// A gas pump which creates vacuums very quickly by pulling from a large radius. However,
	/// its output stats are not much better than the mini pump, and it uses more power.
	/// </summary>
	public sealed class VacuumPumpConfig : IBuildingConfig {
		/// <summary>
		/// The building ID.
		/// </summary>
		internal const string ID = "VacuumPump";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding VacuumPump;

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static PBuilding CreateBuilding() {
			// Inititialize it here to allow localization to change the strings
			return VacuumPump = new PBuilding(ID, SmartPumpsStrings.VACUUMPUMP_NAME) {
				AddAfter = FilteredGasPumpConfig.ID,
				Animation = "pumpVacuum_kanim",
				Category = "HVAC",
				ConstructionTime = 90.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = SmartPumpsStrings.VACUUMPUMP_DESCRIPTION,
				EffectText = SmartPumpsStrings.VACUUMPUMP_EFFECT,
				Entombs = true,
				Floods = true,
				HeatGeneration = 0.1f,
				Height = 2,
				HP = 30,
				LogicIO = {
					PBuilding.CompatLogicPort(LogicPortSpriteType.Input, new CellOffset(0, 1))
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.PLASTIC, tier: 1),
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 0)
				},
				OutputConduits = {
					new ConduitConnection(ConduitType.Gas, new CellOffset(0, 1))
				},
				OverheatTemperature = 75.0f + Constants.CELSIUS2KELVIN,
				Placement = BuildLocationRule.Anywhere,
				PowerInput = new PowerRequirement(90.0f, CellOffset.none),
				RotateMode = PermittedRotations.R360,
				Tech = "ValveMiniaturization",
				ViewMode = OverlayModes.GasConduits.ID,
				Width = 1
			};
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			VacuumPump?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			PGameUtils.CopySoundsToAnim(VacuumPump.Animation, "pumpgas_kanim");
			GeneratedBuildings.RegisterWithOverlay(OverlayScreen.GasVentIDs, ID);
			return VacuumPump?.CreateDef();
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			VacuumPump?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			VacuumPump?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			VacuumPump?.DoPostConfigureComplete(go);
			VacuumPump?.CreateLogicPorts(go);
			go.AddOrGet<LogicOperationalController>();
			go.AddOrGet<LoopingSounds>();
			var filterable = go.AddOrGet<Filterable>();
			filterable.filterElementState = Filterable.ElementState.Gas;
			go.AddOrGet<Storage>().capacityKg = 0.1f;
			var elementConsumer = go.AddOrGet<ElementConsumer>();
			elementConsumer.configuration = ElementConsumer.Configuration.AllGas;
			elementConsumer.elementToConsume = SimHashes.Vacuum;
			elementConsumer.consumptionRate = 0.05f;
			elementConsumer.storeOnConsume = true;
			elementConsumer.showInStatusPanel = false;
			elementConsumer.consumptionRadius = 4;
			elementConsumer.EnableConsumption(false);
			go.AddOrGetDef<OperationalController.Def>();
			go.AddOrGet<PumpFixed>().detectRadius = 2;
		}
	}
}
