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
using System.Reflection;
using PeterHan.PLib.UI;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry which represents Enum and displays a spinner with text options.
	/// </summary>
	internal sealed class SelectOneOptionsEntry : OptionsEntry {
		/// <summary>
		/// Obtains the title and tool tip for an enumeration value.
		/// </summary>
		/// <param name="enumValue">The value in the enumeration.</param>
		/// <param name="fieldType">The type of the Enum field.</param>
		/// <returns>The matching Option</returns>
		private static Option GetAttribute(object enumValue, Type fieldType) {
			if (enumValue == null)
				throw new ArgumentNullException("enumValue");
			string valueName = enumValue.ToString(), title = valueName, tooltip = "";
			foreach (var enumField in fieldType.GetMember(valueName, BindingFlags.Public |
						BindingFlags.Static))
				if (enumField.DeclaringType == fieldType) {
					OptionAttribute oa = null;
					// Search for OptionsAttribute
					foreach (var attrib in enumField.GetCustomAttributes(false))
						if ((oa = GetTitle(attrib)) != null)
							break;
					// If not found, use the default
					if (oa != null) {
						title = oa.Title;
						tooltip = oa.Tooltip;
					}
					break;
				}
			return new Option(title, tooltip, enumValue);
		}

		/// <summary>
		/// The size of the arrows.
		/// </summary>
		private static readonly Vector2 ARROW_SIZE = new Vector2(16.0f, 16.0f);

		protected override object Value {
			get {
				return options[index].Value;
			}
			set {
				int n = options.Count;
				string valueStr = value?.ToString();
				// Find the matching value in the enum tree
				for (int i = 0; i < n; i++)
					if (options[i].Value.ToString() == valueStr) {
						index = i;
						Update();
						break;
					}
			}
		}

		/// <summary>
		/// The current index in the array.
		/// </summary>
		private int index;

		/// <summary>
		/// The realized item label.
		/// </summary>
		private GameObject label;
		
		/// <summary>
		/// The available options to cycle through.
		/// </summary>
		private readonly IList<Option> options;

		internal SelectOneOptionsEntry(string field, string title, string tooltip,
				Type fieldType) : base(field, title, tooltip) {
			var eval = Enum.GetValues(fieldType);
			if (eval == null)
				throw new ArgumentException("No values, or invalid values, for enum");
			int n = eval.Length;
			if (n == 0)
				throw new ArgumentException("Enum has no declared members");
			index = 0;
			label = null;
			options = new List<Option>(n);
			for (int i = 0; i < n; i++)
				options.Add(GetAttribute(eval.GetValue(i), fieldType));
		}

		protected override IUIComponent GetUIComponent() {
			// Find largest option to size the label appropriately
			string longestText = " ";
			foreach (var option in options) {
				string optionText = option.Title;
				if (optionText.Length > longestText.Length)
					longestText = optionText;
			}
			var lbl = new PLabel("Item") {
				ToolTip = "Loading", Text = longestText
			};
			lbl.OnRealize += OnRealizeItemLabel;
			// Build UI with 2 arrow buttons and a label to display the option
			return new PPanel("Select") {
				Direction = PanelDirection.Horizontal, Spacing = 5, DynamicSize = true
			}.AddChild(new PButton("Previous") {
				SpriteSize = ARROW_SIZE, OnClick = OnPrevious, ToolTip = POptions.
				TOOLTIP_PREVIOUS
			}.SetKleiBlueStyle().SetImageLeftArrow()).AddChild(lbl).
			AddChild(new PButton("Next") {
				SpriteSize = ARROW_SIZE, OnClick = OnNext, ToolTip = POptions.TOOLTIP_NEXT
			}.SetKleiBlueStyle().SetImageRightArrow());
		}

		/// <summary>
		/// Goes to the next option.
		/// </summary>
		/// <param name="source">The source button.</param>
		private void OnNext(GameObject source) {
			index++;
			if (index >= options.Count)
				index = 0;
			Update();
		}

		/// <summary>
		/// Called when the item label is realized.
		/// </summary>
		/// <param name="obj">The actual item label.</param>
		private void OnRealizeItemLabel(GameObject obj) {
			label = obj;
			Update();
		}

		/// <summary>
		/// Goes to the previous option.
		/// </summary>
		/// <param name="source">The source button.</param>
		private void OnPrevious(GameObject source) {
			index--;
			if (index < 0)
				index = options.Count - 1;
			Update();
		}

		/// <summary>
		/// Updates the displayed tool tip and text to match the current item.
		/// </summary>
		private void Update() {
			if (label != null) {
				var option = options[index];
				PUIElements.SetText(label, option.Title);
				PUIElements.SetToolTip(label, option.ToolTip);
			}
		}

		/// <summary>
		/// Represents a selectable option.
		/// </summary>
		private sealed class Option {
			/// <summary>
			/// The option title.
			/// </summary>
			public string Title { get; }

			/// <summary>
			/// The option tool tip.
			/// </summary>
			public string ToolTip { get; }

			/// <summary>
			/// The value to assign if this option is chosen.
			/// </summary>
			public object Value { get; }

			internal Option(string title, string toolTip, object value) {
				Title = title;
				ToolTip = toolTip;
				Value = value;
			}

			public override string ToString() {
				return "Option[Title={0},Value={1}]".F(Title, Value);
			}
		}
	}
}
