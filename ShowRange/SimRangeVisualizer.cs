/*
 * Copyright 2026 Peter Han
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

using System;
using System.Collections.Generic;
using PeterHan.PLib.Core;
using UnityEngine;

namespace PeterHan.ShowRange {
	/// <summary>
	/// Visualizes element consumer ranges using the new, much faster Klei visualizer.
	/// </summary>
	internal sealed class SimRangeVisualizer : MonoBehaviour {
		private const int OCCLUSION_HEIGHT = 64;

		private const int OCCLUSION_WIDTH = 64;

		/// <summary>
		/// Calculates a raycast position from the view of the specified camera.
		/// </summary>
		/// <param name="viewingCamera">The camera performing the ray cast.</param>
		/// <param name="point">The point to raycast.</param>
		/// <returns>The point in the camera's coordinates.</returns>
		private static Vector3 CalculateRaycast(Camera viewingCamera, Vector3 point) {
			var ray = viewingCamera.ViewportPointToRay(point);
			return ray.GetPoint(Mathf.Abs(ray.origin.z / ray.direction.z));
		}

		/// <summary>
		/// Creates a new element consumer range finder.
		/// </summary>
		/// <param name="template">The parent game object.</param>
		/// <param name="visualizers">An array of all visualizers to create.</param>
		/// <param name="worstCaseRadius">The radius sufficient to fit all visualizers.</param>
		/// <param name="color">The color to use when displaying the range.</param>
		public static void Create(GameObject template, SimVisualizer[] visualizers,
				int worstCaseRadius, Color color = default) {
			if (color == default)
				color = new Color(0f, 1f, 0.8f, 1f);
			if (template == null)
				throw new ArgumentNullException(nameof(template));
			if (visualizers != null && template.TryGetComponent(out KPrefabID prefabID))
				// Only when instantiated, not on the template
				prefabID.instantiateFn += (obj) => {
					var visualizer = obj.AddComponent<SimVisualizerParams>();
					visualizer.highlightColor = color;
					visualizer.visualizers = visualizers;
					visualizer.worstCaseRadius = worstCaseRadius;
				};
		}
		
		/// <summary>
		/// Enqueues a cell to be visited if it is passable.
		/// </summary>
		/// <param name="seen">The location where reachable cells will be stored.</param>
		/// <param name="newCell">The cell to check.</param>
		/// <param name="cost">The cost of this cell.</param>
		/// <param name="queue">A temporary queue to use for storing the cells to visit.</param>
		private static void EnqueueIfPassable(ICollection<int> seen, int newCell, int cost,
				PriorityDictionary<int, int> queue) {
			if (Grid.IsValidCell(newCell) && Grid.Element[newCell]?.IsSolid == false &&
					!seen.Contains(newCell)) {
				seen.Add(newCell);
				queue.Enqueue(cost, newCell);
			}
		}

		/// <summary>
		/// Calculates the reachable cells from a specified location, based on the rule that
		/// the Sim appears to use.
		/// </summary>
		/// <param name="queue">A temporary queue to use for storing the cells to visit.</param>
		/// <param name="seen">The location where visible cells will be stored.</param>
		/// <param name="startCell">The starting search location.</param>
		/// <param name="radius">The maximum radius to scan.</param>
		internal static void FindReachableCells(PriorityDictionary<int, int> queue,
				ICollection<int> seen, int startCell, int radius) {
			// Initial cell is seen
			queue.Enqueue(0, startCell);
			seen.Add(startCell);
			// Dijkstra's algorithm
			do {
				queue.Dequeue(out int cost, out int newCell);
				if (cost < radius - 1) {
					// Cardinal directions
					EnqueueIfPassable(seen, Grid.CellLeft(newCell), cost + 1, queue);
					EnqueueIfPassable(seen, Grid.CellRight(newCell), cost + 1, queue);
					EnqueueIfPassable(seen, Grid.CellAbove(newCell), cost + 1, queue);
					EnqueueIfPassable(seen, Grid.CellBelow(newCell), cost + 1, queue);
				}
			} while (queue.Count > 0);
		}

		/// <summary>
		/// Reports the currently selected target, if it is a valid target for range
		/// visualization.
		/// </summary>
		/// <param name="target">The target transform.</param>
		/// <param name="visualizerParams">The location where the visualizer to be rendered will be stored.</param>
		/// <returns>true if the target has a visualizer, or false otherwise.</returns>
		private static bool GetSelectedTarget(out Transform target,
				out SimVisualizerParams visualizerParams) {
			var selected = SelectTool.Instance.selected;
			bool found = false;
			if (selected == null) {
				var vis = BuildTool.Instance.visualizer;
				target = vis != null ? vis.transform : null;
			} else
				target = selected.transform;
			if (target == null)
				visualizerParams = null;
			else
				found = target.TryGetComponent(out visualizerParams);
			return found;
		}
		
		/// <summary>
		/// Caches the camera used to render the visualizer.
		/// </summary>
		private Camera cachedCamera;

		/// <summary>
		/// Caches the last selected cell to avoid rebuilding every frame.
		/// </summary>
		private int lastCell;

		/// <summary>
		/// The last selected object to avoid rebuilding every frame.
		/// </summary>
		private Transform lastTransform;

		/// <summary>
		/// The sound to play when the visualization moves.
		/// </summary>
		private readonly string moveSound;

		/// <summary>
		/// The occlusion texture used to show where the range is visible.
		/// </summary>
		private Texture2D occlusionTexture;

		/// <summary>
		/// Caches the property index of shader parameters to avoid recalculating every frame.
		/// </summary>
		private readonly int propHighlightColor;
		private readonly int propOcclusionParams;
		private readonly int propOcclusionTex;
		private readonly int propRangeParams;
		private readonly int propUVOffsetScale;
		private readonly int propWorldParams;
		
		/// <summary>
		/// A cached queue used to store the cells to visit.
		/// </summary>
		private readonly PriorityDictionary<int, int> queue;

		/// <summary>
		/// Initialized to Klei's screen space shader that displays tiles in range.
		/// </summary>
		private Material shader;

		internal SimRangeVisualizer() {
			lastCell = 0;
			lastTransform = null;
			moveSound = GlobalAssets.GetSound("RangeVisualization_movement");
			propHighlightColor = Shader.PropertyToID("_HighlightColor");
			propOcclusionParams = Shader.PropertyToID("_OcclusionParams");
			propOcclusionTex = Shader.PropertyToID("_OcclusionTex");
			propRangeParams = Shader.PropertyToID("_RangeParams");
			propUVOffsetScale = Shader.PropertyToID("_UVOffsetScale");
			propWorldParams = Shader.PropertyToID("_WorldParams");
			queue = new PriorityDictionary<int, int>(64);
		}

		/// <summary>
		/// Called after the normal scene has been rendered.
		/// </summary>
		internal void OnPostRender() {
			if (GetSelectedTarget(out var target, out var visParams)) {
				var position = target.position;
				int w = Grid.WidthInCells, h = Grid.HeightInCells, cell = Grid.PosToCell(
					position), radius = Mathf.Min(visParams.worstCaseRadius, OCCLUSION_WIDTH);
				Grid.PosToXY(position, out int x, out int y);
				// If moved, or a different object selected, update the cells
				if (lastCell != cell || lastTransform != target) {
					SoundEvent.PlayOneShot(moveSound, position);
					lastCell = cell;
					lastTransform = target;
					UpdateLocation(visParams.visualizers, radius);
					// Grid size and occlusion texture are loop invariant
					shader.SetColor(propHighlightColor, visParams.highlightColor);
					shader.SetVector(propOcclusionParams, new Vector4(1.0f / OCCLUSION_WIDTH,
						1.0f / OCCLUSION_HEIGHT, 0.0f, 0.0f));
					shader.SetVector(propWorldParams, new Vector4(w, h, 1.0f / w, 1.0f / h));
					shader.SetTexture(propOcclusionTex, occlusionTexture);
				}
				var bottomLeft = CalculateRaycast(cachedCamera, Vector3.zero);
				var topRight = CalculateRaycast(cachedCamera, Vector3.one);
				float rayX = bottomLeft.x, rayY = bottomLeft.y;
				shader.SetVector(propUVOffsetScale, new Vector4(rayX, rayY, topRight.x - rayX,
					topRight.y - rayY));
				shader.SetVector(propRangeParams, new Vector4(x - radius, y - radius, x +
					radius, y + radius));
				// Potentially slow in terms of draw calls, but the Klei shader does not want
				// to work on mesh renderers
				GL.PushMatrix();
				shader.SetPass(0);
				GL.LoadOrtho();
				GL.Begin(GL.TRIANGLE_STRIP);
				GL.Color(Color.white);
				GL.Vertex3(0f, 0f, 0f);
				GL.Vertex3(0f, 1f, 0f);
				GL.Vertex3(1f, 0f, 0f);
				GL.Vertex3(1f, 1f, 0f);
				GL.End();
				GL.PopMatrix();
			} else {
				lastCell = Grid.InvalidCell;
				lastTransform = null;
			}
		}

		internal void OnDestroy() {
			if (shader != null)
				Destroy(shader);
			if (occlusionTexture != null)
				Destroy(occlusionTexture);
			cachedCamera = null;
			lastTransform = null;
		}
		
		internal void Start() {
			lastCell = Grid.InvalidCell;
			lastTransform = null;
			if (shader == null)
				shader = new Material(Shader.Find("Klei/PostFX/Range"));
			if (cachedCamera == null)
				TryGetComponent(out cachedCamera);
			if (occlusionTexture == null)
				occlusionTexture = new Texture2D(OCCLUSION_WIDTH, OCCLUSION_HEIGHT,
						TextureFormat.Alpha8, false) {
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp
				};
		}

		/// <summary>
		/// Updates the occlusion texture with the currently selected object.
		/// </summary>
		/// <param name="visualizers">The visualizers to render.</param>
		/// <param name="radius">The worst case radius to fit all visualizers.</param>
		private void UpdateLocation(SimVisualizer[] visualizers, int radius) {
			int n = visualizers.Length, origin = lastCell;
			bool rotate = lastTransform.TryGetComponent(out Rotatable rotatable);
			var seen = HashSetPool<int, SimRangeVisualizer>.Allocate();
			for (int index = 0; index < n; index++) {
				var visualizer = visualizers[index];
				var offset = visualizer.offset;
				if (rotate)
					offset = rotatable.GetRotatedCellOffset(offset);
				int startCell = Grid.OffsetCell(origin, offset);
				if (Grid.IsValidCell(startCell) && Grid.Element[startCell]?.IsSolid == false)
					FindReachableCells(queue, seen, startCell, visualizer.radius);
			}
			var pixels = occlusionTexture.GetPixelData<byte>(0);
			int dia = 1 + (radius << 1), pixel = 0, jump = Grid.WidthInCells - dia, stride =
				OCCLUSION_WIDTH - dia, cell = origin - radius * (Grid.WidthInCells + 1);
			for (int i = dia; i > 0; i--) {
				for (int j = dia; j > 0; j--)
					pixels[pixel++] = seen.Contains(cell++) ? byte.MaxValue : (byte)0;
				pixel += stride;
				cell += jump;
			}
			occlusionTexture.Apply(false, false);
			seen.Recycle();
		}
	}

	/// <summary>
	/// A single visualizer (multiple can be added) on a component.
	/// </summary>
	public readonly struct SimVisualizer : IEquatable<SimVisualizer> {
		public readonly CellOffset offset;

		public readonly int radius;

		public SimVisualizer(CellOffset offset, int radius) {
			this.offset = offset;
			this.radius = radius;
		}

		public override bool Equals(object obj) {
			return obj is SimVisualizer other && Equals(other);
		}

		public bool Equals(SimVisualizer other) {
			return other.offset.Equals(offset) && other.radius == radius;
		}

		public override int GetHashCode() {
			return (offset.GetHashCode() << 8) + radius;
		}

		public override string ToString() {
			return string.Format("SimVisualizer[offset=({0:D},{1:D}),radius={2:D}",
				offset.x, offset.y, radius);
		}
	}

	/// <summary>
	/// Visualizes element consumers to the Sim.
	/// </summary>
	[SkipSaveFileSerialization]
	public sealed class SimVisualizerParams : MonoBehaviour {
		public Color highlightColor;

		public SimVisualizer[] visualizers;
		
		public int worstCaseRadius;
	}
}
