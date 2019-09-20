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
	/// A spacer to add into layouts. Has a large flexible width/height to eat all the space.
	/// </summary>
	public class PSpacer : IUIComponent {
		/// <summary>
		/// The preferred size of this spacer.
		/// </summary>
		public Vector2 PreferredSize { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		public string Name { get; }

		public PSpacer() {
			Name = "Spacer";
			PreferredSize = Vector2.zero;
		}

		public GameObject Build() {
			var spacer = new GameObject(Name);
			var le = spacer.AddComponent<LayoutElement>();
			le.flexibleHeight = 1.0f;
			le.flexibleWidth = 1.0f;
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
