/*
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

using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Klei;
using Klei.AI;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Groups patches used to totally turn off diseases (they can be quite meaningless).
	/// </summary>
	public static class NoDiseasePatches {
		/// <summary>
		/// Applies all disable disease patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			var doNothing = new HarmonyMethod(typeof(NoDiseasePatches), nameof(DisableMethod));
			var makeDeprecated = new HarmonyMethod(typeof(NoDiseasePatches), nameof(
				MakeBuildingDeprecated));
			var outFloat = typeof(float).MakeByRefType();
			// Disable auto disinfect
			harmony.Patch(typeof(AutoDisinfectableManager), nameof(AutoDisinfectableManager.
				AddAutoDisinfectable), prefix: doNothing);
			harmony.Patch(typeof(AutoDisinfectableManager), nameof(AutoDisinfectableManager.
				RemoveAutoDisinfectable), prefix: doNothing);
			// Hide the disease info panel
			harmony.Patch(typeof(DiseaseInfoScreen), nameof(DiseaseInfoScreen.
				IsValidForTarget), prefix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(IsValidForTarget_Prefix)));
			harmony.Patch(typeof(DiseaseInfoScreen), nameof(DiseaseInfoScreen.
				Refresh), prefix: doNothing);
			// Turn off disease updates in pipes
			harmony.Patch(typeof(ConduitDiseaseManager), nameof(ConduitDiseaseManager.
				AddDisease), prefix: doNothing);
			harmony.Patch(typeof(ConduitDiseaseManager), nameof(ConduitDiseaseManager.
				ModifyDiseaseCount), prefix: doNothing);
			harmony.Patch(typeof(ConduitDiseaseManager).GetMethodSafe(nameof(
				ConduitDiseaseManager.SetData), false, typeof(HandleVector<int>.Handle),
				typeof(ConduitFlow.ConduitContents).MakeByRefType()), prefix: doNothing);
			harmony.Patch(typeof(ConduitDiseaseManager), nameof(ConduitDiseaseManager.
				Sim200ms), prefix: doNothing);
			// Turn off disease containers
			harmony.Patch(typeof(DiseaseContainers), nameof(DiseaseContainers.AddDisease),
				prefix: doNothing);
			harmony.Patch(typeof(DiseaseContainers), nameof(DiseaseContainers.
				UpdateOverlayColours), prefix: doNothing);
			harmony.Patch(typeof(DiseaseContainers), nameof(DiseaseContainers.
				ModifyDiseaseCount), prefix: doNothing);
			harmony.Patch(typeof(DiseaseContainers), nameof(DiseaseContainers.Sim200ms),
				prefix: doNothing);
			// Disable disease emitters
			harmony.Patch(typeof(DiseaseDropper.Instance), nameof(DiseaseDropper.Instance.
				DropDisease), prefix: doNothing);
			harmony.Patch(typeof(DiseaseEmitter), nameof(DiseaseEmitter.SimRegister),
				prefix: doNothing);
			harmony.Patch(typeof(DiseaseEmitter), nameof(DiseaseEmitter.SimUnregister),
				prefix: doNothing);
			// Disable disinfect
			harmony.Patch(typeof(Disinfectable), nameof(Disinfectable.MarkForDisinfect),
				prefix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(MarkForDisinfect_Prefix)));
			// Turn off germ exposure tracker and monitor
			harmony.Patch(typeof(GermExposureMonitor), nameof(GermExposureMonitor.
				InitializeStates), prefix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(InitializeStates_Prefix)));
			harmony.Patch(typeof(GermExposureMonitor.Instance), nameof(GermExposureMonitor.
				Instance.OnAirConsumed), prefix: doNothing);
			harmony.Patch(typeof(GermExposureMonitor.Instance), nameof(GermExposureMonitor.
				Instance.TryInjectDisease), prefix: doNothing);
			harmony.Patch(typeof(GermExposureMonitor.Instance), nameof(GermExposureMonitor.
				Instance.UpdateReports), prefix: doNothing);
			// Wipe existing world germs on load
			harmony.Patch(typeof(Grid), nameof(Grid.InitializeCells), postfix:
				new HarmonyMethod(typeof(NoDiseasePatches), nameof(InitializeCells_Postfix)));
			// Hide disease overlay
			harmony.Patch(typeof(OverlayMenu), nameof(OverlayMenu.InitializeToggles),
				postfix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(InitializeToggles_Postfix)));
			// Disable disease on all element chunks
			harmony.Patch(typeof(PrimaryElement), nameof(PrimaryElement.AddDisease),
				prefix: doNothing);
			harmony.Patch(typeof(PrimaryElement), nameof(PrimaryElement.
				ForcePermanentDiseaseContainer), prefix: doNothing);
			harmony.Patch(typeof(PrimaryElement), nameof(PrimaryElement.ModifyDiseaseCount),
				prefix: doNothing);
			harmony.Patch(typeof(PrimaryElement), nameof(PrimaryElement.OnDeserialized),
				prefix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(OnDeserialized_Prefix)));
			// Prevent contracting new diseases and do not deserialize existing ones
			harmony.Patch(typeof(Sicknesses), nameof(Sicknesses.CreateInstance), prefix:
				new HarmonyMethod(typeof(NoDiseasePatches), nameof(CreateInstance_Prefix)));
			harmony.Patch(typeof(Sicknesses), nameof(Sicknesses.Infect), prefix: doNothing);
			harmony.Patch(typeof(SicknessInstance), nameof(SicknessInstance.
				InitializeAndStart), prefix: doNothing);
			// Disable disease related sim messages
			harmony.Patch(typeof(SimMessages), nameof(SimMessages.ModifyDiseaseOnCell),
				prefix: doNothing);
			harmony.Patch(typeof(SimMessages), nameof(SimMessages.ModifyCell), prefix:
				new HarmonyMethod(typeof(NoDiseasePatches), nameof(ModifyCell_Prefix)));
			// Disable getting disease from storage
			harmony.Patch(typeof(Storage).GetMethodSafe(nameof(Storage.ConsumeAndGetDisease),
				false, typeof(Tag), typeof(float), outFloat, typeof(SimUtil.DiseaseInfo).
				MakeByRefType(), outFloat), postfix: new HarmonyMethod(
				typeof(NoDiseasePatches), nameof(ConsumeAndGetDisease_Postfix)));
			// Hide the disinfect tool
			harmony.Patch(typeof(ToolMenu), nameof(ToolMenu.CreateBasicTools), postfix:
				new HarmonyMethod(typeof(NoDiseasePatches), nameof(CreateBasicTools_Postfix)));
			// Disable disease from world gen
			harmony.Patch(typeof(ProcGenGame.WorldGen), nameof(ProcGenGame.WorldGen.
				RenderToMap), postfix: new HarmonyMethod(typeof(NoDiseasePatches),
				nameof(RenderToMap_Postfix)));
			// Mark doctor buildings as deprecated (except apothecary in DLC for radpills)
			harmony.Patch(typeof(AdvancedApothecaryConfig), nameof(AdvancedApothecaryConfig.
				CreateBuildingDef), postfix: makeDeprecated);
			harmony.Patch(typeof(AdvancedDoctorStationConfig), nameof(
				AdvancedDoctorStationConfig.CreateBuildingDef), postfix: makeDeprecated);
			if (!DlcManager.FeatureRadiationEnabled())
				harmony.Patch(typeof(ApothecaryConfig), nameof(ApothecaryConfig.
					CreateBuildingDef), postfix: makeDeprecated);
			harmony.Patch(typeof(DoctorStationConfig), nameof(DoctorStationConfig.
				CreateBuildingDef), postfix: makeDeprecated);
			harmony.Patch(typeof(GasConduitDiseaseSensorConfig).GetMethodSafe(nameof(
				SolidConduitDiseaseSensorConfig.CreateBuildingDef), false, Type.EmptyTypes),
				postfix: makeDeprecated);
			harmony.Patch(typeof(LiquidConduitDiseaseSensorConfig).GetMethodSafe(nameof(
				LiquidConduitDiseaseSensorConfig.CreateBuildingDef), false, Type.EmptyTypes),
				postfix: makeDeprecated);
			harmony.Patch(typeof(LogicDiseaseSensorConfig), nameof(LogicDiseaseSensorConfig.
				CreateBuildingDef), postfix: makeDeprecated);
			harmony.Patch(typeof(SolidConduitDiseaseSensorConfig).GetMethodSafe(nameof(
				SolidConduitDiseaseSensorConfig.CreateBuildingDef), false, Type.EmptyTypes),
				postfix: makeDeprecated);
		}

		/// <summary>
		/// Applied after ConsumeAndGetDisease runs.
		/// </summary>
		private static void ConsumeAndGetDisease_Postfix(ref SimUtil.DiseaseInfo disease_info)
		{
			disease_info.count = 0;
			disease_info.idx = SimUtil.DiseaseInfo.Invalid.idx;
		}

		/// <summary>
		/// Applied after CreateBasicTools runs.
		/// </summary>
		private static void CreateBasicTools_Postfix(ToolMenu __instance) {
			__instance.basicTools.RemoveAll(toolCollection => toolCollection.icon ==
				"icon_action_disinfect");
		}

		/// <summary>
		/// Applied before CreateInstance runs.
		/// </summary>
		private static void CreateInstance_Prefix(ref SicknessInstance __result,
				Sickness sickness, Sicknesses __instance) {
			// No report, do not add, no event
			__result = new SicknessInstance(__instance.gameObject, sickness);
		}

		/// <summary>
		/// Prevents the target method from doing anything.
		/// </summary>
		private static bool DisableMethod() {
			return false;
		}

		/// <summary>
		/// Applied after InitializeCells runs.
		/// </summary>
		private static void InitializeCells_Postfix() {
			int n = Grid.WidthInCells * Grid.HeightInCells;
			byte idx = SimUtil.DiseaseInfo.Invalid.idx;
			for (int i = 0; i < n; i++) {
				int germs = Grid.DiseaseCount[i];
				byte disease = Grid.DiseaseIdx[i];
				if (disease != idx && germs > 0)
					// Modify disease to 0
					DiseaseCellModifier.ModifyDiseaseOnCell(i, idx, -germs);
			}
		}

		/// <summary>
		/// Applied before InitializeStates runs.
		/// </summary>
		private static void InitializeStates_Prefix(GermExposureMonitor __instance,
				ref StateMachine.BaseState default_state) {
			default_state = __instance.root;
			__instance.serializable = StateMachine.SerializeType.Never;
		}

		/// <summary>
		/// Applied after InitializeToggles runs.
		/// </summary>
		private static void InitializeToggles_Postfix(OverlayMenu __instance) {
			__instance.overlayToggleInfos.RemoveAll(info => info.icon == "overlay_disease");
		}

		/// <summary>
		/// Applied before IsValidForTarget runs.
		/// </summary>
		private static bool IsValidForTarget_Prefix(ref bool __result) {
			__result = false;
			return false;
		}

		/// <summary>
		/// Makes a building deprecated.
		/// </summary>
		private static void MakeBuildingDeprecated(BuildingDef __result) {
			__result.Deprecated = true;
		}

		/// <summary>
		/// Applied before MarkForDisinfect runs.
		/// </summary>
		private static bool MarkForDisinfect_Prefix(Disinfectable __instance) {
			__instance.isMarkedForDisinfect = false;
			return false;
		}

		/// <summary>
		/// Applied before ModifyCell runs.
		/// </summary>
		private static void ModifyCell_Prefix(ref byte disease_idx, ref int disease_count) {
			disease_count = 0;
			disease_idx = byte.MaxValue;
		}

		/// <summary>
		/// Applied before OnDeserialized runs.
		/// </summary>
		private static void OnDeserialized_Prefix(PrimaryElement __instance) {
			// With this set to zero, the game will never try to create a new disease container
			__instance.diseaseCount = 0;
			__instance.diseaseID.HashValue = 0;
		}

		/// <summary>
		/// Applied after RenderToMap runs.
		/// </summary>
		private static void RenderToMap_Postfix(ref Sim.DiseaseCell[] dcs) {
			int n = dcs.Length;
			byte idx = SimUtil.DiseaseInfo.Invalid.idx;
			for (int i = 0; i < n; i++) {
				ref var cell = ref dcs[i];
				cell.diseaseIdx = idx;
				cell.elementCount = 0;
			}
		}

		/// <summary>
		/// Extracts the SimMessages.ModifyDiseaseOnCell method to modify diseases on load
		/// even with the original method turned off.
		/// </summary>
		[HarmonyPatch(typeof(SimMessages), nameof(SimMessages.ModifyDiseaseOnCell))]
		internal static class DiseaseCellModifier {
			internal static bool Prepare() => FastTrackOptions.Instance.NoDisease;

			[HarmonyReversePatch]
			[HarmonyPatch(nameof(SimMessages.ModifyDiseaseOnCell))]
			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static void ModifyDiseaseOnCell(int gameCell, byte disease_idx,
					int disease_delta) {
				_ = gameCell;
				_ = disease_idx;
				_ = disease_delta;
				// Dummy code to ensure no inlining
				while (System.DateTime.Now.Ticks > 0L)
					throw new NotImplementedException("Reverse patch stub");
			}
		}
	}
}
