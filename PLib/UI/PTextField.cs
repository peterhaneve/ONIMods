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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeterHan.PLib.UI {
	/// <summary>
	/// A custom UI text field factory class.
	/// </summary>
	public sealed class PTextField : IDynamicSizable {
		/// <summary>
		/// The text field's background color.
		/// </summary>
		public Color BackColor { get; set; }

		/// <summary>
		/// Retrieves the built-in type used for Text Mesh Pro.
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

		public bool DynamicSize { get; set; }

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
			DynamicSize = false;
			FlexSize = Vector2.zero;
			MaxLength = 256;
			MinWidth = 32;
			Name = name ?? "TextField";
			Text = null;
			TextAlignment = TextAlignmentOptions.Center;
			TextStyle = PUITuning.Fonts.TextDarkStyle;
			ToolTip = "";
			Type = FieldType.Text;
		}

		public GameObject Build() {
			var textField = PUIElements.CreateUI(Name);
			// Background
			var style = TextStyle ?? PUITuning.Fonts.TextLightStyle;
			textField.AddComponent<Image>().color = style.textColor;
			// Text box with rectangular clipping area
			var textArea = PUIElements.CreateUI("Text Area", true, false);
			textArea.AddComponent<Image>().color = BackColor;
			PUIElements.SetParent(textArea, textField);
			var mask = textArea.AddComponent<RectMask2D>();
			// Scrollable text
			var textBox = PUIElements.CreateUI("Text");
			PUIElements.SetParent(textBox, textArea);
			// Text to display
			var textDisplay = textBox.AddComponent<TextMeshProUGUI>();
			textDisplay.alignment = TextAlignment;
			textDisplay.autoSizeTextContainer = false;
			textDisplay.enabled = true;
			textDisplay.color = style.textColor;
			textDisplay.font = style.sdfFont;
			textDisplay.fontSize = style.fontSize;
			textDisplay.fontStyle = style.style;
			textDisplay.maxVisibleLines = 1;
			// Text field itself
			var onChange = OnTextChanged;
			textField.SetActive(false);
			var textEntry = textField.AddComponent<TMP_InputField>();
			textEntry.textComponent = textDisplay;
			textEntry.textViewport = textArea.rectTransform();
			textField.SetActive(true);
			textEntry.text = Text ?? "";
			textDisplay.text = Text ?? "";
			ConfigureTextEntry(textEntry).onDeselect.AddListener((text) => {
				onChange?.Invoke(textField, (text ?? "").TrimEnd());
			});
			// Add tooltip
			if (!string.IsNullOrEmpty(ToolTip))
				textField.AddComponent<ToolTip>().toolTip = ToolTip;
			mask.enabled = true;
			// Lay out - TMP_InputField does not support auto layout so we have to do a hack
			var minSize = new Vector2(MinWidth, LayoutUtility.GetPreferredHeight(textBox.
				rectTransform()));
			textArea.SetMinUISize(minSize).SetFlexUISize(Vector2.one);
			var lp = new BoxLayoutParams() {
				Direction = PanelDirection.Horizontal, Alignment = TextAnchor.MiddleLeft,
				Margin = new RectOffset(1, 1, 1, 1)
			};
			if (DynamicSize) {
				var layout = textField.AddComponent<BoxLayoutGroup>();
				layout.Params = lp;
				layout.flexibleWidth = FlexSize.x;
				layout.flexibleHeight = FlexSize.y;
				textField.SetMinUISize(minSize);
			} else
				BoxLayoutGroup.LayoutNow(textField, lp, new Vector2(MinWidth, 0.0f)).
					SetFlexUISize(FlexSize);
			OnRealize?.Invoke(textField);
			return textField;
		}

		/// <summary>
		/// Sets up the text entry field.
		/// </summary>
		/// <param name="textEntry">The input field to configure.</param>
		/// <returns>The input field.</returns>
		private TMP_InputField ConfigureTextEntry(TMP_InputField textEntry) {
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
			// Events
			textEntry.restoreOriginalTextOnEscape = true;
			if (OnValidate != null)
				textEntry.onValidateInput = OnValidate;
			return textEntry;
		}

		/// <summary>
		/// Sets the default Klei pink style as this text field's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PTextField SetKleiPinkStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonPinkStyle.inactiveColor;
			return this;
		}

		/// <summary>
		/// Sets the default Klei blue style as this text field's color and text style.
		/// </summary>
		/// <returns>This button for call chaining.</returns>
		public PTextField SetKleiBlueStyle() {
			TextStyle = PUITuning.Fonts.UILightStyle;
			BackColor = PUITuning.Colors.ButtonBlueStyle.inactiveColor;
			return this;
		}

		public override string ToString() {
			return "PTextField[Name={0},Type={1}]".F(Name, Type);
		}

		/// <summary>
		/// The valid text field types supported by this class.
		/// </summary>
		public enum FieldType {
			Text, Integer, Float
		}
	}
}
