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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.FastTrack.UIPatches {
	/// <summary>
	/// Hides components that are off screen when scrolling, instead replacing them with a
	/// virtual component above and below them that is resized to occupy the same space.
	/// </summary>
	public sealed class VirtualScroll : MonoBehaviour {
		/// <summary>
		/// The items currently being scrolled.
		/// </summary>
		private readonly IList<VirtualItem> components;

		/// <summary>
		/// The game objects to always show.
		/// </summary>
		private readonly ISet<GameObject> forceShow;

		/// <summary>
		/// Whether to freeze layouts on rebuild.
		/// </summary>
		public bool freezeLayout;

		/// <summary>
		/// The parent of items to be shown. It is assumed that all items are the same size!
		/// </summary>
		private RectTransform itemList;

		/// <summary>
		/// The last coordinate displayed when scrolling.
		/// </summary>
		private Vector2 lastPosition;

		/// <summary>
		/// The margin to render components in pixels. Should be more than one component size.
		/// </summary>
		private Vector2 margin;

		/// <summary>
		/// The scroll pane which will be virtually scrolled.
		/// </summary>
		private KScrollRect scroll;

		/// <summary>
		/// The rect transform of the component containing the scroll pane.
		/// </summary>
		private RectTransform parentRect;

		/// <summary>
		/// Whether a rebuild is already pending.
		/// </summary>
		private volatile bool pending;

		/// <summary>
		/// The component actually responsible for laying out the children.
		/// </summary>
		private LayoutGroup realLayout;

		/// <summary>
		/// The virtual layout component that keeps the scrollable rect the same size.
		/// </summary>
		private LayoutElement virtualLayout;

		internal VirtualScroll() {
			components = new List<VirtualItem>(64);
			forceShow = new HashSet<GameObject>();
			freezeLayout = false;
			lastPosition = Vector2.up;
			margin = new Vector2(10.0f, 10.0f);
			pending = false;
			realLayout = null;
			virtualLayout = null;
		}

		/// <summary>
		/// Called when the component is first added.
		/// </summary>
		internal void Awake() {
			scroll = GetComponentInParent<KScrollRect>();
			parentRect = scroll.rectTransform();
		}

		/// <summary>
		/// Restores a game object to be hidden if off-screen.
		/// </summary>
		/// <param name="allowHide">The game object to possibly hide.</param>
		internal void ClearForceShow(GameObject allowHide) {
			if (forceShow.Remove(allowHide))
				UpdateScroll();
		}

		/// <summary>
		/// Rebuilds the scroll pane in a coroutine after layout.
		/// </summary>
		private System.Collections.IEnumerator DoRebuild() {
			pending = true;
			yield return null;
			if (!App.IsExiting && scroll != null && itemList != null) {
				int n = itemList.childCount;
				float marginX = 0.0f, marginY = 0.0f;
				bool ice = freezeLayout;
				GameObject go;
				components.Clear();
				for (int i = 0; i < n; i++) {
					var transform = itemList.GetChild(i);
					if (transform != null && (go = transform.gameObject).activeSelf) {
						var vi = new VirtualItem(transform, go, itemList, ice);
						float w = vi.size.x, h = vi.size.y;
						components.Add(vi);
						if (w > marginX)
							marginX = w;
						if (h > marginY)
							marginY = h;
					}
				}
				if (realLayout != null) {
					// Copy the parameters to the virtual layout
					virtualLayout.minHeight = realLayout.minHeight;
					virtualLayout.minWidth = realLayout.minWidth;
					virtualLayout.preferredHeight = realLayout.preferredHeight;
					virtualLayout.preferredWidth = realLayout.preferredWidth;
					virtualLayout.flexibleHeight = realLayout.flexibleHeight;
					virtualLayout.flexibleWidth = realLayout.flexibleWidth;
					virtualLayout.enabled = true;
					realLayout.enabled = false;
				}
				margin = new Vector2(marginX * 1.5f, marginY * 1.5f);
				// Calculate the margin
				UpdateScroll();
			}
			pending = false;
		}

		/// <summary>
		/// Gets the viewable rectangle.
		/// </summary>
		/// <param name="xMin">The minimum visible x coordinate.</param>
		/// <param name="xMax">The maximum visible x coordinate.</param>
		/// <param name="yMin">The minimum visible y coordinate.</param>
		/// <param name="yMax">The maximum visible y coordinate.</param>
		private void GetViewableRect(out float xMin, out float xMax, out float yMin,
				out float yMax) {
			// Get the absolute coordinates of the current viewable rect
			var center = parentRect.position;
			var rect = parentRect.rect;
			float wxMin = rect.x + center.x, wyMin = rect.y + center.y;
			// Transform into itemList space
			Vector3 bl = itemList.InverseTransformPoint(wxMin, wyMin, 0.0f), tr = itemList.
				InverseTransformPoint(wxMin + rect.width, wyMin + rect.height, 0.0f);
			xMin = bl.x - margin.x;
			yMin = bl.y - margin.y;
			xMax = tr.x + margin.x;
			yMax = tr.y + margin.y;
		}

		/// <summary>
		/// Initializes the virtual scroll pane.
		/// </summary>
		/// <param name="target">The target object containing the items, or null to use the
		/// object where this component is applied.</param>
		/// <param name="margin">The display margin in pixels.</param>
		internal void Initialize(RectTransform target = null) {
			if (target == null && !TryGetComponent(out target))
				target = null;
			if (target != null && target != itemList && scroll != null) {
				scroll.onValueChanged.AddListener(OnScroll);
				itemList = target;
				target.TryGetComponent(out realLayout);
				virtualLayout = target.gameObject.AddOrGet<LayoutElement>();
				virtualLayout.enabled = false;
				virtualLayout.layoutPriority = 100;
				Rebuild();
			}
		}

		/// <summary>
		/// Switches off the virtual layout, and turns the real one back on, for when items are
		/// about to be added or removed.
		/// </summary>
		internal void OnBuild() {
			if (realLayout != null && !pending) {
				virtualLayout.enabled = false;
				realLayout.enabled = true;
			}
		}

		internal void OnDestroy() {
			forceShow.Clear();
			if (scroll != null) {
				scroll.onValueChanged.RemoveListener(OnScroll);
				itemList = null;
				realLayout = null;
				scroll = null;
				virtualLayout = null;
			}
		}

		/// <summary>
		/// Called when the scroll rect is scrolled.
		/// It appears that (0, 0) is when at bottom and (0, 1) is at top.
		/// </summary>
		/// <param name="topLeft">The upper left corner location as a fraction.</param>
		private void OnScroll(Vector2 topLeft) {
			float x = topLeft.x, y = topLeft.y;
			if (scroll != null && !Mathf.Approximately(x, lastPosition.x) || !Mathf.
					Approximately(y, lastPosition.y)) {
				lastPosition.x = x;
				lastPosition.y = y;
				UpdateScroll();
			}
		}

		/// <summary>
		/// Rebuilds the list of items next frame.
		/// </summary>
		internal void Rebuild() {
			if (!pending && gameObject.activeSelf)
				StartCoroutine(DoRebuild());
		}

		/// <summary>
		/// Forces a game object to always be active regardless of whether it is on-screen.
		/// </summary>
		/// <param name="disableHide">The game object to show.</param>
		internal void SetForceShow(GameObject disableHide) {
			if (forceShow.Add(disableHide))
				UpdateScroll();
		}

		/// <summary>
		/// Updates the visibility of all items based on the ones that should be visible.
		/// </summary>
		private void UpdateScroll() {
			int n = components.Count;
			if (n > 0) {
				MinMax above = new MinMax(0.0f), below = new MinMax(0.0f), left =
					new MinMax(0.0f), right = new MinMax(0.0f);
				GetViewableRect(out float xl, out float xr, out float yb, out float yt);
				for (int i = 0; i < n; i++) {
					var item = components[i];
					float yMin = item.min.y, xMin = item.min.x, yMax = item.max.y, xMax =
						item.max.x;
					if (forceShow.Contains(item.entry))
						// Always visible
						item.SetVisible(true);
					else if (yMin > yt) {
						// Component above
						above.Add(yMin, yMax);
						item.SetVisible(false);
					} else if (yMax < yb) {
						// Component below
						below.Add(yMin, yMax);
						item.SetVisible(false);
					} else if (xMin > xr) {
						// Component to the right
						right.Add(xMin, xMax);
						item.SetVisible(false);
					} else if (xMax < xl) {
						// Component to the left
						left.Add(xMin, xMax);
						item.SetVisible(false);
					} else
						item.SetVisible(true);
				}
			}
		}

		/// <summary>
		/// Stores information about the virtually scrolled items.
		/// </summary>
		private sealed class VirtualItem {
			/// <summary>
			/// The object to set visible or invisible.
			/// </summary>
			internal readonly GameObject entry;

			/// <summary>
			/// The max coordinates of this item.
			/// </summary>
			internal readonly Vector2 max;

			/// <summary>
			/// The min coordinates of this item.
			/// </summary>
			internal readonly Vector2 min;

			/// <summary>
			/// The size of this item.
			/// </summary>
			internal readonly Vector2 size;

			/// <summary>
			/// Whether the target is visible.
			/// </summary>
			private bool visible;

			public VirtualItem(Transform transform, GameObject go, Transform parent,
					bool freezeLayout) {
				var absOffset = RectTransformUtility.CalculateRelativeRectTransformBounds(
					parent, transform);
				entry = go;
				visible = true;
				min = new Vector2(absOffset.min.x, absOffset.min.y);
				size = new Vector2(absOffset.size.x, absOffset.size.y);
				max = min + size;
				// Destroy and replace layout groups with a fixed element, this helps a lot
				if (transform.TryGetComponent(out LayoutGroup group) && freezeLayout) {
					entry.AddOrGet<LayoutElement>().CopyFrom(group);
					Destroy(group);
				}
			}

			/// <summary>
			/// Shows or hides the target object.
			/// </summary>
			/// <param name="visible">true to make it visible, or false to make it invisible.</param>
			public bool SetVisible(bool visible) {
				bool changed = visible != this.visible;
				if (entry != null && changed) {
					entry.SetActive(visible);
					this.visible = visible;
				}
				return changed;
			}
		}
	}

	/// <summary>
	/// Stores information about the spacers above and below.
	/// </summary>
	public struct Spacer {
		/// <summary>
		/// The layout element to resize the spacer.
		/// </summary>
		internal readonly LayoutElement layout;

		/// <summary>
		/// The game object to destroy the spacer.
		/// </summary>
		internal readonly GameObject go;

		/// <summary>
		/// The transform to reparent the spacer.
		/// </summary>
		internal readonly Transform transform;

		/// <summary>
		/// Whether the spacer was visible last time.
		/// </summary>
		private bool visible;

		internal Spacer(GameObject go) {
			visible = false;
			this.go = go;
			go.TryGetComponent(out layout);
			transform = go.transform;
			go.SetActive(false);
		}

		public void Dispose() {
			UnityEngine.Object.Destroy(go);
			visible = false;
		}

		/// <summary>
		/// Sets the size of this spacer.
		/// </summary>
		/// <param name="width">The spacer width.</param>
		/// <param name="height">The spacer height.</param>
		public void SetSize(float width, float height) {
			var element = layout;
			element.minHeight = height;
			element.preferredHeight = height;
			element.minWidth = width;
			element.preferredWidth = width;
			bool active = width > 0.0f || height > 0.0f;
			if (active != visible) {
				visible = active;
				go.SetActive(active);
			}
		}
	}
}
