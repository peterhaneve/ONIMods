/*
 * Copyright 2020 Peter Han
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

using PeterHan.PLib.UI;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which displays a slider below it.
	/// </summary>
	internal abstract class SlidingBaseOptionsEntry : OptionsEntry {
		/// <summary>
		/// The margin between the slider extra row and the rest of the dialog.
		/// </summary>
		internal static readonly RectOffset ENTRY_MARGIN = new RectOffset(15, 0, 0, 5);
		
		/// <summary>
		/// The margin between the slider and its labels.
		/// </summary>
		internal static readonly RectOffset SLIDER_MARGIN = new RectOffset(10, 10, 0, 0);

		/// <summary>
		/// The limits allowed for the entry.
		/// </summary>
		protected readonly LimitAttribute limits;

		/// <summary>
		/// The realized slider.
		/// </summary>
		protected GameObject slider;

		protected SlidingBaseOptionsEntry(OptionAttribute oa, PropertyInfo prop) : base(prop?.
				Name, oa) {
			limits = FindLimitAttribute(prop);
			slider = null;
		}

		internal override void CreateUIEntry(PGridPanel parent, ref int row) {
			base.CreateUIEntry(parent, ref row);
			double minLimit = limits.Minimum, maxLimit = limits.Maximum;
			// NaN will be false on either comparison
			if (limits != null && maxLimit < float.MaxValue && minLimit > float.MinValue) {
				var slider = GetSlider();
				// Min and max labels
				var minLabel = new PLabel("MinValue") {
					TextStyle = PUITuning.Fonts.TextLightStyle, Text = minLimit.ToString("G"),
					TextAlignment = TextAnchor.MiddleRight
				};
				var maxLabel = new PLabel("MaxValue") {
					TextStyle = PUITuning.Fonts.TextLightStyle, Text = maxLimit.ToString("G"),
					TextAlignment = TextAnchor.MiddleLeft
				};
				// Lay out left to right
				var panel = new PRelativePanel("Slider Grid") {
					FlexSize = Vector2.right, DynamicSize = false
				}.AddChild(slider).AddChild(minLabel).AddChild(maxLabel).AnchorYAxis(slider).
					AnchorYAxis(minLabel, 0.5f).AnchorYAxis(maxLabel, 0.5f).SetLeftEdge(
					minLabel, fraction: 0.0f).SetRightEdge(maxLabel, fraction: 1.0f).
					SetLeftEdge(slider, toRight: minLabel).SetRightEdge(slider, toLeft:
					maxLabel).SetMargin(slider, SLIDER_MARGIN);
				slider.OnRealize += OnRealizeSlider;
				// Add another row for the slider
				parent.AddRow(new GridRowSpec());
				parent.AddChild(panel, new GridComponentSpec(++row, 0) {
					ColumnSpan = 2, Margin = ENTRY_MARGIN
				});
			}
		}

		/// <summary>
		/// Gets the initialized PLib slider to be used for value display.
		/// </summary>
		/// <returns>The slider to be used.</returns>
		protected abstract PSliderSingle GetSlider();

		/// <summary>
		/// Called when the slider is realized.
		/// </summary>
		/// <param name="realized">The actual slider.</param>
		protected void OnRealizeSlider(GameObject realized) {
			slider = realized;
			Update();
		}

		/// <summary>
		/// Updates the displayed value.
		/// </summary>
		protected abstract void Update();
	}
}
