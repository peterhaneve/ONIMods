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

namespace PeterHan.PLib.Options {
	/// <summary>
	/// User dynamic option handlers must implement this type. No type check is done due to
	/// cross-assembly references, but if the methods do not exist the option may fail to
	/// load properly.
	/// </summary>
	public interface IDynamicOption {
		/// <summary>
		/// The options category. It will be queried once before any options are loaded.
		/// </summary>
		string Category { get; }

		/// <summary>
		/// The options title. It will be queried after the initial option value is loaded,
		/// but only once.
		/// </summary>
		string Title { get; }

		/// <summary>
		/// The options tooltip. Will be regularly queried and updated when tooltips are shown.
		/// However, there is a bug in the stock game that will not resize the black background
		/// if the text changes.
		/// </summary>
		string ToolTip { get; }

		/// <summary>
		/// Will be written to set the value with the value read from options, and read when
		/// options are saved.
		/// </summary>
		object Value { get; set; }

		/// <summary>
		/// Gets the UI component that will handle this option. Note that this method must
		/// return a realized GameObject.
		/// </summary>
		/// <returns>The UI entry handler for this option.</returns>
		GameObject GetUIComponent();
	}
}
