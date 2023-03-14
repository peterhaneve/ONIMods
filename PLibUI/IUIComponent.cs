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

namespace PeterHan.PLib.UI {
	/// <summary>
	/// Implemented by PLib UI components.
	/// </summary>
	public interface IUIComponent {
		/// <summary>
		/// The component name.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Creates a physical game object embodying this component.
		/// </summary>
		/// <returns>The game object representing this UI component. Multiple invocations return
		/// unique objects.</returns>
		GameObject Build();

		/// <summary>
		/// Actions invoked when the UI component is actually realized.
		/// </summary>
		event PUIDelegates.OnRealize OnRealize;
	}
}
