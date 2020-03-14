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

using UnityEngine;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// Describes the abilities shared between RelativeLayout and RelativeLayoutGroup.
	/// </summary>
	public interface IRelativeLayout<T, O> where T : class where O : class {
		/// <summary>
		/// Anchors the component's pivot in the X axis to the specified anchor position.
		/// The component will be laid out at its preferred (or overridden) width with its
		/// pivot locked to the specified relative fraction of the parent component's width.
		/// 
		/// Any other existing left or right edge constraints will be overwritten. This method
		/// is equivalent to setting both the left and right edges to the same fraction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="anchor">The fraction to which to align the pivot, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <returns>This object, for call chaining.</returns>
		T AnchorXAxis(O item, float anchor = 0.5f);

		/// <summary>
		/// Anchors the component's pivot in the Y axis to the specified anchor position.
		/// The component will be laid out at its preferred (or overridden) height with its
		/// pivot locked to the specified relative fraction of the parent component's height.
		/// 
		/// Any other existing top or bottom edge constraints will be overwritten. This method
		/// is equivalent to setting both the top and bottom edges to the same fraction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="anchor">The fraction to which to align the pivot, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <returns>This object, for call chaining.</returns>
		T AnchorYAxis(O item, float anchor = 0.5f);

		/// <summary>
		/// Overrides the preferred size of a component. If set, instead of looking at layout
		/// sizes of the component, the specified size will be used instead.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="size">The size to apply. Only dimensions greater than zero will be used.</param>
		/// <returns>This object, for call chaining.</returns>
		T OverrideSize(O item, Vector2 size);

		/// <summary>
		/// Sets the bottom edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the bottom edge anchored to that fraction of the parent's
		/// height. If a component is specified and no fraction is specified, the component
		/// will be anchored with its bottom edge above the top edge of that component.
		/// If neither is specified, all bottom edge constraints will be removed.
		/// 
		/// Any other existing bottom edge constraint will be overwritten.
		/// 
		/// Remember that +Y is in the upwards direction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the bottom edge, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <param name="above">The game object which this component must be above.</param>
		/// <returns>This object, for call chaining.</returns>
		T SetBottomEdge(O item, float fraction = -1.0f, GameObject above = null);

		/// <summary>
		/// Sets the left edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the left edge anchored to that fraction of the parent's
		/// width. If a component is specified and no fraction is specified, the component
		/// will be anchored with its left edge to the right of that component.
		/// If neither is specified, all left edge constraints will be removed.
		/// 
		/// Any other existing left edge constraint will be overwritten.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the left edge, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <param name="toLeft">The game object which this component must be to the right of.</param>
		/// <returns>This object, for call chaining.</returns>
		T SetLeftEdge(O item, float fraction = -1.0f, GameObject toRight = null);

		/// <summary>
		/// Sets the insets of a component from its anchor points. A positive number insets the
		/// component away from the edge, whereas a negative number out-sets the component
		/// across the edge.
		/// 
		/// All components default to no insets.
		/// 
		/// Any reference to a component's edge using other constraints always refers to its
		/// edge <b>before</b> insets are applied.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="insets">The insets to apply. If null, the insets will be set to zero.</param>
		/// <returns>This object, for call chaining.</returns>
		T SetMargin(O item, RectOffset insets);

		/// <summary>
		/// Sets the right edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the right edge anchored to that fraction of the parent's
		/// width. If a component is specified and no fraction is specified, the component
		/// will be anchored with its right edge to the left of that component.
		/// If neither is specified, all right edge constraints will be removed.
		/// 
		/// Any other existing right edge constraint will be overwritten.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the right edge, with 0.0f
		/// being the left and 1.0f being the right.</param>
		/// <param name="toLeft">The game object which this component must be to the left of.</param>
		/// <returns>This object, for call chaining.</returns>
		T SetRightEdge(O item, float fraction = -1.0f, GameObject toLeft = null);

		/// <summary>
		/// Sets the top edge of a game object. If the fraction is supplied, the component
		/// will be laid out with the top edge anchored to that fraction of the parent's
		/// height. If a component is specified and no fraction is specified, the component
		/// will be anchored with its top edge above the bottom edge of that component.
		/// If neither is specified, all top edge constraints will be removed.
		/// 
		/// Any other existing top edge constraint will be overwritten.
		/// 
		/// Remember that +Y is in the upwards direction.
		/// </summary>
		/// <param name="item">The component to adjust.</param>
		/// <param name="fraction">The fraction to which to align the top edge, with 0.0f
		/// being the bottom and 1.0f being the top.</param>
		/// <param name="below">The game object which this component must be below.</param>
		/// <returns>This object, for call chaining.</returns>
		T SetTopEdge(O item, float fraction = -1.0f, GameObject below = null);
	}
}
