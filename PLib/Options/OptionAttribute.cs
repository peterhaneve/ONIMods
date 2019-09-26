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

namespace PeterHan.PLib {
	/// <summary>
	/// An attribute placed on an option field or enum value for a class used as mod options in
	/// order to denote the display title and other options.
	/// </summary>
	public sealed class OptionAttribute : Attribute {
		/// <summary>
		/// The option title.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The option description tooltip.
		/// </summary>
		public string Tooltip { get; }

		public OptionAttribute(string title, string tooltip = null) {
			if (string.IsNullOrEmpty(title))
				throw new ArgumentNullException("title");
			Title = title;
			Tooltip = tooltip;
		}

		public override string ToString() {
			return Title;
		}
	}
}