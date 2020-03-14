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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI.Layouts {
	/// <summary>
	/// The abstract parent of most layout groups.
	/// </summary>
	public abstract class AbstractLayoutGroup : UIBehaviour, ISettableFlexSize, ILayoutElement
	{
		public float minWidth {
			get {
				return mMinWidth;
			}
			set {
				mMinWidth = value;
			}
		}

		public float preferredWidth {
			get {
				return mPreferredWidth;
			}
			set {
				mPreferredWidth = value;
			}
		}

		/// <summary>
		/// The flexible width of the completed layout group can be set.
		/// </summary>
		public float flexibleWidth {
			get {
				return mFlexibleWidth;
			}
			set {
				mFlexibleWidth = value;
			}
		}

		public float minHeight {
			get {
				return mMinHeight;
			}
			set {
				mMinHeight = value;
			}
		}

		public float preferredHeight {
			get {
				return mPreferredHeight;
			}
			set {
				mPreferredHeight = value;
			}
		}

		/// <summary>
		/// The flexible height of the completed layout group can be set.
		/// </summary>
		public float flexibleHeight {
			get {
				return mFlexibleHeight;
			}
			set {
				mFlexibleHeight = value;
			}
		}

		/// <summary>
		/// The priority of this layout group.
		/// </summary>
		public int layoutPriority {
			get {
				return mLayoutPriority;
			}
			set {
				mLayoutPriority = value;
			}
		}

		// The backing fields must be annotated with the attribute to have prefabbed versions
		// successfully copy the values
		[SerializeField]
		private float mMinWidth, mMinHeight, mPreferredWidth, mPreferredHeight;
		[SerializeField]
		private float mFlexibleWidth, mFlexibleHeight;
		[SerializeField]
		private int mLayoutPriority;

		public abstract void CalculateLayoutInputHorizontal();

		public abstract void CalculateLayoutInputVertical();

		public abstract void SetLayoutHorizontal();

		public abstract void SetLayoutVertical();
	}
}
