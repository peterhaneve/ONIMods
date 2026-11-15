/*
 * Copyright 2026 Peter Han
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

namespace PeterHan.PLib.Actions {
	/// <summary>
	/// Stores the same data as Klei's ToolParameterMenu.ToggleData class, but wraps it in
	/// a safer class that handles multiple game versions.
	/// </summary>
	public sealed class PToggleData {
		/// <summary>
		/// The tool name.
		/// </summary>
		public readonly string Name;

		public readonly bool isToggleInclusive;

		internal readonly ToolParameterMenu.ToggleData backing;

		/// <summary>
		/// The current state.
		/// </summary>
		public ToolParameterMenu.ToggleState State {
			get {
				return backing.state;
			}
		}

		internal PToggleData(PToolMode mode) {
			Name = mode.Key;
			isToggleInclusive = false;
			backing = new ToolParameterMenu.ToggleData(Name, mode.State, isToggleInclusive);
			// Add string key
			if (!string.IsNullOrEmpty(mode.Title))
				Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + mode.Key, mode.Title);
		}
	}
}
