/*
 * Copyright 2021 Peter Han
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

namespace PeterHan.FinishTasks {
	/// <summary>
	/// Strings used in Rest for the Weary.
	/// </summary>
	public static class FinishTasksStrings {
		public static class DUPLICANTS {
			public static class CHORES {
				public static class PRECONDITIONS {
					public static LocString CAN_START_NEW_TASK = "Schedule disallows new tasks";
				}
			}
		}

		public static class UI {
			public static class SCHEDULEGROUPS {
				public static class FINISHTASK {
					public const string ID = "FinishTask";

					public static LocString NAME = "Finish-Up";
					public static LocString DESCRIPTION = "During Finish-Up time shifts my Duplicants will finish their current task if they have one.\n\nThey will not start new tasks unless they are close to the " + STRINGS.UI.FormatAsLink("Printing Pod", "HEADQUARTERS") + ".";
					public static LocString NOTIFICATION_TOOLTIP = "During " + STRINGS.UI.PRE_KEYWORD +
						"Finish-Up" + STRINGS.UI.PST_KEYWORD + " shifts my Duplicants will finish their current task but will not start new tasks.";
				}
			}
		}
	}
}
