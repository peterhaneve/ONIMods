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

using PeterHan.PLib.Buildings;
using UnityEngine;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// A liquid pump which only pulls a specified liquid if seen.
	/// </summary>
	public sealed class FilteredLiquidPumpConfig : IBuildingConfig {
		/// <summary>
		/// The building ID.
		/// </summary>
		internal const string ID = "FilteredLiquidPump";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding LiquidPumpFiltered;

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static void RegisterBuilding() {
			// Inititialize it here to allow localization to change the strings
			PBuilding.Register(LiquidPumpFiltered = new PBuilding(ID,
				SmartPumpsStrings.LIQUIDPUMP_NAME) {
				AddAfter = "LiquidMiniPump",
				Animation = "pumpLiquidFiltered_kanim",
				Category = "Plumbing",
				ConstructionTime = 90.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = SmartPumpsStrings.LIQUIDPUMP_DESCRIPTION,
				EffectText = SmartPumpsStrings.LIQUIDPUMP_EFFECT,
				Entombs = true,
				Floods = false,
				HeatGeneration = 2.0f,
				Height = 2,
				HP = 100,
				LogicIO = {
					PBuilding.CompatLogicPort(LogicPortSpriteType.Input, new CellOffset(0, 1))
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 3),
					new BuildIngredient(TUNING.MATERIALS.PLASTIC, tier: 1)
				},
				OutputConduits = {
					new ConduitConnection(ConduitType.Liquid, new CellOffset(1, 1))
				},
				OverheatTemperature = 75.0f + Constants.CELSIUS2KELVIN,
				Placement = BuildLocationRule.Anywhere,
				PowerInput = new PowerRequirement(240.0f, new CellOffset(0, 1)),
				Tech = "ValveMiniaturization",
				Width = 2
			});
		}

		public override BuildingDef CreateBuildingDef() {
			return LiquidPumpFiltered?.CreateDef();
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			LiquidPumpFiltered?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			LiquidPumpFiltered?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			LiquidPumpFiltered?.DoPostConfigureComplete(go);
			LiquidPumpFiltered?.CreateLogicPorts(go);
			go.AddOrGet<LogicOperationalController>();
			go.AddOrGet<LoopingSounds>();
			var filterable = go.AddOrGet<Filterable>();
			filterable.filterElementState = Filterable.ElementState.Liquid;
			go.AddOrGet<Storage>().capacityKg = 20f;
			var elementConsumer = go.AddOrGet<ElementConsumer>();
			elementConsumer.configuration = ElementConsumer.Configuration.Element;
			elementConsumer.elementToConsume = SimHashes.Vacuum;
			elementConsumer.consumptionRate = 10f;
			elementConsumer.storeOnConsume = true;
			elementConsumer.showInStatusPanel = false;
			elementConsumer.consumptionRadius = 2;
			elementConsumer.EnableConsumption(false);
			go.AddOrGetDef<OperationalController.Def>();
			go.AddOrGet<FilteredPump>();
		}
	}
}
