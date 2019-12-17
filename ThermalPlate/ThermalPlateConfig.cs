/*
 * Copyright 2019 Peter Han
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
using System;
using UnityEngine;

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// A drywall replacement which transfers heat between buildings in that tile, even in vacuum.
	/// </summary>
	public class ThermalPlateConfig : IBuildingConfig {
		/// <summary>
		/// The building ID.
		/// </summary>
		internal const string ID = "ThermalInterfacePlate";

		/// <summary>
		/// The completed building template.
		/// </summary>
		internal static PBuilding ThermalInterfacePlate;

		public static void OnLoad() {
			PUtil.InitLibrary();
			RegisterBuilding();
		}

		/// <summary>
		/// Registers this building.
		/// </summary>
		internal static void RegisterBuilding() {
			// Inititialize it here to allow localization to change the strings
			PBuilding.Register(ThermalInterfacePlate = new PBuilding(ID,
					ThermalPlateStrings.THERMALPLATE_NAME) {
				AddAfter = "ExteriorWall",
				Animation = "thermalPlate_kanim",
				AudioCategory = "Metal",
				Category = "Utilities",
				ConstructionTime = 30.0f,
				Description = ThermalPlateStrings.THERMALPLATE_DESCRIPTION,
				EffectText = ThermalPlateStrings.THERMALPLATE_EFFECT,
				Entombs = false,
				Floods = false,
				Height = 1,
				HP = 30,
				Ingredients = {
					new BuildIngredient(TUNING.MATERIALS.REFINED_METALS, tier: 3)
				},
				ObjectLayer = ObjectLayer.Backwall,
				Placement = BuildLocationRule.NotInTiles,
				SceneLayer = Grid.SceneLayer.Backwall,
				Tech = "Suits",
				Width = 1
			});
		}

		public override BuildingDef CreateBuildingDef() {
			var def = ThermalInterfacePlate.CreateDef();
			try {
				// Is "Drywall Hides Pipes" installed? If so, hide pipes with this too
				if (Type.GetType("DrywallHidesPipes.DrywallPatch, DrywallHidesPipes-merged",
						false, false) != null)
					def.SceneLayer = Grid.SceneLayer.LogicGatesFront;
			} catch (Exception e) {
				PUtil.LogExcWarn(e);
			}
			try {
				// Is "Faster Drywall & Plate Construction" installed? If so, reduce
				// construction time by 5x
				if (Type.GetType("Patches.ExteriorWallAdjust, ClassLibrary1", false,
						false) != null)
					def.ConstructionTime = 6.0f;
			} catch (Exception e) {
				PUtil.LogExcWarn(e);
			}
			return def;
		}

		public override void ConfigureBuildingTemplate(GameObject go, Tag prefabTag) {
			GeneratedBuildings.MakeBuildingAlwaysOperational(go);
			go.AddOrGet<AnimTileable>().objectLayer = ObjectLayer.Backwall;
			go.AddComponent<ZoneTile>();
			BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation),
				prefabTag);
		}

		public override void DoPostConfigureComplete(GameObject go) {
			GeneratedBuildings.RemoveLoopingSounds(go);
			go.AddComponent<ThermalInterfacePlate>();
		}
	}

}
