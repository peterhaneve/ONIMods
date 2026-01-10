/*
 * Copyright 2026 Peter Han
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

using Database;
using FACADES = PeterHan.ThermalPlate.ThermalPlateStrings.BUILDINGS.PREFABS.
	THERMALINTERFACEPLATE.FACADES;

namespace PeterHan.ThermalPlate {
	public sealed class ThermalBlueprintProvider : BlueprintProvider {
		private static BuildingFacadeInfo CreateFacade(string color, LocString name,
				LocString desc) {
			return new BuildingFacadeInfo(ThermalPlateConfig.ID + "_" + color,
				name, desc, PermitRarity.Universal, ThermalPlateConfig.ID, "thermalPlate_" +
				color + "_kanim");
		}

		public override void SetupBlueprints() {
			blueprintCollection.buildingFacades.AddRange(new BuildingFacadeInfo[] {
				CreateFacade("pastel_pink", FACADES.PASTELPINK.NAME,
					FACADES.PASTELPINK.DESC),
				CreateFacade("pastel_yellow", FACADES.PASTELYELLOW.NAME,
					FACADES.PASTELYELLOW.DESC),
				CreateFacade("pastel_green", FACADES.PASTELGREEN.NAME,
					FACADES.PASTELGREEN.DESC),
				CreateFacade("pastel_blue", FACADES.PASTELBLUE.NAME,
					FACADES.PASTELBLUE.DESC),
				CreateFacade("pastel_purple", FACADES.PASTELPURPLE.NAME,
					FACADES.PASTELPURPLE.DESC),
				CreateFacade("basic_blue_cobalt", FACADES.BASIC_BLUE_COBALT.NAME,
					FACADES.BASIC_BLUE_COBALT.DESC),
				CreateFacade("basic_green_kelly", FACADES.BASIC_GREEN_KELLY.NAME,
					FACADES.BASIC_GREEN_KELLY.DESC),
				CreateFacade("basic_grey_charcoal", FACADES.BASIC_GREY_CHARCOAL.NAME,
					FACADES.BASIC_GREY_CHARCOAL.DESC),
				CreateFacade("basic_orange_satsuma", FACADES.BASIC_ORANGE_SATSUMA.NAME,
					FACADES.BASIC_ORANGE_SATSUMA.DESC),
				CreateFacade("basic_pink_flamingo", FACADES.BASIC_PINK_FLAMINGO.NAME,
					FACADES.BASIC_PINK_FLAMINGO.DESC),
				CreateFacade("basic_red_deep", FACADES.BASIC_RED_DEEP.NAME,
					FACADES.BASIC_RED_DEEP.DESC),
				CreateFacade("basic_yellow_lemon", FACADES.BASIC_YELLOW_LEMON.NAME,
					FACADES.BASIC_YELLOW_LEMON.DESC)
			});
		}
	}
}
