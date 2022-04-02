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
using System.Reflection.Emit;
using UnityEngine;

using CellVisByLocation = System.Collections.Generic.IDictionary<AxialI, ClusterMapVisualizer>;
using TranspiledMethod = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Applied to AxialI to make the hash code method less likely to collide.
	/// </summary>
	[HarmonyPatch(typeof(AxialI), nameof(AxialI.GetHashCode))]
	public static class AxialI_GetHashCode_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

		/// <summary>
		/// Transpiles GetHashCode to replace (x ^ y) with (x ^ (y << 16)).
		/// </summary>
		internal static TranspiledMethod Transpiler(TranspiledMethod instructions) {
			var r = typeof(AxialI).GetFieldSafe(nameof(AxialI.r), false);
			var q = typeof(AxialI).GetFieldSafe(nameof(AxialI.q), false);
			if (r != null && q != null) {
				// Load r
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, r);
				// Load q
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, q);
				// Shift left 16
				yield return new CodeInstruction(OpCodes.Ldc_I4, 16);
				yield return new CodeInstruction(OpCodes.Shl);
				// Xor
				yield return new CodeInstruction(OpCodes.Xor);
				yield return new CodeInstruction(OpCodes.Ret);
			} else {
				PUtil.LogWarning("Unable to patch AxialI.GetHashCode");
				foreach (var instr in instructions)
					yield return instr;
			}
		}
	}

	/// <summary>
	/// Groups the patches to the Spaced Out starmap screen.
	/// </summary>
	public static class ClusterMapScreenPatches {
		/// <summary>
		/// The core shared code of the MoveToNISPosition method, split out to optimize
		/// updates in ScreenUpdate.
		/// </summary>
		/// <param name="instance">The map screen rendering this method.</param>
		/// <param name="scale">The current scale of the starmap.</param>
		/// <param name="targetScale">The scale that the starmap is moving towards.</param>
		/// <param name="position">The current and new position of the starmap</param>
		/// <param name="selectWhenComplete">The hex to select when complete.</param>
		/// <param name="selected">The currently selected hex.</param>
		private static void NISMoveCore(ClusterMapScreen instance,
				float scale, ref float targetScale, ref Vector3 position) {
			float dt = Time.unscaledDeltaTime, distance;
			bool move = true;
			Vector3 pos = position;
			var targetPosition = instance.targetNISPosition;
			var destination = new Vector3(-targetPosition.x * scale, -targetPosition.y *
				scale, targetPosition.z);
			var cells = instance.m_cellVisByLocation;
			// Always 150.0 when reached
			targetScale = Mathf.Lerp(targetScale, 150.0f, dt * 2.0f);
			position = pos = Vector3.Lerp(pos, destination, dt * 2.5f);
			distance = Vector3.Distance(pos, destination);
			// Close to destination?
			if (distance < 100.0f && cells.TryGetValue(instance.selectOnMoveNISComplete,
					out ClusterMapVisualizer visualizer)) {
				var hex = visualizer.GetComponent<ClusterMapHex>();
				if (instance.m_selectedHex != hex)
					instance.SelectHex(hex);
				// Reached destination?
				if (distance < 10.0f)
					move = false;
			}
			instance.movingToTargetNISPosition = move;
		}

		/// <summary>
		/// Applied to ClusterMapScreen to turn off the floating asteroid animation which is
		/// shockingly slow.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.
			FloatyAsteroidAnimation))]
		internal static class FloatyAsteroidAnimation_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.NoBounce;

			/// <summary>
			/// Applied before FloatyAsteroidAnimation runs.
			/// </summary>
			internal static bool Prefix() {
				return false;
			}
		}

		/// <summary>
		/// Applied to ClusterMapScreen to optimize the move-to animation.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.MoveToNISPosition))]
		internal static class MoveToNISPosition_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before MoveToNISPosition runs.
			/// </summary>
			internal static bool Prefix(ClusterMapScreen __instance) {
				RectTransform content;
				var mapScrollRect = __instance.mapScrollRect;
				if (__instance.movingToTargetNISPosition && mapScrollRect != null && (content =
						mapScrollRect.content) != null) {
					var pos = content.localPosition;
					NISMoveCore(__instance, __instance.m_currentZoomScale, ref __instance.
						m_targetZoomScale, ref pos);
					content.localPosition = pos;
				}
				return false;
			}
		}

		/// <summary>
		/// Applied to ClusterMapScreen to only move things on the map if they need to be updated.
		/// </summary>
		[HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.ScreenUpdate))]
		internal static class ScreenUpdate_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before ScreenUpdate runs.
			/// </summary>
			internal static bool Prefix(ClusterMapScreen __instance) {
				RectTransform content;
				var mapScrollRect = __instance.mapScrollRect;
				if (mapScrollRect != null && (content = mapScrollRect.content) != null) {
					float scale = __instance.m_currentZoomScale, target = __instance.
						m_targetZoomScale;
					var mousePos = KInputManager.GetMousePos();
					var ip = content.InverseTransformPoint(mousePos);
					Vector3 pos = content.localPosition;
					bool move = false;
					if (!Mathf.Approximately(target, scale)) {
						// Only if necessary
						__instance.m_currentZoomScale = scale = Mathf.Lerp(scale, target,
							Mathf.Min(4.0f * Time.unscaledDeltaTime, 0.9f));
						content.localScale = new Vector3(scale, scale, 1f);
						var fp = content.InverseTransformPoint(mousePos);
						if (!Mathf.Approximately(ip.x, fp.x) || !Mathf.Approximately(ip.y,
								fp.y) || !Mathf.Approximately(ip.z, fp.z)) {
							// If the point changed, center it correctly
							pos += (fp - ip) * scale;
							move = true;
						}
					}
					if (__instance.movingToTargetNISPosition) {
						move = true;
						NISMoveCore(__instance, scale, ref target, ref pos);
						__instance.m_targetZoomScale = target;
					}
					if (move)
						content.localPosition = pos;
				}
				return false;
			}
		}
	}
}
