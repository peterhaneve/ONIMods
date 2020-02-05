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

using Harmony;
using PeterHan.PLib.UI;
using System;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An entry in the Options screen for one particular setting.
	/// </summary>
	internal abstract class OptionsEntry : IComparable<OptionsEntry> {
		/// <summary>
		/// Searches for LimitAttribute attributes on the property wrapped by an OptionsEntry.
		/// </summary>
		/// <param name="prop">The property with annotations.</param>
		/// <returns>The Limit attribute if present, or null if none is.</returns>
		protected static LimitAttribute FindLimitAttribute(PropertyInfo prop) {
			LimitAttribute fieldLimits = null;
			foreach (var attr in prop.GetCustomAttributes(false))
				if ((fieldLimits = LimitAttribute.CreateFrom(attr)) != null)
					break;
			return fieldLimits;
		}

		/// <summary>
		/// The category for this entry.
		/// </summary>
		public string Category { get; }

		/// <summary>
		/// The option field name.
		/// </summary>
		public string Field { get; }

		/// <summary>
		/// The option title on screen.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The tool tip to display.
		/// </summary>
		public string ToolTip { get; }

		/// <summary>
		/// The current value selected by the user.
		/// </summary>
		protected abstract object Value { get; set; }

		[Obsolete("Do not use this constructor, it exists only for binary compatibility")]
		protected OptionsEntry(string field, string title, string tooltip) {
			Category = "";
			Field = field;
			Title = title;
			ToolTip = tooltip;
		}

		protected OptionsEntry(string field, OptionAttribute attr) {
			if (attr == null)
				throw new ArgumentNullException("attr");
			Field = field;
			Title = attr.Title;
			ToolTip = attr.Tooltip;
			Category = attr.Category ?? "";
		}

		public int CompareTo(OptionsEntry other) {
			if (other == null)
				throw new ArgumentNullException("other");
			return string.Compare(Category, other.Category, StringComparison.
				CurrentCultureIgnoreCase);
		}

		/// <summary>
		/// Retrieves the full line item entry for this options entry.
		/// </summary>
		/// <returns>A UI component with both the title and editor.</returns>
		internal IUIComponent GetUIEntry() {
			var expandWidth = new Vector2(1.0f, 0.0f);
			return new PPanel("Option_" + Field) {
				Direction = PanelDirection.Horizontal, FlexSize = expandWidth,
				Spacing = 5, Alignment = TextAnchor.MiddleCenter
			}.AddChild(new PLabel("Label") {
				Text = Title, ToolTip = ToolTip, FlexSize = new Vector2(1.0f, 0.0f),
				TextAlignment = TextAnchor.MiddleLeft, DynamicSize = true, TextStyle =
				PUITuning.Fonts.TextLightStyle
			}).AddChild(GetUIComponent());
		}

		/// <summary>
		/// Retrieves the UI component which can alter this setting. It should be sized
		/// properly to display any of the valid settings. The actual value will be set after
		/// the component is realized.
		/// </summary>
		/// <returns>The UI component to display.</returns>
		protected abstract IUIComponent GetUIComponent();

		/// <summary>
		/// Reads the option value from the settings.
		/// </summary>
		/// <param name="settings">The settings object.</param>
		internal void ReadFrom(object settings) {
			try {
				var prop = settings.GetType().GetProperty(Field);
				if (prop != null)
					Value = prop.GetValue(settings, null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (InvalidCastException e) {
				// Our errror!
				PUtil.LogException(e);
			}
		}

		public override string ToString() {
			return "{1}[field={0},title={2}]".F(Field, GetType().Name, Title);
		}

		/// <summary>
		/// Writes the option value from the settings.
		/// </summary>
		/// <param name="settings">The settings object.</param>
		internal void WriteTo(object settings) {
			try {
				var prop = settings.GetType().GetProperty(Field);
				if (prop != null)
					prop.SetValue(settings, Value, null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (InvalidCastException e) {
				// Our errror!
				PUtil.LogException(e);
			}
		}
	}

	/// <summary>
	/// The types of options which are available.
	/// </summary>
	internal enum OptionsType {
		YesNo, SelectOne
	}
}
