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

namespace PeterHan.PLib.Options {
	/// <summary>
	/// The common parent of all classes that can specify the user visible attributes of an
	/// option.
	/// </summary>
	public interface IOptionSpec {
		/// <summary>
		/// The option category. Ignored and replaced with the parent option's category if
		/// this option is part of a custom grouped type.
		/// </summary>
		string Category { get; }

		/// <summary>
		/// The format string to use when displaying this option value. Only applicable for
		/// some types of options.
		/// 
		/// <b>Warning</b>: Attribute may have issues on nested classes that are used as custom
		/// grouped options. To mitigate, try declaring the custom class in a non-nested
		/// context (i.e. not declared inside another class).
		/// </summary>
		string Format { get; }

		/// <summary>
		/// The option title. Ignored for fields which are displayed as custom grouped types
		/// types of other options.
		/// </summary>
		string Title { get; }

		/// <summary>
		/// The option description tooltip. Ignored for fields which are displayed as custom
		/// grouped types of other options.
		/// </summary>
		string Tooltip { get; }
	}
}
