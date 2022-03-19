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
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.ConduitPatches {
	/// <summary>
	/// Renders conduits in MeshRenderers instead of using Graphics.DrawMesh.
	/// </summary>
	internal sealed class ConduitMeshVisualizer : MonoBehaviour {
		/// <summary>
		/// Creates a conduit mesh visualizer.
		/// </summary>
		/// <param name="mesh">The conduit mesh to render.</param>
		/// <param name="shader">The shader to use when rendering.</param>
		/// <returns>The visualizer which can render the mesh.</returns>
		public static ConduitMeshVisualizer Create(Mesh mesh, Material shader) {
			var game = Game.Instance;
			if (game == null)
				throw new ArgumentNullException(nameof(Game.Instance));
			var go = mesh.CreateMeshRenderer("Conduit Flow Mesh", 0);
			var t = go.transform;
			t.SetParent(game.transform);
			t.SetPositionAndRotation(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity);
			// Set up the mesh with the right material
			go.GetComponent<MeshRenderer>().material = shader;
			return go.AddOrGet<ConduitMeshVisualizer>();
		}

		/// <summary>
		/// The last Z location of the conduit layer.
		/// </summary>
		private float lastZ;

		/// <summary>
		/// The last render layer used for the conduits.
		/// </summary>
		private int lastLayer;

		internal ConduitMeshVisualizer() {
			lastLayer = 0;
			lastZ = float.MinValue;
		}

		/// <summary>
		/// Destroys the mesh renderer.
		/// </summary>
		public void DestroyRenderer() {
			Destroy(gameObject);
		}

		/// <summary>
		/// Updates the mesh Z coordinate and layer to match the current overlay settings.
		/// </summary>
		/// <param name="position">The new conduit position.</param>
		/// <param name="layer">The new conduit layer.</param>
		internal void UpdateLayerAndPosition(Vector3 position, int layer) {
			float z = position.z;
			if (z != lastZ) {
				transform.position = position;
				lastZ = z;
			}
			if (layer != lastLayer) {
				gameObject.layer = layer;
				lastLayer = layer;
			}
		}
	}

	/// <summary>
	/// Groups patches for using mesh renderers on conduits.
	/// </summary>
	public static class ConduitFlowMeshPatches {
		/// <summary>
		/// Accesses the private type ConduitFlowVisualizer.ConduitFlowMesh.
		/// </summary>
		private static readonly Type CONDUIT_FLOW_MESH = typeof(ConduitFlowVisualizer).
			GetNestedType("ConduitFlowMesh", PPatchTools.BASE_FLAGS | BindingFlags.Instance);

		/// <summary>
		/// Stores a mapping from the meshes to their visualizers for cleanup.
		/// </summary>
		private static readonly IDictionary<Mesh, ConduitMeshVisualizer> visualizers =
			new Dictionary<Mesh, ConduitMeshVisualizer>(8);

		/// <summary>
		/// Forcefully cleans up all conduit flow visualizer meshes.
		/// </summary>
		internal static void CleanupAll() {
			foreach (var pair in visualizers)
				pair.Value.DestroyRenderer();
			visualizers.Clear();
		}

		/// <summary>
		/// Replaces the Graphics.DrawMesh call to update the mesh attributes if necessary
		/// instead.
		/// </summary>
		private static void PostUpdate(Mesh mesh, Vector3 position, Quaternion quaterion,
				Material material, int layer) {
			_ = quaterion;
			_ = material;
			if (mesh != null && visualizers.TryGetValue(mesh,
					out ConduitMeshVisualizer visualizer))
				visualizer.UpdateLayerAndPosition(position, layer);
		}

		/// <summary>
		/// Applied to ConduitFlowMesh to destroy the renderer when it dies.
		/// </summary>
		[HarmonyPatch]
		internal static class Cleanup_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			internal static MethodBase TargetMethod() {
				return CONDUIT_FLOW_MESH?.GetMethodSafe("Cleanup", false);
			}

			/// <summary>
			/// Applied before Cleanup runs.
			/// </summary>
			internal static void Prefix(Mesh ___mesh) {
				if (___mesh != null && visualizers.TryGetValue(___mesh,
						out ConduitMeshVisualizer visualizer)) {
					visualizer.DestroyRenderer();
					visualizers.Remove(___mesh);
				}
			}
		}

		/// <summary>
		/// Applied to ConduitFlowMesh to create the renderer when it is created.
		/// </summary>
		[HarmonyPatch]
		internal static class Constructor_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			internal static MethodBase TargetMethod() {
				return CONDUIT_FLOW_MESH?.GetConstructor(PPatchTools.BASE_FLAGS | BindingFlags.
					Instance, null, Type.EmptyTypes, null);
			}

			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(Mesh ___mesh, Material ___material) {
				if (___mesh != null) {
					// Destroy the existing renderer if it exists
					if (visualizers.TryGetValue(___mesh, out ConduitMeshVisualizer visualizer))
						visualizer.DestroyRenderer();
					visualizers[___mesh] = ConduitMeshVisualizer.Create(___mesh, ___material);
				}
			}
		}

		/// <summary>
		/// Applied to ConduitFlowMesh to remove the DrawMesh call.
		/// </summary>
		[HarmonyPatch]
		internal static class End_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			internal static MethodBase TargetMethod() {
				return CONDUIT_FLOW_MESH?.GetMethodSafe("End", false, typeof(float),
					typeof(int));
			}

			/// <summary>
			/// Transpiles End to replace the DrawMesh call with a call to update the mesh
			/// attributes.
			/// </summary>
			internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
				var drawMesh = typeof(Graphics).GetMethodSafe(nameof(Graphics.DrawMesh),
					true, typeof(Mesh), typeof(Vector3), typeof(Quaternion), typeof(Material),
					typeof(int));
				var replacement = typeof(ConduitFlowMeshPatches).GetMethodSafe(
					nameof(PostUpdate), true, typeof(Mesh), typeof(Vector3),
					typeof(Quaternion), typeof(Material), typeof(int));
				var newMethod = instructions;
				if (drawMesh != null)
					newMethod = PPatchTools.ReplaceMethodCall(instructions, drawMesh,
						replacement);
				else
					PUtil.LogWarning("Unable to patch ConduitFlowMesh.End");
				foreach (var instr in newMethod)
					yield return instr;
			}
		}
	}
}
