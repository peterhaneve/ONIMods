/*
 * Copyright 2019 Peter Han
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

namespace PeterHan.PLib {
	/// <summary>
	/// A tool mode used in custom tool menus. Shown in the options in the bottom right.
	/// </summary>
	public sealed class PToolMode {
		/// <summary>
		/// Sets up tool options in the tool parameter menu.
		/// </summary>
		/// <param name="menu">The menu to configure.</param>
		/// <param name="options">The available modes.</param>
		/// <returns>A dictionary which is updated in real time to contain the actual state of each mode.</returns>
		public static IDictionary<string, ToolParameterMenu.ToggleState> PopulateMenu(
				ToolParameterMenu menu, ICollection<PToolMode> options) {
			if (options == null)
				throw new ArgumentNullException("options");
			var kOpt = new Dictionary<string, ToolParameterMenu.ToggleState>(options.Count);
			// Add to Klei format, yes it loses the order but it means less of a mess
			foreach (var option in options) {
				string key = option.Key;
				Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + key, option.Title);
				kOpt.Add(key, option.State);
			}
			menu.PopulateMenu(kOpt);
			return kOpt;
		}

		/// <summary>
		/// A unique key used to identify this mode.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// The current state of this tool mode.
		/// </summary>
		public ToolParameterMenu.ToggleState State { get; }

		/// <summary>
		/// The title displayed on-screen for this mode.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// Creates a new tool mode entry.
		/// </summary>
		/// <param name="key">The key which identifies this tool mode.</param>
		/// <param name="title">The title to be displayed.</param>
		/// <param name="state">The initial state, default Off.</param>
		public PToolMode(string key, string title, ToolParameterMenu.ToggleState state =
				ToolParameterMenu.ToggleState.Off) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException("key");
			if (string.IsNullOrEmpty(title))
				throw new ArgumentNullException("title");
			Key = key;
			State = state;
			Title = title;
		}

		public override bool Equals(object obj) {
			var other = obj as PToolMode;
			return other != null && other.Key == Key;
		}

		public override int GetHashCode() {
			return Key.GetHashCode();
		}

		public override string ToString() {
			return "{0} ({1})".F(Key, Title);
		}
	}
}
