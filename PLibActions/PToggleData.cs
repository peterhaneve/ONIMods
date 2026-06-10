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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

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

		// TODO Simplify when versions before U58-719533 no longer need to be supported
		internal readonly object backing;

		private readonly IDictionary<string, ToolParameterMenu.ToggleState> legacyBacking;

		/// <summary>
		/// The current state.
		/// </summary>
		public ToolParameterMenu.ToggleState State {
			get {
				ToolParameterMenu.ToggleState result;
				if (legacyBacking != null) {
					// Use realtime updated Klei dictionary from previous game versions
					if (!legacyBacking.TryGetValue(Name, out result))
						result = ToolParameterMenu.ToggleState.Off;
				} else if (backing != null)
					// Call backing object
					result = PToolModeCompatibility.Instance.GetState(backing);
				else
					throw new ArgumentException("No valid backing object available");
				return result;
			}
		}

		internal PToggleData(PToolMode mode,
				IDictionary<string, ToolParameterMenu.ToggleState> legacyBacking) {
			var backingType = PToolModeCompatibility.TOGGLE_DATA;
			backing = null;
			this.legacyBacking = legacyBacking;
			Name = mode.Key;
			isToggleInclusive = false;
			if (backingType != null)
				try {
					backing = Activator.CreateInstance(backingType, new object[0]);
				} catch (TargetInvocationException e) {
					PUtil.LogExcWarn(e.GetBaseException());
				} catch (Exception e) {
					PUtil.LogExcWarn(e);
				}
			// Add string key
			if (!string.IsNullOrEmpty(mode.Title))
				Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + mode.Key, mode.Title);
		}
	}
}
