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

using System.Collections.Generic;
using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Deals with compatibility with known "problem" mods.
	/// </summary>
	internal static class FastTrackCompat {
		/// <summary>
		/// Turn off tile mesh renderers if this mod is active.
		/// </summary>
		private const string TRUE_TILES_ID = "TrueTiles";

		/// <summary>
		/// Checks for compatibility and applies fast fetch manager updates only if Efficient
		/// Supply is not enabled.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void CheckFetchCompat(Harmony harmony) {
			if (PPatchTools.GetTypeSafe("PeterHan.EfficientFetch.EfficientFetchManager") ==
					null) {
				PathPatches.AsyncBrainGroupUpdater.AllowFastListSwap = true;
				harmony.Patch(typeof(FetchManager.FetchablesByPrefabId),
					nameof(FetchManager.FetchablesByPrefabId.UpdatePickups),
					prefix: new HarmonyMethod(typeof(GamePatches.FetchManagerFastUpdate),
					nameof(GamePatches.FetchManagerFastUpdate.BeforeUpdatePickups)));
#if DEBUG
				PUtil.LogDebug("Patched FetchManager for fast pickup updates");
#endif
			} else {
				PUtil.LogWarning("Disabling fast pickup updates: Efficient Supply active");
				PathPatches.AsyncBrainGroupUpdater.AllowFastListSwap = false;
			}
		}

		/// <summary>
		/// Checks for compatibility and applies no disease only if Diseases Restored or
		/// Diseases Expanded is not enabled.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void CheckNoDiseaseCompat(Harmony harmony) {
			if (PPatchTools.GetTypeSafe("DiseasesReimagined.DiseasesPatch") != null)
				PUtil.LogWarning("Enabling diseases: Diseases Restored active");
			else if (PPatchTools.GetTypeSafe("DiseasesExpanded.ModInfo") != null)
				PUtil.LogWarning("Enabling diseases: Diseases Expanded active");
			else {
				GamePatches.NoDiseasePatches.Apply(harmony);
#if DEBUG
				PUtil.LogDebug("Disabled diseases");
#endif
			}
		}

		/// <summary>
		/// Checks for compatibility and applies stats panel optimizations only if mods which
		/// show or alter attributs are not enabled.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void CheckStatsCompat(Harmony harmony) {
			if (PPatchTools.GetTypeSafe("OniStatsPlusSo.MyMod") == null) {
				UIPatches.MinionStatsPanelWrapper.Apply(harmony);
			} else
				PUtil.LogDebug("Disabling attributes panel optimizations: Stats mod active");
		}

		/// <summary>
		/// Checks for compatibility and applies tile mesh renderer patches only if other tile
		/// mods are not enabled.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		/// <param name="mods">The mods list after all load.</param>
		internal static void CheckTileCompat(Harmony harmony, IReadOnlyList<Mod> mods) {
			int n = mods.Count;
			bool found = false;
			// No Contains in read only lists...
			for (int i = 0; i < n && !found; i++) {
				var m = mods[i];
				if (m.staticID == TRUE_TILES_ID && m.IsEnabledForActiveDlc()) {
					PUtil.LogWarning("Disabling tile mesh renderers: True Tiles active");
					found = true;
				}
			}
			if (!found) {
				VisualPatches.TileMeshPatches.Apply(harmony);
#if DEBUG
				PUtil.LogDebug("Patched BlockTileRenderer for mesh renderers");
#endif
			}
		}
	}
}
