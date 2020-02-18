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
using STRINGS;

using AutomationState = STRINGS.UI.AutomationState;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Strings used in Debug Not Included.
	/// </summary>
	public static class DebugNotIncludedStrings {
		// Mod status changes
		public static LocString MOD_ACTIVATED = UI.FormatAsAutomationState("Activated", AutomationState.Active);
		public static LocString MOD_DEACTIVATED = UI.FormatAsAutomationState("Deactivated", AutomationState.Standby);
		public static LocString MOD_LOADERROR = UI.FormatAsAutomationState("Failed to load", AutomationState.Standby);
	}
}
