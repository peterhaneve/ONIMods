/*
 * Copyright 2019 Peter Han
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
using UnityEngine;
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

		public GameObject Build() {
			if (Child == null)
				throw new InvalidOperationException("No child component");
			var pane = PUIElements.CreateUI(null, Name);
			if (BackColor.a > 0.0f)
				pane.AddComponent<Image>().color = BackColor;
			// Scroll pane itself
			var scroll = pane.AddComponent<KScrollRect>();
			scroll.horizontal = ScrollHorizontal;
			scroll.vertical = ScrollVertical;
			// Viewport
			var viewport = PUIElements.CreateUI(pane, "Viewport", true, PUIAnchoring.Stretch,
				PUIAnchoring.Stretch);
			viewport.AddComponent<RectMask2D>().enabled = true;
			// BoxLayoutGroup resizes the viewport which is undesired, we only want to pass
			// an auto-layout request on to the children
			viewport.AddComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
			scroll.viewport = viewport.rectTransform();
			// Make the child
			var child = Child.Build();
			PUIElements.SetAnchors(PUIElements.SetParent(child, viewport), PUIAnchoring.
				Beginning, PUIAnchoring.Beginning);
			scroll.content = child.rectTransform();
			pane.SetActive(true);
			// Vertical scrollbar
			if (ScrollVertical) {
				scroll.verticalScrollbar = CreateScrollVert(pane);
				scroll.verticalScrollbarVisibility = AlwaysShowVertical ? ScrollRect.
					ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.AutoHide;
			}
			if (ScrollHorizontal) {
				scroll.horizontalScrollbar = CreateScrollHoriz(pane);
				scroll.horizontalScrollbarVisibility = AlwaysShowHorizontal ? ScrollRect.
					ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.AutoHide;
			}
			pane.SetFlexUISize(FlexSize);
			OnRealize?.Invoke(pane);
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
				PUIAnchoring.End);
			track.AddComponent<Image>().sprite = PUITuning.Images.ScrollBorderHorizontal;
			// Scroll track
			var sb = track.AddComponent<Scrollbar>();
			sb.interactable = true;
			sb.transition = Selectable.Transition.ColorTint;
			sb.colors = PUITuning.Colors.ScrollbarColors;
			sb.SetDirection(Scrollbar.Direction.RightToLeft, true);
			// Sliding area
			var area = PUIElements.CreateUI(track, "Sliding Area", false);
			// Handle
			var handle = PUIElements.CreateUI(area, "Handle", true, PUIAnchoring.Stretch,
				PUIAnchoring.Beginning);
			sb.handleRect = handle.rectTransform();
			var hImg = handle.AddComponent<Image>();
			hImg.sprite = PUITuning.Images.ScrollHandleHorizontal;
			sb.targetGraphic = hImg;
			track.SetActive(true);
			// Sizing
			track.SetUISize(new Vector2(TrackSize + 2.0f, TrackSize + 2.0f));
			area.SetUISize(new Vector2(TrackSize, TrackSize));
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
			track.AddComponent<Image>().sprite = PUITuning.Images.ScrollBorderVertical;
			// Scroll track
			var sb = track.AddComponent<Scrollbar>();
			sb.interactable = true;
			sb.transition = Selectable.Transition.ColorTint;
			sb.colors = PUITuning.Colors.ScrollbarColors;
			sb.SetDirection(Scrollbar.Direction.BottomToTop, true);
			// Sliding area
			var area = PUIElements.CreateUI(track, "Sliding Area", false);
			// Handle
			var handle = PUIElements.CreateUI(area, "Handle", true, PUIAnchoring.Stretch,
				PUIAnchoring.Beginning);
			sb.handleRect = handle.rectTransform();
			var hImg = handle.AddComponent<Image>();
			hImg.sprite = PUITuning.Images.ScrollHandleVertical;
			sb.targetGraphic = hImg;
			track.SetActive(true);
			// Sizing
			track.SetUISize(new Vector2(TrackSize + 2.0f, TrackSize + 2.0f));
			area.SetUISize(new Vector2(TrackSize, TrackSize));
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
			return "PScrollPane[Name={0},Child={1}]".F(Name, Child);
		}
	}
}
