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

using PeterHan.PLib.UI;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry that displays static text. Not intended to be serializable to the
	/// options file, instead declare a read-only property that returns null with a type of
	/// LocText, e.g:
	/// 
	/// [Option("Your text goes here", "Tool tip for the text")]
	/// public LocText MyLabel => null;
	/// 
	/// Unity font formatting can be used in the text. The name of a strings table entry can
	/// also be used to allow localization.
	/// </summary>
	public class TextBlockOptionsEntry : OptionsEntry {
		/// <summary>
		/// A font style that looks like TextLightStyle but allows word wrapping.
		/// </summary>
		private static readonly TextStyleSetting WRAP_TEXT_STYLE;

		static TextBlockOptionsEntry() {
			WRAP_TEXT_STYLE = PUITuning.Fonts.TextLightStyle.DeriveStyle();
			WRAP_TEXT_STYLE.enableWordWrapping = true;
		}

		public override object Value {
			get {
				return ignore;
			}
			set {
				if (value is LocText newValue)
					ignore = newValue;
			}
		}

		/// <summary>
		/// This value is not used, it only exists to satisfy the contract.
		/// </summary>
		private LocText ignore;

		public TextBlockOptionsEntry(string field, IOptionSpec spec) : base(field, spec) { }

		public override void CreateUIEntry(PGridPanel parent, ref int row) {
			parent.AddChild(new PLabel(Field) {
				Text = LookInStrings(Title), ToolTip = LookInStrings(Tooltip),
				TextStyle = WRAP_TEXT_STYLE
			}, new GridComponentSpec(row, 0) {
				Margin = CONTROL_MARGIN, Alignment = TextAnchor.MiddleCenter, ColumnSpan = 2
			});
		}

		public override GameObject GetUIComponent() {
			// Will not be invoked
			return new GameObject("Empty");
		}
	}
}
