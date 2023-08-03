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

using System;

namespace PeterHan.MooReproduction {
	/// <summary>
	/// Strings used in Moo Reproduction.
	/// </summary>
	public static class MooReproductionStrings {
		public static class CREATURES {
			public static class SPECIES {
				public static class MOO {
					// Follows the vanilla structure, yes lots of classes
					public static class BABY {
						public static LocString NAME = STRINGS.UI.FormatAsLink("Gassy Moolet", "MOO");

						public static LocString DESC = "A cute little Gassy Moolet.\n\nOne day it will grow into an adult " + STRINGS.UI.FormatAsLink("Gassy Moo", "MOO") + ".";
					}
				}
			}

			public static class STATUSITEMS {
				public static class GIVINGBIRTH {
					public static LocString NAME = "Giving Birth";

					public static LocString TOOLTIP = "Here it comes!";
				}
			}
		}

		public static class UI {
			public static class FRONTEND {
				public static class MOOREPRODUCTION {
					public static LocString DISABLE_METEORS = "Disable Moo Meteors";
				}
			}

			public static class TOOLTIPS {
				public static class MOOREPRODUCTION {
					public static LocString DISABLE_METEORS = "Disables Moo Meteors from falling on any planetoid. Can help avoid an exponential build-up of Gassy Moos.\n\n" +
						"Gassy Moos will also never call for new Moo Meteors when fed.";
				}
			}
		}
	}
}
