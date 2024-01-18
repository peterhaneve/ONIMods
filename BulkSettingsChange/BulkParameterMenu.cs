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

using PeterHan.PLib.Core;
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.BulkSettingsChange {
	/// <summary>
	/// This code was adapted with permission from 0Mayall's "Advanced Filter Menu", which
	/// is available at https://github.com/0Mayall/ONIMods/blob/master/Lib/ModMultiFiltration/MultiToolParameterMenu.cs.
	/// </summary>
	public sealed class BulkParameterMenu : KMonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static BulkParameterMenu Instance { get; private set; }

		/// <summary>
		/// Initializes the instance of this class.
		/// </summary>
		public static void CreateInstance() {
			var parameterMenu = new GameObject("SettingsChangeParams");
			var originalMenu = ToolMenu.Instance.toolParameterMenu;
			if (originalMenu != null)
				parameterMenu.transform.SetParent(originalMenu.transform.parent);
			// The content is not actually a child of tool menu's GO, so this GO can be plain
			Instance = parameterMenu.AddComponent<BulkParameterMenu>();
			parameterMenu.SetActive(true);
			parameterMenu.SetActive(false);
		}

		public static void DestroyInstance() {
			var inst = Instance;
			if (inst != null) {
				inst.ClearMenu();
				Destroy(inst.gameObject);
			}
			Instance = null;
		}

		public bool HasOptions {
			get {
				return options.Count > 0;
			}
		}

		public string SelectedKey { get; private set; }

		/// <summary>
		/// The parent of each layer checkbox.
		/// </summary>
		private GameObject choiceList;
		/// <summary>
		/// The filter menu.
		/// </summary>
		private GameObject content;
		/// <summary>
		/// The checkboxes for each layer.
		/// </summary>
		private readonly IDictionary<string, BulkMenuOption> options;

		public BulkParameterMenu() {
			options = new Dictionary<string, BulkMenuOption>();
			SelectedKey = null;
		}

		/// <summary>
		/// Removes all entries from the menu and hides it.
		/// </summary>
		public void ClearMenu() {
			HideMenu();
			foreach (var option in options)
				Destroy(option.Value.Checkbox);
			options.Clear();
			SelectedKey = null;
		}

		/// <summary>
		/// Gets the toggle state of the specified option.
		/// </summary>
		/// <param name="key">The destroy option to look up.</param>
		/// <returns>Whether that option is selected.</returns>
		public ToolParameterMenu.ToggleState GetState(string key) {
			var state = ToolParameterMenu.ToggleState.Off;
			if (options.TryGetValue(key, out BulkMenuOption option))
				state = option.State;
			return state;
		}

		/// <summary>
		/// Hides the menu without destroying it.
		/// </summary>
		public void HideMenu() {
			if (content != null)
				content.SetActive(false);
		}

		/// <summary>
		/// Updates the visible checkboxes to correspond with the layer settings.
		/// </summary>
		private void OnChange() {
			foreach (var option in options.Values) {
				var checkbox = option.Checkbox;
				switch (option.State) {
				case ToolParameterMenu.ToggleState.On:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_CHECKED);
					break;
				case ToolParameterMenu.ToggleState.Off:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_UNCHECKED);
					break;
				case ToolParameterMenu.ToggleState.Disabled:
				default:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_PARTIAL);
					break;
				}
			}
			BulkChangeTool.UpdateViewMode();
		}

		/// <summary>
		/// When an option is selected, updates the state of other options if necessary and
		/// refreshes the UI.
		/// </summary>
		/// <param name="target">The option check box that was clicked.</param>
		private void OnClick(GameObject target) {
			foreach (var option in options.Values)
				if (option.Checkbox == target) {
					if (option.State == ToolParameterMenu.ToggleState.Off) {
						// Set to on and all others to off
						foreach (var disableOption in options.Values)
							if (disableOption != option)
								disableOption.State = ToolParameterMenu.ToggleState.Off;
						option.State = ToolParameterMenu.ToggleState.On;
						SelectedKey = option.Tool.Key;
						OnChange();
					}
					break;
				}
		}

		protected override void OnCleanUp() {
			ClearMenu();
			if (content != null)
				Destroy(content);
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			var menu = ToolMenu.Instance.toolParameterMenu;
			var baseContent = menu.content;
			base.OnPrefabInit();
			content = Util.KInstantiateUI(baseContent, baseContent.GetParent(), false);
			var transform = content.rectTransform();
			// Add buttons to the chooser
			if (transform.childCount > 1)
				choiceList = transform.GetChild(1).gameObject;
			// Bump up the offset max to allow more space
			transform.offsetMax = new Vector2(0.0f, 300.0f);
			transform.SetAsFirstSibling();
			HideMenu();
		}

		/// <summary>
		/// Populates the menu with the available destroy modes.
		/// </summary>
		/// <param name="parameters">The modes to show in the menu.</param>
		internal void PopulateMenu(IEnumerable<BulkToolMode> parameters) {
			int i = 0;
			var prefab = ToolMenu.Instance.toolParameterMenu.widgetPrefab;
			ClearMenu();
			foreach (var parameter in parameters) {
				// Create prefab based on existing Klei menu
				var widgetPrefab = Util.KInstantiateUI(prefab, choiceList, true);
				PUIElements.SetText(widgetPrefab, parameter.Name);
				var toggle = widgetPrefab.GetComponentInChildren<MultiToggle>();
				if (toggle != null) {
					var checkbox = toggle.gameObject;
					// Set initial state, note that ChangeState is only called by SetCheckState
					// if it appears to be different, but since this executes before the
					// parent is active it must be set to something different
					var option = new BulkMenuOption(parameter, checkbox);
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_PARTIAL);
					if (i == 0) {
						option.State = ToolParameterMenu.ToggleState.On;
						SelectedKey = parameter.Key;
					}
					options.Add(parameter.Key, option);
					toggle.onClick += () => OnClick(checkbox);
				} else
					PUtil.LogWarning("Could not find tool menu checkbox!");
				i++;
			}
		}

		/// <summary>
		/// Sets all check boxes to the same value.
		/// </summary>
		/// <param name="toggleState">The toggle state to set.</param>
		public void SetAll(ToolParameterMenu.ToggleState toggleState) {
			foreach (var option in options)
				option.Value.State = toggleState;
			OnChange();
		}

		/// <summary>
		/// Shows the menu.
		/// </summary>
		public void ShowMenu() {
			content.SetActive(true);
			OnChange();
		}

		/// <summary>
		/// Stores available settings change tools and their current menu states.
		/// </summary>
		private sealed class BulkMenuOption {
			/// <summary>
			/// The check box in the UI.
			/// </summary>
			public GameObject Checkbox { get; }

			/// <summary>
			/// The current option state.
			/// </summary>
			public ToolParameterMenu.ToggleState State { get; set; }

			/// <summary>
			/// The tool mode representing this option.
			/// </summary>
			public BulkToolMode Tool { get; }

			public BulkMenuOption(BulkToolMode tool, GameObject checkbox) {
				Checkbox = checkbox ?? throw new ArgumentNullException(nameof(checkbox));
				Tool = tool ?? throw new ArgumentNullException(nameof(tool));
				State = ToolParameterMenu.ToggleState.Off;
			}

			public override string ToString() {
				return Tool.ToString();
			}
		}
	}
}
