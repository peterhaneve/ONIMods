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

using PeterHan.PLib;
using PeterHan.PLib.Buildings;
using UnityEngine;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// A gas pump which only pulls a specified gas if seen.
	/// </summary>
	public sealed class FilteredGasPumpConfig : IBuildingConfig {
		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding GasPumpFiltered;

		/// <summary>
		/// The building ID.
		/// </summary>
		internal const string ID = "FilteredGasPump";

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static void RegisterBuilding() {
			// Inititialize it here to allow localization to change the strings
			PBuilding.Register(GasPumpFiltered = new PBuilding(ID,
				SmartPumpsStrings.GASPUMP_NAME) {
				AddAfter = "GasMiniPump",
				Animation = "pumpGasFiltered_kanim",
				Category = "HVAC",
				ConstructionTime = 45.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = SmartPumpsStrings.GASPUMP_DESCRIPTION,
				EffectText = SmartPumpsStrings.GASPUMP_EFFECT,
				Entombs = true,
				Floods = true,
				HeatGeneration = 2.0f,
				Height = 2,
				HP = 30,
				LogicIO = {
					PBuilding.CompatLogicPort(LogicPortSpriteType.Input, new CellOffset(0, 1))
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 1),
					new BuildIngredient(TUNING.MATERIALS.PLASTIC, tier: 0)
				},
				OutputConduits = {
					new ConduitConnection(ConduitType.Gas, new CellOffset(1, 1))
				},
				OverheatTemperature = 75.0f + Constants.CELSIUS2KELVIN,
				Placement = BuildLocationRule.Anywhere,
				PowerInput = new PowerRequirement(240.0f, new CellOffset(0, 1)),
				Tech = "ValveMiniaturization",
				ViewMode = OverlayModes.GasConduits.ID,
				Width = 2
			});
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			GasPumpFiltered?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			PUtil.CopySoundsToAnim(GasPumpFiltered.Animation, "pumpgas_kanim");
			return GasPumpFiltered?.CreateDef();
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			GasPumpFiltered?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			GasPumpFiltered?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			GasPumpFiltered?.DoPostConfigureComplete(go);
			GasPumpFiltered?.CreateLogicPorts(go);
			go.AddOrGet<LogicOperationalController>();
			go.AddOrGet<LoopingSounds>();
			var filterable = go.AddOrGet<Filterable>();
			filterable.filterElementState = Filterable.ElementState.Gas;
			go.AddOrGet<Storage>().capacityKg = 1f;
			var elementConsumer = go.AddOrGet<ElementConsumer>();
			elementConsumer.configuration = ElementConsumer.Configuration.Element;
			elementConsumer.elementToConsume = SimHashes.Vacuum;
			elementConsumer.consumptionRate = 0.5f;
			elementConsumer.storeOnConsume = true;
			elementConsumer.showInStatusPanel = false;
			elementConsumer.consumptionRadius = 2;
			elementConsumer.EnableConsumption(false);
			go.AddOrGetDef<OperationalController.Def>();
			go.AddOrGet<FilteredPump>();
		}
	}
}
