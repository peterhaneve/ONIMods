/*
 * Copyright 2023 Peter Han
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
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders FallingWater using a mesh renderer instead of drawing a mesh every frame.
	/// </summary>
	internal sealed class FallingWaterMeshRenderer : MonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static FallingWaterMeshRenderer Instance { get; private set; }

		/// <summary>
		/// Creates a mesh renderer object for falling water.
		/// </summary>
		/// <param name="instance">The falling water object to render.</param>
		internal static void CreateInstance(FallingWater instance) {
			var game = Game.Instance;
			if (game == null)
				throw new ArgumentNullException(nameof(Game.Instance));
			DestroyInstance();
			var go = instance.mesh.CreateMeshRenderer("Falling Water", LayerMask.NameToLayer(
				"Water"));
			var t = go.transform;
			t.SetParent(game.transform);
			t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			// Set up the mesh with the right material
			if (go.TryGetComponent(out MeshRenderer renderer)) {
				renderer.material = instance.material;
				renderer.SetPropertyBlock(instance.propertyBlock);
			}
			Instance = go.AddOrGet<FallingWaterMeshRenderer>();
		}

		/// <summary>
		/// Destroys the prioritizable mesh renderer.
		/// </summary>
		public static void DestroyInstance() {
			var inst = Instance;
			if (inst != null)
				Util.KDestroyGameObject(inst.gameObject);
			Instance = null;
		}
	}

	/// <summary>
	/// Applied to FallingWater to destroy the mesh renderer object on close.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.OnCleanUp))]
	public static class FallingWater_OnCleanUp_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
			FastTrackOptions.MeshRendererSettings.None;

		/// <summary>
		/// Applied before OnCleanUp runs.
		/// </summary>
		internal static void Prefix(FallingWater __instance) {
			// Destroy the mesh renderer game object
			FallingWaterMeshRenderer.DestroyInstance();
			// Destroy the mesh
			UnityEngine.Object.Destroy(__instance.mesh);
			__instance.mesh = null;
		}
	}

	/// <summary>
	/// Applied to FallingWater to create the mesh renderer object on startup.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.OnSpawn))]
	public static class FallingWater_OnSpawn_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
			FastTrackOptions.MeshRendererSettings.None;

		/// <summary>
		/// Applied after OnSpawn runs.
		/// </summary>
		internal static void Postfix(FallingWater __instance) {
			FallingWaterMeshRenderer.CreateInstance(__instance);
		}
	}

	/// <summary>
	/// Applied to FallingWater to turn off manual graphics mesh rendering.
	/// </summary>
	[HarmonyPatch(typeof(FallingWater), nameof(FallingWater.Render))]
	public static class FallingWater_Render_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions !=
			FastTrackOptions.MeshRendererSettings.None;

		/// <summary>
		/// Transpiles Render to disable the actual Graphics.DrawMesh call.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var drawMesh = typeof(Graphics).GetMethodSafe(nameof(Graphics.DrawMesh),
				true, typeof(Mesh), typeof(Vector3), typeof(Quaternion), typeof(Material),
				typeof(int), typeof(Camera), typeof(int), typeof(MaterialPropertyBlock));
			TranspiledMethod newMethod;
			if (drawMesh != null)
				newMethod = PPatchTools.RemoveMethodCall(instructions, drawMesh);
			else {
				PUtil.LogWarning("Unable to patch FallingWater.Render");
				newMethod = instructions;
			}
			return newMethod;
		}
	}
}
