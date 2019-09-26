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

using Harmony;
using PeterHan.PLib.UI;
using System;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An entry in the Options screen for one particular setting.
	/// </summary>
	internal abstract class OptionsEntry {
		/// <summary>
		/// Tries to retrieve the limits for an option.
		/// </summary>
		/// <param name="attr">The annotation to check.</param>
		/// <returns>The LimitAttribute matching that annotation, or null if it is not a LimitAttribute.</returns>
		internal static LimitAttribute GetLimits(object attr) {
			if (attr == null)
				throw new ArgumentNullException("attr");
			LimitAttribute la = null;
			if (attr.GetType().Name == typeof(LimitAttribute).Name) {
				// Has limit type
				var trAttr = Traverse.Create(attr);
				double min = trAttr.GetProperty<double>("Minimum"), max = trAttr.
					GetProperty<double>("Maximum");
				if (min != 0.0 || max != 0.0)
					la = new LimitAttribute(min, max);
			}
			return la;
		}

		/// <summary>
		/// Tries to retrieve the option's title and tool tip.
		/// </summary>
		/// <param name="attr">The annotation to check.</param>
		/// <returns>The OptionAttribute matching that annotation, or null if it is not an OptionAttribute.</returns>
		internal static OptionAttribute GetTitle(object attr) {
			if (attr == null)
				throw new ArgumentNullException("attr");
			OptionAttribute oa = null;
			if (attr.GetType().Name == typeof(OptionAttribute).Name) {
				// Has the Options attribute, but is cross-mod...
				var trAttr = Traverse.Create(attr);
				string title = trAttr.GetProperty<string>("Title"), tooltip = trAttr.
					GetProperty<string>("Tooltip") ?? "";
				if (!string.IsNullOrEmpty(title))
					oa = new OptionAttribute(title, tooltip);
			}
			return oa;
		}

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

		protected OptionsEntry(string field, string title, string tooltip) {
			Field = field;
			Title = title;
			ToolTip = tooltip;
		}

		/// <summary>
		/// Retrieves the full line item entry for this options entry.
		/// </summary>
		/// <returns>A UI component with both the title and editor.</returns>
		internal IUIComponent GetUIEntry() {
			return new PPanel("Option_" + Field) {
				Direction = PanelDirection.Horizontal, FlexSize = new Vector2(1.0f, 0.0f),
				Spacing = 5, Alignment = TextAnchor.MiddleCenter
			}.AddChild(new PLabel("Label") {
				Text = Title, ToolTip = ToolTip, FlexSize = new Vector2(1.0f, 0.0f),
				TextAlignment = TextAnchor.MiddleLeft, DynamicSize = true
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
