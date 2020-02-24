/*
 * Copyright 2020 Peter Han
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI combo box factory class.
	/// </summary>
	public sealed class PComboBox<T> : IDynamicSizable where T : class, IListableOption {
		/// <summary>
		/// The default margin around items in the pulldown.
		/// </summary>
		private static readonly RectOffset DEFAULT_ITEM_MARGIN = new RectOffset(3, 3, 3, 3);

		/// <summary>
		/// Sets the selected option in a realized combo box.
		/// </summary>
		/// <param name="realized">The realized combo box.</param>
		/// <param name="option">The option to set.</param>
		/// <param name="fireListener">true to fire the on select listener, or false otherwise.</param>
		public static void SetSelectedItem(GameObject realized, IListableOption option,
				bool fireListener = false) {
			if (option != null && realized != null)
				realized.GetComponent<PComboBoxComponent>()?.SetSelectedItem(option,
					fireListener);
		}

		/// <summary>
		/// The size of the sprite used to expand/contract the options.
		/// </summary>
		public Vector2 ArrowSize { get; set; }

		/// <summary>
		/// The combo box's background color.
		/// </summary>
		public ColorStyleSetting BackColor { get; set; }

		/// <summary>
		/// The content of this combo box.
		/// </summary>
		public IEnumerable<T> Content { get; set; }

		public bool DynamicSize { get; set; }

		/// <summary>
		/// The background color for each entry in the combo box pulldown.
		/// </summary>
		public ColorStyleSetting EntryColor { get; set; }

		/// <summary>
		/// The flexible size bounds of this combo box.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The initially selected item of the combo box.
		/// </summary>
		public T InitialItem { get; set; }

		/// <summary>
		/// The margin around each item in the pulldown.
		/// </summary>
		public RectOffset ItemMargin { get; set; }

		/// <summary>
		/// The margin around the component.
		/// </summary>
		public RectOffset Margin { get; set; }

		public string Name { get; }

		/// <summary>
		/// The action to trigger when an item is selected. It is passed the realized source
		/// object.
		/// </summary>
		public PUIDelegates.OnDropdownChanged<T> OnOptionSelected { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		/// <summary>
		/// The text alignment in the combo box.
		/// </summary>
		public TextAnchor TextAlignment { get; set; }

		/// <summary>
		/// The combo box's text color, font, word wrap settings, and font size.
		/// </summary>
		public TextStyleSetting TextStyle { get; set; }

		/// <summary>
		/// The tool tip text.
		/// </summary>
		public string ToolTip { get; set; }

		public PComboBox() : this("Dropdown") { }

		public PComboBox(string name) {
			ArrowSize = new Vector2(8.0f, 8.0f);
			BackColor = null;
			Content = null;
			DynamicSize = false;
			FlexSize = Vector2.zero;
			InitialItem = null;
			ItemMargin = DEFAULT_ITEM_MARGIN;
			Margin = PButton.BUTTON_MARGIN;
			Name = name;
			TextAlignment = TextAnchor.MiddleLeft;
			TextStyle = null;
			ToolTip = null;
		}

		public GameObject Build() {
			var combo = PUIElements.CreateUI(null, Name);
			var style = TextStyle ?? PUITuning.Fonts.UILightStyle;
			var entryColor = EntryColor ?? PUITuning.Colors.ButtonBlueStyle;
			RectOffset margin = Margin, im = ItemMargin;
			// Background color
			var bgImage = combo.AddComponent<KImage>();
			bgImage.colorStyleSetting = BackColor ?? PUITuning.Colors.ButtonBlueStyle;
			PButton.SetupButtonBackground(bgImage);
			// Need a LocText (selected item)
			var selection = PUIElements.CreateUI(combo, "SelectedItem");
			var selectedLabel = PUIElements.AddLocText(selection, style);
			// Vertical flow panel with the choices
			var contentContainer = PUIElements.CreateUI(null, "Content");
			contentContainer.AddComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
			// Scroll pane with items is laid out below everything else
			var pullDown = new PScrollPane("PullDown") {
				ScrollHorizontal = false, ScrollVertical = true, AlwaysShowVertical = true,
				FlexSize = Vector2.right, TrackSize = 8.0f, BackColor = entryColor.
				inactiveColor
			}.BuildScrollPane(combo, contentContainer);
			pullDown.rectTransform().pivot = new Vector2(0.5f, 1.0f);
			// Initialize the drop down
			var comboBox = combo.AddComponent<PComboBoxComponent>();
			comboBox.ContentContainer = contentContainer.rectTransform();
			comboBox.EntryPrefab = BuildRowPrefab(style, entryColor);
			comboBox.Pulldown = pullDown;
			comboBox.SelectedLabel = selectedLabel;
			comboBox.SetItems(Content);
			comboBox.SetSelectedItem(InitialItem);
			comboBox.OnSelectionChanged = (obj, item) => OnOptionSelected?.Invoke(obj.
				gameObject, item as T);
			// Inner component with the pulldown image
			var image = PUIElements.CreateUI(combo, "OpenImage");
			var icon = image.AddComponent<Image>();
			icon.sprite = PUITuning.Images.Contract;
			icon.color = style.textColor;
			// Button component
			var dropButton = combo.AddComponent<KButton>();
			PButton.SetupButton(dropButton, bgImage);
			dropButton.fgImage = icon;
			dropButton.onClick += comboBox.OnClick;
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				combo.AddComponent<ToolTip>().toolTip = ToolTip;
			combo.SetActive(true);
			// Button gets laid out on the right, rest of space goes to the label
			// Scroll pane is laid out on the bottom
			new RelativeLayout(combo).AnchorYAxis(selection).SetLeftEdge(selection, fraction:
				0.0f).SetRightEdge(selection, toLeft: image).AnchorYAxis(image).SetRightEdge(
				image, fraction: 1.0f).SetMargin(selection, new RectOffset(margin.left,
				im.right, margin.top, margin.bottom)).SetMargin(image, new RectOffset(0,
				margin.right, margin.top, margin.bottom)).OverrideSize(image, ArrowSize).
				AnchorYAxis(pullDown, 0.0f).OverrideSize(pullDown, new Vector2(100.0f, 1.0f)).Execute(true);
			// Scroll pane is hidden right away
			pullDown.SetActive(false);
			combo.SetFlexUISize(FlexSize);
			OnRealize?.Invoke(combo);
			return combo;
		}

		/// <summary>
		/// Builds a row selection prefab object for this combo box.
		/// </summary>
		/// <param name="style">The text style for the entries.</param>
		/// <param name="entryColor">The color for the entry backgrounds.</param>
		/// <returns>A template for each row in the dropdown.</returns>
		private GameObject BuildRowPrefab(TextStyleSetting style, ColorStyleSetting entryColor)
		{
			var im = ItemMargin;
			var rowPrefab = PUIElements.CreateUI(null, "RowEntry");
			// Background of the entry
			var bgImage = rowPrefab.AddComponent<KImage>();
			bgImage.colorStyleSetting = entryColor;
			bgImage.ApplyColorStyleSetting();
			// Checkmark for the front of the entry
			var isSelected = PUIElements.CreateUI(rowPrefab, "Selected");
			var fgImage = isSelected.AddComponent<Image>();
			fgImage.color = Color.white;
			fgImage.sprite = PUITuning.Images.Checked;
			// Button for the entry to select it
			var entryButton = rowPrefab.AddComponent<KButton>();
			PButton.SetupButton(entryButton, bgImage);
			entryButton.fgImage = fgImage;
			// Text for the entry
			var textContainer = PUIElements.CreateUI(rowPrefab, "Text");
			PUIElements.AddLocText(textContainer, style).SetText(" ");
			// Configure the entire layout in 1 statement! (jk this is awful)
			new RelativeLayout(rowPrefab).AnchorYAxis(isSelected).OverrideSize(
				isSelected, new Vector2(12.0f, 12.0f)).SetLeftEdge(isSelected, fraction: 0.0f).
				SetMargin(isSelected, im).AnchorYAxis(textContainer).SetLeftEdge(textContainer,
				toRight: isSelected).SetRightEdge(textContainer, 1.0f).SetMargin(textContainer,
				new RectOffset(0, im.right, im.top, im.bottom)).Execute(true);
			rowPrefab.SetActive(false);
			return rowPrefab;
		}

		public override string ToString() {
			return "PDropdown[Name={0}]".F(Name);
		}
	}
}
