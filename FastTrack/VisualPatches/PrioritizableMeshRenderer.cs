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
using System.Reflection.Emit;
using UnityEngine;

using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders PrioritizableRenderer with a MeshRenderer instead of using Graphics.DrawMesh.
	/// </summary>
	public sealed class PrioritizableMeshRenderer : KMonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static PrioritizableMeshRenderer Instance { get; private set; }

		/// <summary>
		/// Creates a mesh renderer object for the priority overlay.
		/// </summary>
		/// <param name="targetMesh">The mesh to render.</param>
		/// <param name="shader">The shader to use for rendering.</param>
		internal static void CreateInstance(Mesh targetMesh, Material shader) {
			var game = Game.Instance;
			if (targetMesh == null)
				throw new ArgumentNullException(nameof(targetMesh));
			if (game == null)
				throw new ArgumentNullException(nameof(Game.Instance));
			DestroyInstance();
			var go = new GameObject("Priority Overlay", typeof(PrioritizableMeshRenderer),
				typeof(MeshRenderer), typeof(MeshFilter));
			go.layer = LayerMask.NameToLayer("UI");
			var t = go.transform;
			t.SetParent(game.gameObject.transform);
			t.SetPositionAndRotation(new Vector3(0.0f, 0.0f, Grid.GetLayerZ(Grid.SceneLayer.
				FXFront2)), Quaternion.identity);
			Instance = go.GetComponent<PrioritizableMeshRenderer>();
			// Set up the mesh with the right material
			var renderer = go.GetComponent<MeshRenderer>();
			renderer.allowOcclusionWhenDynamic = false;
			renderer.material = shader;
			renderer.receiveShadows = false;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			// Set the mesh to render
			var filter = go.GetComponent<MeshFilter>();
			filter.sharedMesh = targetMesh;
			go.SetActive(false);
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

		/// <summary>
		/// Turns the mesh renderer on or off depending on the current overlay.
		/// </summary>
		internal static void SetInstanceVisibility() {
			var inst = SimDebugView.Instance;
			if (Instance != null)
				Instance.SetVisible(GameScreenManager.Instance != null && inst != null &&
					inst.GetMode() == OverlayModes.Priorities.ID);
		}

		/// <summary>
		/// Tracks whether the renderer was active last frame.
		/// </summary>
		private bool wasActive;

		internal PrioritizableMeshRenderer() {
			wasActive = false;
		}

		/// <summary>
		/// Turns the mesh rendere object on or off.
		/// </summary>
		/// <param name="visible">true to make it visible, or false to make it invisible.</param>
		internal void SetVisible(bool visible) {
			if (visible != wasActive) {
				PUtil.LogDebug("Set visible to " + visible);
				gameObject.SetActive(visible);
				wasActive = visible;
			}
		}
	}

	/// <summary>
	/// Applied to PrioritizableRenderer to destroy the mesh renderer object on close.
	/// </summary>
	[HarmonyPatch(typeof(PrioritizableRenderer), nameof(PrioritizableRenderer.Cleanup))]
	public static class PrioritizableRenderer_Cleanup_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

		/// <summary>
		/// Applied before Cleanup runs.
		/// </summary>
		internal static void Prefix() {
			// Destroy the mesh renderer game object
			PrioritizableMeshRenderer.DestroyInstance();
		}
	}

	/// <summary>
	/// Applied to PrioritizableRenderer to create the mesh renderer object on startup.
	/// </summary>
	[HarmonyPatch(typeof(PrioritizableRenderer), MethodType.Constructor, new Type[0])]
	public static class PrioritizableRenderer_Constructor_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

		/// <summary>
		/// Applied before Cleanup runs.
		/// </summary>
		internal static void Postfix(Mesh ___mesh, Material ___material) {
			PrioritizableMeshRenderer.CreateInstance(___mesh, ___material);
		}
	}

	/// <summary>
	/// Applied to PrioritizableRenderer to turn off manual graphics mesh rendering.
	/// </summary>
	[HarmonyPatch(typeof(PrioritizableRenderer), nameof(PrioritizableRenderer.RenderEveryTick))]
	public static class PrioritizableRenderer_RenderEveryTick_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.UseMeshRenderers;

		/// <summary>
		/// Transpiles PrioritizableRenderer to first update the mesh renderer status, then
		/// disable the actual Graphics.DrawMesh call.
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var target = typeof(PrioritizableMeshRenderer).GetMethodSafe(nameof(
				PrioritizableMeshRenderer.SetInstanceVisibility), true);
			var drawMesh = typeof(Graphics).GetMethodSafe(nameof(Graphics.DrawMesh),
				true, typeof(Mesh), typeof(Vector3), typeof(Quaternion), typeof(Material),
				typeof(int), typeof(Camera), typeof(int), typeof(MaterialPropertyBlock),
				typeof(bool), typeof(bool));
			yield return new CodeInstruction(OpCodes.Call, target);
			var newMethod = instructions;
			// Assigning triangles automatically recalculates bounds!
			if (drawMesh != null)
				newMethod = PPatchTools.ReplaceMethodCall(instructions, drawMesh, null);
			else
				PUtil.LogWarning("Unable to patch PrioritizableRenderer.RenderEveryTick");
			foreach (var instr in newMethod)
				yield return instr;
		}
	}
}
