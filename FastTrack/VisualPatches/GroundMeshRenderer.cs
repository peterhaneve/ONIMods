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
using System;
using System.Collections.Generic;
using UnityEngine;

using RenderData = GroundRenderer.ElementChunk.RenderData;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders the ground in MeshRenderers instead of using Graphics.DrawMesh.
	/// </summary>
	internal sealed class GroundMeshRenderer : MonoBehaviour {
		/// <summary>
		/// Creates a solid tile mesh visualizer.
		/// </summary>
		/// <param name="mesh">The ground mesh to render.</param>
		/// <param name="shader">The shader to use when rendering.</param>
		/// <returns>The visualizer which can render the mesh.</returns>
		public static GroundMeshRenderer Create(Mesh mesh, Material shader) {
			var game = Game.Instance;
			if (game == null)
				throw new ArgumentNullException(nameof(Game.Instance));
			var go = mesh.CreateMeshRenderer("Solid Tile Mesh", LayerMask.NameToLayer("World"),
				shader);
			var t = go.transform;
			t.SetParent(game.transform);
			t.SetPositionAndRotation(new Vector3(0.5f, 0.5f, 0.0f), Quaternion.identity);
			go.SetActive(false);
			return go.AddOrGet<GroundMeshRenderer>();
		}

		/// <summary>
		/// The last Z location of the ground layer.
		/// </summary>
		private float lastZ;

		/// <summary>
		/// Whether the mesh renderer was active last frame.
		/// </summary>
		private bool wasActive;

		internal GroundMeshRenderer() {
			lastZ = float.MinValue;
			wasActive = false;
		}

		/// <summary>
		/// Destroys the mesh renderer.
		/// </summary>
		public void DestroyRenderer() {
			Destroy(gameObject);
		}

		/// <summary>
		/// Updates the mesh Z coordinate and layer to match the current element settings.
		/// </summary>
		/// <param name="position">The new element position.</param>
		/// <param name="active">true to set the mesh renderer active, or false to set it
		/// inactive.</param>
		internal void UpdatePosition(Vector3 position, bool active) {
			float z = position.z;
			if (!Mathf.Approximately(z, lastZ)) {
				transform.position = position;
				lastZ = z;
			}
			if (active != wasActive) {
				gameObject.SetActive(active);
				wasActive = active;
			}
		}
	}

	/// <summary>
	/// Groups patches for using mesh renderers on solid tiles.
	/// </summary>
	public static class GroundRendererDataPatches {
		/// <summary>
		/// Stores a mapping from the meshes to their visualizers for cleanup.
		/// </summary>
		private static readonly IDictionary<Mesh, GroundMeshRenderer> visualizers =
			new Dictionary<Mesh, GroundMeshRenderer>(128);

		/// <summary>
		/// Forcefully cleans up all ground visualizer meshes.
		/// </summary>
		internal static void CleanupAll() {
			foreach (var pair in visualizers)
				pair.Value.DestroyRenderer();
			visualizers.Clear();
		}

		/// <summary>
		/// Applied to RenderData to destroy the renderer when it dies.
		/// </summary>
		[HarmonyPatch(typeof(RenderData), nameof(RenderData.ClearMesh))]
		internal static class ClearMesh_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
				FastTrackOptions.MeshRendererSettings.None;

			/// <summary>
			/// Applied before ClearMesh runs.
			/// </summary>
			internal static void Prefix(Mesh ___mesh) {
				if (___mesh != null && visualizers.TryGetValue(___mesh,
						out GroundMeshRenderer visualizer)) {
					visualizer.DestroyRenderer();
					visualizers.Remove(___mesh);
				}
			}
		}

		/// <summary>
		/// Applied to RenderData to create the renderer when it is created.
		/// </summary>
		[HarmonyPatch(typeof(RenderData), MethodType.Constructor, typeof(Material))]
		internal static class Constructor_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
				FastTrackOptions.MeshRendererSettings.None;

			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(Mesh ___mesh, Material material) {
				if (___mesh != null) {
					// Destroy the existing renderer if it exists
					if (visualizers.TryGetValue(___mesh, out GroundMeshRenderer visualizer))
						visualizer.DestroyRenderer();
					visualizers[___mesh] = GroundMeshRenderer.Create(___mesh, material);
				}
			}
		}

		/// <summary>
		/// Applied to RenderData to remove the DrawMesh call.
		/// </summary>
		[HarmonyPatch(typeof(RenderData), nameof(RenderData.Render))]
		internal static class Render_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
				FastTrackOptions.MeshRendererSettings.None;

			/// <summary>
			/// Applied before Render runs.
			/// </summary>
			internal static bool Prefix(Vector3 position, RenderData __instance) {
				var mesh = __instance.mesh;
				if (mesh != null && visualizers.TryGetValue(mesh, out GroundMeshRenderer
						visualizer))
					visualizer.UpdatePosition(position, __instance.pos.Count > 0 && !
						FullScreenDialogPatches.DialogVisible);
				return false;
			}
		}
	}
}
