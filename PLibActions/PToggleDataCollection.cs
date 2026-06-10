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
			int i = 0, n = options.Count;
			initialStates = new Dictionary<string, ToolParameterMenu.ToggleState>(n);
			toggles = new PToggleData[n];
			// Add to Klei format, yes it loses the order but it means less of a mess
			foreach (var option in toggles)
				initialStates.Add(option.Name, option.State);
			foreach (var option in options)
				toggles[i++] = new PToggleData(option, initialStates);
		}

		/// <summary>
		/// Creates an array of native Klei ToggleData objects to pass to the tool menu
		/// initialization methods.
		/// </summary>
		/// <returns>An array of Klei ToggleData objects matching the toggle states of this collection.</returns>
		internal object GetToggleDataArray() {
			var arrayElementType = PToolModeCompatibility.TOGGLE_DATA;
			if (arrayElementType == null)
				throw new ArgumentNullException("New method used on previous game version");
			int n = toggles.Length;
			var result = Array.CreateInstance(arrayElementType, n);
			for (int i = 0; i < n; i++)
				result.SetValue(toggles[i].backing, i);
			return result;
		}

		/// <summary>
		/// Creates a dictionary of raw initial toggle states to pass to legacy tool menu
		/// initialization methods.
		/// 
		/// TODO Remove when versions before U58-719533 no longer need to be supported
		/// </summary>
		internal Dictionary<string, ToolParameterMenu.ToggleState> GetToggleDataDictionary() {
			return initialStates;
		}
	}
}
