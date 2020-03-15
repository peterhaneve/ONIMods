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

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Describes handlers which can be used in OptionsDialog to retrieve information that
	/// will be shown in the dialog.
	/// </summary>
	internal interface IOptionsHandler {
		/// <summary>
		/// Retrieve the location where the options file will be stored. The name of the file
		/// will be set by the annotation in the options type.
		/// </summary>
		string ConfigPath { get; }

		/// <summary>
		/// The default URL of the "Mod Homepage" button if one is not found in the annotation.
		/// </summary>
		string DefaultURL { get; }

		/// <summary>
		/// Retrieves the dialog title.
		/// </summary>
		/// <param name="baseTitle">The title retrieved from the ModInfo attribute if supplied, otherwise null.</param>
		/// <returns>The dialog title; a default title will be used if the result is null or empty.</returns>
		string GetTitle(string baseTitle);

		/// <summary>
		/// Invoked if the user cancels the dialog. The parameter might be null if the original
		/// options were also null.
		/// </summary>
		/// <param name="oldOptions">The unmodified options retrieved when the dialog was shown.</param>
		void OnCancel(object oldOptions);

		/// <summary>
		/// Invoked <b>after</b> saving the options when OK is pressed. The parameter will not
		/// be null.
		/// 
		/// This method will not be invoked after cancelling the dialog.
		/// </summary>
		/// <param name="newOptions">The new options values chosen by the user.</param>
		void OnSaveOptions(object newOptions);
	}
}
