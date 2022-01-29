/*
 * Copyright 2022 Peter Han
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

namespace PeterHan.Resculpt {
	/// <summary>
	/// Stores the strings used in Resculpt.
	/// </summary>
	public static class ResculptStrings {
		// The button in the user menu to trigger resculpting or repainting
		public static readonly LocString REPAINT_BUTTON = "Repaint";
		public static readonly LocString RESCULPT_BUTTON = "Resculpt";
		public static readonly LocString ROTATE_BUTTON = "Rotate";

		// The sprite names used for the repainting and resculpting icons
		public const string REPAINT_SPRITE = "action_repaint";
		public const string RESCULPT_SPRITE = "action_resculpt";
		public const string ROTATE_SPRITE = "action_direction_both";

		public static readonly LocString RESCULPT_TOOLTIP = "Changes the design of this object";
		public static readonly LocString ROTATE_TOOLTIP = "Rotates artwork. {Hotkey}";
	}
}
