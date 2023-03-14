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

using PeterHan.PLib.Core;
using PeterHan.PLib.UI.Layouts;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A factory for scrollable panes.
	/// </summary>
	public sealed class PScrollPane : IUIComponent {
		/// <summary>
		/// The track size of scrollbars is based on the sprite.
		/// </summary>
		private const float DEFAULT_TRACK_SIZE = 16.0f;

		/// <summary>
		/// Whether the horizontal scrollbar is always visible.
		/// </summary>
		public bool AlwaysShowHorizontal { get; set; }

		/// <summary>
		/// Whether the vertical scrollbar is always visible.
		/// </summary>
		public bool AlwaysShowVertical { get; set; }

		/// <summary>
		/// The background color of this scroll pane.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// The child of this scroll pane.
		/// </summary>
		public IUIComponent Child { get; set; }

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		public string Name { get; }

		/// <summary>
		/// Whether horizontal scrolling is allowed.
		/// </summary>
		public bool ScrollHorizontal { get; set; }

		/// <summary>
		/// Whether vertical scrolling is allowed.
		/// </summary>
		public bool ScrollVertical { get; set; }

		/// <summary>
		/// The size of the scrollbar track.
		/// </summary>
		public float TrackSize { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		public PScrollPane() : this(null) { }

		public PScrollPane(string name) {
			AlwaysShowHorizontal = AlwaysShowVertical = true;
			BackColor = PUITuning.Colors.Transparent;
			Child = null;
			FlexSize = Vector2.zero;
			Name = name ?? "Scroll";
			ScrollHorizontal = false;
			ScrollVertical = false;
			TrackSize = DEFAULT_TRACK_SIZE;
		}

		/// <summary>
		/// Adds a handler when this scroll pane is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This scroll pane for call chaining.</returns>
		public PScrollPane AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public GameObject Build() {
			if (Child == null)
				throw new InvalidOperationException("No child component");
			var pane = BuildScrollPane(null, Child.Build());
			OnRealize?.Invoke(pane);
			return pane;
		}

		/// <summary>
		/// Builds the actual scroll pane object.
		/// </summary>
		/// <param name="parent">The parent of this scroll pane.</param>
		/// <param name="child">The child element of this scroll pane.</param>
		/// <returns>The realized scroll pane.</returns>
		internal GameObject BuildScrollPane(GameObject parent, GameObject child) {
			var pane = PUIElements.CreateUI(parent, Name);
			if (BackColor.a > 0.0f)
				pane.AddComponent<Image>().color = BackColor;
			pane.SetActive(false);
			// Scroll pane itself
			var scroll = pane.AddComponent<KScrollRect>();
			scroll.horizontal = ScrollHorizontal;
			scroll.vertical = ScrollVertical;
			// Viewport
			var viewport = PUIElements.CreateUI(pane, "Viewport");
			viewport.rectTransform().pivot = Vector2.up;
			viewport.AddComponent<RectMask2D>().enabled = true;
			viewport.AddComponent<ViewportLayoutGroup>();
			scroll.viewport = viewport.rectTransform();
			// Give the Child a separate Canvas to reduce layout rebuilds
			child.AddOrGet<Canvas>().pixelPerfect = false;
			child.AddOrGet<GraphicRaycaster>();
			PUIElements.SetAnchors(child.SetParent(viewport), PUIAnchoring.Beginning,
				PUIAnchoring.End);
			scroll.content = child.rectTransform();
			// Vertical scrollbar
			if (ScrollVertical) {
				scroll.verticalScrollbar = CreateScrollVert(pane);
				scroll.verticalScrollbarVisibility = AlwaysShowVertical ? ScrollRect.
					ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.
					AutoHideAndExpandViewport;
			}
			if (ScrollHorizontal) {
				scroll.horizontalScrollbar = CreateScrollHoriz(pane);
				scroll.horizontalScrollbarVisibility = AlwaysShowHorizontal ? ScrollRect.
					ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.
					AutoHideAndExpandViewport;
			}
			pane.SetActive(true);
			// Custom layout to pass child sizes to the scroll pane
			var layout = pane.AddComponent<PScrollPaneLayout>();
			layout.flexibleHeight = FlexSize.y;
			layout.flexibleWidth = FlexSize.x;
			return pane;
		}

		/// <summary>
		/// Creates a horizontal scroll bar.
		/// </summary>
		/// <param name="parent">The parent component.</param>
		/// <returns>The scroll bar component.</returns>
		private Scrollbar CreateScrollHoriz(GameObject parent) {
			// Outer scrollbar
			var track = PUIElements.CreateUI(parent, "Scrollbar H", true, PUIAnchoring.Stretch,
				PUIAnchoring.Beginning);
			var bg = track.AddComponent<Image>();
			bg.sprite = PUITuning.Images.ScrollBorderHorizontal;
			bg.type = Image.Type.Sliced;
			// Scroll track
			var sb = track.AddComponent<Scrollbar>();
			sb.interactable = true;
			sb.transition = Selectable.Transition.ColorTint;
			sb.colors = PUITuning.Colors.ScrollbarColors;
			sb.SetDirection(Scrollbar.Direction.RightToLeft, true);
			// Handle
			var handle = PUIElements.CreateUI(track, "Handle", true, PUIAnchoring.Stretch,
				PUIAnchoring.End);
			PUIElements.SetAnchorOffsets(handle, 1.0f, 1.0f, 1.0f, 1.0f);
			sb.handleRect = handle.rectTransform();
			var hImg = handle.AddComponent<Image>();
			hImg.sprite = PUITuning.Images.ScrollHandleHorizontal;
			hImg.type = Image.Type.Sliced;
			sb.targetGraphic = hImg;
			track.SetActive(true);
			PUIElements.SetAnchorOffsets(track, 2.0f, 2.0f, -TrackSize, 0.0f);
			return sb;
		}

		/// <summary>
		/// Creates a vertical scroll bar.
		/// </summary>
		/// <param name="parent">The parent component.</param>
		/// <returns>The scroll bar component.</returns>
		private Scrollbar CreateScrollVert(GameObject parent) {
			// Outer scrollbar
			var track = PUIElements.CreateUI(parent, "Scrollbar V", true, PUIAnchoring.End,
				PUIAnchoring.Stretch);
			var bg = track.AddComponent<Image>();
			bg.sprite = PUITuning.Images.ScrollBorderVertical;
			bg.type = Image.Type.Sliced;
			// Scroll track
			var sb = track.AddComponent<Scrollbar>();
			sb.interactable = true;
			sb.transition = Selectable.Transition.ColorTint;
			sb.colors = PUITuning.Colors.ScrollbarColors;
			sb.SetDirection(Scrollbar.Direction.BottomToTop, true);
			// Handle
			var handle = PUIElements.CreateUI(track, "Handle", true, PUIAnchoring.Stretch,
				PUIAnchoring.Beginning);
			PUIElements.SetAnchorOffsets(handle, 1.0f, 1.0f, 1.0f, 1.0f);
			sb.handleRect = handle.rectTransform();
			var hImg = handle.AddComponent<Image>();
			hImg.sprite = PUITuning.Images.ScrollHandleVertical;
			hImg.type = Image.Type.Sliced;
			sb.targetGraphic = hImg;
			track.SetActive(true);
			PUIElements.SetAnchorOffsets(track, -TrackSize, 0.0f, 2.0f, 2.0f);
			return sb;
		}

		/// <summary>
		/// Sets the background color to the default Klei dialog blue.
		/// </summary>
		/// <returns>This scroll pane for call chaining.</returns>
		public PScrollPane SetKleiBlueColor() {
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the background color to the Klei dialog header pink.
		/// </summary>
		/// <returns>This scroll pane for call chaining.</returns>
		public PScrollPane SetKleiPinkColor() {
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			return this;
		}

		public override string ToString() {
			return string.Format("PScrollPane[Name={0},Child={1}]", Name, Child);
		}

		/// <summary>
		/// Handles layout for scroll panes. Not freezable.
		/// </summary>
		private sealed class PScrollPaneLayout : AbstractLayoutGroup {
			/// <summary>
			/// Caches elements when calculating layout to improve performance.
			/// </summary>
			private Component[] calcElements;

			/// <summary>
			/// The calculated horizontal size of the child element.
			/// </summary>
			private LayoutSizes childHorizontal;

			/// <summary>
			/// The calculated vertical size of the child element.
			/// </summary>
			private LayoutSizes childVertical;

			/// <summary>
			/// The child object inside the scroll rect.
			/// </summary>
			private GameObject child;

			/// <summary>
			/// Caches elements when setting layout to improve performance.
			/// </summary>
			private ILayoutController[] setElements;

			/// <summary>
			/// The viewport which clips the child rectangle.
			/// </summary>
			private GameObject viewport;

			internal PScrollPaneLayout() {
				minHeight = minWidth = 0.0f;
				child = viewport = null;
			}

			public override void CalculateLayoutInputHorizontal() {
				if (child == null)
					UpdateComponents();
				if (child != null) {
					calcElements = child.GetComponents<Component>();
					// Lay out children
					childHorizontal = PUIUtils.CalcSizes(child, PanelDirection.Horizontal,
						calcElements);
					if (childHorizontal.ignore)
						throw new InvalidOperationException("ScrollPane child ignores layout!");
					preferredWidth = childHorizontal.preferred;
				}
			}

			public override void CalculateLayoutInputVertical() {
				if (child == null)
					UpdateComponents();
				if (child != null && calcElements != null) {
					// Lay out children
					childVertical = PUIUtils.CalcSizes(child, PanelDirection.Vertical,
						calcElements);
					preferredHeight = childVertical.preferred;
					calcElements = null;
				}
			}

			protected override void OnEnable() {
				base.OnEnable();
				UpdateComponents();
			}

			public override void SetLayoutHorizontal() {
				if (viewport != null && child != null) {
					// Observe the flex width
					float prefWidth = childHorizontal.preferred;
					float actualWidth = viewport.rectTransform().rect.width;
					if (prefWidth < actualWidth && childHorizontal.flexible > 0.0f)
						prefWidth = actualWidth;
					// Resize child
					setElements = child.GetComponents<ILayoutController>();
					child.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.
						Horizontal, prefWidth);
					// ScrollRect does not rebuild the child's layout
					foreach (var component in setElements)
						component.SetLayoutHorizontal();
				}
			}

			public override void SetLayoutVertical() {
				if (viewport != null && child != null && setElements != null) {
					// Observe the flex height
					float prefHeight = childVertical.preferred;
					float actualHeight = viewport.rectTransform().rect.height;
					if (prefHeight < actualHeight && childVertical.flexible > 0.0f)
						prefHeight = actualHeight;
					// Resize child
					child.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.
						Vertical, prefHeight);
					// ScrollRect does not rebuild the child's layout
					foreach (var component in setElements)
						component.SetLayoutVertical();
					setElements = null;
				}
			}

			/// <summary>
			/// Caches the child component for performance reasons at runtime.
			/// </summary>
			private void UpdateComponents() {
				var obj = gameObject;
				if (obj != null && obj.TryGetComponent(out ScrollRect sr)) {
					child = sr.content?.gameObject;
					viewport = sr.viewport?.gameObject;
				} else
					child = viewport = null;
			}
		}

		/// <summary>
		/// A layout group object that does nothing. While it seems completely pointless,
		/// it allows LayoutRebuilder to pass by the viewport on Scroll Rects on its way up
		/// the tree, thus ensuring that the scroll rect gets rebuilt.
		/// 
		/// On the way back down, this component gets skipped over by PScrollPaneLayout to
		/// save on processing, and the child layout is built directly.
		/// </summary>
		private sealed class ViewportLayoutGroup : UIBehaviour, ILayoutGroup {
			public void SetLayoutHorizontal() { }

			public void SetLayoutVertical() { }
		}
	}
}
