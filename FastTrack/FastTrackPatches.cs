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
	/// Patches which will be applied via annotations for Fast Track.
	/// </summary>
	public sealed class FastTrackPatches : KMod.UserMod2 {
		/// <summary>
		/// Set to true when the game gets off its feet, and false while it is still loading.
		/// </summary>
		internal static bool GameRunning { get; private set; }

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
			if (options.SensorOpts)
				SensorPatches.SensorPatches.Init();
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
			if (options.ConduitOpts)
				ConduitPatches.BackgroundConduitUpdater.DestroyInstance();
			AsyncJobManager.DestroyInstance();
			GameRunning = false;
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
				if (options.AsyncPathProbe)
					go.AddOrGet<PathPatches.PathProbeJobManager>();
				if (options.ReduceSoundUpdates)
					go.AddOrGet<SoundUpdater>();
				// If debugging is on, start logging
				if (options.Metrics)
					go.AddOrGet<Metrics.DebugMetrics>();
				inst.StartCoroutine(WaitForCleanLoad());
			}
			if (options.ConduitOpts)
				ConduitPatches.BackgroundConduitUpdater.CreateInstance();
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
			// In case this goes in stock bug fix later
			if (options.UnstackLights)
				PRegistry.PutData("Bugs.StackedLights", true);
			GameRunning = false;
		}

		/// <summary>
		/// Waits a few frames as a coroutine, then allows things that require game stability
		/// to run.
		/// </summary>
		private static System.Collections.IEnumerator WaitForCleanLoad() {
			for (int i = 0; i < 4; i++)
				yield return null;
			GameRunning = true;
			yield break;
		}

		/// <summary>
		/// Applied to MinionConfig to apply several patches from different areas of the mod.
		/// </summary>
		[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.OnSpawn))]
		public static class MinionConfig_OnSpawn_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.SensorOpts;
			}

			/// <summary>
			/// Applied after OnSpawn runs.
			/// </summary>
			internal static void Postfix(GameObject go) {
				if (go != null) {
					var options = FastTrackOptions.Instance;
					if (options.SensorOpts)
						SensorPatches.SensorPatches.RemoveBalloonArtistSensor(go);
				}
			}
		}

		/// <summary>
		/// Applied to Global to start up some expensive things before Game.LateUpdate runs.
		/// </summary>
		[HarmonyPatch(typeof(Global), "LateUpdate")]
		public static class Global_LateUpdate_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.ConduitOpts;
			}

			/// <summary>
			/// Applied before LateUpdate runs.
			/// </summary>
			internal static void Prefix() {
				var options = FastTrackOptions.Instance;
				if (options.ConduitOpts)
					ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
			}
		}

		/// <summary>
		/// Applied to Global to start up some expensive things before Game.Update runs.
		/// </summary>
		[HarmonyPatch(typeof(Global), "Update")]
		public static class Global_Update_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.ConduitOpts;
			}

			/// <summary>
			/// Applied before Update runs.
			/// </summary>
			internal static void Prefix() {
				var options = FastTrackOptions.Instance;
				if (options.ConduitOpts)
					ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
			}
		}
	}
}
