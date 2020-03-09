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
		internal static PBuilding AirlockDoor;

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static void RegisterBuilding() {
			// Inititialize it here to allow localization to change the strings
			PBuilding.Register(AirlockDoor = new PBuilding(ID,
					AirlockDoorStrings.AIRLOCKDOOR_NAME) {
				AddAfter = PressureDoorConfig.ID,
				Animation = "door_external_kanim",
				Category = "Base",
				ConstructionTime = 60.0f,
				Decor = TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
				Description = AirlockDoorStrings.AIRLOCKDOOR_DESCRIPTION,
				EffectText = AirlockDoorStrings.AIRLOCKDOOR_EFFECT,
				Entombs = false,
				Floods = false,
				Height = 2,
				HP = 30,
				LogicIO = {
					LogicPorts.Port.InputPort(Door.OPEN_CLOSE_PORT_ID, CellOffset.none,
						AirlockDoorStrings.AIRLOCKDOOR_LOGIC_OPEN,
						AirlockDoorStrings.AIRLOCKDOOR_LOGIC_OPEN_ACTIVE,
						AirlockDoorStrings.AIRLOCKDOOR_LOGIC_OPEN_INACTIVE)
				},
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METAL, tier: 4),
					new BuildIngredient(TUNING.MATERIALS.PLASTIC, tier: 0)
				},
				OverheatTemperature = 75.0f + Constants.CELSIUS2KELVIN,
				Placement = BuildLocationRule.Tile,
				PowerInput = new PowerRequirement(120.0f, new CellOffset(0, 1)),
				RotateMode = PermittedRotations.R90,
				SceneLayer = Grid.SceneLayer.TileMain,
				Tech = "ValveMiniaturization",
				Width = 1
			});
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag) {
			base.ConfigureBuildingTemplate(go, prefab_tag);
			AirlockDoor?.ConfigureBuildingTemplate(go);
		}

		public override BuildingDef CreateBuildingDef() {
			var def = AirlockDoor?.CreateDef();
			def.ForegroundLayer = Grid.SceneLayer.InteriorWall;
			def.TileLayer = ObjectLayer.FoundationTile;
			return def;
		}

		public override void DoPostConfigureUnderConstruction(GameObject go) {
			AirlockDoor?.CreateLogicPorts(go);
		}

		public override void DoPostConfigurePreview(BuildingDef def, GameObject go) {
			AirlockDoor?.CreateLogicPorts(go);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			AirlockDoor?.DoPostConfigureComplete(go);
			AirlockDoor?.CreateLogicPorts(go);
			go.AddOrGet<AirlockDoor>();
			go.AddOrGet<ZoneTile>();
			go.AddOrGet<AccessControl>().controlEnabled = true;
			go.AddOrGet<KBoxCollider2D>();
			Prioritizable.AddRef(go);
			go.AddOrGet<CopyBuildingSettings>();
			go.AddOrGet<Workable>().workTime = 3f;
			UnityEngine.Object.DestroyImmediate(go.GetComponent<BuildingEnabledButton>());
			go.GetComponent<KBatchedAnimController>().initialAnim = "closed";
		}
	}
}
