/*
 * Copyright 2022 Peter Han
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
			yield return typeof(Tutorial).GetMethodSafe("Render1000ms", false,
				PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe("LoadHiddenTutorialMessages", false,
				PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe("OnSpawn", false);
			yield return typeof(Tutorial).GetMethodSafe("OnCleanUp", false);
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
			yield return typeof(AirConditioner).GetMethodSafe("OnSpawn", false);
			yield return typeof(AlgaeHabitat).GetMethodSafe("OnSpawn", false);
			yield return typeof(ArcadeMachine).GetMethodSafe("OnSpawn", false);
			yield return typeof(ConduitConsumer).GetMethodSafe("OnSpawn", false);
			yield return typeof(ConduitDispenser).GetMethodSafe("OnSpawn", false);
			yield return typeof(Diggable).GetMethodSafe("OnReachableChanged", false,
				PPatchTools.AnyArguments);
			yield return typeof(ElectricalUtilityNetwork).GetMethodSafe("UpdateOverloadTime",
				false, PPatchTools.AnyArguments);
			yield return typeof(EspressoMachine).GetMethodSafe("OnSpawn", false);
			yield return typeof(FetchList2).GetMethodSafe("UpdateStatusItem", false,
				PPatchTools.AnyArguments);
			yield return typeof(HandSanitizer.Work).GetMethodSafe("OnPrefabInit", false);
			yield return typeof(HotTub).GetMethodSafe("OnSpawn", false);
			yield return typeof(IceCooledFan).GetMethodSafe("OnSpawn", false);
			yield return typeof(Juicer).GetMethodSafe("OnSpawn", false);
			yield return typeof(LiquidCooledFanWorkable).GetMethodSafe("OnSpawn", false);
			yield return typeof(MicrobeMusher).GetMethodSafe("OnSpawn", false);
			yield return typeof(MinionResume).GetMethodSafe(nameof(MinionResume.MasterSkill),
				false, PPatchTools.AnyArguments);
			yield return typeof(Moppable).GetMethodSafe("OnReachableChanged", false,
				PPatchTools.AnyArguments);
			yield return typeof(Phonobox).GetMethodSafe("OnSpawn", false);
			yield return typeof(SodaFountain).GetMethodSafe("OnSpawn", false);
			yield return typeof(SpaceHeater).GetMethodSafe("OnSpawn", false);
			yield return typeof(Telephone).GetMethodSafe("OnSpawn", false);
			yield return typeof(VerticalWindTunnel).GetMethodSafe("OnSpawn", false);
			yield return typeof(WaterCooler).GetMethodSafe("OnSpawn", false);
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
			return PPatchTools.ReplaceMethodCall(instructions, typeof(GameScheduler).
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
			yield return typeof(Tutorial).GetMethodSafe("OnDiscover", false,
				PPatchTools.AnyArguments);
			yield return typeof(Tutorial).GetMethodSafe("TutorialMessage", false,
				PPatchTools.AnyArguments);
		}

		/// <summary>
		/// Applied before these methods run.
		/// </summary>
		internal static bool Prefix() {
			return false;
		}
	}
}
