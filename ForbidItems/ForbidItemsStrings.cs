/*
 * Copyright 2025 Peter Han
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

namespace PeterHan.ForbidItems {
	/// <summary>
	/// Strings used in Forbid Items.
	/// </summary>
	public static class ForbidItemsStrings {
		public static class MISC {
			public static class STATUSITEMS {
				public static class FORBIDDEN {
					public static LocString NAME = "Item Forbidden";
					public static LocString TOOLTIP = "This item cannot be picked up by Duplicants or " +
						STRINGS.UI.PRE_KEYWORD + "Auto-Sweepers" + STRINGS.UI.PST_KEYWORD;
				}
			}
		}

		public static class UI {
			public static class ALLOW_FORBIDDEN_SIDE_SCREEN {
				public static LocString FORBIDDEN = "Allow Forbidden Items";
				public static LocString FORBIDDEN_TOOLTIP = "Allow the use of forbidden items";
			}

			public static class USERMENUACTIONS {
				public static class FORBIDITEM {
					public static LocString NAME = "Forbid Item";
					public static LocString NAME_OFF = "Reclaim Item";

					public static LocString TOOLTIP = "Prevent this item from being picked up";
					public static LocString TOOLTIP_OFF = "Allow this item to be picked up";
				}
			}
		}
	}
}
