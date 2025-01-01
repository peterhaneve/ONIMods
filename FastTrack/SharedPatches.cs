/*
 * Copyright 2024 Peter Han
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
using UnityEngine;

namespace PeterHan.FastTrack {
	/// <summary>
	/// Applied to Game to start the second phase of conduit updates.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.LateUpdate))]
	[HarmonyPriority(Priority.Low)]
	public static class Game_LateUpdate_Patch {
		internal static bool Prepare() => !FastTrackOptions.Instance.ConduitOpts &&
			ConduitPatches.ConduitFlowVisualizerRenderer.Prepare();

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static void Prefix(Game __instance) {
			if (__instance.gasConduitSystem.IsDirty)
				ConduitPatches.ConduitFlowVisualizerRenderer.ForceUpdate(__instance.
					gasFlowVisualizer);
			if (__instance.liquidConduitSystem.IsDirty)
				ConduitPatches.ConduitFlowVisualizerRenderer.ForceUpdate(__instance.
					liquidFlowVisualizer);
		}
	}

	/// <summary>
	/// Applied to Game to start property texture updates after Sim data arrives and
	/// fast Reachability updates before the sim cycle starts.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.Update))]
	public static class Game_Update_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.ReduceTileUpdates || options.FastReachability || options.
				FlattenAverages || (!options.ConduitOpts && ConduitPatches.
				ConduitFlowVisualizerRenderer.Prepare()) || options.ParallelInventory ||
				options.AsyncPathProbe;
		}

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		[HarmonyPriority(Priority.High)]
		internal static void Prefix(Game __instance) {
			if (!FastTrackOptions.Instance.ConduitOpts && ConduitPatches.
					ConduitFlowVisualizerRenderer.Prepare()) {
				if (__instance.gasConduitSystem.IsDirty)
					ConduitPatches.ConduitFlowVisualizerRenderer.ForceUpdate(__instance.
						gasFlowVisualizer);
				if (__instance.liquidConduitSystem.IsDirty)
					ConduitPatches.ConduitFlowVisualizerRenderer.ForceUpdate(__instance.
						liquidFlowVisualizer);
			}
			UIPatches.BackgroundInventoryUpdater.Instance?.EndUpdateAll();
			// Updating the group prober can trigger reachability changes in the world
			// inventory, so do not start it until after the BWI has finished
			SensorPatches.FastGroupProber.Instance?.Update();
		}

		/// <summary>
		/// Applied after Update runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static void Postfix() {
			VisualPatches.PropertyTextureUpdater.Instance?.StartUpdate();
			GamePatches.AsyncAmountsUpdater.Instance?.Finish();
			PathPatches.PathProbeJobManager.Instance?.EndJob();
		}
	}

	/// <summary>
	/// Applied to Global to start up some expensive things before Game.LateUpdate runs.
	/// </summary>
	[HarmonyPatch(typeof(Global), nameof(Global.LateUpdate))]
	public static class Global_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.ConduitOpts;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static void Prefix() {
			if (Game.Instance != null)
				ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
		}
	}

	/// <summary>
	/// Applied to Global to start up some expensive things before Game.Update runs.
	/// </summary>
	[HarmonyPatch(typeof(Global), nameof(Global.Update))]
	public static class Global_Update_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.ConduitOpts || options.ParallelInventory || options.CachePaths;
		}

		/// <summary>
		/// Applied before Update runs.
		/// </summary>
		internal static void Prefix() {
			var options = FastTrackOptions.Instance;
			if (Game.Instance != null) {
				if (options.ConduitOpts)
					ConduitPatches.BackgroundConduitUpdater.StartUpdateAll();
				UIPatches.BackgroundInventoryUpdater.Instance?.StartUpdateAll();
#if DEBUG
				if (options.Metrics)
					Metrics.FastTrackProfiler.LogGC();
#endif
			}
			if (options.CachePaths)
				PathPatches.PathCacher.UpdateTime(Time.timeAsDouble);
		}
	}

	/// <summary>
	/// Applied to MinionConfig to apply several patches from different areas of the mod.
	/// </summary>
	[HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.OnSpawn))]
	public static class MinionConfig_OnSpawn_Patch {
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.SensorOpts || options.NoSplash;
		}

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(GameObject go) {
			if (go != null) {
				var options = FastTrackOptions.Instance;
				if (options.SensorOpts)
					SensorPatches.SensorPatches.RemoveBalloonArtistSensor(go);
				if (options.NoSplash && go.TryGetComponent(out Navigator nav))
					nav.transitionDriver.overrideLayers.RemoveAll((layer) => layer is
						SplashTransitionLayer);
			}
		}
	}

	/// <summary>
	/// Applied to World to finish up expensive things after Game.LateUpdate has run.
	/// </summary>
	[HarmonyPatch(typeof(World), nameof(World.LateUpdate))]
	public static class World_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.PickupOpts;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static void Prefix() {
			PathPatches.DeferredTriggers.Instance?.RequestDefer();
			PathPatches.AsyncBrainGroupUpdater.Instance?.StartBrainUpdate();
		}

		/// <summary>
		/// Applied after LateUpdate runs.
		/// </summary>
		internal static void Postfix() {
			if (!Game.IsQuitting() && !KMonoBehaviour.isLoadingScene) {
				var dt = PathPatches.DeferredTriggers.Instance;
				PathPatches.AsyncBrainGroupUpdater.Instance?.EndBrainUpdate();
				if (dt != null) {
					dt.EndDefer();
					dt.Process();
				}
			}
		}
	}
}
