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

using HarmonyLib;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to Tutorial to disable ALL of the tutorial class, including the warnings!
	/// </summary>
	[HarmonyPatch]
	public static class DisableAllTutorialMethodsPatch {
		internal static bool Prepare() => FastTrackOptions.Instance.DisableTutorial ==
			FastTrackOptions.TutorialMessageDisable.None;

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.Render1000ms), false,
				PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.
				LoadHiddenTutorialMessages), false, PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.OnSpawn), false);
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.OnCleanUp), false);
		}

		/// <summary>
		/// Applied before these methods run.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}

	/// <summary>
	/// Applied to multiple methods to disable calls to the scheduler for the tutorial.
	/// </summary>
	[HarmonyPatch]
	public static class DisableTutorialSchedulerPatch {
		internal static bool Prepare() => FastTrackOptions.Instance.DisableTutorial !=
			FastTrackOptions.TutorialMessageDisable.All;

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(AirConditioner).GetMethodSafe(nameof(AirConditioner.OnSpawn),
				false);
			yield return typeof(AlgaeHabitat).GetMethodSafe(nameof(AlgaeHabitat.OnSpawn),
				false);
			yield return typeof(ArcadeMachine).GetMethodSafe(nameof(ArcadeMachine.OnSpawn),
				false);
			yield return typeof(ConduitConsumer).GetMethodSafe(nameof(ConduitConsumer.OnSpawn),
				false);
			yield return typeof(ConduitDispenser).GetMethodSafe(nameof(ConduitDispenser.
				OnSpawn), false);
			yield return typeof(Diggable).GetMethodSafe(nameof(Diggable.OnReachableChanged),
				false, PPatchTools.AnyArguments);
			yield return typeof(ElectricalUtilityNetwork).GetMethodSafe(nameof(
				ElectricalUtilityNetwork.UpdateOverloadTime), false, PPatchTools.AnyArguments);
			yield return typeof(EspressoMachine).GetMethodSafe(nameof(EspressoMachine.OnSpawn),
				false);
			yield return typeof(FetchList2).GetMethodSafe(nameof(FetchList2.UpdateStatusItem),
				false, PPatchTools.AnyArguments);
			yield return typeof(HandSanitizer.Work).GetMethodSafe(nameof(HandSanitizer.Work.
				OnPrefabInit), false);
			yield return typeof(HotTub).GetMethodSafe(nameof(HotTub.OnSpawn), false);
			yield return typeof(IceCooledFanWorkable).GetMethodSafe(nameof(
				IceCooledFanWorkable.OnSpawn), false);
			yield return typeof(Juicer).GetMethodSafe(nameof(Juicer.OnSpawn), false);
			yield return typeof(LiquidCooledFanWorkable).GetMethodSafe(nameof(
				LiquidCooledFanWorkable.OnSpawn), false);
			yield return typeof(MicrobeMusher).GetMethodSafe(nameof(MicrobeMusher.OnSpawn),
				false);
			yield return typeof(MinionResume).GetMethodSafe(nameof(MinionResume.MasterSkill),
				false, PPatchTools.AnyArguments);
			yield return typeof(Moppable).GetMethodSafe(nameof(Moppable.OnReachableChanged),
				false, PPatchTools.AnyArguments);
			yield return typeof(Phonobox).GetMethodSafe(nameof(Phonobox.OnSpawn), false);
			yield return typeof(SodaFountain).GetMethodSafe(nameof(SodaFountain.OnSpawn),
				false);
			yield return typeof(SpaceHeater).GetMethodSafe(nameof(SpaceHeater.OnSpawn), false);
			yield return typeof(Telephone).GetMethodSafe(nameof(Telephone.OnSpawn), false);
			yield return typeof(VerticalWindTunnel).GetMethodSafe(nameof(VerticalWindTunnel.
				OnSpawn), false);
			yield return typeof(WaterCooler).GetMethodSafe(nameof(WaterCooler.OnSpawn), false);
		}

		/// <summary>
		/// Replaces GameScheduler.Schedule with a method that does nothing.
		/// </summary>
		private static SchedulerHandle DoNotSchedule(GameScheduler scheduler, string name,
				float time, Action<object> callback, object callback_data,
				SchedulerGroup group) {
			_ = scheduler;
			_ = name;
			_ = time;
			_ = callback;
			_ = callback_data;
			_ = group;
			return new SchedulerHandle();
		}

		/// <summary>
		/// Transpiles the methods to remove the GameScheduler calls that create the tutorial
		/// messages.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			return PPatchTools.ReplaceMethodCallSafe(instructions, typeof(GameScheduler).
				GetMethodSafe(nameof(GameScheduler.Schedule), false, typeof(string), typeof(
				float), typeof(Action<object>), typeof(object), typeof(SchedulerGroup)),
				typeof(DisableTutorialSchedulerPatch).GetMethodSafe(nameof(DoNotSchedule),
				true, PPatchTools.AnyArguments));
		}
	}

	/// <summary>
	/// Applied to Tutorial to disable the remaining tutorial message methods.
	/// </summary>
	[HarmonyPatch]
	public static class DisableTutorialMethodsPatch {
		internal static bool Prepare() => FastTrackOptions.Instance.DisableTutorial !=
			FastTrackOptions.TutorialMessageDisable.All;

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.OnDiscover), false,
				PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe(nameof(Tutorial.TutorialMessage),
				false, PPatchTools.AnyArguments);
		}

		/// <summary>
		/// Applied before these methods run.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
