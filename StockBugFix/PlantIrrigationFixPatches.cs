// This code was reused from "Better Farming Effects and Tweaks" by Sanchozz. It is
// available from https://github.com/SanchozzDeponianin/ONIMods/blob/master/LICENSE under
// the MIT license.

using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.StockBugFix {
	/// <summary>
	/// Fixes a nasty bug where plants continue to burn their fertilizer even when wilted.
	/// </summary>
	internal static class PlantIrrigationFixPatches {
		private static readonly EventSystem.IntraObjectHandler<TreeBud> OnGrowDelegate =
			new EventSystem.IntraObjectHandler<TreeBud>((component, data) => {
				if (component != null)
					component.buddingTrunk?.Get()?.Trigger((int)GameHashes.Grow);
			});
		
		private static HarmonyMethod PatchMethod(string name) => new HarmonyMethod(
			typeof(PlantIrrigationFixPatches), name);

		/// <summary>
		/// Applies the patches for plant irrigation.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			// FertilizationMonitor
			harmony.Patch(typeof(FertilizationMonitor.Instance), nameof(FertilizationMonitor.
				Instance.StartAbsorbing), prefix: PatchMethod(nameof(StartAbsorbing_Prefix)));
			harmony.Patch(typeof(FertilizationMonitor), nameof(FertilizationMonitor.
				InitializeStates), postfix: PatchMethod(nameof(FM_InitializeStates_Postfix)));
			harmony.PatchTranspile(typeof(FertilizationMonitor), nameof(FertilizationMonitor.
				InitializeStates), PatchMethod(nameof(InitializeStates_Transpiler)));
			// IrrigationMonitor
			harmony.Patch(typeof(IrrigationMonitor.Instance), nameof(IrrigationMonitor.
				Instance.UpdateAbsorbing), prefix: PatchMethod(nameof(
				UpdateAbsorbing_Prefix)));
			harmony.Patch(typeof(IrrigationMonitor), nameof(IrrigationMonitor.
				InitializeStates), postfix: PatchMethod(nameof(IM_InitializeStates_Postfix)));
			// TreeBud
			harmony.Patch(typeof(PlantBranch.Instance), "SubscribeToTrunk",
				postfix: PatchMethod(nameof(SubscribeToTrunk_Postfix)));
			harmony.Patch(typeof(PlantBranch.Instance), "UnsubscribeToTrunk",
				postfix: PatchMethod(nameof(UnsubscribeToTrunk_Postfix)));
			// EntityTemplates
			var ep = PatchMethod(nameof(ExtendPlant_Postfix));
			harmony.Patch(typeof(EntityTemplates), nameof(EntityTemplates.
				ExtendPlantToFertilizable), postfix: ep);
			harmony.Patch(typeof(EntityTemplates).GetMethodSafe(nameof(EntityTemplates.
				ExtendPlantToIrrigated), true, typeof(GameObject),
				typeof(PlantElementAbsorber.ConsumeInfo[])), postfix: ep);
		}
		
		/// <summary>
		/// Applied after ExtendPlantToFertilizable and ExtendPlantToIrrigated runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void ExtendPlant_Postfix(GameObject __result) {
			__result.AddOrGet<PlantStatusMonitorFixed>();
		}

		/// <summary>
		/// Transpiles InitializeStates to update plant status monitors on any tag change,
		/// not just when plants wilt or recover.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static IEnumerable<CodeInstruction> InitializeStates_Transpiler(
				IEnumerable<CodeInstruction> instructions) {
			return PPatchTools.ReplaceConstant(instructions, (int)GameHashes.WiltRecover,
				(int)GameHashes.TagsChanged, true);
		}

		/// <summary>
		/// Applied after FertilizationMonitor.InitializeStates runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void FM_InitializeStates_Postfix(FertilizationMonitor __instance) {
			__instance.replanted.fertilized.absorbing.
				Enter(PlantStatusMonitorFixed.Subscribe).
				Exit(PlantStatusMonitorFixed.Unsubscribe);
		}

		/// <summary>
		/// Applied after IrrigationMonitor.InitializeStates runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void IM_InitializeStates_Postfix(IrrigationMonitor __instance) {
			__instance.replanted.irrigated.absorbing.
				Enter(PlantStatusMonitorFixed.Subscribe).
				Exit(PlantStatusMonitorFixed.Unsubscribe);
		}

		/// <summary>
		/// Applied before StartAbsorbing runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static bool StartAbsorbing_Prefix(FertilizationMonitor.Instance __instance) {
			var go = __instance.gameObject;
			bool absorb = go == null || (!go.HasTag(GameTags.Wilting) && (
				!go.TryGetComponent(out PlantStatusMonitorFixed monitor) ||
				monitor.ShouldAbsorb));
			if (!absorb)
				__instance.StopAbsorbing();
			return absorb;
		}

		/// <summary>
		/// Applied after SubscribeToTrunk runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void SubscribeToTrunk_Postfix(TreeBud __instance) {
			__instance.Subscribe((int)GameHashes.Grow, OnGrowDelegate);
		}

		/// <summary>
		/// Applied after UnsubscribeToTrunk runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void UnsubscribeToTrunk_Postfix(TreeBud __instance) {
			__instance.Unsubscribe((int)GameHashes.Grow, OnGrowDelegate, true);
		}

		/// <summary>
		/// Applied before UpdateAbsorbing runs.
		/// </summary>
		[HarmonyPriority(Priority.LowerThanNormal)]
		private static void UpdateAbsorbing_Prefix(IrrigationMonitor.Instance __instance,
				ref bool allow) {
			var go = __instance.gameObject;
			if (go != null)
				allow = allow && !go.HasTag(GameTags.Wilting) && (!go.TryGetComponent(
					out PlantStatusMonitorFixed monitor) || monitor.ShouldAbsorb);
		}
	}
}
