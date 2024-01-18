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

namespace PeterHan.SandboxTools {
	/// <summary>
	/// Stores the strings used in Sandbox Tools.
	/// </summary>
	public static class SandboxToolsStrings {
		// Icon name for the filtered destroy tool
		public const string TOOL_DESTROY_ICON = "filtered_destroy";

		// Name and tooltip for the filtered destroy tool
		public static LocString TOOL_DESTROY_NAME = "Filtered Destroy";
		public static LocString TOOL_DESTROY_TOOLTIP = "Delete objects from the selected cell(s) {Hotkey}";

		// Filter names for the spawner
		public static LocString FILTER_ARTIFACTS = "Artifacts";
		public static LocString FILTER_GEYSERS = "Geysers";
		public static LocString FILTER_POIPROPS = "Props";

		// Filter names for the filtered "Destroy" tool
		public static LocString DESTROY_ALL = STRINGS.UI.TOOLS.FILTERLAYERS.ALL;
		public static LocString DESTROY_ELEMENTS = "Elements";
		public static LocString DESTROY_ITEMS = "Items";
		public static LocString DESTROY_CREATURES = "Creatures";
		public static LocString DESTROY_PLANTS = "Plants";
		public static LocString DESTROY_BUILDINGS = STRINGS.UI.TOOLS.FILTERLAYERS.BUILDINGS;
		public static LocString DESTROY_DRYWALL = STRINGS.UI.TOOLS.FILTERLAYERS.BACKWALL;
		public static LocString DESTROY_LPIPES = STRINGS.UI.TOOLS.FILTERLAYERS.LIQUIDPIPES;
		public static LocString DESTROY_GPIPES = STRINGS.UI.TOOLS.FILTERLAYERS.GASPIPES;
		public static LocString DESTROY_POWER = STRINGS.UI.TOOLS.FILTERLAYERS.WIRES;
		public static LocString DESTROY_AUTO = STRINGS.UI.TOOLS.FILTERLAYERS.LOGIC;
		public static LocString DESTROY_SHIPPING = STRINGS.UI.TOOLS.FILTERLAYERS.SOLIDCONDUITS;
	}
}
