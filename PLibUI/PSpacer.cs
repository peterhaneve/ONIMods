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

using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A spacer to add into layouts. Has a large flexible width/height by default to eat all
	/// the extra space.
	/// </summary>
	public class PSpacer : IUIComponent {
		/// <summary>
		/// The flexible size of this spacer. Defaults to (1, 1) but can be set to (0, 0) to
		/// make this spacer a fixed size area instead.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The preferred size of this spacer.
		/// </summary>
		public Vector2 PreferredSize { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		public string Name { get; }

		public PSpacer() {
			Name = "Spacer";
			FlexSize = Vector2.one;
			PreferredSize = Vector2.zero;
		}

		public GameObject Build() {
			var spacer = new GameObject(Name);
			var le = spacer.AddComponent<LayoutElement>();
			le.flexibleHeight = FlexSize.y;
			le.flexibleWidth = FlexSize.x;
			le.minHeight = 0.0f;
			le.minWidth = 0.0f;
			le.preferredHeight = PreferredSize.y;
			le.preferredWidth = PreferredSize.x;
			OnRealize?.Invoke(spacer);
			return spacer;
		}

		public override string ToString() {
			return "PSpacer";
		}
	}
}
