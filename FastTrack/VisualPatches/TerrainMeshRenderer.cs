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
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders TerrainBG using a mesh renderer instead of drawing meshes every frame.
	/// </summary>
	internal sealed class TerrainMeshRenderer : IDisposable {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static TerrainMeshRenderer Instance { get; internal set; }

		/// <summary>
		/// Destroys the prioritizable mesh renderer.
		/// </summary>
		public static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The gas mesh to render.
		/// </summary>
		private readonly Mesh gasMesh;

		/// <summary>
		/// Renders the rear gas mesh.
		/// </summary>
		private readonly GameObject gasRenderBack;

		/// <summary>
		/// Renders the front gas mesh.
		/// </summary>
		private readonly GameObject gasRenderFront;

		/// <summary>
		/// A noise texture to clean up on quit.
		/// </summary>
		private readonly Texture3D noiseVolume;

		/// <summary>
		/// Renders the stars mesh.
		/// </summary>
		private readonly MeshRenderer starsRender;

		/// <summary>
		/// The stars mesh to render.
		/// </summary>
		private readonly Mesh starsMesh;

		internal TerrainMeshRenderer(TerrainBG instance) {
			int layer = instance.layer;
			var gasMaterial = instance.gasMaterial;
			gasMesh = instance.gasPlane;
			noiseVolume = instance.noiseVolume;
			starsMesh = instance.starsPlane;
			Create(starsMesh, "Stars", null, layer, Grid.GetLayerZ(Grid.SceneLayer.
				Background) + 1.0f).TryGetComponent(out starsRender);
			gasRenderBack = Create(gasMesh, "Gas Back", gasMaterial, layer, Grid.GetLayerZ(
				Grid.SceneLayer.Gas));
			gasRenderFront = Create(gasMesh, "Gas Front", gasMaterial, layer, Grid.GetLayerZ(
				Grid.SceneLayer.GasFront));
		}

		public void Dispose() {
			Util.KDestroyGameObject(starsRender.gameObject);
			Util.KDestroyGameObject(gasRenderBack);
			Util.KDestroyGameObject(gasRenderFront);
			UnityEngine.Object.Destroy(noiseVolume);
			UnityEngine.Object.Destroy(gasMesh);
			UnityEngine.Object.Destroy(starsMesh);
		}

		/// <summary>
		/// Creates a mesh rendering game object.
		/// </summary>
		/// <param name="targetMesh">The mesh to render.</param>
		/// <param name="name">The mesh name.</param>
		/// <param name="shader">The material to use for rendering.</param>
		/// <param name="layer">The layer to use for rendering.</param>
		/// <param name="z">The z coordinate where the mesh should be drawn.</param>
		/// <returns>A game object to render that mesh.</returns>
		private GameObject Create(Mesh targetMesh, string name, Material shader, int layer,
				float z) {
			var game = Game.Instance;
			if (game == null)
				throw new ArgumentNullException(nameof(Game.Instance));
			var go = targetMesh.CreateMeshRenderer(name, layer, shader);
			var t = go.transform;
			t.SetParent(game.transform);
			t.SetPositionAndRotation(new Vector3(0.0f, 0.0f, z), Quaternion.identity);
			go.SetActive(true);
			return go;
		}

		/// <summary>
		/// Updates the material used for drawing the stars.
		/// </summary>
		/// <param name="material">The material to use.</param>
		internal void UpdateStarsMaterial(Material material) {
			starsRender.material = material;
		}
	}

	/// <summary>
	/// Applied to TerrainBG to create the mesh renderer object on startup.
	/// </summary>
	[HarmonyPatch(typeof(TerrainBG), nameof(TerrainBG.OnSpawn))]
	public static class TerrainBG_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
			FastTrackOptions.MeshRendererSettings.None;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(TerrainBG __instance) {
			TerrainMeshRenderer.DestroyInstance();
			TerrainMeshRenderer.Instance = new TerrainMeshRenderer(__instance);
		}
	}

	/// <summary>
	/// Applied to TerrainBG to turn off manual graphics mesh rendering.
	/// </summary>
	[HarmonyPatch(typeof(TerrainBG), nameof(TerrainBG.LateUpdate))]
	public static class TerrainBG_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
			FastTrackOptions.MeshRendererSettings.None;

		/// <summary>
		/// Wraps Material.SetTexture to update the correct stars material to its mesh
		/// renderer.
		/// </summary>
		/// <param name="material">The stars material.</param>
		/// <param name="name">The noise texture name.</param>
		/// <param name="texture">The noise texture to apply.</param>
		private static void SetTexture(Material material, string name, Texture texture) {
			if (material != null) {
				material.SetTexture(name, texture);
				TerrainMeshRenderer.Instance?.UpdateStarsMaterial(material);
			}
		}

		/// <summary>
		/// Transpiles LateUpdate to disable all of the relevant Graphics.DrawMesh calls.
		/// Leaves the worldPlane ones for now.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var drawMesh = typeof(Graphics).GetMethodSafe(nameof(Graphics.DrawMesh),
				true, typeof(Mesh), typeof(Vector3), typeof(Quaternion), typeof(Material),
				typeof(int));
			var target = typeof(Material).GetMethodSafe(nameof(Material.SetTexture), false,
				typeof(string), typeof(Texture));
			var replacement = typeof(TerrainBG_LateUpdate_Patch).GetMethodSafe(nameof(
				SetTexture), true, typeof(Material), typeof(string), typeof(Texture));
			TranspiledMethod newMethod;
			if (drawMesh != null && target != null && replacement != null)
				newMethod = PPatchTools.ReplaceMethodCallSafe(instructions,
					new Dictionary<MethodInfo, MethodInfo>() {
						{ drawMesh, PPatchTools.RemoveCall }, { target, replacement }
					});
			else {
				newMethod = instructions;
				PUtil.LogWarning("Unable to patch TerrainBG.LateUpdate");
			}
			return newMethod;
		}
	}
}
