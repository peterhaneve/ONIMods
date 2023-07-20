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
using Rendering;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Turns off mesh-based kanim rendering (in-world animations) when any full screen dialog
	/// is visible, as the kanims would be hidden anyways.
	/// </summary>
	public static class FullScreenDialogPatches {
		/// <summary>
		/// Returns true if a full-screen dialog is visible, or false otherwise.
		/// </summary>
		internal static bool DialogVisible { get; private set; }

		/// <summary>
		/// On startup, no dialog is visible.
		/// </summary>
		internal static void Init() {
			DialogVisible = false;
		}

		/// <summary>
		/// Applied to BlockTileRenderer to stop rendering tiles if a full-screen dialog is
		/// visible.
		/// </summary>
		[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.LateUpdate))]
		public static class BlockTileRenderer_LateUpdate_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.RenderTicks && options.MeshRendererOptions == FastTrackOptions.
					MeshRendererSettings.None;
			}

			/// <summary>
			/// Applied before LateUpdate runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix() {
				return !DialogVisible || GameUtil.IsCapturingTimeLapse();
			}
		}

		/// <summary>
		/// Applied to full-screen dialogs to set flags when they are visible.
		/// </summary>
		[HarmonyPatch]
		public static class FullScreenDialog_OnShow_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Patches each fullscreen dialog's OnShow method.
			/// </summary>
			internal static IEnumerable<MethodBase> TargetMethods() {
				yield return typeof(ClusterMapScreen).GetMethodSafe(nameof(ClusterMapScreen.
					OnShow), false, typeof(bool));
				yield return typeof(ResearchScreen).GetMethodSafe(nameof(ResearchScreen.
					OnShow), false, typeof(bool));
				yield return typeof(StarmapScreen).GetMethodSafe(nameof(StarmapScreen.OnShow),
					false, typeof(bool));
				yield return typeof(SkillsScreen).GetMethodSafe(nameof(SkillsScreen.OnShow),
					false, typeof(bool));
			}

			/// <summary>
			/// Applied after OnShow runs.
			/// </summary>
			internal static void Postfix(bool show) {
				DialogVisible = show;
			}
		}

		/// <summary>
		/// Applied to GroundRenderer to hide the ground if a full-screen dialog is visible.
		/// </summary>
		[HarmonyPatch(typeof(GroundRenderer), nameof(GroundRenderer.Render))]
		public static class GroundRenderer_Render_Patch {
			internal static bool Prepare() {
				var options = FastTrackOptions.Instance;
				return options.RenderTicks && options.MeshRendererOptions == FastTrackOptions.
					MeshRendererSettings.None;
			}

			/// <summary>
			/// Applied before Render runs.
			/// </summary>
			[HarmonyPriority(Priority.Low)]
			internal static bool Prefix(bool forceVisibleRebuild) {
				return forceVisibleRebuild || !DialogVisible;
			}
		}

		/// <summary>
		/// Applied to KAnimBatchManager to set the active area to an invisible zone if the
		/// full-screen dialogs are visible.
		/// </summary>
		[HarmonyPatch(typeof(KAnimBatchManager), nameof(KAnimBatchManager.UpdateActiveArea))]
		public static class KAnimBatchManager_UpdateActiveArea_Patch {
			internal static bool Prepare() => FastTrackOptions.Instance.RenderTicks;

			/// <summary>
			/// Applied before UpdateActiveArea runs.
			/// </summary>
			internal static void Prefix(ref Vector2I vis_chunk_min, ref Vector2I vis_chunk_max)
			{
				int maxX = vis_chunk_max.x, maxY = vis_chunk_max.y;
				if (maxX <= 9000 && maxY <= 9000 && DialogVisible) {
					// It is not over 9000! (9999 is used for "show all" in the timelapse)
					vis_chunk_min.x = int.MinValue;
					vis_chunk_min.y = int.MinValue;
					vis_chunk_max.x = int.MinValue + 1;
					vis_chunk_max.y = int.MinValue + 1;
				}
			}
		}
	}
}
