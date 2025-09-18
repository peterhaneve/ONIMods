/*
 * Copyright 2025 Peter Han
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

using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;

namespace PeterHan.ThermalPlate {
	/// <summary>
	/// Patches which will be applied for Thermal Interface Plate.
	/// </summary>
	public sealed class ThermalPlatePatches : KMod.UserMod2 {
		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PLocalization().Register();
			new PBuildingManager().Register(ThermalPlateConfig.CreateBuilding());
			new PVersionCheck().Register(this, new SteamVersionChecker());
			new PPatchManager(harmony).RegisterPatchClass(typeof(ThermalPlatePatches));
		}

		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
			ThermalInterfaceManager.DestroyInstance();
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			Game.Instance.gameObject.AddOrGet<ThermalInterfaceManager>();
		}

		/// <summary>
		/// Applied to Blueprints to add our collection source.
		/// </summary>
		[HarmonyPatch(typeof(Blueprints), MethodType.Constructor)]
		public static class Blueprints_Constructor_Patch {
			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(Blueprints __instance) {
				__instance.all.AddBlueprintsFrom(new ThermalBlueprintProvider());
			}
		}
	}
}
