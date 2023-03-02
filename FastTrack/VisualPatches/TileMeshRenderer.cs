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

using Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;

using Bits = Rendering.BlockTileRenderer.Bits;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Renders tiles and tiled objects (like wires) using mesh renderers.
	/// </summary>
	public sealed class TileMeshRenderer : IDisposable {
		/// <summary>
		/// The size of tile "chunks" that can be rendered.
		/// </summary>
		public const int CHUNK_SIZE = 16;

		/// <summary>
		/// Scale factors used in tile decor tops.
		/// </summary>
		private static readonly Vector2 SIMPLEX_SCALE = new Vector2(92.41f, 87.16f);

		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static TileMeshRenderer Instance { get; private set; }

		/// <summary>
		/// Creates the singleton instance of this class.
		/// </summary>
		/// <param name="colorSource">The tile renderer to use for tile colors.</param>
		internal static void CreateInstance(BlockTileRenderer colorSource) {
			DestroyInstance();
			Instance = new TileMeshRenderer(colorSource);
		}

		/// <summary>
		/// Destroys the singleton instance of this class.
		/// </summary>
		internal static void DestroyInstance() {
			Instance?.Dispose();
			Instance = null;
		}

		/// <summary>
		/// The tile renderer to query for the color tint of each tile.
		/// </summary>
		private readonly BlockTileRenderer colorSource;

		/// <summary>
		/// The coordinates that were deactivated from the previous frame.
		/// </summary>
		private readonly IList<Vector2I> deactivated;

		/// <summary>
		/// Whether a rebuild has been forced.
		/// </summary>
		private bool forceRebuild;

		/// <summary>
		/// The highlighted cell (for colouring purposes).
		/// </summary>
		private int highlightCell;

		/// <summary>
		/// The invalid placement cell (for colouring purposes).
		/// </summary>
		private int invalidPlaceCell;

		/// <summary>
		/// The last visible chunk extents.
		/// </summary>
		private Extents lastVisible;

		/// <summary>
		/// Stores the state of all rendered tile-like buildings.
		/// </summary>
		private readonly IDictionary<BuildingDef, TileRenderInfo> renderInfo;

		/// <summary>
		/// The currently selected cell (for colouring purposes).
		/// </summary>
		private int selectedCell;

		internal TileMeshRenderer(BlockTileRenderer colorSource) {
			this.colorSource = colorSource;
			deactivated = new List<Vector2I>(16);
			forceRebuild = false;
			lastVisible = new Extents(0, 0, 0, 0);
			renderInfo = new Dictionary<BuildingDef, TileRenderInfo>(128);
			selectedCell = Grid.InvalidCell;
			highlightCell = Grid.InvalidCell;
			invalidPlaceCell = Grid.InvalidCell;
		}

		/// <summary>
		/// Adds a tile-like object to render.
		/// </summary>
		/// <param name="renderLayer">The layer to use for rendering.</param>
		/// <param name="def">The building def to render.</param>
		/// <param name="isReplacement">true if replacing another item, or false otherwise.</param>
		/// <param name="element">The element used to build the def.</param>
		/// <param name="cell">The cell the building will occupy.</param>
		public void AddBlock(int renderLayer, BuildingDef def, bool isReplacement,
				SimHashes element, int cell) {
			int layer = (int)BlockTileRenderer.GetRenderInfoLayer(isReplacement, element);
			// If building never seen before
			if (!renderInfo.TryGetValue(def, out TileRenderInfo infoForTile))
				renderInfo.Add(def, infoForTile = new TileRenderInfo());
			int queryLayer = (int)(isReplacement ? def.ReplacementLayer : def.TileLayer);
			var infos = infoForTile.infos;
			// Create the layer if not yet populated
			var info = infos[layer];
			if (info == null) {
				info = new LayerRenderInfo(queryLayer, renderLayer, def, element);
				infos[layer] = info;
			}
			info.AddCell(cell);
		}

		public void Dispose() {
			foreach (var pair in renderInfo)
				pair.Value.Dispose();
			renderInfo.Clear();
		}

		/// <summary>
		/// Forces a full tile rebuild.
		/// </summary>
		public void ForceRebuild() {
			forceRebuild = true;
		}

		/// <summary>
		/// Gets the highlight color of the building in a particular cell.
		/// </summary>
		/// <param name="cell">The cell to query.</param>
		/// <param name="element">The element used to build the building, or SimHashes.Void
		/// for preview buildings.</param>
		/// <returns>The highlight color to apply to the mesh there.</returns>
		internal Color GetCellColor(int cell, SimHashes element) {
			Color result;
			if (colorSource == null)
				result = Color.white;
			else {
				colorSource.selectedCell = selectedCell;
				colorSource.invalidPlaceCell = invalidPlaceCell;
				colorSource.highlightCell = highlightCell;
				result = colorSource.GetCellColour(cell, element);
			}
			return result;
		}

		/// <summary>
		/// Highlights a cell.
		/// </summary>
		/// <param name="cell">The cell to update.</param>
		/// <param name="enabled">true to highlight it, or false to clear the highlight.</param>
		public void HighlightCell(int cell, bool enabled) {
			UpdateCellStatus(ref highlightCell, cell, enabled);
		}

		/// <summary>
		/// Rebuilds only a specific cell.
		/// </summary>
		/// <param name="layer">The layer to rebuild.</param>
		/// <param name="cell">The cell in that layer to rebuild.</param>
		public void Rebuild(ObjectLayer layer, int cell) {
			if (Grid.IsValidCell(cell))
				foreach (var pair in renderInfo)
					if (pair.Key.TileLayer == layer)
						pair.Value.MarkDirty(cell, false);
		}

		/// <summary>
		/// Removes a tile-like object from rendering.
		/// </summary>
		/// <param name="def">The building def to render.</param>
		/// <param name="isReplacement">true if replacing another item, or false otherwise.</param>
		/// <param name="element">The element used to build the def.</param>
		/// <param name="cell">The cell the building occupied.</param>
		public void RemoveBlock(BuildingDef def, bool isReplacement, SimHashes element,
				int cell) {
			int layer = (int)BlockTileRenderer.GetRenderInfoLayer(isReplacement, element);
			LayerRenderInfo info;
			if (renderInfo.TryGetValue(def, out TileRenderInfo infoForTile) && (info =
					infoForTile.infos[layer]) != null)
				info.RemoveCell(cell);
		}

		/// <summary>
		/// Renders all tilable objects in view.
		/// </summary>
		internal void Render() {
			Vector2I min, max;
			var cc = CameraController.Instance;
			if (cc != null && !GameUtil.IsCapturingTimeLapse()) {
				// Use camera area
				var visibleArea = cc.VisibleArea.CurrentArea;
				min = visibleArea.Min;
				max = visibleArea.Max;
				min.x /= CHUNK_SIZE;
				min.y /= CHUNK_SIZE;
				// Round UP!
				max.x = (max.x + CHUNK_SIZE - 1) / CHUNK_SIZE;
				max.y = (max.y + CHUNK_SIZE - 1) / CHUNK_SIZE;
			} else {
				min = Vector2I.zero;
				max = new Vector2I(Grid.WidthInCells / CHUNK_SIZE, Grid.HeightInCells /
					CHUNK_SIZE);
			}
			int minX = min.x, maxX = max.x, minY = min.y, maxY = max.y;
			int lMinX = lastVisible.x, lMaxX = lastVisible.width, lMinY = lastVisible.y,
				lMaxY = lastVisible.height;
			// Turn off hidden chunks
			for (int x = lMinX; x < lMaxX; x++)
				for (int y = lMinY; y < lMaxY; y++)
					if (y < minY || y >= maxY || x < minX || x >= maxX)
						deactivated.Add(new Vector2I(x, y));
			// Store the last visible volume
			lastVisible.x = minX;
			lastVisible.y = minY;
			lastVisible.width = maxX;
			lastVisible.height = maxY;
			int n = deactivated.Count;
			foreach (var pair in renderInfo) {
				var info = pair.Value;
				for (int i = 0; i < n; i++) {
					var coords = deactivated[i];
					info.Deactivate(coords.x, coords.y);
				}
				for (int x = minX; x < maxX; x++)
					for (int y = minY; y < maxY; y++)
						info.Rebuild(this, x, y);
			}
			deactivated.Clear();
			forceRebuild = false;
		}

		/// <summary>
		/// Selects a cell.
		/// </summary>
		/// <param name="cell">The cell to update.</param>
		/// <param name="enabled">true to select it, or false to deselect it.</param>
		public void SelectCell(int cell, bool enabled) {
			UpdateCellStatus(ref selectedCell, cell, enabled);
		}

		/// <summary>
		/// Sets a cell as an invalid (graphically) place location.
		/// </summary>
		/// <param name="cell">The cell to update.</param>
		/// <param name="enabled">true to mark it as invalid, or false to mark it as valid.</param>
		public void SetInvalidPlaceCell(int cell, bool enabled) {
			UpdateCellStatus(ref invalidPlaceCell, cell, enabled);
		}

		/// <summary>
		/// Updates a particular cell's status.
		/// </summary>
		/// <param name="cellStatus">The cell status (class field) to update.</param>
		/// <param name="cell">The new cell for that field.</param>
		/// <param name="enabled">true to enable it, or false to disable it.</param>
		private void UpdateCellStatus(ref int cellStatus, int cell, bool enabled) {
			int curValue = cellStatus;
			if (enabled) {
				if (cell != curValue) {
					// Mark the old value dirty
					if (curValue != Grid.InvalidCell)
						foreach (var pair in renderInfo)
							pair.Value.MarkDirty(curValue, true);
					cellStatus = cell;
					// Mark the new value dirty
					foreach (var pair in renderInfo)
						pair.Value.MarkDirty(cell, true);
				}
			} else if (curValue == cell) {
				foreach (var pair in renderInfo)
					pair.Value.MarkDirty(curValue, false);
				cellStatus = Grid.InvalidCell;
			}
		}

		/// <summary>
		/// Stores render information for one layer of buildings.
		/// </summary>
		internal sealed class LayerRenderInfo : IDisposable {
			/// <summary>
			/// The offset of the decor ornaments from the tile itself.
			/// </summary>
			private const float DECOR_Z_OFFSET = -1.5f;

			/// <summary>
			/// The atlas information used to render the tiles.
			/// </summary>
			private readonly AtlasInfo[] atlasInfo;

			/// <summary>
			/// The decor meshes to render.
			/// </summary>
			private readonly MeshChunk[,] decorChunks;

			/// <summary>
			/// The material used for rendering the tile decor.
			/// </summary>
			private readonly Material decorMaterial;

			/// <summary>
			/// The triangles used for rendering the decor mesh.
			/// </summary>
			private readonly List<TriangleInfo> decorTriangles;

			/// <summary>
			/// Used to render ornamental tile tops.
			/// </summary>
			private readonly BlockTileDecorInfo decorRenderInfo;

			/// <summary>
			/// The element to use for the color and material.
			/// </summary>
			private readonly SimHashes element;

			/// <summary>
			/// The meshes to render.
			/// </summary>
			private readonly MeshChunk[,] renderChunks;

			/// <summary>
			/// The cells with a tile in them.
			/// </summary>
			private readonly IDictionary<int, int> occupiedCells;

			/// <summary>
			/// The object layer to look for buildings of this type.
			/// </summary>
			private readonly int queryLayer;

			/// <summary>
			/// The material used for rendering the tiles.
			/// </summary>
			private readonly Material renderMaterial;

			public LayerRenderInfo(int queryLayer, int renderLayer, BuildingDef def,
					SimHashes element) {
				int width = Grid.WidthInCells / CHUNK_SIZE + 1;
				int height = Grid.HeightInCells / CHUNK_SIZE + 1;
				float z = Grid.GetLayerZ(def.SceneLayer);
				this.queryLayer = queryLayer;
				this.element = element;
				occupiedCells = new Dictionary<int, int>(CHUNK_SIZE * CHUNK_SIZE / 4);
				renderMaterial = def.GetMaterial(element);
				renderChunks = new MeshChunk[width, height];
				for (int x = 0; x < width; x++)
					for (int y = 0; y < height; y++)
						renderChunks[x, y] = new MeshChunk(renderMaterial, renderLayer, z);
				// Spawn tile decor if needed
				decorRenderInfo = (element == SimHashes.Void) ? def.DecorPlaceBlockTileInfo :
					def.DecorBlockTileInfo;
				if (decorRenderInfo == null) {
					decorMaterial = null;
					decorTriangles = null;
					decorChunks = null;
				} else {
					float decorZOffset;
					// Decor has to appear in front to avoid z-fighting
					if (def.BlockTileIsTransparent)
						decorZOffset = Grid.GetLayerZ(Grid.SceneLayer.TileFront) - Grid.
							GetLayerZ(Grid.SceneLayer.Liquid) + DECOR_Z_OFFSET;
					else
						decorZOffset = DECOR_Z_OFFSET;
					decorMaterial = def.GetDecorMaterial(decorRenderInfo);
					decorTriangles = new List<TriangleInfo>(32);
					decorChunks = new MeshChunk[width, height];
					for (int x = 0; x < width; x++)
						for (int y = 0; y < height; y++)
							decorChunks[x, y] = new MeshChunk(decorMaterial, renderLayer, z +
								decorZOffset);
				}
				atlasInfo = def.GenerateAtlas();
			}

			/// <summary>
			/// Adds a cell to render.
			/// </summary>
			/// <param name="cell">The cell containing the constructed building.</param>
			internal void AddCell(int cell) {
				if (!occupiedCells.TryGetValue(cell, out int num))
					num = 0;
				occupiedCells[cell] = num + 1;
				MarkDirty(cell);
			}

			/// <summary>
			/// Turns off the mesh renderer at a given chunk coordinate.
			/// </summary>
			/// <param name="chunkX">The chunk X coordinate.</param>
			/// <param name="chunkY">The chunk Y coordinate.</param>
			internal void Deactivate(int chunkX, int chunkY) {
				renderChunks[chunkX, chunkY].SetActive(false);
			}

			public void Dispose() {
				int width = renderChunks.GetLength(0), height = renderChunks.GetLength(1);
				for (int x = 0; x < width; x++)
					for (int y = 0; y < height; y++)
						renderChunks[x, y].Dispose();
				if (decorMaterial != null) {
					for (int x = 0; x < width; x++)
						for (int y = 0; y < height; y++)
							decorChunks[x, y].Dispose();
					decorTriangles.Clear();
					UnityEngine.Object.DestroyImmediate(decorMaterial);
				}
				UnityEngine.Object.DestroyImmediate(renderMaterial);
				occupiedCells.Clear();
			}

			/// <summary>
			/// Generates the decor vertices for the specified tile.
			/// </summary>
			/// <param name="x">The tile x coordinate.</param>
			/// <param name="y">The tile y coordinate.</param>
			/// <param name="color">The color tint to apply.</param>
			private void GenerateDecorVertices(int x, int y, Color color) {
				var dri = decorRenderInfo.decor;
				int n = dri.Length;
				Bits connected = TileRendererUtils.GetDecorConnectionBits(x, y, queryLayer);
				for (int i = 0; i < n; i++) {
					var decor = dri[i];
					var variants = decor.variants;
					Bits required = decor.requiredConnections;
					if (variants != null && variants.Length > 0 && (connected & required) ==
							required && (connected & decor.forbiddenConnections) == 0) {
						// Use a seeded random noise to determine if the tops spawn here
						float score = PerlinSimplexNoise.noise((float)(i + x + connected) *
							SIMPLEX_SCALE.x, (float)(i + y + connected) * SIMPLEX_SCALE.y);
						if (score >= decor.probabilityCutoff)
							TileRendererUtils.AddDecorVertexInfo(ref decor, x, y, score, color,
								decorTriangles);
					}
				}
			}

			/// <summary>
			/// Generates the vertices for the specified tile.
			/// </summary>
			/// <param name="x">The tile x coordinate.</param>
			/// <param name="y">The tile y coordinate.</param>
			/// <param name="color">The color tint to apply.</param>
			private void GenerateVertices(int x, int y, Color color) {
				// Difficult to optimize this further as bits can have any combination
				Bits connected = TileRendererUtils.GetConnectionBits(x, y, queryLayer);
				int n = atlasInfo.Length;
				for (int i = 0; i < n; i++) {
					var ai = atlasInfo[i];
					Bits require = ai.requiredConnections;
					// If all required bits, and no forbidden bits, render this one
					if ((require & connected) == require && (ai.forbiddenConnections &
							connected) == 0) {
						ai.AddVertexInfo(x, y, connected, color);
						break;
					}
				}
			}

			/// <summary>
			/// Marks a cell as dirty.
			/// </summary>
			/// <param name="cell">The cell to mark dirty.</param>
			/// <param name="ifOccupied">true to only mark dirty if occupied, or false to
			/// always mark it dirty.</param>
			internal void MarkDirty(int cell, bool ifOccupied = false) {
				if (Grid.IsValidCell(cell) && (!ifOccupied || occupiedCells.ContainsKey(cell)))
				{
					Grid.CellToXY(cell, out int x, out int y);
					x /= CHUNK_SIZE;
					y /= CHUNK_SIZE;
					renderChunks[x, y].dirty = true;
				}
			}

			/// <summary>
			/// Removes a cell from rendering.
			/// </summary>
			/// <param name="cell">The cell containing the deconstructed building.</param>
			internal void RemoveCell(int cell) {
				if (occupiedCells.TryGetValue(cell, out int num)) {
					if (num > 1)
						occupiedCells[cell] = num - 1;
					else
						occupiedCells.Remove(cell);
					MarkDirty(cell);
				}
			}

			/// <summary>
			/// Rebuilds the mesh at the given chunk coordinates.
			/// </summary>
			/// <param name="renderer">The renderer that is rebuilding the mesh.</param>
			/// <param name="chunkX">The chunk X coordinate.</param>
			/// <param name="chunkY">The chunk Y coordinate.</param>
			public void Rebuild(TileMeshRenderer renderer, int chunkX, int chunkY) {
				var chunk = renderChunks[chunkX, chunkY];
				bool decor = decorMaterial != null;
				if (chunk.dirty || renderer.forceRebuild) {
					int w = Grid.WidthInCells, maxY = CHUNK_SIZE * (chunkY + 1), minX =
						CHUNK_SIZE * chunkX, maxX = CHUNK_SIZE + minX;
					// Create vertex arrays
					chunk.Clear();
					for (int y = chunkY * CHUNK_SIZE; y < maxY; y++)
						for (int x = minX; x < maxX; x++) {
							int cell = y * w + x;
							if (occupiedCells.ContainsKey(cell)) {
								var color = renderer.GetCellColor(cell, element);
								GenerateVertices(x, y, color);
							}
						}
					if (MeshUtil.vertices.Count > 0) {
						chunk.CreateMesh();
						chunk.BuildMesh();
						chunk.SetActive(true);
						// Deal with the decor
						if (decor)
							RebuildDecor(renderer, chunkX, chunkY);
					} else {
						chunk.DestroyMesh();
						if (decor)
							decorChunks[chunkX, chunkY].DestroyMesh();
					}
					chunk.dirty = false;
				} else {
					// It still needs to be drawn if it exists
					chunk.SetActive(true);
					if (decor)
						decorChunks[chunkX, chunkY].SetActive(true);
				}
			}

			/// <summary>
			/// Rebuilds the decor mesh at the given coordinates.
			/// </summary>
			/// <param name="renderer">The renderer that is rebuilding the mesh.</param>
			/// <param name="chunkX">The chunk X coordinate.</param>
			/// <param name="chunkY">The chunk Y coordinate.</param>
			private void RebuildDecor(TileMeshRenderer renderer, int chunkX, int chunkY) {
				int w = Grid.WidthInCells, maxY = CHUNK_SIZE * (chunkY + 1), minX =
					CHUNK_SIZE * chunkX, maxX = CHUNK_SIZE + minX;
				var chunk = decorChunks[chunkX, chunkY];
				// Create vertex arrays
				chunk.Clear();
				decorTriangles.Clear();
				for (int y = chunkY * CHUNK_SIZE; y < maxY; y++)
					for (int x = minX; x < maxX; x++) {
						int cell = y * w + x;
						if (occupiedCells.ContainsKey(cell)) {
							var color = renderer.GetCellColor(cell, element);
							GenerateDecorVertices(x, y, color);
						}
					}
				if (MeshUtil.vertices.Count > 0) {
					var indices = MeshUtil.indices;
					int d = decorTriangles.Count;
					chunk.CreateMesh();
					// Triangles must be in sorted order
					decorTriangles.Sort(TriangleComparer.Instance);
					for (int i = 0; i < d; i++) {
						var triangle = decorTriangles[i];
						indices.Add(triangle.i0);
						indices.Add(triangle.i1);
						indices.Add(triangle.i2);
					}
					chunk.BuildMesh();
					chunk.SetActive(true);
				} else
					chunk.DestroyMesh();
			}
		}
	}
}
