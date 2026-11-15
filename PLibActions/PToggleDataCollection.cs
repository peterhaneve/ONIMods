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

using System;
using System.Collections.Generic;

namespace PeterHan.PLib.Actions {
	/// <summary>
	/// Stores the toggle state of all tools in a tool menu.
	/// </summary>
	public sealed class PToggleDataCollection {
		/// <summary>
		/// Stores the state of each tool. Passed to Klei methods when using the new
		/// compatibility mode.
		/// </summary>
		private readonly PToggleData[] toggles;

		/// <summary>
		/// Reports the number of tool modes in this collection.
		/// </summary>
		public int ToolCount => toggles.Length;

		/// <summary>
		/// The initial states, which in previous game versions will be automatically updated
		/// when states change.
		/// </summary>
		private readonly Dictionary<string, ToolParameterMenu.ToggleState> initialStates;

		/// <summary>
		/// Indexes the 
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public PToggleData this[int index] {
			get {
				if (index < 0 || index >= toggles.Length)
					throw new ArgumentOutOfRangeException(nameof(index));
				return toggles[index];
			}
		}

		internal PToggleDataCollection(ICollection<PToolMode> options) {
			int i = 0;
			toggles = new PToggleData[options.Count];
			foreach (var option in options)
				toggles[i++] = new PToggleData(option);
		}

		/// <summary>
		/// Creates an array of native Klei ToggleData objects to pass to the tool menu
		/// initialization methods.
		/// </summary>
		/// <returns>An array of Klei ToggleData objects matching the toggle states of this collection.</returns>
		internal ToolParameterMenu.ToggleData[] GetToggleDataArray() {
			int n = toggles.Length;
			var result = new ToolParameterMenu.ToggleData[n];
			for (int i = 0; i < n; i++)
				result[i] = toggles[i].backing;
			return result;
		}
	}
}
