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

using HarmonyLib;
using PeterHan.PLib.Core;
using System.Collections.Generic;
using UnityEngine;

using ConduitFlowMesh = ConduitFlowVisualizer.ConduitFlowMesh;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.ConduitPatches {
	/// <summary>
	/// Applied to ConduitFlowVisualizer to reduce the frame rate of conduit updates if the
	/// quality has been turned down.
	///
	/// Manual patching is now required due to naughty static initializers in
	/// ConduitFlowVisualizer.
	/// </summary>
	public static class ConduitFlowVisualizerPatches {
		/// <summary>
		/// If the width or height of the visible grid exceeds this many cells, updates are
		/// automatically reduced even further, as you can barely see them anyways, and many
		/// conduits are likely to be visible.
		/// </summary>
		private const int MAX_ZOOM = 128;

		/// <summary>
		/// Tracks when each flow visualizer was last updated.
		/// </summary>
		private static readonly IDictionary<ConduitFlowVisualizer, double> NEXT_UPDATE =
			new Dictionary<ConduitFlowVisualizer, double>(8);

		/// <summary>
		/// The time interval in seconds for updates in Minimal mode.
		/// </summary>
		private const double UPDATE_RATE_MINIMAL = 0.5;

		/// <summary>
		/// The time interval in seconds for updates in Reduced mode.
		/// </summary>
		private const double UPDATE_RATE_REDUCED = 0.1;

		/// <summary>
		/// The time interval in seconds for updates when zoomed far out.
		/// </summary>
		private const double UPDATE_RATE_ZOOMED = 1.0;

		static ConduitFlowVisualizerPatches() {
			ReduceFlowUpdates = FastTrackOptions.Instance.DisableConduitAnimation !=
				FastTrackOptions.ConduitAnimationQuality.Full;
		}
		
		/// <summary>
		/// Returns true if conduit flow throttling patches should be applied.
		/// </summary>
		internal static bool ReduceFlowUpdates { get; }

		/// <summary>
		/// The update rate in seconds currently being used.
		/// </summary>
		private static double updateRate;

		/// <summary>
		/// Whether to use a mesh for flow visualization.
		/// </summary>
		private static bool useMesh;

		/// <summary>
		/// Applies the required conduit flow patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void ApplyPatch(Harmony harmony) {
			var opts = FastTrackOptions.Instance;
			var targetType = PPatchTools.GetTypeSafe(nameof(ConduitFlowVisualizer));
			useMesh = opts.MeshRendererOptions != FastTrackOptions.MeshRendererSettings.None;
			if (ReduceFlowUpdates)
				harmony.Patch(targetType, nameof(ConduitFlowVisualizer.Render),
					new HarmonyMethod(typeof(ConduitFlowVisualizerPatches),
					nameof(Render_Prefix)));
			if (opts.RenderTicks)
				harmony.PatchTranspile(targetType, nameof(ConduitFlowVisualizer.RenderMesh),
					new HarmonyMethod(typeof(ConduitFlowVisualizerPatches),
					nameof(RenderMesh_Transpiler)));
		}

		/// <summary>
		/// Avoid leaking a Game instance by cleaning up when Game is disposed.
		/// </summary>
		internal static void Cleanup() {
			NEXT_UPDATE.Clear();
		}

		/// <summary>
		/// Draws an existing ConduitFlowMesh without updating it.
		/// </summary>
		/// <param name="flowMesh">The mesh to draw.</param>
		/// <param name="z">The z coordinate to render the mesh.</param>
		/// <param name="layer">The layer for rendering.</param>
		private static void DrawMesh(ConduitFlowMesh flowMesh, float z, int layer) {
			if (flowMesh != null)
				Graphics.DrawMesh(flowMesh.mesh, new Vector3(0.5f, 0.5f, z - 0.1f), Quaternion.
					identity, flowMesh.material, layer);
		}

		/// <summary>
		/// Forces a conduit update to run next time.
		/// </summary>
		/// <param name="instance">The conduit flow visualizer to invalidate, or null to
		/// invalidate all.</param>
		internal static void ForceUpdate(ConduitFlowVisualizer instance) {
			if (instance == null)
				NEXT_UPDATE.Clear();
			else if (NEXT_UPDATE.Count > 0)
				NEXT_UPDATE.Remove(instance);
		}
		
		/// <summary>
		/// Gets the current precomputed visible area.
		/// </summary>
		/// <returns>The visible area of the grid.</returns>
		private static GridArea GetVisibleArea() {
			return CameraController.Instance.VisibleArea.CurrentArea;
		}

		/// <summary>
		/// Initializes conduit flow visualizer rate throttling.
		/// </summary>
		internal static void Init() {
			NEXT_UPDATE.Clear();
			switch (FastTrackOptions.Instance.DisableConduitAnimation) {
			case FastTrackOptions.ConduitAnimationQuality.Reduced:
				updateRate = UPDATE_RATE_REDUCED;
				break;
			case FastTrackOptions.ConduitAnimationQuality.Minimal:
				updateRate = UPDATE_RATE_MINIMAL;
				break;
			case FastTrackOptions.ConduitAnimationQuality.Full:
			default:
				updateRate = 0.0;
				break;
			}
		}

		/// <summary>
		/// Applied before Render runs.
		/// </summary>
		[HarmonyPriority(Priority.Low)]
		internal static bool Render_Prefix(ConduitFlowVisualizer __instance, float z) {
			double now = Time.unscaledTime, calcUpdateRate = updateRate;
			var cc = CameraController.Instance;
			bool update = true;
			// Calculate update rate otherwise
			if (updateRate > 0.0 && cc != null) {
				// Set updates to 1 Hz if zoomed way way out
				var area = cc.VisibleArea.CurrentArea;
				var max = area.Max;
				var min = area.Min;
				if (max.x - min.x > MAX_ZOOM || max.y - min.y > MAX_ZOOM)
					calcUpdateRate = UPDATE_RATE_ZOOMED;
				if (NEXT_UPDATE.TryGetValue(__instance, out double nextConduitUpdate))
					update = now > nextConduitUpdate;
				if (update)
					NEXT_UPDATE[__instance] = now + calcUpdateRate;
			}
			update |= __instance.showContents;
			// If not updating, render the last mesh
			if (!update) {
				__instance.animTime += Time.deltaTime;
				if (!useMesh) {
					int layer = __instance.layer;
					DrawMesh(__instance.movingBallMesh, z, layer);
					DrawMesh(__instance.staticBallMesh, z, layer);
				}
			}
			return update;
		}
		
		/// <summary>
		/// Transpiles RenderMesh to insert a call to a pipe filter right after the context
		/// calculates what pipes are visible to render.
		/// </summary>
		internal static TranspiledMethod RenderMesh_Transpiler(TranspiledMethod instructions) {
			TranspiledMethod result;
			// Lower priority optimization: use CameraController.Instance.VisibleArea to
			// save some math
			var oldArea = typeof(GridVisibleArea).GetMethodSafe(nameof(GridVisibleArea.
				GetVisibleArea), true);
			var newArea = typeof(ConduitFlowVisualizerPatches).GetMethodSafe(nameof(
				GetVisibleArea), true);
			if (oldArea != null && newArea != null) {
				result = PPatchTools.ReplaceMethodCallSafe(instructions, oldArea, newArea);
#if DEBUG
				PUtil.LogDebug("Patched ConduitFlowVisualizer.RenderMesh");
#endif
			} else
				result = instructions;
			return result;
		}
	}
}
