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
using Rendering;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Handles all Tile Mesh Renderer patches, as they all must be applied manually.
	/// </summary>
	public static class TileMeshPatches {
		/// <summary>
		/// Applied before AddBlock runs.
		/// </summary>
		private static bool AddBlock_Prefix(int renderLayer, BuildingDef def,
				bool isReplacement, SimHashes element, int cell) {
			TileMeshRenderer.Instance?.AddBlock(renderLayer, def, isReplacement, element,
				cell);
			return false;
		}

		/// <summary>
		/// Applies all tile mesh renderer patches.
		/// </summary>
		/// <param name="harmony">The Harmony instance to use for patching.</param>
		internal static void Apply(Harmony harmony) {
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.AddBlock),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(AddBlock_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.HighlightCell),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(
				HighlightCell_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.LateUpdate),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(LateUpdate_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.Rebuild),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(Rebuild_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.RemoveBlock),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(
				RemoveBlock_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.SelectCell),
				prefix: new HarmonyMethod(typeof(TileMeshPatches), nameof(
				SelectCell_Prefix)));
			harmony.Patch(typeof(BlockTileRenderer), nameof(BlockTileRenderer.
				SetInvalidPlaceCell), prefix: new HarmonyMethod(typeof(TileMeshPatches),
				nameof(SetInvalidPlaceCell_Prefix)));
			harmony.Patch(typeof(World), nameof(World.OnPrefabInit), postfix:
				new HarmonyMethod(typeof(TileMeshPatches), nameof(OnPrefabInit_Postfix)));
		}

		/// <summary>
		/// Applied before HighlightCell runs.
		/// </summary>
		private static bool HighlightCell_Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.HighlightCell(cell, enabled);
			return false;
		}

		/// <summary>
		/// Applied before LateUpdate runs.
		/// </summary>
		private static bool LateUpdate_Prefix() {
			TileMeshRenderer.Instance?.Render();
			return false;
		}

		/// <summary>
		/// Applied after OnPrefabInit runs.
		/// </summary>
		private static void OnPrefabInit_Postfix(World __instance) {
			TileMeshRenderer.CreateInstance(__instance.blockTileRenderer);
		}

		/// <summary>
		/// Applied before Rebuild runs.
		/// </summary>
		private static bool Rebuild_Prefix(ObjectLayer layer, int cell) {
			TileMeshRenderer.Instance?.Rebuild(layer, cell);
			return false;
		}

		/// <summary>
		/// Applied before RemoveBlock runs.
		/// </summary>
		private static bool RemoveBlock_Prefix(BuildingDef def, bool isReplacement,
				SimHashes element, int cell) {
			TileMeshRenderer.Instance?.RemoveBlock(def, isReplacement, element, cell);
			return false;
		}

		/// <summary>
		/// Applied before SelectCell runs.
		/// </summary>
		private static bool SelectCell_Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.SelectCell(cell, enabled);
			return false;
		}

		/// <summary>
		/// Applied before SetInvalidPlaceCell runs.
		/// </summary>
		private static bool SetInvalidPlaceCell_Prefix(int cell, bool enabled) {
			TileMeshRenderer.Instance?.SetInvalidPlaceCell(cell, enabled);
			return false;
		}
	}
}
