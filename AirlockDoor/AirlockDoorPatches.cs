/*
 * Copyright 2024 Peter Han
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

using System;
using HarmonyLib;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using System.Collections.Generic;
using KMod;
using PeterHan.PLib.PatchManager;
using UnityEngine;

namespace PeterHan.AirlockDoor {
	/// <summary>
	/// Patches which will be applied via annotations for Airlock Door.
	/// </summary>
	public sealed class AirlockDoorPatches : KMod.UserMod2 {
		/// <summary>
		/// The layer to check for airlock doors.
		/// </summary>
		private static int BUILDING_LAYER;
		
		/// <summary>
		/// Applied to ScoutRoverConfig and/or BaseRoverConfig to ensure they properly use
		/// airlock doors.
		/// </summary>
		private static void AddTransitionLayer(GameObject inst) {
			if (inst.TryGetComponent(out Navigator nav))
				nav.transitionDriver.overrideLayers.Add(new AirlockDoorTransitionLayer(nav));
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			var baseRover = PPatchTools.GetTypeSafe(nameof(BaseRoverConfig));
			var targetMethod = new HarmonyMethod(typeof(AirlockDoorPatches),
				nameof(AddTransitionLayer));
			harmony.Patch(baseRover ?? typeof(ScoutRoverConfig), nameof(ScoutRoverConfig.
				OnSpawn), targetMethod);
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			BUILDING_LAYER = (int)PGameUtils.GetObjectLayer(nameof(ObjectLayer.Building),
				ObjectLayer.Building);
			PUtil.InitLibrary();
			new PPatchManager(harmony).RegisterPatchClass(typeof(AirlockDoorPatches));
			new PBuildingManager().Register(AirlockDoorConfig.CreateBuilding());
			new PLocalization().Register();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to BaseMinionConfig to add the navigator transition for airlocks.
		/// </summary>
		[PLibPatch(RunAt.AfterDbInit, nameof(BaseMinionConfig.BaseOnSpawn),
			RequireType = nameof(BaseMinionConfig), PatchType = HarmonyPatchType.Postfix)]
		internal static void MinionSpawn_Postfix(GameObject go) {
			if (go.TryGetComponent(out Navigator nav))
				nav.transitionDriver.overrideLayers.Add(new AirlockDoorTransitionLayer(nav));
		}
		
		/// <summary>
		/// Checks to see if a grid cell is solid and not an Airlock Door.
		/// </summary>
		/// <param name="cell">The grid cell to check.</param>
		/// <returns>true if the cell is solid and not inside an Airlock Door, or false
		/// otherwise.</returns>
		private static bool SolidAndNotAirlock(ref Grid.BuildFlagsSolidIndexer _, int cell) {
			GameObject go;
			return Grid.Solid[cell] && (!Grid.IsValidCell(cell) || (go = Grid.Objects[cell,
				BUILDING_LAYER]) == null || !go.TryGetComponent(out AirlockDoor _));
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
				// Does it have an airlock door?
				if (__instance.TryGetComponent(out Building building) && building.Def.
						BuildingComplete.TryGetComponent(out AirlockDoor _))
					___waitForFetchesBeforeDigging = true;
			}
		}

		/// <summary>
		/// Applied to DoorTransitionLayer to fix a base game bug where the void offsets
		/// (which can be null if swapped with an empty transition) are recklessly used by
		/// DoorTransitionLayer without any checks.
		/// </summary>
		[HarmonyPatch(typeof(DoorTransitionLayer), nameof(DoorTransitionLayer.BeginTransition))]
		public static class DoorTransitionLayer_BeginTransition_Patch {
			/// <summary>
			/// Applied before BeginTransition runs.
			/// </summary>
			internal static void Prefix(Navigator.ActiveTransition transition) {
				ref var pending = ref transition.navGridTransition;
				if (pending.voidOffsets == null)
					pending.voidOffsets = Array.Empty<CellOffset>();
			}
		}

		/// <summary>
		/// Applied to FallMonitor.Instance to fix the entombment monitor triggering on
		/// Duplicants inside an airlock door [was changed with the Re-Rocketry update,
		/// EX-449549].
		/// </summary>
		[HarmonyPatch(typeof(FallMonitor.Instance), nameof(FallMonitor.Instance.
			UpdateFalling))]
		public static class FallMonitor_Instance_UpdateFalling_Patch {
			/// <summary>
			/// Transpiles UpdateFalling to replace Grid.Solid calls with calls that respect
			/// Airlock Door.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				// Default indexer (this[]) is hardcode named Item
				// https://docs.microsoft.com/en-us/dotnet/api/system.type.getproperty?view=net-5.0
				return PPatchTools.ReplaceMethodCallSafe(method, typeof(Grid.
					BuildFlagsSolidIndexer).GetPropertyIndexedSafe<bool>("Item", false,
					typeof(int))?.GetGetMethod(), typeof(AirlockDoorPatches).GetMethodSafe(
					nameof(SolidAndNotAirlock), true, typeof(Grid.BuildFlagsSolidIndexer).
					MakeByRefType(), typeof(int)));
			}
		}
	}
}
