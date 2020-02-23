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

using Harmony;
using PeterHan.PLib;
using UnityEngine;

namespace PeterHan.SmartPumps {
	/// <summary>
	/// Patches which will be applied via annotations for Smart Pumps.
	/// </summary>
	public static class SmartPumpsPatches {
		public static void OnLoad() {
			PUtil.InitLibrary();
			FilteredGasPumpConfig.RegisterBuilding();
			FilteredLiquidPumpConfig.RegisterBuilding();
			VacuumPumpConfig.RegisterBuilding();
		}

		/// <summary>
		/// Applied to Db to properly register the status items on load.
		/// </summary>
		[HarmonyPatch(typeof(Db), "Initialize")]
		public static class Db_Initialize_Patch {
			/// <summary>
			/// Applied after Initialize runs.
			/// </summary>
			internal static void Postfix() {
				FilteredPump.CreateStatusItems();
			}
		}

		/// <summary>
		/// Applied to FilterSideScreen to show the screen for filtered gas and liquid pumps.
		/// </summary>
		[HarmonyPatch(typeof(FilterSideScreen), "IsValidForTarget")]
		public static class FilterSideScreen_IsValidForTarget_Patch {
			/// <summary>
			/// Applied after IsValidForTarget runs.
			/// </summary>
			internal static void Postfix(FilterSideScreen __instance, GameObject target,
					ref bool __result) {
				var prefabID = target.GetComponentSafe<KPrefabID>();
				if (target.GetComponent<Filterable>() != null && __instance.isLogicFilter &&
						prefabID != null) {
					// Some targets do not have an ID?
					var id = prefabID.PrefabTag;
					if (id == FilteredGasPumpConfig.ID || id == FilteredLiquidPumpConfig.ID)
						__result = true;
				}
			}
		}
	}
}
