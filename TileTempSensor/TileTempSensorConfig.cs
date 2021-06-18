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

using PeterHan.PLib.Core;
using PeterHan.PLib.Buildings;
using UnityEngine;

namespace PeterHan.TileTempSensor {
	/// <summary>
	/// A Thermo Sensor which occupies a full tile.
	/// </summary>
	public sealed class TileTempSensorConfig : IBuildingConfig {
		/// <summary>
		/// The building ID.
		/// </summary>
		internal const string ID = "TileTempSensor";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding TileTempSensor;

		/// <summary>
		/// Creates this building.
		/// </summary>
		/// <returns>The building instance to be registered.</returns>
		internal static PBuilding CreateBuilding() {
			return TileTempSensor = new PBuilding(ID,
					TileTempSensorStrings.BUILDINGS.PREFABS.TILETEMPSENSOR.NAME) {
				AddAfter = LogicTemperatureSensorConfig.ID,
				Animation = "thermo_tile_kanim",
				AudioCategory = "Metal",
				AudioSize = "small",
				Category = "Automation",
				ConstructionTime = 30.0f,
				Decor = TUNING.BUILDINGS.DECOR.BONUS.TIER0,
				Description = null,
				EffectText = null,
				Entombs = false,
				Floods = false,
				Height = 1,
				HP = 100,
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METALS, tier: 2)
				},
				IsSolidTile = true,
				LogicIO = {
					LogicPorts.Port.OutputPort(LogicSwitch.PORT_ID, new CellOffset(0, 0),
					STRINGS.BUILDINGS.PREFABS.LOGICTEMPERATURESENSOR.LOGIC_PORT,
					STRINGS.BUILDINGS.PREFABS.LOGICTEMPERATURESENSOR.LOGIC_PORT_ACTIVE,
					STRINGS.BUILDINGS.PREFABS.LOGICTEMPERATURESENSOR.LOGIC_PORT_INACTIVE, true)
				},
				ObjectLayer = PGameUtils.GetObjectLayer(nameof(ObjectLayer.Backwall),
					ObjectLayer.Backwall),
				Placement = BuildLocationRule.Tile,
				SceneLayer = Grid.SceneLayer.TileMain,
				Tech = "HVAC",
				ViewMode = OverlayModes.Logic.ID,
				Width = 1
			};
		}

		public override BuildingDef CreateBuildingDef() {
#if false
			// An honest effort, but the thermo sensor has no sounds anyways
			string animName = TileTempSensor.Animation;
			PUtil.CopySoundsToAnim(animName, "switchthermal_kanim");
			SoundEventVolumeCache.instance.AddVolume(animName, "PowerSwitch_on",
				TUNING.NOISE_POLLUTION.NOISY.TIER3);
			SoundEventVolumeCache.instance.AddVolume(animName, "PowerSwitch_off",
				TUNING.NOISE_POLLUTION.NOISY.TIER3);
#endif
			GeneratedBuildings.RegisterWithOverlay(OverlayModes.Logic.HighlightItemIDs, ID);
			return TileTempSensor.CreateDef();
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			TileTempSensor?.ConfigureBuildingTemplate(go);
			BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation),
				prefab_tag);
			// Must occupy the tile to transfer heat
			var occupier = go.AddOrGet<SimCellOccupier>();
			occupier.movementSpeedMultiplier = TUNING.DUPLICANTSTATS.MOVEMENT.NEUTRAL;
			occupier.notifyOnMelt = true;
			go.AddOrGet<TileTemperature>();
			go.AddOrGet<BuildingHP>().destroyOnDamaged = true;
		}

		public override void DoPostConfigureComplete(GameObject go) {
			TileTempSensor?.DoPostConfigureComplete(go);
			TileTempSensor?.CreateLogicPorts(go);
			var tempSensor = go.AddOrGet<LogicTemperatureSensor>();
			tempSensor.manuallyControlled = false;
			tempSensor.minTemp = 0f;
			tempSensor.maxTemp = 9999f;
			go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles, false);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			TileTempSensor?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			TileTempSensor?.CreateLogicPorts(go);
		}
	}
}
