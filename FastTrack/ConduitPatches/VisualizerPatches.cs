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
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.ConduitPatches {
	/// <summary>
	/// Applied to ConduitFlowVisualizer to reduce the frame rate of conduit updates if the
	/// quality has been turned down.
	/// </summary>
	[HarmonyPatch(typeof(ConduitFlowVisualizer), nameof(ConduitFlowVisualizer.Render))]
	public static class ConduitFlowVisualizer_Render_Patch {
		/// <summary>
		/// The ConduitFlowMesh class is private, but thankfully it is not a mutable struct!
		/// </summary>
		private static readonly Type CONDUIT_FLOW_MESH = typeof(ConduitFlowVisualizer).
			GetNestedType("ConduitFlowMesh", PPatchTools.BASE_FLAGS | BindingFlags.Instance);

		/// <summary>
		/// Getters generated at runtime for ConduitFlowVisualizer.
		/// </summary>
		private static Func<object, Material> GET_MATERIAL;

		private static Func<object, Mesh> GET_MESH;

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
		private const double UPDATE_RATE_REDUCED = 1.0 / 10.0;

		/// <summary>
		/// The time interval in seconds for updates when zoomed far out.
		/// </summary>
		private const double UPDATE_RATE_ZOOMED = 1.0;

		/// <summary>
		/// The update rate in seconds currently being used.
		/// </summary>
		private static double updateRate;

		/// <summary>
		/// Sets up the delegates that will be used for this patch.
		/// </summary>
		internal static bool Prepare() => FastTrackOptions.Instance.DisableConduitAnimation !=
			FastTrackOptions.ConduitAnimationQuality.Full;

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
		private static void DrawMesh(object flowMesh, float z, int layer) {
			if (flowMesh != null && CONDUIT_FLOW_MESH.IsAssignableFrom(flowMesh.GetType())) {
				var mesh = GET_MESH.Invoke(flowMesh);
				var material = GET_MATERIAL.Invoke(flowMesh);
				Graphics.DrawMesh(mesh, new Vector3(0.5f, 0.5f, z - 0.1f), Quaternion.identity,
					material, layer);
			}
		}

		/// <summary>
		/// Initializes conduit flow visualizer rate throttling.
		/// </summary>
		internal static void Init() {
			NEXT_UPDATE.Clear();
			if (GET_MATERIAL == null || GET_MESH == null)
				updateRate = 0.0;
			else
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
		internal static bool Prefix(ConduitFlowVisualizer __instance, bool ___showContents,
				ref double ___animTime, float z, int ___layer, object ___movingBallMesh,
				object ___staticBallMesh) {
			double now = Time.unscaledTime, calcUpdateRate = updateRate;
			var cc = CameraController.Instance;
			bool update = true;
			// Calculate update rate otherwise
			if (__instance != null && updateRate > 0.0 && cc != null) {
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
			update |= ___showContents;
			// If not updating, render the last mesh
			if (!update) {
				___animTime += Time.deltaTime;
				DrawMesh(___movingBallMesh, z, ___layer);
				DrawMesh(___staticBallMesh, z, ___layer);
			}
			return update;
		}

		/// <summary>
		/// Creates the runtime delegates for getting the private fields required.
		/// </summary>
		internal static void SetupDelegates() {
			if (CONDUIT_FLOW_MESH != null) {
				GET_MATERIAL = CONDUIT_FLOW_MESH.GenerateGetter<Material>("material");
				GET_MESH = CONDUIT_FLOW_MESH.GenerateGetter<Mesh>("mesh");
			}
			if (GET_MATERIAL == null || GET_MESH == null)
				PUtil.LogWarning("Unable to create delegates for ConduitFlowMesh");
		}
	}

	/// <summary>
	/// Applied to ConduitFlowVisualizer to save some math when calculating conduit flow.
	/// </summary>
	[HarmonyPatch(typeof(ConduitFlowVisualizer), "RenderMesh")]
	public static class ConduitFlowVisualizer_RenderMesh_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Gets the current precomputed visible area.
		/// </summary>
		/// <returns>The visible area of the grid.</returns>
		private static GridArea GetVisibleArea() {
			return CameraController.Instance.VisibleArea.CurrentArea;
		}

		/// <summary>
		/// Transpiles RenderMesh to insert a call to a pipe filter right after the context
		/// calculates what pipes are visible to render.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			TranspiledMethod result;
			// Lower priority optimization: use CameraController.Instance.VisibleArea to
			// save some math
			var oldArea = typeof(GridVisibleArea).GetMethodSafe(nameof(GridVisibleArea.
				GetVisibleArea), true);
			var newArea = typeof(ConduitFlowVisualizer_RenderMesh_Patch).GetMethodSafe(
				nameof(GetVisibleArea), true);
			if (oldArea != null && newArea != null) {
				result = PPatchTools.ReplaceMethodCall(instructions, oldArea, newArea);
#if DEBUG
				PUtil.LogDebug("Patched ConduitFlowVisualizer.RenderMesh");
#endif
			} else
				result = instructions;
			return result;
		}
	}

	/// <summary>
	/// Applied to ConduitFlowVisualizer.RenderMeshContext.ctor to avoid updating liquid and
	/// gas conduits inside tiles when not in a conduit overlay. Makes a small difference on
	/// most bases, but zoomed out pipe spaghetti can make a huge difference!
	/// 
	/// Classified as RenderTicks because there is barely any discernable visual fidelity loss.
	/// </summary>
	[HarmonyPatch]
	public static class ConduitFlowVisualizer_RenderMeshContext_Constructor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Targets the ConduitFlowVisualizer.RenderMestContext constructor.
		/// </summary>
		internal static MethodBase TargetMethod() {
			var meshContext = typeof(ConduitFlowVisualizer).GetNestedType("RenderMeshContext",
				PPatchTools.BASE_FLAGS | BindingFlags.Instance);
			return meshContext?.GetConstructor(PPatchTools.BASE_FLAGS | BindingFlags.Instance,
				null, new Type[] { typeof(ConduitFlowVisualizer), typeof(float),
				typeof(Vector2I), typeof(Vector2I) }, null);
		}

		/// <summary>
		/// Removes all conduits that are inside solid, opaque grid cells.
		/// </summary>
		private static bool FilterConduitsInTiles(bool bounds, int cell, bool showContents) {
			if (!showContents && bounds)
				bounds = !Grid.IsSolidCell(cell) || Grid.Transparent[cell];
			return bounds;
		}

		/// <summary>
		/// Transpiles the constructor to insert a call to the conduit filter in the list of
		/// conditions for deciding which conduits to render.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var cellToXY = typeof(Grid).GetMethodSafe(nameof(Grid.CellToXY), true,
				typeof(int));
			var showContentsField = typeof(ConduitFlowVisualizer).GetFieldSafe("showContents",
				false);
			var filter = typeof(ConduitFlowVisualizer_RenderMeshContext_Constructor_Patch).
				GetMethodSafe(nameof(FilterConduitsInTiles), true, typeof(bool), typeof(int),
				typeof(bool));
			// This name is .NET system defined: https://docs.microsoft.com/en-us/dotnet/api/system.decimal.op_lessthanorequal?view=netframework-4.7.1
			var targetMethod = typeof(Vector2I).GetMethodSafe("op_LessThanOrEqual", true,
				typeof(Vector2I), typeof(Vector2I));
			if (filter == null)
				throw new InvalidOperationException("Cannot find replacement method!");
			if (targetMethod == null || cellToXY == null || showContentsField == null) {
				PUtil.LogWarning("Unable to find target entities: ConduitFlowVisualizer.RenderMeshContext");
				foreach (var instr in instructions)
					yield return instr;
			} else {
				var showContentsLocal = generator.DeclareLocal(typeof(bool));
				var cellLocal = generator.DeclareLocal(typeof(int));
				// Load outer.showContents and save
				yield return new CodeInstruction(OpCodes.Ldarg_1);
				yield return new CodeInstruction(OpCodes.Ldfld, showContentsField);
				yield return new CodeInstruction(OpCodes.Stloc_S, (byte)showContentsLocal.
					LocalIndex);
				bool patched = false;
				foreach (var instr in instructions) {
					if (instr.Is(OpCodes.Call, cellToXY)) {
						// Copy argument to local
						yield return new CodeInstruction(OpCodes.Dup);
						yield return new CodeInstruction(OpCodes.Stloc_S, (byte)cellLocal.
							LocalIndex);
					}
					yield return instr;
					if (!patched && instr.Is(OpCodes.Call, targetMethod)) {
						// Load the cell
						yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)cellLocal.
							LocalIndex);
						// Load showContents
						yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)
							showContentsLocal.LocalIndex);
						// Call the filter
						yield return new CodeInstruction(OpCodes.Call, filter);
#if DEBUG
						PUtil.LogDebug("Patched ConduitFlowVisualizer.RenderMeshContext");
#endif
						patched = true;
					}
				}
				if (!patched)
					PUtil.LogWarning("Failed to patch ConduitFlowVisualizer.RenderMeshContext");
			}
		}
	}
}
