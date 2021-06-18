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

using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// A mode that the bulk item change tool can use.
	/// </summary>
	sealed class BulkToolMode {
		/// <summary>
		/// A dictionary to look tool modes up by key.
		/// </summary>
		private static readonly IDictionary<string, BulkToolMode> MODES = new Dictionary<
			string, BulkToolMode>(10);

		/// <summary>
		/// Retrieves a collection of all tool modes.
		/// </summary>
		/// <returns>A collection of all modes that the bulk change tool supports.</returns>
		public static IEnumerable<BulkToolMode> AllTools() {
			InitDictionary();
			return MODES.Values;
		}

		/// <summary>
		/// Initializes the dictionary of tools if it is not already populated.
		/// </summary>
		private static void InitDictionary() {
			lock (MODES) {
				if (MODES.Count < 1) {
					// Initialize dictionary
					BulkChangeTools.DisableBuildings.AddTo(MODES);
					BulkChangeTools.EnableBuildings.AddTo(MODES);
					BulkChangeTools.DisableDisinfect.AddTo(MODES);
					BulkChangeTools.EnableDisinfect.AddTo(MODES);
					BulkChangeTools.DisableRepair.AddTo(MODES);
					BulkChangeTools.EnableRepair.AddTo(MODES);
					BulkChangeTools.DisableCompost.AddTo(MODES);
					BulkChangeTools.EnableCompost.AddTo(MODES);
					BulkChangeTools.DisableEmpty.AddTo(MODES);
					BulkChangeTools.EnableEmpty.AddTo(MODES);
#if DEBUG
					foreach (var mode in MODES)
						PUtil.LogDebug("Tool mode: " + mode.Value);
#endif
				}
			}
		}

		/// <summary>
		/// Retrieves the bulk change tool mode based on the key used in the UI.
		/// </summary>
		/// <param name="key">The tool key.</param>
		/// <returns>The tool used.</returns>
		public static BulkToolMode FromKey(string key) {
			InitDictionary();
			MODES.TryGetValue(key, out BulkToolMode mode);
			return mode;
		}

		/// <summary>
		/// The internal key used to reference the tool.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// The name shown in the options menu.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The pop-up text to display when the action is performed.
		/// </summary>
		public string PopupText { get; }

		/// <summary>
		/// The title shown to the user when dragging the tool.
		/// </summary>
		public string Title {
			get {
				return Name.ToUpper();
			}
		}

		public BulkToolMode(string key, LocString name, LocString popup) {
			Key = key ?? throw new ArgumentNullException("key");
			Name = name ?? throw new ArgumentNullException("name");
			PopupText = popup ?? throw new ArgumentNullException("popup");
		}

		/// <summary>
		/// Adds this tool to the tool list.
		/// </summary>
		/// <param name="tools">The location where the tool should be added.</param>
		private void AddTo(IDictionary<string, BulkToolMode> tools) {
			tools.Add(Key, this);
		}

		/// <summary>
		/// Returns true if this mode is selected.
		/// </summary>
		/// <param name="options">The current tool options.</param>
		/// <returns>true if this mode is selected, or false otherwise.</returns>
		public bool IsOn(IDictionary<string, ToolParameterMenu.ToggleState> options) {
			return options[Key] == ToolParameterMenu.ToggleState.On;
		}

		/// <summary>
		/// Creates a tool mode from PLib to add it to the options menu.
		/// </summary>
		/// <param name="state">The default state of this tool mode.</param>
		/// <returns>The options entry.</returns>
		public PToolMode ToToolMode(ToolParameterMenu.ToggleState state = ToolParameterMenu.
				ToggleState.On) {
			return new PToolMode(Key, Name, state);
		}

		public override string ToString() {
			return "{0} ({1})".F(Key, Name);
		}
	}
}
