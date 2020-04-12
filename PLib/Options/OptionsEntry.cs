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

using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using OptionsList = System.Collections.Generic.ICollection<PeterHan.PLib.Options.OptionsEntry>;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An entry in the Options screen for one particular setting.
	/// </summary>
	internal abstract class OptionsEntry : IComparable<OptionsEntry> {
		/// <summary>
		/// The margins around the control used in each entry.
		/// </summary>
		protected static readonly RectOffset CONTROL_MARGIN = new RectOffset(0, 0, 2, 2);

		/// <summary>
		/// The margins around the label for each entry.
		/// </summary>
		protected static readonly RectOffset LABEL_MARGIN = new RectOffset(0, 5, 2, 2);

		/// <summary>
		/// Adds an options entry to the category list, creating a new category if necessary.
		/// </summary>
		/// <param name="entries">The existing categories.</param>
		/// <param name="entry">The option entry to add.</param>
		private static void AddToCategory(IDictionary<string, OptionsList> entries,
				OptionsEntry entry) {
			string category = entry.Category ?? "";
			if (!entries.TryGetValue(category, out OptionsList inCat)) {
				inCat = new List<OptionsEntry>(16);
				entries.Add(category, inCat);
			}
			inCat.Add(entry);
		}

		/// <summary>
		/// Builds the options entries from the type.
		/// </summary>
		/// <param name="forType">The type of the options class.</param>
		/// <returns>A list of all public properties annotated for options dialogs.</returns>
		internal static IDictionary<string, OptionsList> BuildOptions(Type forType) {
			var entries = new SortedList<string, OptionsList>(8);
			OptionAttribute oa;
			DynamicOptionAttribute doa;
			foreach (var prop in forType.GetProperties())
				// Must have the annotation
				foreach (var attr in prop.GetCustomAttributes(false))
					if ((oa = OptionAttribute.CreateFrom(attr)) != null) {
						// Attempt to find a class that will represent it
						var entry = CreateOptions(prop, oa);
						if (entry != null)
							AddToCategory(entries, entry);
						break;
					} else if ((doa = DynamicOptionAttribute.CreateFrom(attr)) != null) {
						AddToCategory(entries, new DynamicOptionsEntry(prop.Name, doa));
						break;
					}
			return entries;
		}

		/// <summary>
		/// Creates an options entry wrapper for the specified property.
		/// </summary>
		/// <param name="info">The property to wrap.</param>
		/// <param name="oa">The option title and tool tip.</param>
		/// <returns>An options wrapper, or null if none can handle this type.</returns>
		private static OptionsEntry CreateOptions(PropertyInfo info, OptionAttribute oa) {
			OptionsEntry entry = null;
			Type type = info.PropertyType;
			string field = info.Name;
			// Enumeration type
			if (type.IsEnum)
				entry = new SelectOneOptionsEntry(field, oa, type);
			else if (type == typeof(bool))
				entry = new CheckboxOptionsEntry(field, oa);
			else if (type == typeof(int))
				entry = new IntOptionsEntry(oa, info);
			else if (type == typeof(float))
				entry = new FloatOptionsEntry(oa, info);
			else if (type == typeof(string))
				entry = new StringOptionsEntry(oa, info);
			else if (type == typeof(System.Action))
				// Should not actually be serialized to the JSON
				entry = new ButtonOptionsEntry(oa, info);
			else if (type == typeof(LocText))
				entry = new TextBlockOptionsEntry(oa, info);
			return entry;
		}

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
		/// First looks to see if the string exists in the string database; if it does, returns
		/// the localized value, otherwise returns the string unmodified.
		/// 
		/// This method is somewhat slow. Cache the result if possible.
		/// </summary>
		/// <param name="keyOrValue">The string key to check.</param>
		/// <returns>The string value with that key, or the key if there is no such localized
		/// string value.</returns>
		internal static string LookInStrings(string keyOrValue) {
			string result = keyOrValue;
			if (!string.IsNullOrEmpty(keyOrValue) && Strings.TryGet(keyOrValue, out StringEntry
					entry))
				result = entry.String;
			return result;
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
		public string Title { get; protected set; }

		/// <summary>
		/// The tool tip to display.
		/// </summary>
		public string ToolTip { get; protected set; }

		/// <summary>
		/// The current value selected by the user.
		/// </summary>
		protected abstract object Value { get; set; }

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
		/// Adds the line item entry for this options entry.
		/// </summary>
		/// <param name="parent">The location to add this entry.</param>
		/// <param name="row">The layout row index to use. If updated, the row index will
		/// continue to count up from the new value.</param>
		internal virtual void CreateUIEntry(PGridPanel parent, ref int row) {
			parent.AddChild(new PLabel("Label") {
				Text = LookInStrings(Title), ToolTip = LookInStrings(ToolTip),
				TextStyle = PUITuning.Fonts.TextLightStyle
			}, new GridComponentSpec(row, 0) {
				Margin = LABEL_MARGIN, Alignment = TextAnchor.MiddleLeft
			});
			parent.AddChild(GetUIComponent(), new GridComponentSpec(row, 1) {
				Alignment = TextAnchor.MiddleRight, Margin = CONTROL_MARGIN
			});
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
				if (prop != null && prop.CanRead)
					Value = prop.GetValue(settings, null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (InvalidCastException e) {
				// Our error!
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
				if (prop != null && prop.CanWrite)
					prop.SetValue(settings, Value, null);
			} catch (TargetInvocationException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (AmbiguousMatchException e) {
				// Other mod's error
				PUtil.LogException(e);
			} catch (InvalidCastException e) {
				// Our error!
				PUtil.LogException(e);
			}
		}
	}
}
