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

using PeterHan.PLib.Core;
using Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Bits = Rendering.BlockTileRenderer.Bits;

namespace PeterHan.FastTrack.VisualPatches {
	/// <summary>
	/// Caches information about the atlas used to render the tiles.
	/// </summary>
	internal struct AtlasInfo {
		public Bits forbiddenConnections;

		public string name;

		public Bits requiredConnections;

		public Vector4 uvBox;
	}

	/// <summary>
	/// Contains the mesh renderer used to render a particular chunk of tileables.
	/// </summary>
	internal sealed class MeshChunk : IDisposable {
		/// <summary>
		/// Whether this chunk is dirty.
		/// </summary>
		internal bool dirty;

		/// <summary>
		/// The renderer that draws the mesh.
		/// </summary>
		private GameObject gameObject;

		/// <summary>
		/// The material to use for rendering.
		/// </summary>
		private readonly Material material;

		/// <summary>
		/// The mesh to render.
		/// </summary>
		internal Mesh mesh;

		/// <summary>
		/// The renderer that draws the mesh.
		/// </summary>
		private MeshFilter renderer;

		/// <summary>
		/// The layer to use for rendering.
		/// </summary>
		private readonly int renderLayer;

		/// <summary>
		/// Whether this mesh was active last frame.
		/// </summary>
		private bool wasActive;

		/// <summary>
		/// The z-coordinate to use when rendering.
		/// </summary>
		private readonly float z;

		public MeshChunk(Material material, int renderLayer, float z) {
			// MeshUtil was useful before for shared arrays, but Unity's mesh renderers
			// extract the array, so it needs to be separate
			dirty = true;
			gameObject = null;
			this.material = material ?? throw new ArgumentNullException(nameof(material));
			mesh = null;
			renderer = null;
			this.renderLayer = renderLayer;
			wasActive = false;
			this.z = z;
		}

		/// <summary>
		/// Builds the mesh from the vertex arrays.
		/// </summary>
		public void BuildMesh() {
			var vertices = MeshUtil.vertices;
			int n = vertices.Count;
			mesh.Clear();
			mesh.SetVertices(vertices, 0, n, MeshUpdateFlags.DontValidateIndices);
			mesh.SetUVs(0, MeshUtil.uvs, 0, n, MeshUpdateFlags.DontValidateIndices);
			mesh.SetColors(MeshUtil.colours, 0, n, MeshUpdateFlags.DontValidateIndices);
			mesh.SetTriangles(MeshUtil.indices, 0, true);
		}

		/// <summary>
		/// Clears all of the vertex arrays.
		/// </summary>
		public void Clear() {
			MeshUtil.vertices.Clear();
			MeshUtil.uvs.Clear();
			MeshUtil.indices.Clear();
			MeshUtil.colours.Clear();
		}

		/// <summary>
		/// Creates the mesh renderer if it has not already been created.
		/// </summary>
		public void CreateMesh() {
			if (mesh == null) {
				mesh = new Mesh { name = nameof(MeshChunk) };
				if (renderer == null) {
					// Avoid creating all the renderers right away
					var go = mesh.CreateMeshRenderer(nameof(TileMeshRenderer),
						renderLayer, material);
					go.transform.position = new Vector3(0.0f, 0.0f, z);
					go.TryGetComponent(out renderer);
					gameObject = go;
				}
				renderer.mesh = mesh;
				gameObject.SetActive(true);
				wasActive = true;
			}
		}

		/// <summary>
		/// Destroys the mesh renderer if it exists.
		/// </summary>
		public void DestroyMesh() {
			if (mesh != null) {
				// Only if the renderer was ever created
				if (renderer != null) {
					if (wasActive)
						gameObject.SetActive(false);
					renderer.mesh = null;
				}
				UnityEngine.Object.DestroyImmediate(mesh);
				mesh = null;
				wasActive = false;
			}
			Clear();
		}

		public void Dispose() {
			DestroyMesh();
			UnityEngine.Object.Destroy(renderer);
			renderer = null;
		}

		/// <summary>
		/// Enables or disables the mesh.
		/// </summary>
		/// <param name="active">true to set the mesh active, or false to set it inactive.</param>
		public void SetActive(bool active) {
			if (mesh != null && active != wasActive) {
				gameObject.SetActive(active);
				wasActive = active;
			}
		}
	}

	/// <summary>
	/// Utility methods and constants used in TileMeshRenderer.
	/// </summary>
	internal static class TileRendererUtils {
		/// <summary>
		/// The shader keyword to make tiles shiny.
		/// </summary>
		private const string SHINE = "ENABLE_SHINE";

		/// <summary>
		/// A small trimming performed on the uv coordinates of tiles.
		/// </summary>
		private static readonly Vector2 TRIM_UV_SIZE = new Vector2(0.03125f, 0.03125f);

		/// <summary>
		/// How much to trim off of UV coordinates on unconnected sides. Some "overlap" is
		/// rendered on connected tiles in the cardinal directions to make the pattern repeat
		/// correctly.
		/// </summary>
		private const float WORLD_TRIM_SIZE = 0.25f;

		/// <summary>
		/// Generates the vertices for one decor triangle in the mesh.
		/// </summary>
		/// <param name="decor">The decor information to use for rendering.</param>
		/// <param name="x">The bottom left X coordinate.</param>
		/// <param name="y">The bottom left Y coordinate.</param>
		/// <param name="score">The index in the variants list to use for rendering.</param>
		/// <param name="color">The tint color to use when rendering.</param>
		/// <param name="triangles">The current mesh triangle buffer.</param>
		public static void AddDecorVertexInfo(ref BlockTileDecorInfo.Decor decor, int x,
				int y, float score, Color color, ICollection<TriangleInfo> triangles) {
			var variants = decor.variants;
			var vertices = MeshUtil.vertices;
			var colors = MeshUtil.colours;
			var uvs = MeshUtil.uvs;
			int index = (int)((variants.Length - 1) * score);
			var variant = variants[index];
			Vector3 offset = new Vector3(x, y, 0.0f) + variant.offset;
			Vector2[] atlasUVs = variant.atlasItem.uvs;
			Vector3[] atlasVertices = variant.atlasItem.vertices;
			int[] atlasIndices = variant.atlasItem.indices;
			// Copy vertex coordinates with offset
			int n = atlasVertices.Length, order = decor.sortOrder, count = vertices.Count;
			if (n != atlasUVs.Length)
				PUtil.LogError("Atlas UVs and vertices do not match!");
			for (int i = 0; i < n; i++) {
				vertices.Add(atlasVertices[i] + offset);
				colors.Add(color);
				uvs.Add(atlasUVs[i]);
			}
			// Generate triangle indexes
			n = atlasIndices.Length;
			for (int i = 0; i < n; i += 3)
				triangles.Add(new TriangleInfo {
					sortOrder = order,
					i0 = atlasIndices[i] + count,
					i1 = atlasIndices[i + 1] + count,
					i2 = atlasIndices[i + 2] + count
				});
		}

		/// <summary>
		/// Generates the vertices for one tile in the mesh.
		/// </summary>
		/// <param name="atlasInfo">The texture atlas to use for rendering.</param>
		/// <param name="x">The bottom left X coordinate.</param>
		/// <param name="y">The bottom left Y coordinate.</param>
		/// <param name="connected">The sides which are connected.</param>
		/// <param name="color">The tint color to use when rendering.</param>
		public static void AddVertexInfo(this AtlasInfo atlasInfo, int x, int y,
				Bits connected, Color color) {
			float size = Grid.CellSizeInMeters;
			var botLeft = new Vector2(x, y);
			var topRight = new Vector2(x + size, y + size);
			var uvWX = new Vector2(atlasInfo.uvBox.x, atlasInfo.uvBox.w);
			var uvYZ = new Vector2(atlasInfo.uvBox.z, atlasInfo.uvBox.y);
			var vertices = MeshUtil.vertices;
			var uvs = MeshUtil.uvs;
			var indices = MeshUtil.indices;
			var colors = MeshUtil.colours;
			if ((connected & Bits.Left) == 0)
				botLeft.x -= WORLD_TRIM_SIZE;
			else
				uvWX.x += TRIM_UV_SIZE.x;
			if ((connected & Bits.Right) == 0)
				topRight.x += WORLD_TRIM_SIZE;
			else
				uvYZ.x -= TRIM_UV_SIZE.x;
			if ((connected & Bits.Up) == 0)
				topRight.y += WORLD_TRIM_SIZE;
			else
				uvYZ.y -= TRIM_UV_SIZE.y;
			if ((connected & Bits.Down) == 0)
				botLeft.y -= WORLD_TRIM_SIZE;
			else
				uvWX.y += TRIM_UV_SIZE.y;
			int v0 = vertices.Count;
			vertices.Add(botLeft);
			vertices.Add(new Vector2(topRight.x, botLeft.y));
			vertices.Add(topRight);
			vertices.Add(new Vector2(botLeft.x, topRight.y));
			uvs.Add(uvWX);
			uvs.Add(new Vector2(uvYZ.x, uvWX.y));
			uvs.Add(uvYZ);
			uvs.Add(new Vector2(uvWX.x, uvYZ.y));
			indices.Add(v0);
			indices.Add(v0 + 1);
			indices.Add(v0 + 2);
			indices.Add(v0);
			indices.Add(v0 + 2);
			indices.Add(v0 + 3);
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
		}

		/// <summary>
		/// Generates the required atlas information for a tile.
		/// </summary>
		/// <param name="def">The building definition containing the atlas.</param>
		/// <returns>The atlas information used for rendering.</returns>
		public static AtlasInfo[] GenerateAtlas(this BuildingDef def) {
			var tileAtlas = def.BlockTileAtlas.items;
			int forbidIndex = tileAtlas[0].name.Length - 12, startIndex = forbidIndex - 9,
				n = tileAtlas.Length;
			var atlasInfo = new AtlasInfo[n];
			for (int k = 0; k < n; k++) {
				var atlasEntry = tileAtlas[k];
				string name = atlasEntry.name;
				string requiredStr = name.Substring(startIndex, 8);
				string forbiddenStr = name.Substring(forbidIndex, 8);
				atlasInfo[k].requiredConnections = (Bits)Convert.ToInt32(requiredStr, 2);
				atlasInfo[k].forbiddenConnections = (Bits)Convert.ToInt32(forbiddenStr, 2);
				atlasInfo[k].uvBox = atlasEntry.uvBox;
				atlasInfo[k].name = name;
			}
			return atlasInfo;
		}

		/// <summary>
		/// Checks the connectivity of a cell.
		/// </summary>
		/// <param name="x">The cell's x coordinate.</param>
		/// <param name="y">The cell's y coordinate.</param>
		/// <param name="queryLayer">The object layer to check.</param>
		/// <returns>A bit mask of the neighboring cells in all 8 directions that have the same
		/// building def as this cell on the same layer.</returns>
		public static Bits GetConnectionBits(int x, int y, int queryLayer) {
			Bits bits = 0;
			int w = Grid.WidthInCells, cell = y * w + x;
			var go = Grid.Objects[cell, queryLayer];
			BuildingDef def = null;
			if (go != null && go.TryGetComponent(out Building building))
				def = building.Def;
			// Below
			if (y > 0) {
				int cellsBelow = cell - w;
				if (x > 0 && MatchesDef(cellsBelow - 1, queryLayer, def))
					bits |= Bits.DownLeft;
				if (MatchesDef(cellsBelow, queryLayer, def))
					bits |= Bits.Down;
				if (x < w - 1 && MatchesDef(cellsBelow + 1, queryLayer, def))
					bits |= Bits.DownRight;
			}
			if (x > 0 && MatchesDef(cell - 1, queryLayer, def))
				bits |= Bits.Left;
			if (x < w - 1 && MatchesDef(cell + 1, queryLayer, def))
				bits |= Bits.Right;
			// Above
			if (y < Grid.HeightInCells - 1) {
				int cellsAbove = cell + w;
				if (x > 0 && MatchesDef(cellsAbove - 1, queryLayer, def))
					bits |= Bits.UpLeft;
				if (MatchesDef(cellsAbove, queryLayer, def))
					bits |= Bits.Up;
				if (x < w - 1 && MatchesDef(cellsAbove + 1, queryLayer, def))
					bits |= Bits.UpRight;
			}
			return bits;
		}

		/// <summary>
		/// Checks the tile decor connectivity of a cell.
		/// </summary>
		/// <param name="x">The cell's x coordinate.</param>
		/// <param name="y">The cell's y coordinate.</param>
		/// <param name="queryLayer">The object layer to check.</param>
		/// <returns>A bit mask of the neighboring cells in all 8 directions that have a
		/// building on the requested layer.</returns>
		public static Bits GetDecorConnectionBits(int x, int y, int queryLayer) {
			Bits bits = 0;
			int w = Grid.WidthInCells, cell = y * w + x;
			var go = Grid.Objects[cell, queryLayer];
			// Below
			if (y > 0) {
				int cellsBelow = cell - w;
				if (x > 0 && Grid.Objects[cellsBelow - 1, queryLayer] != null)
					bits |= Bits.DownLeft;
				if (Grid.Objects[cellsBelow, queryLayer] != null)
					bits |= Bits.Down;
				if (x < w - 1 && Grid.Objects[cellsBelow + 1, queryLayer] != null)
					bits |= Bits.DownRight;
			}
			if (x > 0 && IsDecorConnectable(go, Grid.Objects[cell - 1, queryLayer]))
				bits |= Bits.Left;
			if (x < w - 1 && IsDecorConnectable(go, Grid.Objects[cell + 1, queryLayer]))
				bits |= Bits.Right;
			// Above
			if (y < Grid.HeightInCells - 1) {
				int cellsAbove = cell + w;
				if (x > 0 && Grid.Objects[cellsAbove - 1, queryLayer] != null)
					bits |= Bits.UpLeft;
				if (Grid.Objects[cellsAbove, queryLayer] != null)
					bits |= Bits.Up;
				if (x < w - 1 && Grid.Objects[cellsAbove + 1, queryLayer] != null)
					bits |= Bits.UpRight;
			}
			return bits;
		}

		/// <summary>
		/// Gets a base material used for all tile rendering.
		/// </summary>
		/// <param name="def">The building def to render.</param>
		/// <returns>A common parent material for decor and tile alike.</returns>
		private static Material GetBaseMaterial(BuildingDef def) {
			var material = new Material(def.BlockTileMaterial);
			if (def.BlockTileIsTransparent)
				material.renderQueue = RenderQueues.Liquid;
			else if (def.SceneLayer == Grid.SceneLayer.TileMain)
				material.renderQueue = RenderQueues.BlockTiles;
			return material;
		}

		/// <summary>
		/// Gets the material to use for rendering decor ornaments on tiled buildings.
		/// </summary>
		/// <param name="def">The building def to render.</param>
		/// <param name="decorInfo">The decor atlases to use for the ornaments.</param>
		/// <returns>The material to use for the mesh renderers.</returns>
		public static Material GetDecorMaterial(this BuildingDef def,
				BlockTileDecorInfo decorInfo) {
			var material = GetBaseMaterial(def);
			material.SetTexture("_MainTex", decorInfo.atlas.texture);
			var atlasSpec = decorInfo.atlasSpec;
			if (atlasSpec != null) {
				material.SetTexture("_SpecularTex", atlasSpec.texture);
				material.EnableKeyword(SHINE);
			} else
				material.DisableKeyword(SHINE);
			return material;
		}

		/// <summary>
		/// Gets the material to use for rendering tiled buildings.
		/// </summary>
		/// <param name="def">The building def to render.</param>
		/// <param name="element">The element which was used to build it.</param>
		/// <returns>The material to use for the mesh renderers.</returns>
		public static Material GetMaterial(this BuildingDef def, SimHashes element) {
			var material = GetBaseMaterial(def);
			if (element != SimHashes.Void) {
				material.SetTexture("_MainTex", def.BlockTileAtlas.texture);
				material.name = def.BlockTileAtlas.name + "Mat";
				if (def.BlockTileShineAtlas != null) {
					material.SetTexture("_SpecularTex", def.BlockTileShineAtlas.texture);
					material.EnableKeyword(SHINE);
				} else
					material.DisableKeyword(SHINE);
			} else {
				material.SetTexture("_MainTex", def.BlockTilePlaceAtlas.texture);
				material.name = def.BlockTilePlaceAtlas.name + "Mat";
				material.DisableKeyword(SHINE);
			}
			return material;
		}

		/// <summary>
		/// Checks to see if two objects can have their decor connected. This check only works
		/// horizontally (for things like the tile tops).
		/// </summary>
		/// <param name="src">The source tile object.</param>
		/// <param name="target">The destination tile object.</param>
		/// <returns></returns>
		private static bool IsDecorConnectable(GameObject src, GameObject target) {
			return src != null && target != null && src.TryGetComponent(
				out IBlockTileInfo srcInfo) && target.TryGetComponent(
				out IBlockTileInfo dstInfo) && srcInfo.GetBlockTileConnectorID() ==
				dstInfo.GetBlockTileConnectorID();
		}

		/// <summary>
		/// Checks to see if two cells have matching building definitions.
		/// </summary>
		/// <param name="cell">The cell to check.</param>
		/// <param name="layer">The layer to look up.</param>
		/// <param name="def">The def it needs to have.</param>
		/// <returns>true if the cell has a building and uses that definition, or false otherwise.</returns>
		private static bool MatchesDef(int cell, int layer, BuildingDef def) {
			var obj = Grid.Objects[cell, layer];
			return obj != null && obj.TryGetComponent(out Building b) && b.Def == def;
		}
	}

	/// <summary>
	/// Stores the render information for built, under-construction, and replacement
	/// tileable buildings.
	/// </summary>
	internal sealed class TileRenderInfo : IDisposable {
		private const int N_LAYERS = 3;

		/// <summary>
		/// The state of constructed, replacement, and planned buildings with this def.
		/// </summary>
		internal TileMeshRenderer.LayerRenderInfo[] infos;

		public TileRenderInfo() {
			infos = new TileMeshRenderer.LayerRenderInfo[N_LAYERS];
		}

		/// <summary>
		/// Deactivates a chunk coordinate in all layers.
		/// </summary>
		/// <param name="x">The chunk x coordinate to rebuild.</param>
		/// <param name="y">The chunk y coordinate to rebuild.</param>
		public void Deactivate(int x, int y) {
			for (int i = 0; i < N_LAYERS; i++)
				infos[i]?.Deactivate(x, y);
		}

		public void Dispose() {
			for (int i = 0; i < N_LAYERS; i++) {
				var info = infos[i];
				if (info != null) {
					info.Dispose();
					infos[i] = null;
				}
			}
		}

		/// <summary>
		/// Marks a cell dirty in all layers.
		/// </summary>
		/// <param name="cell">The cell to mark dirty.</param>
		/// <param name="ifOccupied">true to only mark dirty if it is occupied, or false to
		/// always mark it dirty.</param>
		public void MarkDirty(int cell, bool ifOccupied) {
			for (int i = 0; i < N_LAYERS; i++)
				infos[i]?.MarkDirty(cell, ifOccupied);
		}

		/// <summary>
		/// Rebuilds a chunk coordinate for all layers.
		/// </summary>
		/// <param name="renderer">The renderer doing the rebuilding.</param>
		/// <param name="x">The chunk x coordinate to rebuild.</param>
		/// <param name="y">The chunk y coordinate to rebuild.</param>
		public void Rebuild(TileMeshRenderer renderer, int x, int y) {
			for (int i = 0; i < N_LAYERS; i++)
				infos[i]?.Rebuild(renderer, x, y);
		}
	}

	/// <summary>
	/// Stores a triangle used to render tile decor.
	/// </summary>
	internal struct TriangleInfo {
		public int sortOrder;

		public int i0;

		public int i1;

		public int i2;
	}

	/// <summary>
	/// A singleton class used to compare triangles.
	/// </summary>
	internal sealed class TriangleComparer : IComparer<TriangleInfo> {
		/// <summary>
		/// Compares two TriangleInfo instances by their sort order.
		/// </summary>
		internal static readonly IComparer<TriangleInfo> Instance = new TriangleComparer();

		public int Compare(TriangleInfo x, TriangleInfo y) {
			return x.sortOrder.CompareTo(y.sortOrder);
		}

		private TriangleComparer() { }
	}
}
