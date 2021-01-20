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

using Harmony;
using PeterHan.PLib;
using PeterHan.PLib.Datafiles;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// Patches which will be applied via annotations for Airlock Door.
	/// </summary>
	public static class AirlockDoorPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
			AirlockDoorConfig.RegisterBuilding();
			PLocalization.Register();
			LocString.CreateLocStringKeys(typeof(AirlockDoorStrings.BUILDING));
			LocString.CreateLocStringKeys(typeof(AirlockDoorStrings.BUILDINGS));
		}

		/// <summary>
		/// Applied to Constructable to ensure that Duplicants will wait to dig out airlock
		/// doors until they are ready to be built.
		/// </summary>
		[HarmonyPatch(typeof(Constructable), "OnSpawn")]
		public static class Constructable_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(Constructable __instance,
					ref bool ___waitForFetchesBeforeDigging) {
				var building = __instance.GetComponent<Building>();
				// Does it have an airlock door?
				if (building != null && building.Def.BuildingComplete.GetComponent<
						AirlockDoor>() != null)
					___waitForFetchesBeforeDigging = true;
			}
		}

		/// <summary>
		/// Applied to MinionConfig to add the navigator transition for airlocks.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), "OnSpawn")]
		public static class MinionConfig_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				var nav = go.GetComponent<Navigator>();
				nav.transitionDriver.overrideLayers.Add(new AirlockDoorTransitionLayer(nav));
			}
		}
	}
}
