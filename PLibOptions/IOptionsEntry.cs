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

using PeterHan.PLib.UI;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// All options handlers, including user dynamic option handlers, implement this type.
	/// </summary>
	public interface IOptionsEntry : IOptionSpec {
		/// <summary>
		/// Creates UI components that will present this option.
		/// </summary>
		/// <param name="parent">The parent panel where the components should be added.</param>
		/// <param name="row">The row index where the component should be placed. If multiple
		/// rows of components are added, increment this value for each additional row.</param>
		void CreateUIEntry(PGridPanel parent, ref int row);

		/// <summary>
		/// Reads the option value into the UI from the provided settings object.
		/// </summary>
		/// <param name="settings">The settings object.</param>
		void ReadFrom(object settings);

		/// <summary>
		/// Writes the option value from the UI into the provided settings object.
		/// </summary>
		/// <param name="settings">The settings object.</param>
		void WriteTo(object settings);
	}
}
