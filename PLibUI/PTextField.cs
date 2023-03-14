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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI text field factory class.
	/// </summary>
	public sealed class PTextField : IUIComponent {
		/// <summary>
		/// Configures a Text Mesh Pro field.
		/// </summary>
		/// <param name="component">The text component to configure.</param>
		/// <param name="style">The desired text color, font, and style.</param>
		/// <param name="alignment">The text alignment.</param>
		/// <returns>The component, for call chaining.</returns>
		internal static TextMeshProUGUI ConfigureField(TextMeshProUGUI component,
				TextStyleSetting style, TextAlignmentOptions alignment) {
			component.alignment = alignment;
			component.autoSizeTextContainer = false;
			component.enabled = true;
			component.color = style.textColor;
			component.font = style.sdfFont;
			component.fontSize = style.fontSize;
			component.fontStyle = style.style;
			component.overflowMode = TextOverflowModes.Overflow;
			return component;
		}

		/// <summary>
		/// Gets a text field's text.
		/// </summary>
		/// <param name="textField">The UI element to retrieve.</param>
		/// <returns>The current text in the field.</returns>
		public static string GetText(GameObject textField) {
			if (textField == null)
				throw new ArgumentNullException(nameof(textField));
			return textField.TryGetComponent(out TMP_InputField field) ? field.text : "";
		}

		/// <summary>
		/// The text field's background color.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// Retrieves the built-in field type used for Text Mesh Pro.
		/// </summary>
		private TMP_InputField.ContentType ContentType {
			get {
				TMP_InputField.ContentType cType;
				switch (Type) {
				case FieldType.Float:
					cType = TMP_InputField.ContentType.DecimalNumber;
					break;
				case FieldType.Integer:
					cType = TMP_InputField.ContentType.IntegerNumber;
					break;
				case FieldType.Text:
				default:
					cType = TMP_InputField.ContentType.Standard;
					break;
				}
				return cType;
			}
		}

		/// <summary>
		/// The flexible size bounds of this component.
		/// </summary>
		public Vector2 FlexSize { get; set; }

		/// <summary>
		/// The maximum number of characters in this text field.
		/// </summary>
		public int MaxLength { get; set; }

		/// <summary>
		/// The minimum width in units (not characters!) of this text field.
		/// </summary>
		public int MinWidth { get; set; }

		/// <summary>
		/// The placeholder text style (including color, font, and word wrap settings) if the
		/// field is empty.
		/// </summary>
		public TextStyleSetting PlaceholderStyle { get; set; }

		/// <summary>
		/// The placeholder text if the field is empty.
		/// </summary>
		public string PlaceholderText { get; set; }

		public string Name { get; }

		/// <summary>
		/// The text alignment in the text field.
		/// </summary>
		public TextAlignmentOptions TextAlignment { get; set; }

		/// <summary>
		/// The initial text in the text field.
		/// </summary>
		public string Text { get; set; }

		/// <summary>
		/// The text field's text color, font, word wrap settings, and font size.
		/// </summary>
		public TextStyleSetting TextStyle { get; set; }

		/// <summary>
		/// The tool tip text.
		/// </summary>
		public string ToolTip { get; set; }

		/// <summary>
		/// The field type.
		/// </summary>
		public FieldType Type { get; set; }

		public event PUIDelegates.OnRealize OnRealize;

		/// <summary>
		/// The action to trigger on text change. It is passed the realized source object.
		/// </summary>
		public PUIDelegates.OnTextChanged OnTextChanged { get; set; }

		/// <summary>
		/// The callback to invoke when validating input.
		/// </summary>
		public TMP_InputField.OnValidateInput OnValidate { get; set; }

		public PTextField() : this(null) { }

		public PTextField(string name) {
			BackColor = PUITuning.Colors.BackgroundLight;
			FlexSize = Vector2.zero;
			MaxLength = 256;
			MinWidth = 32;
			Name = name ?? "TextField";
			PlaceholderText = null;
			Text = null;
			TextAlignment = TextAlignmentOptions.Center;
			TextStyle = PUITuning.Fonts.TextDarkStyle;
			PlaceholderStyle = TextStyle;
			ToolTip = "";
			Type = FieldType.Text;
		}

		/// <summary>
		/// Adds a handler when this text field is realized.
		/// </summary>
		/// <param name="onRealize">The handler to invoke on realization.</param>
		/// <returns>This text field for call chaining.</returns>
		public PTextField AddOnRealize(PUIDelegates.OnRealize onRealize) {
			OnRealize += onRealize;
			return this;
		}

		public GameObject Build() {
			var textField = PUIElements.CreateUI(null, Name);
			var style = TextStyle ?? PUITuning.Fonts.TextLightStyle;
			// Background
			var border = textField.AddComponent<Image>();
			border.sprite = PUITuning.Images.BoxBorderWhite;
			border.type = Image.Type.Sliced;
			border.color = style.textColor;
			// Text box with rectangular clipping area
			var textArea = PUIElements.CreateUI(textField, "Text Area", false);
			textArea.AddComponent<Image>().color = BackColor;
			var mask = textArea.AddComponent<RectMask2D>();
			// Scrollable text
			var textBox = PUIElements.CreateUI(textArea, "Text");
			// Text to display
			var textDisplay = ConfigureField(textBox.AddComponent<TextMeshProUGUI>(), style,
				TextAlignment);
			textDisplay.enableWordWrapping = false;
			textDisplay.maxVisibleLines = 1;
			textDisplay.raycastTarget = true;
			// Text field itself
			textField.SetActive(false);
			var textEntry = textField.AddComponent<TMP_InputField>();
			textEntry.textComponent = textDisplay;
			textEntry.textViewport = textArea.rectTransform();
			textEntry.text = Text ?? "";
			textDisplay.text = Text ?? "";
			// Placeholder
			if (PlaceholderText != null) {
				var placeholder = PUIElements.CreateUI(textArea, "Placeholder Text");
				var textPlace = ConfigureField(placeholder.AddComponent<TextMeshProUGUI>(),
					PlaceholderStyle ?? style, TextAlignment);
				textPlace.maxVisibleLines = 1;
				textPlace.text = PlaceholderText;
				textEntry.placeholder = textPlace;
			}
			// Events!
			ConfigureTextEntry(textEntry);
			var events = textField.AddComponent<PTextFieldEvents>();
			events.OnTextChanged = OnTextChanged;
			events.OnValidate = OnValidate;
			events.TextObject = textBox;
			// Add tooltip
			PUIElements.SetToolTip(textField, ToolTip);
			mask.enabled = true;
			PUIElements.SetAnchorOffsets(textBox, new RectOffset());
			textField.SetActive(true);
			// Lay out
			var rt = textBox.rectTransform();
			LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
			var layout = PUIUtils.InsetChild(textField, textArea, Vector2.one, new Vector2(
				MinWidth, LayoutUtility.GetPreferredHeight(rt))).AddOrGet<LayoutElement>();
			layout.flexibleWidth = FlexSize.x;
			layout.flexibleHeight = FlexSize.y;
			OnRealize?.Invoke(textField);
			return textField;
		}

		/// <summary>
		/// Sets up the text entry field.
		/// </summary>
		/// <param name="textEntry">The input field to configure.</param>
		private void ConfigureTextEntry(TMP_InputField textEntry) {
			textEntry.characterLimit = Math.Max(1, MaxLength);
			textEntry.contentType = ContentType;
			textEntry.enabled = true;
			textEntry.inputType = TMP_InputField.InputType.Standard;
			textEntry.interactable = true;
			textEntry.isRichTextEditingAllowed = false;
			textEntry.keyboardType = TouchScreenKeyboardType.Default;
			textEntry.lineType = TMP_InputField.LineType.SingleLine;
			textEntry.navigation = Navigation.defaultNavigation;
			textEntry.richText = false;
			textEntry.selectionColor = PUITuning.Colors.SelectionBackground;
			textEntry.transition = Selectable.Transition.None;
			textEntry.restoreOriginalTextOnEscape = true;
		}

		/// <summary>
		/// Sets the default Klei pink style as this text field's color and text style.
		/// </summary>
		/// <returns>This text field for call chaining.</returns>
		public PTextField SetKleiPinkStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue style as this text field's color and text style.
		/// </summary>
		/// <returns>This text field for call chaining.</returns>
		public PTextField SetKleiBlueStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the minimum (and preferred) width of this text field in characters.
		/// 
		/// The width is computed using the currently selected text style.
		/// </summary>
		/// <param name="chars">The number of characters to be displayed.</param>
		/// <returns>This text field for call chaining.</returns>
		public PTextField SetMinWidthInCharacters(int chars) {
			int width = Mathf.RoundToInt(chars * PUIUtils.GetEmWidth(TextStyle));
			if (width > 0)
				MinWidth = width;
			return this;
		}

		public override string ToString() {
			return string.Format("PTextField[Name={0},Type={1}]", Name, Type);
		}

		/// <summary>
		/// The valid text field types supported by this class.
		/// </summary>
		public enum FieldType {
			Text, Integer, Float
		}
	}
}
