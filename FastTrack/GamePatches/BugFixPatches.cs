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
using System.Collections.Generic;
using System.Reflection.Emit;

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
}
