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

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// An improved variant of DropDown/Dropdown.
	/// </summary>
	internal sealed class PComboBoxComponent : KMonoBehaviour {
		/// <summary>
		/// The container where the combo box items will be placed.
		/// </summary>
		internal RectTransform ContentContainer { get; set; }

		/// <summary>
		/// The color for the checkbox if it is selected.
		/// </summary>
		internal Color CheckColor { get; set; }

		/// <summary>
		/// The prefab used to display each row.
		/// </summary>
		internal GameObject EntryPrefab { get; set; }

		/// <summary>
		/// The maximum number of rows to be shown.
		/// </summary>
		internal int MaxRowsShown { get; set; }

		/// <summary>
		/// Called when an item is selected.
		/// </summary>
		internal Action<PComboBoxComponent, IListableOption> OnSelectionChanged { get; set; }

		/// <summary>
		/// The object which contains the pull down section of the combo box.
		/// </summary>
		internal GameObject Pulldown { get; set; }

		/// <summary>
		/// The selected label.
		/// </summary>
		internal TMP_Text SelectedLabel { get; set; }

		/// <summary>
		/// The items which are currently shown in this combo box.
		/// </summary>
		private readonly IList<ComboBoxItem> currentItems;

		/// <summary>
		/// The currently active mouse event handler, or null if not yet configured.
		/// </summary>
		private MouseEventHandler handler;

		/// <summary>
		/// Whether the combo box is expanded.
		/// </summary>
		private bool open;

		internal PComboBoxComponent() {
			CheckColor = Color.white;
			currentItems = new List<ComboBoxItem>(32);
			handler = null;
			MaxRowsShown = 8;
			open = false;
		}

		/// <summary>
		/// Closes the pulldown. The selected choice is not saved.
		/// </summary>
		public void Close() {
			Pulldown?.SetActive(false);
			open = false;
		}

		/// <summary>
		/// Triggered when the combo box is clicked.
		/// </summary>
		public void OnClick() {
			if (open)
				Close();
			else
				Open();
		}

		protected override void OnPrefabInit() {
			base.OnPrefabInit();
			handler = gameObject.AddOrGet<MouseEventHandler>();
		}

		/// <summary>
		/// Opens the pulldown.
		/// </summary>
		public void Open() {
			var pdn = Pulldown;
			if (pdn != null) {
				float rowHeight = 0.0f;
				int itemCount = currentItems.Count, rows = Math.Min(MaxRowsShown, itemCount);
				var canvas = pdn.AddOrGet<Canvas>();
				pdn.SetActive(true);
				// Calculate desired height of scroll pane
				if (itemCount > 0) {
					var rt = currentItems[0].rowInstance.rectTransform();
					LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
					rowHeight = LayoutUtility.GetPreferredHeight(rt);
				}
				// Update size and enable/disable scrolling if not needed
				pdn.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
					rows * rowHeight);
				if (pdn.TryGetComponent(out ScrollRect sp))
					sp.vertical = itemCount >= MaxRowsShown;
				// Move above normal UI elements but below the tooltips
				if (canvas != null) {
					canvas.overrideSorting = true;
					canvas.sortingOrder = 2;
				}
			}
			open = true;
		}

		/// <summary>
		/// Sets the items which will be shown in this combo box.
		/// </summary>
		/// <param name="items">The items to show.</param>
		public void SetItems(IEnumerable<IListableOption> items) {
			if (items != null) {
				var content = ContentContainer;
				var pdn = Pulldown;
				int n = content.childCount;
				var prefab = EntryPrefab;
				bool wasOpen = open;
				if (wasOpen)
					Close();
				// Destroy only destroys it at end of frame!
				for (int i = 0; i < n; i++)
					Destroy(content.GetChild(i));
				currentItems.Clear();
				foreach (var item in items) {
					string tooltip = "";
					var rowInstance = Util.KInstantiate(prefab, content.gameObject);
					// Update the text shown
					rowInstance.GetComponentInChildren<TextMeshProUGUI>().SetText(item.
						GetProperName());
					// Apply the listener for the button
					var button = rowInstance.GetComponentInChildren<KButton>();
					button.ClearOnClick();
					button.onClick += () => {
						SetSelectedItem(item, true);
						Close();
					};
					// Assign the tooltip if possible
					if (item is ITooltipListableOption extended)
						tooltip = extended.GetToolTipText();
					if (rowInstance.TryGetComponent(out ToolTip tt)) {
						if (string.IsNullOrEmpty(tooltip))
							tt.ClearMultiStringTooltip();
						else
							tt.SetSimpleTooltip(tooltip);
					}
					rowInstance.SetActive(true);
					currentItems.Add(new ComboBoxItem(item, rowInstance));
				}
				SelectedLabel?.SetText((currentItems.Count > 0) ? currentItems[0].data.
					GetProperName() : "");
				if (wasOpen)
					Open();
			}
		}

		/// <summary>
		/// Sets the selected item in the combo box.
		/// </summary>
		/// <param name="option">The option that was chosen.</param>
		/// <param name="fireListener">true to also fire the option selected listener, or false otherwise.</param>
		public void SetSelectedItem(IListableOption option, bool fireListener = false) {
			if (option != null) {
				SelectedLabel?.SetText(option.GetProperName());
				// No guarantee that the options are hashable
				foreach (var item in currentItems) {
					var data = item.data;
					// Show or hide the check mark next to the selected option
					item.rowImage.color = (data != null && data.Equals(option)) ? CheckColor :
						PUITuning.Colors.Transparent;
				}
				if (fireListener)
					OnSelectionChanged?.Invoke(this, option);
			}
		}

		/// <summary>
		/// Called each frame by Unity, checks to see if the user clicks/scrolls outside of
		/// the dropdown while open, and closes it if so.
		/// </summary>
		internal void Update() {
			if (open && handler != null && !handler.IsOver && (Input.GetMouseButton(0) ||
					Input.GetAxis("Mouse ScrollWheel") != 0.0f))
				Close();
		}

		/// <summary>
		/// The items in a combo box, paired with the game object owning that row and the
		/// object that goes there.
		/// </summary>
		private struct ComboBoxItem {
			public readonly IListableOption data;
			public readonly Image rowImage;
			public readonly GameObject rowInstance;

			public ComboBoxItem(IListableOption data, GameObject rowInstance) {
				this.data = data;
				this.rowInstance = rowInstance;
				rowImage = rowInstance.GetComponentInChildrenOnly<Image>();
			}
		}

		/// <summary>
		/// Handles mouse events on the pulldown.
		/// </summary>
		private sealed class MouseEventHandler : MonoBehaviour, IPointerEnterHandler,
				IPointerExitHandler {
			/// <summary>
			/// Whether the mouse is over this component.
			/// </summary>
			public bool IsOver { get; private set; }

			internal MouseEventHandler() {
				IsOver = true;
			}

			public void OnPointerEnter(PointerEventData data) {
				IsOver = true;
			}

			public void OnPointerExit(PointerEventData data) {
				IsOver = false;
			}
		}
	}
}
