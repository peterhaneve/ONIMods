/*
 * Copyright 2019 Peter Han
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
using PeterHan.PLib.Options;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.EfficientFetch {
	/// <summary>
	/// Patches which will be applied via annotations for Efficient Fetch.
	/// </summary>
	public static class EfficientFetchPatches {
		/// <summary>
		/// The options for this mod.
		/// </summary>
		private static EfficientFetchOptions options;

		public static void OnLoad() {
			PUtil.InitLibrary();
			options = new EfficientFetchOptions();
			POptions.RegisterOptions(typeof(EfficientFetchOptions));
		}

		/// <summary>
		/// Applied to FetchChore to evaluate the list of pickups more carefully for one that
		/// actually makes progress.
		/// </summary>
		[HarmonyPatch(typeof(FetchChore), "FindFetchTarget")]
		public static class FetchChore_FindFetchTarget_Patch {
			/// <summary>
			/// Applied before FindFetchTarget runs.
			/// </summary>
			internal static bool Prefix(ref FetchChore __instance, ref ChoreConsumerState
					consumer_state, ref Pickupable __result) {
				var inst = EfficientFetchManager.Instance;
				bool cont = true;
				if (inst != null && options.MinimumAmountPercent > 0)
					cont = inst.FindFetchTarget(__instance, consumer_state, out __result);
				return cont;
			}
		}

		/// <summary>
		/// Applied to FetchablesByPrefabId to rip out the .
		/// </summary>
		[HarmonyPatch(typeof(FetchManager.FetchablesByPrefabId), "UpdatePickups")]
		public static class FetchablesByPrefabId_UpdatePickups_Patch {
			/// <summary>
			/// Applied before UpdatePickups runs.
			/// </summary>
			internal static bool Prefix(ref FetchManager.FetchablesByPrefabId __instance,
					ref Navigator worker_navigator, ref GameObject worker_go,
					ref Dictionary<int, int> ___cellCosts) {
				var inst = EfficientFetchManager.Instance;
				bool cont = true;
				if (inst != null && options.MinimumAmountPercent > 0)
					try {
						inst.UpdatePickups(__instance, worker_navigator, worker_go,
							___cellCosts);
						cont = false;
					} catch (Exception e) {
						// Crashing will bring down simdll with no stack trace
						PUtil.LogException(e);
					}
				return cont;
			}
		}

		/// <summary>
		/// Applied to Game to load settings when the mod starts up.
		/// </summary>
		[HarmonyPatch(typeof(Game), "OnPrefabInit")]
		public static class Game_OnPrefabInit_Patch {
			/// <summary>
			/// Applied after OnPrefabInit runs.
			/// </summary>
			internal static void Postfix() {
				options = POptions.ReadSettings<EfficientFetchOptions>() ??
					new EfficientFetchOptions();
				PUtil.LogDebug("EfficientFetch starting: Min Ratio={0:D}%".F(options.
					MinimumAmountPercent));
				EfficientFetchManager.CreateInstance(options.GetMinimumRatio());
			}
		}

		/// <summary>
		/// Applied to Game to clean up the fetch manager on close.
		/// </summary>
		[HarmonyPatch(typeof(Game), "DestroyInstances")]
		public static class Game_DestroyInstances_Patch {
			/// <summary>
			/// Applied after DestroyInstances runs.
			/// </summary>
			internal static void Postfix() {
				PUtil.LogDebug("Destroying EfficientFetch");
				EfficientFetchManager.DestroyInstance();
			}
		}
	}
}
