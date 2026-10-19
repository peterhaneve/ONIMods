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
using UnityEngine;
using UnityEngine.EventSystems;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Allows a dialog to be moved by dragging its title bar with the left mouse button. The
	/// dialog cannot be dragged outside of its parent.
	/// </summary>
	internal sealed class PDialogDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler,
			IEndDragHandler {
		/// <summary>
		/// The dialog to move.
		/// </summary>
		[field: SerializeField]
		internal RectTransform Target { get; set; }

		/// <summary>
		/// Whether a drag with the left mouse button is in progress.
		/// </summary>
		private bool dragging;

		/// <summary>
		/// The offset from the pointer to the dialog position when the drag started.
		/// </summary>
		private Vector2 offset;

		internal PDialogDragHandle() {
			dragging = false;
			offset = Vector2.zero;
			Target = null;
		}

		/// <summary>
		/// Limits the dialog movement so that it stays inside its parent. If the dialog is
		/// larger than its parent on an axis, movement on that axis is not limited.
		/// </summary>
		/// <param name="delta">The requested movement in the parent's coordinates.</param>
		/// <returns>The movement, adjusted to keep the dialog inside its parent.</returns>
		private Vector2 ClampDelta(Vector2 delta) {
			if (Target.parent is RectTransform parent) {
				var corners = new Vector3[4];
				Target.GetWorldCorners(corners);
				// Lower left and upper right corners in the parent's coordinates
				Vector2 min = parent.InverseTransformPoint(corners[0]),
					max = parent.InverseTransformPoint(corners[2]);
				var bounds = parent.rect;
				if (max.x - min.x <= bounds.width)
					delta.x = Mathf.Clamp(delta.x, bounds.xMin - min.x, bounds.xMax - max.x);
				if (max.y - min.y <= bounds.height)
					delta.y = Mathf.Clamp(delta.y, bounds.yMin - min.y, bounds.yMax - max.y);
			}
			return delta;
		}

		public void OnBeginDrag(PointerEventData data) {
			dragging = false;
			if (data.button == PointerEventData.InputButton.Left && ToParentPoint(data,
					out var point)) {
				dragging = true;
				offset = Target.anchoredPosition - point;
			}
		}

		public void OnDrag(PointerEventData data) {
			if (dragging && ToParentPoint(data, out var point))
				Target.anchoredPosition += ClampDelta(point + offset - Target.anchoredPosition);
		}

		public void OnEndDrag(PointerEventData data) {
			dragging = false;
		}

		/// <summary>
		/// Converts a pointer position to the coordinates of the dialog's parent.
		/// </summary>
		/// <param name="data">The pointer event.</param>
		/// <param name="point">The location in the parent's coordinates.</param>
		/// <returns>true if the position could be converted, or false otherwise.</returns>
		private bool ToParentPoint(PointerEventData data, out Vector2 point) {
			point = Vector2.zero;
			return Target != null && Target.parent is RectTransform parent &&
				RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position,
				data.pressEventCamera, out point);
		}
	}
}
