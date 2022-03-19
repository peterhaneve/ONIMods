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
using System.Collections.Generic;
using UnityEngine;

using MaterialType = KAnimBatchGroup.MaterialType;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders Klei animations in MeshRenderers instead of using Graphics.DrawMesh.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class KAnimMeshRenderer : MonoBehaviour {
		/// <summary>
		/// Creates a Klei animation mesh visualizer.
		/// </summary>
		/// <param name="mesh">The animation group mesh to render.</param>
		/// <param name="shader">The shader to use when rendering.</param>
		/// <param name="layer">The layer to use for rendering.</param>
		/// <param name="id">The animation batch's ID.</param>
		/// <returns>The visualizer which can render the mesh.</returns>
		public static KAnimMeshRenderer Create(Mesh mesh, Material shader, int layer, int id) {
			float z = 0.01f / (1.0f + id % 256);
			var go = mesh.CreateMeshRenderer("KAnim Mesh: " + id, layer);
			var t = go.transform;
			t.SetPositionAndRotation(new Vector3(0.0f, 0.0f, z), Quaternion.identity);
			// Set up the mesh with the right material
			var renderer = go.GetComponent<MeshRenderer>();
			renderer.material = shader;
			go.SetActive(false);
			var kmr = go.AddOrGet<KAnimMeshRenderer>();
			kmr.renderer = renderer;
			kmr.zOffset = z;
			return kmr;
		}

		/// <summary>
		/// The last Z location of the animation layer.
		/// </summary>
		private float lastZ;

		/// <summary>
		/// The mesh renderer to modify.
		/// </summary>
		private MeshRenderer renderer;

		/// <summary>
		/// Whether the mesh renderer was active last frame.
		/// </summary>
		private bool wasActive;

		/// <summary>
		/// The Z offset of the animation layer to reduce z-fighting.
		/// </summary>
		private float zOffset;

		internal KAnimMeshRenderer() {
			lastZ = 0.0f;
			wasActive = false;
		}

		/// <summary>
		/// Turns off the mesh renderer.
		/// </summary>
		internal void Deactivate() {
			if (wasActive) {
				gameObject.SetActive(false);
				wasActive = false;
			}
		}

		/// <summary>
		/// Destroys the mesh renderer.
		/// </summary>
		public void DestroyRenderer() {
			Destroy(gameObject);
		}

		/// <summary>
		/// Sets the material properties of the mesh renderer.
		/// </summary>
		/// <param name="properties">The material properties to use.</param>
		internal void SetProperties(MaterialPropertyBlock properties) {
			if (renderer != null)
				renderer.SetPropertyBlock(properties);
		}

		/// <summary>
		/// Updates the mesh Z coordinate to match the current animation settings.
		/// </summary>
		/// <param name="position">The new Z coordinate.</param>
		/// <param name="active">true to set the mesh renderer active, or false to set it
		/// inactive.</param>
		internal void UpdatePosition(float z, bool active) {
			if (z != lastZ) {
				transform.position = new Vector3(0.0f, 0.0f, z + zOffset);
				lastZ = z;
			}
			if (active != wasActive) {
				gameObject.SetActive(active);
				wasActive = active;
			}
		}
	}

	/// <summary>
	/// Groups patches for using mesh renderers on Klei animations.
	/// </summary>
	public static class KAnimMeshRendererPatches {
		/// <summary>
		/// Stores a mapping from the meshes to their visualizers for cleanup.
		/// </summary>
		private static readonly IList<KAnimMeshRenderer> visualizers =
			new List<KAnimMeshRenderer>(512);

		/// <summary>
		/// Renders the active kanims.
		/// </summary>
		private static void Render(IEnumerable<BatchSet> batches) {
			int vn = visualizers.Count;
			foreach (var batchSet in batches)
				if (batchSet != null) {
					int n = batchSet.batchCount;
					for (int i = 0; i < n; i++) {
						var batch = batchSet.GetBatch(i);
						int id = batch.id;
						if (id < vn)
							visualizers[id]?.UpdatePosition(batch.position.z, batch.size > 0 &&
								batch.active);
					}
				}
		}

		/// <summary>
		/// Updates the material properties on the mesh renderer for an animation batch.
		/// </summary>
		/// <param name="batch">The batch to update.</param>
		private static void UpdateMaterialProperties(KAnimBatch batch) {
			int id = batch.id;
			if (batch.materialType != MaterialType.UI && id < visualizers.Count)
				visualizers[id]?.SetProperties(batch.matProperties);
		}

		/// <summary>
		/// Applied to KAnimBatch to destroy the mesh renderer objects when they are disposed.
		/// Currently this is dead and not called when the game quits, so the visualizer list
		/// is not a Dictionary and need not really support removals.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Clear))]
		internal static class Clear_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			/// <summary>
			/// Applied before Clear runs.
			/// </summary>
			internal static void Prefix(KAnimBatch __instance) {
				int id = __instance.id;
				if (__instance.materialType != MaterialType.UI && id < visualizers.Count) {
					visualizers[id]?.DestroyRenderer();
					visualizers[id] = null;
				}
			}
		}

		/// <summary>
		/// Applied to KAnimBatch to set up a mesh renderer in the constructor.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), MethodType.Constructor, typeof(KAnimBatchGroup),
			typeof(int), typeof(float), typeof(MaterialType))]
		internal static class Constructor_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			/// <summary>
			/// Applied after the constructor runs.
			/// </summary>
			internal static void Postfix(KAnimBatch __instance, KAnimBatchGroup group,
					int layer, MaterialType material_type) {
				if (group != null && material_type != MaterialType.UI) {
					int id = __instance.id, n = visualizers.Count;
					// Destroy the existing renderer if it exists
					if (id < n)
						visualizers[id]?.DestroyRenderer();
					for (int i = n; i <= id; i++)
						visualizers.Add(null);
					visualizers[id] = KAnimMeshRenderer.Create(group.mesh, group.
						GetMaterial(material_type), layer, id);
				}
			}
		}

		/// <summary>
		/// Applied to KAnimBatch to turn off the renderer when it gets marked inactive.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Deactivate))]
		public static class Deactivate_Patch {
			/// <summary>
			/// Applied after Deactivate runs.
			/// </summary>
			internal static void Postfix(KAnimBatch __instance) {
				int id = __instance.id;
				if (__instance.materialType != MaterialType.UI && id < visualizers.Count)
					visualizers[id]?.Deactivate();
			}
		}

		/// <summary>
		/// Applied to KAnimBatch to update the material properties when they need to be
		/// updated.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.Init))]
		internal static class Init_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			/// <summary>
			/// Applied after Init runs.
			/// </summary>
			internal static void Postfix(KAnimBatch __instance) {
				UpdateMaterialProperties(__instance);
			}
		}

		/// <summary>
		/// Applied to KAnimBatchManager to remove the DrawMesh call and render it ourselves.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatchManager), nameof(KAnimBatchManager.Render))]
		internal static class Render_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			/// <summary>
			/// Applied before Render runs.
			/// </summary>
			internal static bool Prefix(bool ___ready, IList<BatchSet> ___activeBatchSets) {
				if (___ready)
					Render(___activeBatchSets);
				return false;
			}
		}

		/// <summary>
		/// Applied to KAnimBatch to turn off the renderer when it is removed from a set.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.SetBatchSet))]
		public static class SetBatchSet_Patch {
			/// <summary>
			/// Applied after Deactivate runs.
			/// </summary>
			internal static void Postfix(KAnimBatch __instance, BatchSet newBatchSet) {
				int id = __instance.id;
				if (newBatchSet == null && __instance.materialType != MaterialType.UI &&
						id < visualizers.Count)
					visualizers[id]?.Deactivate();
			}
		}

		/// <summary>
		/// Applied to KAnimBatch to update the material properties when they need to be
		/// updated.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatch), nameof(KAnimBatch.UpdateDirty))]
		internal static class UpdateDirty_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

			/// <summary>
			/// Applied after UpdateDirty runs.
			/// </summary>
			internal static void Postfix(int __result, KAnimBatch __instance) {
				if (__result > 0)
					UpdateMaterialProperties(__instance);
			}
		}
	}
}
