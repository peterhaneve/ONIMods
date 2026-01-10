/*
 * Copyright 2026 Peter Han
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

using System.Collections;
using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

using GeyserType = GeyserConfigurator.GeyserType;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// Applied to Accumulators to fill the first value if the average has yet to be
	/// calculated.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.Accumulate))]
	public static class Accumulators_Accumulate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before Accumulate runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Accumulators __instance, HandleVector<int>.Handle handle,
				float amount) {
			var accumulated = __instance.accumulated;
			var average = __instance.average;
			float data = accumulated.GetData(handle);
			accumulated.SetData(handle, data + amount);
			// Prime the pump
			if (float.IsNaN(average.GetData(handle)))
				average.SetData(handle, amount);
			return false;
		}
	}

	/// <summary>
	/// Applied to Accumulators to preload an invalid value, for notifying the averaging
	/// system that it needs initialization.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.Add))]
	public static class Accumulators_Add_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied before Add runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(Accumulators __instance, ref HandleVector<int>.
				Handle __result) {
			__result = __instance.accumulated.Allocate(0f);
			__instance.average.Allocate(float.NaN);
			return false;
		}
	}

	/// <summary>
	/// Applied to Accumulators to substitute zero if no samples have been accumulated at all.
	/// </summary>
	[HarmonyPatch(typeof(Accumulators), nameof(Accumulators.GetAverageRate))]
	public static class Accumulators_GetAverageRate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		/// <summary>
		/// Applied after GetAverageRate runs.
		/// </summary>
		internal static void Postfix(ref float __result) {
			float r = __result;
			if (float.IsNaN(r) || float.IsInfinity(r))
				__result = 0.0f;
		}
	}

	/// <summary>
	/// Applied to ClusterManager to reduce memory allocations when accessing
	/// WorldContainers.
	/// </summary>
	[HarmonyPatch(typeof(ClusterManager), nameof(ClusterManager.WorldContainers),
		MethodType.Getter)]
	public static class ClusterManager_WorldContainers_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.AllocOpts;

		/// <summary>
		/// Transpiles the WorldContainers getter to remove the AsReadOnly call.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var targetMethod = typeof(List<>).MakeGenericType(typeof(WorldContainer)).
				GetMethodSafe(nameof(List<WorldContainer>.AsReadOnly), false);
			var method = new List<CodeInstruction>(instructions);
			if (targetMethod != null) {
				method.RemoveAll(instr => instr.Is(OpCodes.Callvirt, targetMethod));
#if DEBUG
				PUtil.LogDebug("Patched ClusterManager.WorldContainers");
#endif
			} else
				PUtil.LogWarning("Unable to patch ClusterManager.WorldContainers");
			return method;
		}
	}

	/// <summary>
	/// Applied to GeyserConfigurator to cache geyser types instead of linear lookups every
	/// frame.
	/// </summary>
	[HarmonyPatch(typeof(GeyserConfigurator), nameof(GeyserConfigurator.FindType))]
	public static class GeyserConfigurator_FindType_Patch {
		/// <summary>
		/// Caches geyser type lookups.
		/// </summary>
		private static readonly IDictionary<HashedString, GeyserType> CACHE =
			new Dictionary<HashedString, GeyserType>(32);

		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		/// <summary>
		/// Clears the geyser type cache.
		/// </summary>
		internal static void Cleanup() {
			CACHE.Clear();
		}

		/// <summary>
		/// Applied before FindType runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(HashedString typeId, ref GeyserType __result) {
			GeyserType geyserType;
			if (typeId.IsValid) {
				// Populate the cache
				if (CACHE.Count < 1) {
					var types = GeyserConfigurator.geyserTypes;
					int n = types.Count;
					for (int i = 0; i < n; i++) {
						var type = types[i];
						CACHE[type.id] = type;
					}
				}
				if (!CACHE.TryGetValue(typeId, out geyserType)) {
					PUtil.LogError("No such geyser ID: {0}!".F(typeId));
					geyserType = null;
				}
			} else {
				PUtil.LogError("Invalid geyser type ID specified!");
				geyserType = null;
			}
			__result = geyserType;
			return false;
		}
	}

	/// <summary>
	/// Applied to Growing.States to work around a base game bug where Arbor Tree branches set
	/// their growth rate too early during load.
	/// </summary>
	[HarmonyPatch(typeof(Growing.States), nameof(Growing.States.InitializeStates))]
	public static class Growing_States_InitializeStates_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.FlattenAverages;

		private static IEnumerator FixLoadCoroutine(Growing.StatesInstance smi) {
			var master = smi.master;
			yield return null;
			if (master != null && master.rm.Replanted)
				smi.GoTo(smi.sm.growing.planted);
		}

		/// <summary>
		/// Applied after InitializeStates runs.
		/// </summary>
		internal static void Postfix(Growing.States __instance) {
			// It can only go from wild to farmed as the invalid value is only read wild
			__instance.growing.wild.Enter("Fix Arbor Tree on-load bug", smi =>
				smi.master.StartCoroutine(FixLoadCoroutine(smi)));
		}
	}

	/// <summary>
	/// Applied to SpaceScannerNetworkManager to fix a racy out of bounds bug and reduce
	/// memory allocations.
	/// </summary>
	[HarmonyPatch(typeof(SpaceScannerNetworkManager), nameof(SpaceScannerNetworkManager.
		CalcWorldNetworkQuality))]
	public static class SpaceScannerNetworkManager_CalcWorldNetworkQuality_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MiscOpts;

		// The base game only calls CalcWorldNetworkQuality from a Sim100ms on the main thread
		private static readonly BitArray COVERAGE = new BitArray(1024, false);

		/// <summary>
		/// Applied before CalcWorldNetworkQuality runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(WorldContainer world, ref float __result) {
			var cmps = Components.DetectorNetworks.CreateOrGetCmps(world.id);
			int width = world.Width, n = cmps.Count, start = world.WorldOffset.x, total = 0;
			COVERAGE.SetAll(false);
			var cells = HashSetPool<int, SpaceScannerNetworkManager>.Allocate();
			for (int i = 0; i < n; i++) {
				var network = cmps[i];
				Operational operational;
				if (network != null && (operational = network.GetComponent<Operational>()) !=
						null && operational.IsOperational) {
					cells.Clear();
					CometDetectorConfig.SKY_VISIBILITY_INFO.CollectVisibleCellsTo(cells, Grid.
						PosToCell(network.transform.position), world);
					foreach (int cell in cells) {
						int x = Grid.CellToXY(cell).x - start;
						// Tally unique cells, but only in range
						if (x >= 0 && x < width && !COVERAGE.Get(x)) {
							COVERAGE.Set(x, true);
							total++;
						}
					}
				}
			}
			cells.Recycle();
			__result = Mathf.Clamp01(2.0f * total / width);
			return false;
		}
	}

#if false
	/// <summary>
	/// Applied to RobotElectroBankMonitor to try and figure out how a Flydo gets an invalid
	/// power bank.
	/// </summary>
	[HarmonyPatch(typeof(RobotElectroBankMonitor), nameof(RobotElectroBankMonitor.
		ChargeDecent))]
	public static class RobotElectroBankMonitor_ChargeDecent_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before ChargeDecent runs.
		/// </summary>
		internal static void Prefix(RobotElectroBankMonitor.Instance smi) {
			var items = smi.electroBankStorage.items;
			var parent = smi.gameObject;
			int n = items.Count;
			for (int i = 0; i < n; i++) {
				var go = items[i];
				if (go == null)
					PUtil.LogWarning("Power bank was destroyed before drone: " + parent);
				else if (!go.TryGetComponent(out Electrobank _))
					PUtil.LogWarning("Non-power bank item in the power bank storage: " + go);
			}
		}
	}

	/// <summary>
	/// Applied to Sensors to try and debug why ClosestPickupableSensor is crashing. The
	/// default error will not even include the correct class name (it is a generic class)
	/// </summary>
	[HarmonyPatch(typeof(Sensors), nameof(Sensors.UpdateSensors))]
	public static class Sensors_UpdateSensors_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.Metrics;

		/// <summary>
		/// Applied before UpdateSensors runs.
		/// </summary>
		internal static bool Prefix(Sensors __instance) {
			var allSensors = __instance.sensors;
			int n = allSensors.Count;
			for (int i = 0; i < n; i++) {
				var sensor = allSensors[i];
				if (sensor.IsEnabled) {
					try {
						sensor.Update();
					} catch (System.Exception e) {
						PUtil.LogError("When updating sensor " + sensor.GetType().FullName +
							" on " + __instance.gameObject);
						PUtil.LogException(e);
						throw;
					}
				}
			}
			return false;
		}
	}
#endif
}
