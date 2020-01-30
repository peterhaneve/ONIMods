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

using System;

namespace PeterHan.PLib {
	/// <summary>
	/// Strings used as localized descriptions for key bindings.
	/// </summary>
	internal static class KeyCodeStrings {
		// Utility
		public static LocString HOME = "Home";
		public static LocString END = "End";
		public static LocString DELETE = "Delete";
		public static LocString PAGEUP = "Page Up";
		public static LocString PAGEDOWN = "Page Down";
		public static LocString SYSRQ = "SysRq";
		public static LocString PRTSCREEN = "Print Screen";
		public static LocString PAUSE = "Pause";

		// Arrows
		public static LocString ARROWLEFT = "Left Arrow";
		public static LocString ARROWUP = "Up Arrow";
		public static LocString ARROWRIGHT = "Right Arrow";
		public static LocString ARROWDOWN = "Down Arrow";
	}
}
