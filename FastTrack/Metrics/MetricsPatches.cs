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
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.Metrics {
	/// <summary>
	/// Applied to EventSystem to log event triggers.
	/// -1061186183 (AnimQueueComplete): 5,142us
	/// -1697596308 (OnStorageChange): 4,494us
	/// 387220196 (DestinationReached): 3,280us
	/// </summary>
	[HarmonyPatch(typeof(EventSystem), nameof(EventSystem.Trigger))]
	public static class EventSystem_Trigger_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(int hash, Stopwatch __state) {
			if (__state != null)
				DebugMetrics.EVENTS.AddSlice(hash.ToString(), __state.ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to every Update() method to log update metrics.
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
			return FindTargets(nameof(Game.Update));
		}

		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(Behaviour __instance, Stopwatch __state) {
			if (__state != null && __instance != null)
				DebugMetrics.UPDATE.AddSlice(__instance.GetType().FullName, __state.
					ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to every LateUpdate() method to log late update metrics.
	/// </summary>
	[HarmonyPatch]
	public static class ProfileLateUpdates {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		internal static IEnumerable<MethodBase> TargetMethods() {
			return ProfileUpdates.FindTargets(nameof(Game.LateUpdate));
		}

		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(Behaviour __instance, Stopwatch __state) {
			if (__state != null && __instance != null)
				DebugMetrics.LATE_UPDATE.AddSlice(__instance.GetType().FullName, __state.
					ElapsedTicks);
		}
	}

	/// <summary>
	/// Applied to StateMachineUpdater.BucketGroup.AdvanceOneSubTick to log sim/render update
	/// metrics if enabled.
	/// </summary>
	[HarmonyPatch(typeof(StateMachineUpdater.BucketGroup), nameof(StateMachineUpdater.
		BucketGroup.AdvanceOneSubTick))]
	public static class StateMachineUpdater_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Transpiles AdvanceOneSubTick to call our method wrapper instead.
		/// </summary>
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
			if (genArgs.Length > 0)
				targetType = genArgs[0].FullName;
			if (targetType == null || targetType.StartsWith("ISim") || targetType.StartsWith(
					"IRender"))
				bucket.Update(dt);
			else {
				var now = Stopwatch.StartNew();
				bucket.Update(dt);
				DebugMetrics.SIMANDRENDER[(int)group.updateRate].AddSlice(targetType, now.
					ElapsedTicks);
			}
		}
	}

	/// <summary>
	/// Applied to SimAndRenderScheduler.RenderEveryTickUpdater to log sim/render update
	/// metrics if enabled.
	/// </summary>
	[HarmonyPatch(typeof(SimAndRenderScheduler.RenderEveryTickUpdater),
		nameof(SimAndRenderScheduler.RenderEveryTickUpdater.Update))]
	public static class SimAndRenderScheduler_RenderEveryTickUpdater_Update_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
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
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(ref Stopwatch __state) {
			__state = Stopwatch.StartNew();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix(ISim4000ms updater, Stopwatch __state) {
			DebugMetrics.SIMANDRENDER[(int)UpdateRate.SIM_4000ms].AddSlice(updater.
				GetType().FullName, __state.ElapsedTicks);
		}
	}
}
