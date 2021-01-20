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
using System.Collections.Generic;
using UnityEngine;

using IntHandle = HandleVector<int>.Handle;

namespace PeterHan.ResourcesInMotion {
	/// <summary>
	/// Patches which will be applied via annotations for ResourcesInMotion.
	/// </summary>
	public static class ResourcesInMotionPatches {
		/// <summary>
		/// The integrated period of the Accumulators class.
		/// </summary>
		private static float ACCUMULATOR_PERIOD = 3.0f;

		/// <summary>
		/// The safe value to use for PressureVulnerable's patch to plants wilting on load.
		/// It needs to be > 0.0f so the atmosphere check succeeds, and should be in the
		/// legal atmosphere range (kg) on plants so the density check succeeds.
		/// </summary>
		private const float PRESSURE_SAFE_VALUE = 0.3f;

		/// <summary>
		/// Whether accumulators are in their one cycle grace period.
		/// </summary>
		private static bool accumulatorGracePeriod;

		/// <summary>
		/// Clears the saved progress of a door animation.
		/// </summary>
		/// <param name="smi">The state machine instance to update.</param>
		/// <param name="oldState">The state name of the instance, prior to the Exit.</param>
		private static void ClearDoorAnimProgress(StateMachine.Instance smi, string oldState) {
			// smi.GetCurrentState().name reports the state AFTER the exit
			smi.Get<AnimationTrackerComponent>()?.ExitState(oldState);
		}

		/// <summary>
		/// Wraps calls to Accumulator.GetAverageRate to return a safe value while the game
		/// has not yet run long enough for PressureVulnerable to integrate the correct
		/// atmosphere density and element.
		/// </summary>
		/// <param name="accum">The (singleton) accumulators instance to wrap.</param>
		/// <param name="index">The accumulator index requested.</param>
		/// <returns>PRESSURE_SAFE_VALUE if the accumulators are being initialized, or the
		/// value of accum.GetAverageRate(index) otherwise.</returns>
		private static float GracefulAccumulate(Accumulators accum, IntHandle index) {
			return accumulatorGracePeriod ? PRESSURE_SAFE_VALUE : accum.GetAverageRate(index);
		}

		public static void OnLoad() {
			PUtil.InitLibrary();
			PUtil.RegisterPatchClass(typeof(ResourcesInMotionPatches));
			accumulatorGracePeriod = true;
			// Calculate the actual period
			if (typeof(Accumulators).GetFieldSafe("TIME_WINDOW", true)?.GetValue(null) is
					float period)
				ACCUMULATOR_PERIOD = period;
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			// Game.instance.accumulators was just initialized
			accumulatorGracePeriod = true;
		}

		[PLibPatch(RunAt.AfterDbInit, "OnPrefabInit", RequireType = "Door",
			RequireAssembly = "Assembly-CSharp", PatchType = HarmonyPatchType.Postfix)]
		internal static void PatchDoorPrefabInit(Component __instance) {
			// Door cannot be referenced before Db.Initialize because it has a static
			// constructor that requires Assets to be loaded
			__instance.gameObject.AddOrGet<AnimationTrackerComponent>();
		}

		/// <summary>
		/// Restores door animation progress based on how far it got when saved.
		/// </summary>
		/// <param name="smi">The state machine instance to update.</param>
		private static void RestoreDoorAnimProgress(StateMachine.Instance smi) {
			var tracker = smi.Get<AnimationTrackerComponent>();
			if (tracker != null) {
				string state = smi.GetCurrentState().name;
				float pct = tracker.GetPositionPercent(state);
				if (pct > 0.0f)
					smi.Get<KBatchedAnimController>()?.SetPositionPercent(pct);
				tracker.EnterState(state);
			}
		}

		/// <summary>
		/// Applied to Accumulators to implement the 3 second (one accumulator window) grace
		/// period on game load.
		/// </summary>
		[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.Sim200ms))]
		public static class Accumulators_Sim200ms_Patch {
			/// <summary>
			/// Applied before Sim200ms runs.
			/// </summary>
			internal static void Prefix(float ___elapsedTime, float dt) {
				if (___elapsedTime + dt >= ACCUMULATOR_PERIOD)
					accumulatorGracePeriod = false;
			}
		}

		/// <summary>
		/// Applied to Door.Controller to track door opening and closing status.
		/// </summary>
		[HarmonyPatch(typeof(Door.Controller), nameof(Door.Controller.InitializeStates))]
		public static class Door_Controller_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(Door.Controller __instance) {
				var opening = __instance.opening;
				var closing = __instance.closing;
				opening.Enter("ReloadProgress", RestoreDoorAnimProgress).Exit(
					"StopStateTracking", (smi) => ClearDoorAnimProgress(smi, opening.name));
				closing.Enter("ReloadProgress", RestoreDoorAnimProgress).Exit(
					"StopStateTracking", (smi) => ClearDoorAnimProgress(smi, closing.name));
			}
		}

		/// <summary>
		/// Applied to Growing to track the time spent in the fully grown state.
		/// </summary>
		[HarmonyPatch(typeof(Growing), "OnPrefabInit")]
		public static class Growing_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix(Growing __instance) {
				__instance.gameObject.AddOrGet<HarvestTrackerComponent>();
			}
		}

		/// <summary>
		/// Applied to Growing.States to track plants automatically harvesting after a few
		/// cycles.
		/// </summary>
		[HarmonyPatch(typeof(Growing.States), nameof(Growing.States.InitializeStates))]
		public static class Growing_States_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(Growing.States __instance) {
				__instance.grown.idle.Enter("ReloadProgress", (smi) => {
					var tracker = smi.Get<HarvestTrackerComponent>();
					var amount = Db.Get().Amounts.OldAge.Lookup(smi.master);
					if (tracker != null && amount != null) {
						string state = smi.GetCurrentState().name;
						float lastAge = tracker.GetOldAgeTracker(state);
						// Only apply to plants which should auto drop
						if (lastAge > 0.0f && smi.master.shouldGrowOld)
							amount.SetValue(lastAge);
						tracker.EnterState(state);
					}
				}).Exit("StopStateTracking", (smi) => smi.
					Get<HarvestTrackerComponent>()?.ExitState(smi.GetCurrentState().name));
			}
		}

		/// <summary>
		/// Applied to PressureVulnerable to fix plants wilting for one accumulation cycle on
		/// reload.
		/// 
		/// It has the side effect of allowing plants which would have otherwise been stifled
		/// for atmosphere reasons to grow for one accumulator cycle (3s). Can't win them
		/// all...
		/// </summary>
		[HarmonyPatch(typeof(PressureVulnerable), nameof(PressureVulnerable.SlicedSim1000ms))]
		public static class PressureVulnerable_SlicedSim1000ms_Patch {
			/// <summary>
			/// Transpiles SlicedSim1000ms to wrap accumulator calls with our own.
			/// </summary>
			internal static IEnumerable<CodeInstruction> Transpiler(
					IEnumerable<CodeInstruction> method) {
				return PPatchTools.ReplaceMethodCall(method, typeof(Accumulators).
					GetMethodSafe(nameof(Accumulators.GetAverageRate), false, typeof(
					IntHandle)), typeof(ResourcesInMotionPatches).GetMethodSafe(nameof(
					GracefulAccumulate), true, PPatchTools.AnyArguments));
			}
		}
	}
}
