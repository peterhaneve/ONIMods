/*
 * Copyright 2025 Peter Han
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
using System;

namespace PeterHan.SandboxTools {
	/// <summary>
	/// A filter option for the sandbox destroy tool.
	/// </summary>
	internal sealed class DestroyFilter {
		/// <summary>
		/// The ID to use internally.
		/// </summary>
		public string ID { get; }

		/// <summary>
		/// The action to perform when this filter is selected for each destroyed cell.
		/// </summary>
		public Action<int> OnPaintCell { get; }
		
		/// <summary>
		/// The overlay mode that will select this filter.
		/// </summary>
		public HashedString OverlayMode { get; }

		/// <summary>
		/// The title of the filter.
		/// </summary>
		public string Title { get; }
		
		public DestroyFilter(string id, HashedString overlayMode, string title) :
			this(id, overlayMode, title, null) { }

		public DestroyFilter(string id, HashedString overlayMode, string title,
				Action<int> onPaintCell) {
			if (string.IsNullOrEmpty(id))
				throw new ArgumentNullException(nameof(id));
			if (string.IsNullOrEmpty(title))
				throw new ArgumentNullException(nameof(title));
			ID = id;
			OverlayMode = overlayMode;
			Title = title;
			OnPaintCell = onPaintCell;
		}

		public override string ToString() {
			return "SandboxDestroyFilter[ID={0},Title={1}]".F(ID, Title);
		}
	}
}
