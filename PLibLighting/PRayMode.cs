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

using PeterHan.PLib.Detours;
using System;
using UnityEngine;

namespace PeterHan.PLib.Lighting {
	/// <summary>
	/// Implements the most common cases for light ray drawing.
	/// </summary>
	public class PRayMode : IRayMode {
		private static readonly IDetouredField<LightBuffer, Camera> LIGHTBUFFER_CAMERA = PDetours.
			TryDetourField<LightBuffer, Camera>("Camera");

		private static readonly IDetouredField<LightBuffer, Mesh> LIGHTBUFFER_MESH = PDetours.
			TryDetourField<LightBuffer, Mesh>("Mesh");

		/// <summary>
		/// Filters by the light shape being used. If &lt; 0, the filter is disabled.
		/// </summary>
		protected readonly LightShape filter;

		/// <summary>
		/// The layer to use for rendering.
		/// </summary>
		protected int layer;

		/// <summary>
		/// The material used to render the rays. It uses a vanilla shader with few parameters.
		/// </summary>
		protected Material material;

		/// <summary>
		/// The texture backing the material.
		/// </summary>
		protected readonly Texture2D texture;

		/// <summary>
		/// Creates a ray mode. Ray modes should be created and registered in OnLoad after the
		/// relevant texture is loaded.
		/// </summary>
		/// <param name="texture">The texture to be used for drawing.</param>
		/// <param name="filter">The light shape to filter drawing; to disable the filter, use
		/// (LightShape)(-1). Complex filtering can be done by overriding FilterDrawing.</param>
		public PRayMode(Texture2D texture, LightShape filter = (LightShape)(-1)) {
			if (texture == null)
				throw new ArgumentNullException(nameof(texture));
			this.filter = filter;
			layer = LayerMask.NameToLayer("Lights");
			material = null;
			this.texture = texture;
		}

		public void DrawCustomRay(Light2D light, LightBuffer lightBuffer) {
			if (FilterDrawing(light)) {
				Vector2 direction = light.Direction.normalized;
				Vector3 position = light.transform.position + (Vector3)light.Offset;
				Camera camera;
				float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
				var matrix = Matrix4x4.Translate(position) * Matrix4x4.Rotate(Quaternion.
					AngleAxis(angle, Vector3.forward));
				// Fallback to slow method if the field gets removed
				if (LIGHTBUFFER_CAMERA == null)
					lightBuffer.TryGetComponent(out camera);
				else
					camera = LIGHTBUFFER_CAMERA.Get(lightBuffer);
				var mesh = LIGHTBUFFER_MESH?.Get(lightBuffer);
				// Find transform matrix and render it
				if (mesh != null && camera != null) {
					GetTransformMatrix(ref matrix);
					Graphics.DrawMesh(mesh, matrix, material, layer, camera, 0, light.
						materialPropertyBlock);
				}
			}
		}

		/// <summary>
		/// Filters whether this ray will be drawn.
		/// </summary>
		/// <param name="light">The light that will be drawn.</param>
		/// <returns>true to continue drawing the rays, or false otherwise.</returns>
		protected virtual bool FilterDrawing(Light2D light) {
			return light != null && (filter < LightShape.Circle || light.shape == filter);
		}

		/// <summary>
		/// Calculate the desired transformation matrix for rendering rays.
		/// </summary>
		/// <param name="matrix">The default transform matrix, which should be updated
		/// with the desired new matrix if changes must be made.</param>
		protected virtual void GetTransformMatrix(ref Matrix4x4 matrix) { }

		public virtual void Prepare(LightBuffer lightBuffer) {
			if (material == null) {
				var instance = CameraController.Instance;
				if (instance != null)
					material = new Material(instance.LightCircleOverlay) {
						mainTexture = texture
					};
			}
			material?.SetTexture("_PropertyWorldLight", lightBuffer.WorldLight);
		}
	}
}
