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

using System.Collections.Generic;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An optional interface which can be implemented to give mods the ability to dynamically
	/// add new options at runtime, or to get a notification when options are updated to the
	/// options file.
	/// 
	/// This interface is <b>optional</b>. There is no need to implement it to use PLib
	/// Options. But if one method is implemented, the other must also be. If not used,
	/// OnOptionsChanged should be empty, and CreateOptions should return an empty collection.
	/// </summary>
	public interface IOptions {
		/// <summary>
		/// Called to create additional options. After the options in this class have been
		/// read from the data file, but before the dialog is shown, this method will be
		/// invoked. Each return value must be of a type that implements IOptionsEntry.
		/// 
		/// The options will be sorted and categorized normally as if they were present at the
		/// end of the property list in a regular options class.
		/// 
		/// This method can be an enumerator using code like
		/// yield return new MyOptionsHandler();
		/// </summary>
		/// <returns>The custom options to implement.</returns>
		IEnumerable<IOptionsEntry> CreateOptions();

		/// <summary>
		/// Called when options are written to the file. The current object will have the same
		/// values as the data that was just written to the file. This call happens after the
		/// options have been stored to disk, but before any restart required dialog is shown
		/// (if [RestartRequired] is also on this class).
		/// </summary>
		void OnOptionsChanged();
	}
}
