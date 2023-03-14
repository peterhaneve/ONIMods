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

namespace PeterHan.FastSave {
	/// <summary>
	/// Strings used in Fast Save.
	/// </summary>
	internal static class FastSaveStrings {
		// Auto-save failed: non-I/O exception
		public static LocString AUTOSAVE_FAILED = "Autosave failed!";

		// Why is the save button disabled?
		public static LocString AUTOSAVE_PROGRESS = "Auto-Save in progress";

		// Warns the user that quitting may cause data loss
		public static LocString DESKTOP_QUIT_WARNING = "Exit to Desktop may corrupt the current autosave!";

		// Auto-save failed: I/O exception
		public static LocString IO_ERROR = "IOException. You may not have enough free space!";
	}
}
