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

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// An airlock door that requires power, but allows Duplicants to pass without ever
	/// transmitting liquid or gas (unless set to Open).
	/// </summary>
	public sealed class AirlockDoorConfig : IBuildingConfig {
		public const string ID = "PAirlockDoor";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding AirlockDoorTemplate;

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static void RegisterBuilding() {
			// Inititialize it here to allow localization to change the strings
			PBuilding.Register(AirlockDoorTemplate = new PBuilding(ID,
					AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.NAME) {
				AddAfter = PressureDoorConfig.ID,
				Animation = "airlock_door_kanim",
				Category = "Base",
				ConstructionTime = 60.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = null, EffectText = null,
				Entombs = false,
				Floods = false,
				Height = 2,
				HP = 30,
				LogicIO = {
					LogicPorts.Port.InputPort(AirlockDoor.OPEN_CLOSE_PORT_ID, CellOffset.none,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_ACTIVE,
						AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_INACTIVE)
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 4),
				},
				// Overheating is not possible on solid tile buildings because they bypass
				// structure temperatures so sim will never send the overheat notification
				Placement = BuildLocationRule.Tile,
				PowerInput = new PowerRequirement(120.0f, new CellOffset(0, 0)),
				RotateMode = PermittedRotations.Unrotatable,
				SceneLayer = Grid.SceneLayer.InteriorWall,
				Tech = "ImprovedGasPiping",
				Width = 3
			});
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			AirlockDoorTemplate?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			var def = AirlockDoorTemplate?.CreateDef();
			def.ForegroundLayer = Grid.SceneLayer.TileMain;
			def.PreventIdleTraversalPastBuilding = true;
			// /5 multiplier to thermal conductivity
			def.ThermalConductivity = 0.2f;
			def.TileLayer = ObjectLayer.FoundationTile;
			return def;
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			AirlockDoorTemplate?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			AirlockDoorTemplate?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			AirlockDoorTemplate?.DoPostConfigureComplete(go);
			AirlockDoorTemplate?.CreateLogicPorts(go);
			var ad = go.AddOrGet<AirlockDoor>();
			ad.EnergyCapacity = 10000.0f;
			ad.EnergyPerUse = 2000.0f;
			var occupier = go.AddOrGet<SimCellOccupier>();
			occupier.doReplaceElement = true;
			occupier.notifyOnMelt = true;
			go.AddOrGet<TileTemperature>();
			go.AddOrGet<AccessControl>().controlEnabled = true;
			go.AddOrGet<KBoxCollider2D>();
			go.AddOrGet<BuildingHP>().destroyOnDamaged = true;
			Prioritizable.AddRef(go);
			go.AddOrGet<CopyBuildingSettings>().copyGroupTag = GameTags.Door;
			go.AddOrGet<Workable>().workTime = 3f;
			Object.DestroyImmediate(go.GetComponent<BuildingEnabledButton>());
			go.GetComponent<KBatchedAnimController>().initialAnim = "closed";
		}
	}
}
