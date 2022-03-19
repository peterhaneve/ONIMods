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
	public static class ConduitFlowVisualizerRenderer {
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
		/// Forces a conduit update to run next time.
		/// </summary>
		/// <param name="instance">The conduit flow visualizer to invalidate, or null to invalidate all.</param>
		internal static void ForceUpdate(ConduitFlowVisualizer instance) {
			if (instance == null)
				NEXT_UPDATE.Clear();
			else if (NEXT_UPDATE.Count > 0)
				NEXT_UPDATE.Remove(instance);
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
	/// Applied to ConduitFlowVisualizer.RenderMeshTask.ctor to fix a call that excessively
	/// allocates memory.
	/// </summary>
	[HarmonyPatch]
	public static class ConduitRendererMemoryPatch {
		/// <summary>
		/// Accesses the private struct type ConduitFlowVisualizer.RenderMeshTask.
		/// </summary>
		private static readonly Type RENDER_MESH_TASK = typeof(ConduitFlowVisualizer).
			GetNestedType("RenderMeshTask", PPatchTools.BASE_FLAGS | BindingFlags.Instance);

		/// <summary>
		/// The dynamic method to run when replacing the conduit capacity.
		/// </summary>
		private static MethodBase cachedDynamicMethod = null;

		/// <summary>
		/// Targets the ConduitFlowVisualizer.RenderMeshTask constructor.
		/// </summary>
		internal static MethodBase TargetMethod() {
			return RENDER_MESH_TASK?.GetConstructor(PPatchTools.BASE_FLAGS | BindingFlags.
				Instance, null, new Type[] { typeof(int), typeof(int) }, null);
		}

		/// <summary>
		/// Create a method at runtime to use for resizing lists!
		/// </summary>
		/// <param name="ballType">The retrieved type of ConduitFlowVisualizer.RenderMeshTask.Ball.</param>
		/// <param name="resizeBalls">The Capacity property to modify.</param>
		private static MethodBase GenerateDynamicMethod(Type ballType,
				PropertyInfo resizeBalls) {
			if (ballType == null)
				throw new ArgumentNullException(nameof(ballType));
			if (resizeBalls == null)
				throw new ArgumentNullException(nameof(resizeBalls));
			if (cachedDynamicMethod == null) {
				var ballListType = typeof(List<>).MakeGenericType(ballType);
				var newMethod = new DynamicMethod("SetCapacityIfNeeded", null, new Type[] {
					ballListType, typeof(int)
				});
				var generator = newMethod.GetILGenerator();
				var end = generator.DefineLabel();
				// Get current capacity
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Callvirt, resizeBalls.GetGetMethod(true));
				// Compare to new capacity
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Bge, end);
				// Set new capacity
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Ldarg_1);
				generator.Emit(OpCodes.Callvirt, resizeBalls.GetSetMethod(true));
				// Return
				generator.MarkLabel(end);
				generator.Emit(OpCodes.Ret);
				var delegateType = typeof(Action<,>).MakeGenericType(ballListType,
					typeof(int));
				newMethod.CreateDelegate(delegateType);
				cachedDynamicMethod = newMethod;
			}
			return cachedDynamicMethod;
		}

		/// <summary>
		/// Resizes a list only if needed.
		/// </summary>
		/// <param name="list">The list to resize.</param>
		/// <param name="capacity">The new list capacity.</param>
		private static void SetCapacity(List<ConduitFlow.Conduit> list, int capacity) {
			if (capacity > list.Capacity)
				list.Capacity = capacity;
		}

		/// <summary>
		/// Transpiles the constructor to avoid unnecessary resizes.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions,
				ILGenerator generator) {
			var ballType = RENDER_MESH_TASK.GetNestedType("Ball", PPatchTools.BASE_FLAGS |
				BindingFlags.Instance);
			var resizeConduits = typeof(List<>).MakeGenericType(typeof(ConduitFlow.Conduit)).
				GetPropertySafe<int>(nameof(List<int>.Capacity), false);
			PropertyInfo resizeBalls = null;
			bool patched = false;
			// Calculate the target using the private Ball struct type
			if (ballType != null)
				resizeBalls = typeof(List<>).MakeGenericType(ballType).GetPropertySafe<int>(
					nameof(List<int>.Capacity), false);
			var targetConduits = typeof(ConduitRendererMemoryPatch).GetMethodSafe(nameof(
				SetCapacity), true, typeof(List<ConduitFlow.Conduit>), typeof(int));
			if (resizeBalls != null && resizeConduits != null && targetConduits != null) {
				var setBallCap = resizeBalls.GetSetMethod(true);
				var setConduitCap = resizeConduits.GetSetMethod(true);
				var dynamicMethod = GenerateDynamicMethod(ballType, resizeBalls);
				foreach (var instr in instructions) {
					var labels = instr.labels;
					if (instr.Is(OpCodes.Callvirt, setConduitCap)) {
						// Easy, swap it with our method
						instr.opcode = OpCodes.Call;
						instr.operand = targetConduits;
#if DEBUG
						PUtil.LogDebug("Patched RenderMeshTask.set_Capacity 1");
#endif
						patched = true;
					} else if (instr.Is(OpCodes.Callvirt, setBallCap)) {
						// Harder, have to use the method we generated at runtime :/
						instr.opcode = OpCodes.Call;
						instr.operand = dynamicMethod;
#if DEBUG
						PUtil.LogDebug("Patched RenderMeshTask.set_Capacity 2");
#endif
					}
					yield return instr;
				}
			} else
				foreach (var instr in instructions)
					yield return instr;
			if (!patched)
				PUtil.LogWarning("Unable to patch ConduitFlowVisualizer.RenderMeshTask");
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
		internal static bool Prepare() {
			var options = FastTrackOptions.Instance;
			return options.RenderTicks || options.ConduitOpts;
		}

		/// <summary>
		/// Targets the ConduitFlowVisualizer.RenderMeshContext constructor.
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

	/// <summary>
	/// Applied to Game if conduit optimizations are off, to update the visualizer if conduits
	/// are dirty.
	/// </summary>
	[HarmonyPatch]
	public static class Game_Update_Patch {
		internal static bool Prepare() => !FastTrackOptions.Instance.ConduitOpts &&
			ConduitFlowVisualizerRenderer.Prepare();

		internal static IEnumerable<MethodBase> TargetMethods() {
			yield return typeof(Game).GetMethodSafe("Update", false);
			yield return typeof(Game).GetMethodSafe("LateUpdate", false);
		}

		/// <summary>
		/// Applied before Update and LateUpdate run.
		/// </summary>
		internal static void Prefix(Game __instance) {
			if (__instance.gasConduitSystem.IsDirty)
				ConduitFlowVisualizerRenderer.ForceUpdate(__instance.gasFlowVisualizer);
			if (__instance.liquidConduitSystem.IsDirty)
				ConduitFlowVisualizerRenderer.ForceUpdate(__instance.liquidFlowVisualizer);
		}
	}
}
