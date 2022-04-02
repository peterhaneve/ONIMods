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
using Rendering;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Applied to BlockTileRenderer to redirect add calls to our own.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.AddBlock))]
	public static class BlockTileRenderer_AddBlock_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before AddBlock runs.
		/// </summary>
		internal static bool Prefix(int renderLayer, BuildingDef def, bool isReplacement,
				SimHashes element, int cell) {
			TileMeshRenderer.Instance?.AddBlock(renderLayer, def, isReplacement, element,
				cell);
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to replace its LateUpdate method with our own.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.LateUpdate))]
	public static class BlockTileRenderer_LateUpdate_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		internal static bool Prefix() {
			TileMeshRenderer.Instance?.Render();
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to redirect Rebuild calls to our own.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.Rebuild))]
	public static class BlockTileRenderer_Rebuild_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before Rebuild runs.
		/// </summary>
		internal static bool Prefix(ObjectLayer layer, int cell) {
			TileMeshRenderer.Instance?.Rebuild(layer, cell);
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to redirect remove calls to our own.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.RemoveBlock))]
	public static class BlockTileRenderer_RemoveBlock_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before RemoveBlock runs.
		/// </summary>
		internal static bool Prefix(BuildingDef def, bool isReplacement, SimHashes element,
				int cell) {
			TileMeshRenderer.Instance?.RemoveBlock(def, isReplacement, element, cell);
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to highlight tiles using our version instead.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.HighlightCell))]
	public static class BlockTileRenderer_HighlightCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before HighlightPlaceCell runs.
		/// </summary>
		internal static bool Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.HighlightCell(cell, enabled);
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to select tiles using our version instead.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.SelectCell))]
	public static class BlockTileRenderer_SelectCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before SelectCell runs.
		/// </summary>
		internal static bool Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.SelectCell(cell, enabled);
			return false;
		}
	}

	/// <summary>
	/// Applied to BlockTileRenderer to redirect invalid place cells to our version.
	/// </summary>
	[HarmonyPatch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.SetInvalidPlaceCell))]
	public static class BlockTileRenderer_SetInvalidPlaceCell_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied before SetInvalidPlaceCell runs.
		/// </summary>
		internal static bool Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.SetInvalidPlaceCell(cell, enabled);
			return false;
		}
	}

	/// <summary>
	/// Applied to World to create a tile mesh renderer before the Game is loaded.
	/// </summary>
	[HarmonyPatch(typeof(World), nameof(World.OnPrefabInit))]
	public static class World_OnPrefabInit_Patch {
		internal static bool Prepare() => FastTrackOptions.Instance.MeshRendererOptions ==
			FastTrackOptions.MeshRendererSettings.All;

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		internal static void Postfix() {
			TileMeshRenderer.CreateInstance();
		}
	}
}
