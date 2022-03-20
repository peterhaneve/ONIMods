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
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.Metrics {
	// Global, Game, World
#if DEBUG
	// Replace with method to patch
	// Game#LateUpdate is 100-150ms/1000ms
	// Game#Update is 200-250ms
	// Pathfinding#UpdateNavGrids is <20ms
	// StatusItemRenderer#RenderEveryTick could use some work but is only ~10ms
	//  (need to excise GetComponent calls which is a massive transpiler)
	// ElectricalUtilityNetwork#Update is ~10ms
	// KBatchedAnimUpdater#LateUpdate is ~50ms
	// AnimEventManager#Update is 20ms but not much can be done
	// KBatchedAnimUpdater#UpdateRegisteredAnims is 40ms
	// KAnimBatchManager#Render was 25ms
	// KAnimBatchManager#UpdateDirty is 30ms+
	// ConduitFlow.Sim200ms is <10ms
	// ChoreConsumer.FindNextChore is <10ms
	[HarmonyPatch(typeof(KBatchedAnimUpdater), "UpdateVisibility")]
	public static class TimePatch1 {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		internal static void Postfix(Stopwatch __state) {
			if (__state != null)
				DebugMetrics.TRACKED[0].Log(__state.ElapsedTicks);
		}
	}

	[HarmonyPatch(typeof(KBatchedAnimUpdater), "UpdateRegisteredAnims")]
	public static class TimePatch2 {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		internal static void Postfix(Stopwatch __state) {
			if (__state != null)
				DebugMetrics.TRACKED[1].Log(__state.ElapsedTicks);
		}
	}
#endif

	/// <summary>
	/// Applied to BrainScheduler.BrainGroup to dump load balancing statistics.
	/// </summary>
	[HarmonyPatch]
	public static class BrainScheduler_BrainGroup_AdjustLoad_Patch {
		/// <summary>
		/// References the private inner type BrainScheduler.BrainGroup.
		/// </summary>
		private static readonly Type BRAIN_GROUP = typeof(BrainScheduler).GetNestedType(
			"BrainGroup", BindingFlags.Instance | PPatchTools.BASE_FLAGS);

		/// <summary>
		/// Gets the number of probes to run.
		/// </summary>
		private static readonly MethodInfo GET_PROBE_COUNT = BRAIN_GROUP?.
			GetPropertySafe<int>("probeCount", false)?.GetGetMethod(true);

		/// <summary>
		/// Gets the depth that probes will iterate.
		/// </summary>
		private static readonly MethodInfo GET_PROBE_SIZE = BRAIN_GROUP?.
			GetPropertySafe<int>("probeSize", false)?.GetGetMethod(true);

		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static MethodBase TargetMethod() {
			return BRAIN_GROUP?.GetMethodSafe(nameof(ICPULoad.AdjustLoad), false,
				typeof(float), typeof(float));
		}

		/// <summary>
		/// Applied after AdjustLoad runs.
		/// </summary>
		internal static void Postfix(float currentFrameTime, float frameTimeDelta,
				object __instance) {
			if (GET_PROBE_COUNT == null || !(GET_PROBE_COUNT.Invoke(__instance, null) is
					int probeCount))
				probeCount = 0;
			if (GET_PROBE_SIZE == null || !(GET_PROBE_SIZE.Invoke(__instance, null) is
					int probeSize))
				probeSize = 0;
			DebugMetrics.LogBrainBalance(__instance.GetType().Name, frameTimeDelta,
				currentFrameTime, probeCount, probeSize);
		}
	}

	/// <summary>
	/// Applied to every RenderImage() to log render metrics.
	/// </summary>
	[HarmonyPatch]
	public static class ProfileRenderImages {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static IEnumerable<MethodBase> TargetMethods() {
			var targets = new List<MethodBase>(128);
			foreach (var type in Assembly.GetAssembly(typeof(Game)).DefinedTypes)
				if (type != null && typeof(Behaviour).IsAssignableFrom(type)) {
					var method = type.GetMethod("OnRenderImage", PPatchTools.BASE_FLAGS |
						BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new Type[] {
							typeof(RenderTexture), typeof(RenderTexture)
						}, null);
					if (method != null)
						targets.Add(method);
				}
			return targets;
		}

		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		internal static void Postfix(Behaviour __instance, Stopwatch __state) {
			if (__state != null && __instance != null)
				DebugMetrics.RENDER_IMAGE.AddSlice(__instance.GetType().FullName, __state.
					ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to every Update() to log update metrics.
	/// </summary>
	[HarmonyPatch]
	public static class ProfileUpdates {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Finds all Unity base methods on classes with the specified name.
		/// </summary>
		/// <param name="name">The method name to look up.</param>
		/// <returns>A list of all base game classes defining that method.</returns>
		internal static IEnumerable<MethodBase> FindTargets(string name) {
			var targets = new List<MethodBase>(128);
			foreach (var type in Assembly.GetAssembly(typeof(Game)).DefinedTypes)
				if (type != null && typeof(Behaviour).IsAssignableFrom(type)) {
					var method = type.GetMethod(name, PPatchTools.BASE_FLAGS | BindingFlags.
						Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
					if (method != null)
						targets.Add(method);
				}
			return targets;
		}

		internal static IEnumerable<MethodBase> TargetMethods() {
			return FindTargets("Update");
		}

		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		internal static void Postfix(Behaviour __instance, Stopwatch __state) {
			if (__state != null && __instance != null)
				DebugMetrics.UPDATE.AddSlice(__instance.GetType().FullName, __state.
					ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to every LateUpdate() to log late update metrics.
	/// </summary>
	[HarmonyPatch]
	public static class ProfileLateUpdates {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static IEnumerable<MethodBase> TargetMethods() {
			return ProfileUpdates.FindTargets("LateUpdate");
		}

		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		internal static void Postfix(Behaviour __instance, Stopwatch __state) {
			if (__state != null && __instance != null)
				DebugMetrics.LATE_UPDATE.AddSlice(__instance.GetType().FullName, __state.
					ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to Sensors to log sensor update metrics if enabled.
	/// </summary>
	[HarmonyPatch(typeof(Sensors), nameof(Sensors.UpdateSensors))]
	public static class Sensors_UpdateSensors_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before UpdateSensors runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after UpdateSensors runs.
		/// </summary>
		internal static void Postfix(Stopwatch __state) {
			DebugMetrics.SENSORS.Log(__state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.RenderEveryTickUpdater to log sim/render update
	/// metrics if enabled.
	/// </summary>
	[HarmonyPatch(typeof(StateMachineUpdater.BucketGroup), nameof(StateMachineUpdater.
		BucketGroup.AdvanceOneSubTick))]
	public static class StateMachineUpdater_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static TranspiledMethod Transpiler(TranspiledMethod method) {
			var victim = typeof(StateMachineUpdater.BaseUpdateBucket).GetMethodSafe(nameof(
				StateMachineUpdater.BaseUpdateBucket.Update), false, typeof(float));
			foreach (var instr in method) {
				if (victim != null && instr.Is(OpCodes.Callvirt, victim)) {
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					instr.operand = typeof(StateMachineUpdater_Patch).GetMethodSafe(nameof(
						UpdateAndReport), true, typeof(StateMachineUpdater.BaseUpdateBucket),
						typeof(float), typeof(StateMachineUpdater.BucketGroup));
				}
				yield return instr;
			}
		}

		private static void UpdateAndReport(StateMachineUpdater.BaseUpdateBucket bucket,
				float dt, StateMachineUpdater.BucketGroup group) {
			var genArgs = bucket.GetType().GetGenericArguments();
			string targetType = null;
			Stopwatch now;
			if (genArgs != null && genArgs.Length > 0)
				targetType = genArgs[0]?.FullName;
			if (targetType == null || targetType.StartsWith("ISim") || targetType.StartsWith(
					"IRender"))
				bucket.Update(dt);
			else {
				now = Stopwatch.StartNew();
				bucket.Update(dt);
				DebugMetrics.SIMANDRENDER[(int)group.updateRate].AddSlice(targetType, now.
					ElapsedTicks);
			}
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.RenderEveryTickUpdater to log sim/render update
	/// metrics if enabled.
	[HarmonyPatch(typeof(SimAndRenderScheduler.RenderEveryTickUpdater),
		nameof(SimAndRenderScheduler.RenderEveryTickUpdater.Update))]
	public static class SimAndRenderScheduler_RenderEveryTickUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(IRenderEveryTick updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.RENDER_EVERY_TICK].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Render200ms to log sim/render update metrics if
	/// enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Render200ms),
		nameof(SimAndRenderScheduler.Render200ms.Update))]
	public static class SimAndRenderScheduler_Render200ms_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(IRender200ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.RENDER_200ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Render1000msUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Render1000msUpdater),
		nameof(SimAndRenderScheduler.Render1000msUpdater.Update))]
	public static class SimAndRenderScheduler_Render1000msUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(IRender1000ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.RENDER_1000ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.SimEveryTickUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.SimEveryTickUpdater),
		nameof(SimAndRenderScheduler.SimEveryTickUpdater.Update))]
	public static class SimAndRenderScheduler_SimEveryTickUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(ISimEveryTick updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_EVERY_TICK].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Sim33msUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Sim33msUpdater),
		nameof(SimAndRenderScheduler.Sim33msUpdater.Update))]
	public static class SimAndRenderScheduler_Sim33msUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(ISim33ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_33ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Sim200msUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Sim200msUpdater),
		nameof(SimAndRenderScheduler.Sim200msUpdater.Update))]
	public static class SimAndRenderScheduler_Sim200msUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(ISim200ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_200ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Sim1000msUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Sim1000msUpdater),
		nameof(SimAndRenderScheduler.Sim1000msUpdater.Update))]
	public static class SimAndRenderScheduler_Sim1000msUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(ISim1000ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_1000ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.Sim4000msUpdater to log sim/render update metrics
	/// if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.Sim4000msUpdater),
		nameof(SimAndRenderScheduler.Sim4000msUpdater.Update))]
	public static class SimAndRenderScheduler_Sim4000msUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		internal static void Postfix(ISim4000ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_4000ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}
}
