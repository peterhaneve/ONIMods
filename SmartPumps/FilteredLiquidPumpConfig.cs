﻿/*
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

using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
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
		internal static PBuilding CreateBuilding() {
			// Inititialize it here to allow localization to change the strings
			return LiquidPumpFiltered = new PBuilding(ID, SmartPumpsStrings.BUILDINGS.PREFABS.
					FILTEREDLIQUIDPUMP.NAME) {
				AddAfter = "LiquidMiniPump",
				Animation = "pumpLiquidFiltered_kanim",
				Category = "Plumbing",
				ConstructionTime = 90.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = null,
				EffectText = null,
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
				PowerInput = new PowerRequirement(Mathf.Max(1.0f, SmartPumpsOptions.Instance.
					PowerLargeLiquidPump), new CellOffset(0, 1)),
				SubCategory = "pumps",
				Tech = "ValveMiniaturization",
				ViewMode = OverlayModes.LiquidConduits.ID,
				Width = 2
			};
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			LiquidPumpFiltered?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			// Believe it or not, stock game pumps make no noise pollution
			PGameUtils.CopySoundsToAnim(LiquidPumpFiltered.Animation, "pumpliquid_kanim");
			GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, ID);
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
			elementConsumer.consumptionRate = Mathf.Max(0.0f, Mathf.Min(10.0f,
				SmartPumpsOptions.Instance.RateLargeLiquidPump));
			elementConsumer.storeOnConsume = true;
			elementConsumer.showInStatusPanel = false;
			elementConsumer.consumptionRadius = 2;
			elementConsumer.EnableConsumption(false);
			go.AddOrGetDef<OperationalController.Def>();
			go.AddOrGet<FilteredPump>();
		}
	}
}
