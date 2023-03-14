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
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.EfficientFetch {
	/// <summary>
	/// Patches which will be applied via annotations for Efficient Supply.
	/// </summary>
	public sealed class EfficientFetchPatches : KMod.UserMod2 {
		/// <summary>
		/// The number of errors encountered so far in the pickup loop.
		/// </summary>
		private static int errorCount = 0;

		/// <summary>
		/// The maximum number of error messages which will be logged before being shushed.
		/// </summary>
		private const int ERROR_THRESHOLD = 10;

		/// <summary>
		/// The options for this mod.
		/// </summary>
		private static EfficientFetchOptions options;

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			options = new EfficientFetchOptions();
			new PPatchManager(harmony).RegisterPatchClass(typeof(EfficientFetchPatches));
			new POptions().RegisterOptions(this, typeof(EfficientFetchOptions));
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to FetchChore to evaluate the list of pickups more carefully for one that
		/// actually makes progress.
		/// </summary>
		[HarmonyPatch(typeof(FetchChore), nameof(FetchChore.FindFetchTarget))]
		public static class FetchChore_FindFetchTarget_Patch {
			/// <summary>
			/// Applied before FindFetchTarget runs.
			/// </summary>
			internal static bool Prefix(FetchChore __instance, ChoreConsumerState
					consumer_state, ref Pickupable __result) {
				var inst = EfficientFetchManager.Instance;
				bool cont = true;
				if (inst != null && options.MinimumAmountPercent > 0)
					cont = inst.FindFetchTarget(__instance, consumer_state, out __result);
				return cont;
			}
		}

		/// <summary>
		/// Applied to FetchablesByPrefabId to replace the existing pickup calculation code
		/// with one that takes the required amount into effect.
		/// </summary>
		[HarmonyPatch(typeof(FetchManager.FetchablesByPrefabId), nameof(FetchManager.
			FetchablesByPrefabId.UpdatePickups))]
		public static class FetchablesByPrefabId_UpdatePickups_Patch {
			/// <summary>
			/// Applied before UpdatePickups runs.
			/// </summary>
			internal static bool Prefix(FetchManager.FetchablesByPrefabId __instance,
					Navigator worker_navigator, GameObject worker_go,
					Dictionary<int, int> ___cellCosts) {
				var inst = EfficientFetchManager.Instance;
				bool cont = true;
				if (inst != null && options.MinimumAmountPercent > 0)
					try {
						inst.UpdatePickups(__instance, worker_navigator, worker_go,
							___cellCosts);
						cont = false;
					} catch (Exception e) {
						// Crashing will bring down simdll with no stack trace
						if (++errorCount < ERROR_THRESHOLD)
							PUtil.LogException(e);
					}
				return cont;
			}
		}

		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
			PUtil.LogDebug("Destroying EfficientFetch");
			EfficientFetchManager.DestroyInstance();
		}

		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			options = POptions.ReadSettings<EfficientFetchOptions>() ??
				new EfficientFetchOptions();
			PUtil.LogDebug("EfficientFetch starting: Min Ratio={0:D}%".F(options.
				MinimumAmountPercent));
			EfficientFetchManager.CreateInstance(options.GetMinimumRatio());
		}
	}
}
