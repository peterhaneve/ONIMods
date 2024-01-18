/*
 * Copyright 2024 Peter Han
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

namespace PeterHan.PreserveSeed {
	/// <summary>
	/// Strings used in Preserve Random Seed.
	/// </summary>
	public static class PreserveSeedStrings {
		public static class UI {
			public static class FRONTEND {
				public static class PRESERVESEED {
					public static LocString CATEGORY_POD = "Printing Pod";

					public static LocString PRESERVEPODSEED = "Preserve Pod Seed";
					public static LocString RECHARGENORMAL = "Normal Recharge Time";
					public static LocString RECHARGEREJECT = "Recharge Time on Reject All";
				}
			}

			public static class TOOLTIPS {
				public static class PRESERVESEED {
					public static LocString PRESERVEPODSEED = "Preserves the items offered in the Printing Pod even when reloading.";
					public static LocString RECHARGENORMAL = "The time (in cycles) it takes to recharge the Printing Pod when a choice is printed.\n\n<b>Only applies after the first two prints.</b>";
					public static LocString RECHARGEREJECT = "The time (in cycles) it takes to recharge the Printing Pod when all choices are rejected.";

					public static LocString REJECTTOOLTIP = "Rejecting all will recharge the Printing Pod in just {0:F1} cycles.";
				}
			}
		}
	}
}
