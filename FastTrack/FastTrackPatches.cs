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
using KMod;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Patches which will be applied via annotations for FastTrack.
	/// </summary>
	public sealed class FastTrackPatches : KMod.UserMod2 {
		/// <summary>
		/// Caches the value of the debug flag.
		/// </summary>
		internal static bool DEBUG = false;

		/// <summary>
		/// Initializes several patches after Db is initialized.
		/// </summary>
		[PLibMethod(RunAt.AfterDbInit)]
		internal static void AfterDbInit() {
			var options = FastTrackOptions.Instance;
			if (options.ThreatOvercrowding)
				CritterPatches.OvercrowdingMonitor_UpdateState_Patch.InitTagBits();
			if (options.DisableConduitAnimation != FastTrackOptions.ConduitAnimationQuality.
					Full)
				ConduitPatches.ConduitFlowVisualizer_Render_Patch.SetupDelegates();
		}

		/// <summary>
		/// Cleans up the mod caches after the game ends.
		/// </summary>
		[PLibMethod(RunAt.OnEndGame)]
		internal static void OnEndGame() {
			var options = FastTrackOptions.Instance;
			ConduitPatches.ConduitFlowVisualizer_Render_Patch.Cleanup();
			if (options.CachePaths) {
				PathPatches.NavGrid_InitializeGraph_Patch.Cleanup();
				PathPatches.PathCacher.Cleanup();
			}
			// FastCellChangeMonitor did not help, because pretty much all updates were to
			// things that actually had a listener
			if (options.UnstackLights)
				VisualPatches.LightBufferManager.Cleanup();
			if (options.ReduceTileUpdates)
				VisualPatches.PropertyTextureUpdater.DestroyInstance();
			AsyncJobManager.DestroyInstance();
		}

		/// <summary>
		/// Initializes the nav grids on game start, since Pathfinding.AddNavGrid gets inlined.
		/// </summary>
		[PLibMethod(RunAt.OnStartGame)]
		internal static void OnStartGame() {
			var inst = Game.Instance;
			var options = FastTrackOptions.Instance;
			if (options.CachePaths)
				PathPatches.NavGrid_InitializeGraph_Patch.Init();
			// Slices updates to Duplicant sensors
			if (inst != null) {
				var go = inst.gameObject;
				go.AddOrGet<AsyncJobManager>();
				if (options.SensorOpts || options.PickupOpts)
					go.AddOrGet<SensorPatches.SensorWrapperUpdater>();
				if (options.AsyncPathProbe)
					go.AddOrGet<PathPatches.PathProbeJobManager>();
				if (options.ReduceSoundUpdates)
					go.AddOrGet<SoundUpdater>();
				// If debugging is on, start logging
				if (DEBUG)
					go.AddOrGet<Metrics.DebugMetrics>();
			}
			ConduitPatches.ConduitFlowVisualizer_Render_Patch.Init();
			if (options.UnstackLights)
				VisualPatches.LightBufferManager.Init();
		}

		public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods) {
			base.OnAllModsLoaded(harmony, mods);
			// Manual patch in the rewritten FetchManager.UpdatePickups only if Efficient
			// Supply is not enabled
			if (FastTrackOptions.Instance.FastUpdatePickups) {
				if (PPatchTools.GetTypeSafe("PeterHan.EfficientFetch.EfficientFetchManager") ==
						null) {
					harmony.Patch(typeof(FetchManager.FetchablesByPrefabId),
						nameof(FetchManager.FetchablesByPrefabId.UpdatePickups),
						prefix: new HarmonyMethod(typeof(FetchManagerFastUpdate),
						nameof(FetchManagerFastUpdate.BeforeUpdatePickups)));
#if DEBUG
					PUtil.LogDebug("Patched FetchManager for fast pickup updates");
#endif
				} else
					PUtil.LogWarning("Disabling fast pickup updates: Efficient Supply active");
			}
		}

		public override void OnLoad(Harmony harmony) {
			base.OnLoad(harmony);
			var options = FastTrackOptions.Instance;
			PUtil.InitLibrary();
			new POptions().RegisterOptions(this, typeof(FastTrackOptions));
			new PPatchManager(harmony).RegisterPatchClass(typeof(FastTrackPatches));
			new PVersionCheck().Register(this, new SteamVersionChecker());
			DEBUG = options.Metrics;
			// In case this goes in stock bug fix later
			if (options.UnstackLights)
				PRegistry.PutData("Bugs.StackedLights", true);
		}

		/// <summary>
		/// Applied to MinionConfig to add an instance of SensorWrapper to each Duplicant.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.OnSpawn))]
		public static class MinionConfig_OnSpawn_Patch {
			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				var opts = FastTrackOptions.Instance;
				if (opts.SensorOpts || opts.PickupOpts)
					go.AddOrGet<SensorPatches.SensorWrapper>();
			}
		}
	}
}
