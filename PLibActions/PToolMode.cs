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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;

namespace PeterHan.PLib.Actions {
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
				throw new ArgumentNullException(nameof(options));
			var kOpt = new Dictionary<string, ToolParameterMenu.ToggleState>(options.Count);
			// Add to Klei format, yes it loses the order but it means less of a mess
			foreach (var option in options) {
				string key = option.Key;
				if (!string.IsNullOrEmpty(option.Title))
					Strings.Add("STRINGS.UI.TOOLS.FILTERLAYERS." + key, option.Title);
				kOpt.Add(key, option.State);
			}
			menu.PopulateMenu(kOpt);
			return kOpt;
		}

		/// <summary>
		/// Registers a tool with the game. It still must be added to a tool collection to be
		/// visible.
		/// </summary>
		/// <typeparam name="T">The tool type to register.</typeparam>
		/// <param name="controller">The player controller which will be its parent; consider
		/// using in a postfix on PlayerController.OnPrefabInit.</param>
		public static void RegisterTool<T>(PlayerController controller) where T : InterfaceTool
		{
			if (controller == null)
				throw new ArgumentNullException(nameof(controller));
			// Create list so that new tool can be appended at the end
			var interfaceTools = ListPool<InterfaceTool, PlayerController>.Allocate();
			interfaceTools.AddRange(controller.tools);
			var newTool = new UnityEngine.GameObject(typeof(T).Name);
			var tool = newTool.AddComponent<T>();
			// Reparent tool to the player controller, then enable/disable to load it
			newTool.transform.SetParent(controller.gameObject.transform);
			newTool.gameObject.SetActive(true);
			newTool.gameObject.SetActive(false);
			interfaceTools.Add(tool);
			controller.tools = interfaceTools.ToArray();
			interfaceTools.Recycle();
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
		public LocString Title { get; }

		/// <summary>
		/// Creates a new tool mode entry.
		/// </summary>
		/// <param name="key">The key which identifies this tool mode.</param>
		/// <param name="title">The title to be displayed. If null, the title will be taken
		/// from the default location in STRINGS.UI.TOOLS.FILTERLAYERS.</param>
		/// <param name="state">The initial state, default Off.</param>
		public PToolMode(string key, LocString title, ToolParameterMenu.ToggleState state =
				ToolParameterMenu.ToggleState.Off) {
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));
			Key = key;
			State = state;
			Title = title;
		}

		public override bool Equals(object obj) {
			return obj is PToolMode other && other.Key == Key;
		}

		public override int GetHashCode() {
			return Key.GetHashCode();
		}

		public override string ToString() {
			return "{0} ({1})".F(Key, Title);
		}
	}
}
