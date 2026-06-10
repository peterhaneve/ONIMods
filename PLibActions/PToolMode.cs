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
	/// A tool mode used in custom tool menus. Shown in the options in the bottom right.
	/// </summary>
	public sealed class PToolMode {
		/// <summary>
		/// Sets up tool options in the tool parameter menu.
		/// </summary>
		/// <param name="menu">The menu to configure.</param>
		/// <param name="options">The available modes.</param>
		/// <returns>A list of tool options that store the real time state of each mode.</returns>
		public static PToggleDataCollection CreateMenu(ToolParameterMenu menu,
				ICollection<PToolMode> options) {
			var compat = PToolModeCompatibility.Instance;
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			compat.LazyInitializeCompatibility();
			var toggles = new PToggleDataCollection(options);
			compat.InvokePopulateMenu(menu, compat.UseNewWrapper ? toggles.
				GetToggleDataArray() : toggles.GetToggleDataDictionary());
			return toggles;
		}

		/// <summary>
		/// Sets up tool options in the tool parameter menu.
		/// </summary>
		/// <param name="menu">The menu to configure.</param>
		/// <param name="options">The available modes.</param>
		/// <returns>A dictionary which is updated in real time to contain the actual state of each mode.</returns>
		[Obsolete("Use CreateMenu instead")]
		public static IDictionary<string, ToolParameterMenu.ToggleState> PopulateMenu(
				ToolParameterMenu menu, ICollection<PToolMode> options) {
			var compat = PToolModeCompatibility.Instance;
			IDictionary<string, ToolParameterMenu.ToggleState> kOpt;
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			compat.LazyInitializeCompatibility();
			var toggles = new PToggleDataCollection(options);
			if (compat.UseNewWrapper) {
				kOpt = new ToolMenuDictionary(toggles);
				compat.InvokePopulateMenu(menu, toggles.GetToggleDataArray());
			} else {
				// TODO Remove when versions before U58-719533 no longer need to be supported
				kOpt = toggles.GetToggleDataDictionary();
				compat.InvokePopulateMenu(menu, kOpt);
			}
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

	/// <summary>
	/// Checks which game version is being used and handles compatibility with the wrapper for
	/// previous versions of PLib.
	/// </summary>
	internal sealed class PToolModeCompatibility {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static PToolModeCompatibility Instance { get; } = new PToolModeCompatibility();

		internal static readonly Type TOGGLE_DATA = PPatchTools.GetTypeSafe(
			nameof(ToolParameterMenu.ToggleData));

		internal bool UseNewWrapper => TOGGLE_DATA != null;
		
		private volatile bool compatibilityProbed;

		private FieldInfo getState;

		private MethodInfo populateMenu;

		internal PToolModeCompatibility() {
			compatibilityProbed = false;
			getState = null;
			populateMenu = null;
		}

		internal void InvokePopulateMenu(ToolParameterMenu menu, object parameter) {
			populateMenu?.Invoke(menu, new object[] { parameter });
		}

		internal ToolParameterMenu.ToggleState GetState(object kToggleData) {
			var result = ToolParameterMenu.ToggleState.Off;
			if (kToggleData == null)
				throw new ArgumentNullException(nameof(kToggleData));
			if (getState == null)
				throw new ArgumentException("Not initialized");
			if (getState.GetValue(kToggleData) is ToolParameterMenu.ToggleState toggleState)
				result = toggleState;
			return result;
		}

		/// <summary>
		/// When first used, probes for the compatibility mode to use on ToolParameterMenu and
		/// sets the correct 
		/// </summary>
		internal void LazyInitializeCompatibility() {
			lock (this) {
				if (!compatibilityProbed) {
					MethodInfo target = null;
					var targets = typeof(ToolParameterMenu).GetMethods(PPatchTools.BASE_FLAGS |
						BindingFlags.Instance);
					foreach (var method in targets)
						if (method.Name == nameof(ToolParameterMenu.PopulateMenu)) {
							target = method;
							break;
						}
					populateMenu = target;
					getState = TOGGLE_DATA?.GetFieldSafe(nameof(ToolParameterMenu.
						ToggleData.state), false);
					compatibilityProbed = true;
				}
			}
		}
	}
}
