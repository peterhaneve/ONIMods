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

namespace PeterHan.AutoEject {
	/// <summary>
	/// Patches which will be applied via annotations for AutoEject.
	/// </summary>
	public sealed class AutoEjectPatches : KMod.UserMod2 {
		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			PUtil.InitLibrary();
			new PVersionCheck().Register(this, new SteamVersionChecker());
		}

		/// <summary>
		/// Applied to GeneShuffler.GeneShufflerSM to immediately fire the "Complete Neural
		/// Vacillation" process when it completes.
		/// </summary>
		[HarmonyPatch(typeof(GeneShuffler.GeneShufflerSM), nameof(GeneShuffler.GeneShufflerSM.
			InitializeStates))]
		public static class GeneShuffler_GeneShufflerSM_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(GeneShuffler.GeneShufflerSM __instance) {
				__instance.working.complete.Enter((smi) => {
					if (smi.master.WorkComplete)
						smi.master.SetWorkTime(0.0f);
				});
			}
		}

		/// <summary>
		/// Applied to WarpPortal.WarpPortalSM to immediately fire "Teleport" when the
		/// Duplicant arrives.
		/// </summary>
		[HarmonyPatch(typeof(WarpPortal.WarpPortalSM), nameof(WarpPortal.WarpPortalSM.
			InitializeStates))]
		public static class WarpPortal_WarpPortalSM_InitializeStates_Patch {
			/// <summary>
			/// Applied after InitializeStates runs.
			/// </summary>
			internal static void Postfix(WarpPortal.WarpPortalSM __instance) {
				__instance.occupied.waiting.Enter((smi) => {
					smi.master.StartWarpSequence();
				});
			}
		}
	}
}
