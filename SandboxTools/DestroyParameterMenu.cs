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
using PeterHan.PLib.UI;
using System.Collections.Generic;
using UnityEngine;

using ToolToggleState = ToolParameterMenu.ToggleState;

namespace PeterHan.SandboxTools {
	/// <summary>
	/// This code was adapted with permission from 0Mayall's "Advanced Filter Menu", which
	/// is available at https://github.com/0Mayall/ONIMods/blob/master/Lib/ModMultiFiltration/MultiToolParameterMenu.cs.
	/// </summary>
	public sealed class DestroyParameterMenu : KMonoBehaviour {
		/// <summary>
		/// The singleton instance of this class.
		/// </summary>
		public static DestroyParameterMenu Instance { get; private set; }

		/// <summary>
		/// Initializes the instance of this class.
		/// </summary>
		public static void CreateInstance() {
			var parameterMenu = new GameObject("DestroyParams");
			var originalMenu = ToolMenu.Instance.toolParameterMenu;
			if (originalMenu != null)
				parameterMenu.transform.SetParent(originalMenu.transform.parent);
			Instance = parameterMenu.AddComponent<DestroyParameterMenu>();
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

		public bool AllSelected { get; private set; }

		public bool HasOptions => options.Count > 0;

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
		private readonly IDictionary<string, DestroyMenuOption> options;

		public DestroyParameterMenu() {
			AllSelected = false;
			options = new Dictionary<string, DestroyMenuOption>();
		}

		/// <summary>
		/// Removes all entries from the menu and hides it.
		/// </summary>
		public void ClearMenu() {
			HideMenu();
			foreach (var option in options)
				Destroy(option.Value.Checkbox);
			options.Clear();
		}

		/// <summary>
		/// Gets the toggle state of the specified option.
		/// </summary>
		/// <param name="key">The destroy option to look up.</param>
		/// <returns>Whether that option is selected.</returns>
		public ToolToggleState GetState(string key) {
			var state = ToolToggleState.Off;
			if (options.TryGetValue(key, out var option))
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
			bool all = true;
			foreach (var option in options.Values) {
				var checkbox = option.Checkbox;
				switch (option.State) {
				case ToolToggleState.On:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_CHECKED);
					break;
				case ToolToggleState.Off:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_UNCHECKED);
					all = false;
					break;
				case ToolToggleState.Disabled:
				default:
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_PARTIAL);
					all = false;
					break;
				}
			}
			AllSelected = all;
		}

		/// <summary>
		/// When an option is selected, updates the state of other options if necessary and
		/// refreshes the UI.
		/// </summary>
		/// <param name="target">The option check box that was clicked.</param>
		private void OnClick(GameObject target) {
			foreach (var option in options.Values)
				if (option.Checkbox == target) {
					var value = option.State;
					if (value != ToolToggleState.Disabled) {
						if (SandboxToolsPatches.AdvancedFilterEnabled)
							// Flip the state
							option.State = value == ToolToggleState.On ? ToolToggleState.Off :
								ToolToggleState.On;
						else if (value == ToolToggleState.Off) {
							// Set to on and all others to off
							foreach (var disableOption in options.Values)
								if (disableOption != option)
									disableOption.State = ToolToggleState.Off;
							option.State = ToolToggleState.On;
						}
						OnChange();
					}
					break;
				}
		}

		protected override void OnCleanUp() {
			ClearMenu();
			base.OnCleanUp();
		}

		protected override void OnPrefabInit() {
			var menu = ToolMenu.Instance.toolParameterMenu;
			var baseContent = menu.content;
			base.OnPrefabInit();
			content = Util.KInstantiateUI(baseContent, baseContent.GetParent());
			var t = content.rectTransform();
			// Add buttons to the chooser
			if (t.childCount > 1)
				choiceList = t.GetChild(1).gameObject;
			if (SandboxToolsPatches.AdvancedFilterEnabled) {
				// Selects all options
				var allButton = new PButton {
					Text = "All", OnClick = _ => SetAll(ToolToggleState.On)
				}.SetKleiPinkStyle();
				// Deselects all options
				var noneButton = new PButton {
					Text = "None", OnClick = _ => SetAll(ToolToggleState.Off)
				};
				new PRelativePanel {
					BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor
				}.AddChild(allButton).AddChild(noneButton).SetLeftEdge(allButton,
					fraction: 0.0f).SetRightEdge(allButton, fraction: 0.5f).SetLeftEdge(
					noneButton, toRight: allButton).SetRightEdge(noneButton, fraction: 1.0f).
					AddTo(content, -1);
			}
			// Bump up the offset max to allow more space
			t.offsetMax = new Vector2(0.0f, 300.0f);
			t.SetAsFirstSibling();
			HideMenu();
		}

		/// <summary>
		/// Populates the menu with the available destroy modes.
		/// </summary>
		/// <param name="parameters">The modes to show in the menu.</param>
		internal void PopulateMenu(IEnumerable<DestroyFilter> parameters) {
			int i = 0;
			var prefab = ToolMenu.Instance.toolParameterMenu.widgetPrefab;
			ClearMenu();
			foreach (var parameter in parameters) {
				// Create prefab based on existing Klei menu
				var widgetPrefab = Util.KInstantiateUI(prefab, choiceList, true);
				PUIElements.SetText(widgetPrefab, parameter.Title);
				var toggle = widgetPrefab.GetComponentInChildren<MultiToggle>();
				if (toggle != null) {
					var checkbox = toggle.gameObject;
					// Set initial state, note that ChangeState is only called by SetCheckState
					// if it appears to be different, but since this executes before the
					// parent is active it must be set to something different
					var option = new DestroyMenuOption(checkbox);
					PCheckBox.SetCheckState(checkbox, PCheckBox.STATE_PARTIAL);
					if (i == 0 || SandboxToolsPatches.AdvancedFilterEnabled)
						option.State = ToolToggleState.On;
					options.Add(parameter.ID, option);
					toggle.onClick += () => OnClick(checkbox);
				} else
					PUtil.LogWarning("Could not find destroy menu checkbox!");
				i++;
			}
		}

		/// <summary>
		/// Sets all check boxes to the same value.
		/// </summary>
		/// <param name="toggleState">The toggle state to set.</param>
		public void SetAll(ToolToggleState toggleState) {
			foreach (var option in options)
				option.Value.State = toggleState;
			OnChange();
		}

		/// <summary>
		/// Sets all check boxes to off except for the specified one, which will be on.
		/// </summary>
		/// <param name="id">The check box ID to enable.</param>
		public void SetTo(string id) {
			if (options.ContainsKey(id)) {
				foreach (var option in options) {
					var checkbox = option.Value;
					if (checkbox.State != ToolToggleState.Disabled)
						checkbox.State = option.Key == id ? ToolToggleState.On :
							ToolToggleState.Off;
				}
				OnChange();
			}
		}

		/// <summary>
		/// Shows the menu.
		/// </summary>
		public void ShowMenu() {
			content.SetActive(true);
			OnChange();
		}

		/// <summary>
		/// Stores filters and their current menu states.
		/// </summary>
		private sealed class DestroyMenuOption {
			/// <summary>
			/// The check box in the UI.
			/// </summary>
			public GameObject Checkbox { get; }
			
			/// <summary>
			/// The current option state.
			/// </summary>
			public ToolToggleState State { get; set; }

			public DestroyMenuOption(GameObject checkbox) {
				Checkbox = checkbox;
				State = ToolToggleState.Off;
			}
		}
	}
}
