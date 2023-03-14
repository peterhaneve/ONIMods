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
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using OptionsList = System.Collections.Generic.ICollection<PeterHan.PLib.Options.IOptionsEntry>;
using AllOptions = System.Collections.Generic.IDictionary<string, System.Collections.Generic.
	ICollection<PeterHan.PLib.Options.IOptionsEntry>>;
using PeterHan.PLib.Core;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An abstract parent class containing methods shared by all built-in options handlers.
	/// </summary>
	public abstract class OptionsEntry : IOptionsEntry, IComparable<OptionsEntry>,
			IUIComponent {
		private const BindingFlags INSTANCE_PUBLIC = BindingFlags.Public | BindingFlags.
			Instance;

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
		internal static void AddToCategory(AllOptions entries, IOptionsEntry entry) {
			string category = entry.Category ?? "";
			if (!entries.TryGetValue(category, out OptionsList inCat)) {
				inCat = new List<IOptionsEntry>(16);
				entries.Add(category, inCat);
			}
			inCat.Add(entry);
		}

		/// <summary>
		/// Builds the options entries from the type.
		/// </summary>
		/// <param name="forType">The type of the options class.</param>
		/// <returns>A list of all public properties annotated for options dialogs.</returns>
		internal static AllOptions BuildOptions(Type forType) {
			var entries = new SortedList<string, OptionsList>(8);
			OptionsHandlers.InitPredefinedOptions();
			foreach (var prop in forType.GetProperties(INSTANCE_PUBLIC)) {
				// Must have the annotation
				var entry = TryCreateEntry(prop, 0);
				if (entry != null)
					AddToCategory(entries, entry);
			}
			return entries;
		}

		/// <summary>
		/// Creates a default UI entry. This entry will have the title and tool tip in the
		/// first column, and the provided UI component in the second column. Only one row is
		/// added by this method.
		/// </summary>
		/// <param name="entry">The options entry to be presented.</param>
		/// <param name="parent">The parent where the components will be added.</param>
		/// <param name="row">The row index where the components will be added.</param>
		/// <param name="presenter">The presenter that can display this option's value.</param>
		public static void CreateDefaultUIEntry(IOptionsEntry entry, PGridPanel parent,
				int row, IUIComponent presenter) {
			parent.AddChild(new PLabel("Label") {
				Text = LookInStrings(entry.Title), ToolTip = LookInStrings(entry.Tooltip),
				TextStyle = PUITuning.Fonts.TextLightStyle
			}, new GridComponentSpec(row, 0) {
				Margin = LABEL_MARGIN, Alignment = TextAnchor.MiddleLeft
			});
			parent.AddChild(presenter, new GridComponentSpec(row, 1) {
				Alignment = TextAnchor.MiddleRight, Margin = CONTROL_MARGIN
			});
		}

		/// <summary>
		/// Creates a dynamic options entry.
		/// </summary>
		/// <param name="prop">The property to be created.</param>
		/// <param name="handler">The type which can handle the property.</param>
		/// <returns>The created entry, or null if no entry could be created.</returns>
		private static IOptionsEntry CreateDynamicOption(PropertyInfo prop, Type handler) {
			IOptionsEntry result = null;
			var constructors = handler.GetConstructors(BindingFlags.Public | BindingFlags.
				Instance);
			string name = prop.Name;
			int n = constructors.Length;
			for (int i = 0; i < n && result == null; i++)
				try {
					if (ExecuteConstructor(prop, constructors[i]) is IOptionsEntry entry)
						result = entry;
				} catch (TargetInvocationException e) {
					PUtil.LogError("Unable to create option handler for property " +
						name + ":");
					PUtil.LogException(e.GetBaseException());
				} catch (MemberAccessException) {
					// Should never happen, filtered to public and instance
				} catch (AmbiguousMatchException) {
				} catch (TypeLoadException e) {
					PUtil.LogError("Unable to instantiate option handler for property " +
						name + ":");
					PUtil.LogException(e.GetBaseException());
				}
			if (result == null)
				PUtil.LogWarning("Unable to create option handler for property " +
					name + ", it must have a public constructor");
			return result;
		}

		/// <summary>
		/// Runs a dynamic option constructor.
		/// </summary>
		/// <param name="prop">The property to be created.</param>
		/// <param name="cons">The constructor to run.</param>
		/// <returns>The constructed dynamic option.</returns>
		private static object ExecuteConstructor(PropertyInfo prop, ConstructorInfo cons) {
			object result;
			var parameters = cons.GetParameters();
			int p = parameters.Length;
			if (p == 0)
				// Public default
				result = cons.Invoke(null);
			else {
				var values = new object[p];
				for (int j = 0; j < p; j++) {
					var targetType = parameters[j].ParameterType;
					// Custom attribute
					if (typeof(Attribute).IsAssignableFrom(targetType))
						values[j] = prop.GetCustomAttribute(targetType);
					else if (targetType == typeof(IOptionSpec))
						values[j] = prop.GetCustomAttribute<OptionAttribute>();
					else if (targetType == typeof(string))
						values[j] = prop.Name;
					else
						PUtil.LogWarning("DynamicOption cannot handle constructor parameter of type " +
							targetType.FullName);
				}
				result = cons.Invoke(values);
			}
			return result;
		}

		/// <summary>
		/// Substitutes default strings for an options entry with an empty title.
		/// </summary>
		/// <param name="spec">The option attribute supplied (Format is still accepted!)</param>
		/// <param name="member">The item declaring the attribute.</param>
		/// <returns>A substitute attribute with default values from STRINGS.</returns>
		internal static IOptionSpec HandleDefaults(IOptionSpec spec, MemberInfo member) {
			// Replace with entries takem from the strings
			string prefix = "STRINGS.{0}.OPTIONS.{1}.".F(member.DeclaringType?.
				Namespace?.ToUpperInvariant(), member.Name.ToUpperInvariant());
			string category = "";
			if (Strings.TryGet(prefix + "CATEGORY", out var entry))
				category = entry.String;
			return new OptionAttribute(prefix + "NAME", prefix + "TOOLTIP", category) {
				Format = spec.Format
			};
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
		public static string LookInStrings(string keyOrValue) {
			string result = keyOrValue;
			if (!string.IsNullOrEmpty(keyOrValue) && Strings.TryGet(keyOrValue, out var
					entry))
				result = entry.String;
			return result;
		}

		/// <summary>
		/// Shared code to create an options entry if an [Option] attribute is found on a
		/// property.
		/// </summary>
		/// <param name="prop">The property to inspect.</param>
		/// <param name="depth">The current depth of iteration to avoid infinite loops.</param>
		/// <returns>The OptionsEntry created, or null if none was.</returns>
		internal static IOptionsEntry TryCreateEntry(PropertyInfo prop, int depth) {
			IOptionsEntry result = null;
			// Must have the annotation, cannot be indexed
			var indexes = prop.GetIndexParameters();
			if (indexes.Length < 1) {
				var attributes = ListPool<Attribute, OptionsEntry>.Allocate();
				bool dlcMatch = true;
				attributes.AddRange(prop.GetCustomAttributes());
				int n = attributes.Count;
				for (int i = 0; i < n; i++)
					// Do not create an entry if the DLC does not match
					if (attributes[i] is RequireDLCAttribute requireDLC && DlcManager.
							IsContentActive(requireDLC.DlcID) != requireDLC.Required) {
						dlcMatch = false;
						break;
					}
				if (dlcMatch)
					for (int i = 0; i < n; i++) {
						result = TryCreateEntry(attributes[i], prop, depth);
						if (result != null)
							break;
					}
				attributes.Recycle();
			}
			return result;
		}

		/// <summary>
		/// Creates an options entry if an attribute is a valid IOptionSpec or
		/// DynamicOptionAttribute.
		/// </summary>
		/// <param name="attribute">The attribute to parse.</param>
		/// <param name="prop">The property to inspect.</param>
		/// <param name="depth">The current depth of iteration to avoid infinite loops.</param>
		/// <returns>The OptionsEntry created from the attribute, or null if none was.</returns>
		private static IOptionsEntry TryCreateEntry(Attribute attribute, PropertyInfo prop,
				int depth) {
			IOptionsEntry result = null;
			if (prop == null)
				throw new ArgumentNullException(nameof(prop));
			if (attribute is IOptionSpec spec) {
				if (string.IsNullOrEmpty(spec.Title))
					spec = HandleDefaults(spec, prop);
				// Attempt to find a class that will represent it
				var type = prop.PropertyType;
				result = OptionsHandlers.FindOptionClass(spec, prop);
				// See if it has entries that can themselves be added, ignore
				// value types and avoid infinite recursion
				if (result == null && !type.IsValueType && depth < 16 && type !=
						prop.DeclaringType)
					result = CompositeOptionsEntry.Create(spec, prop, depth);
			} else if (attribute is DynamicOptionAttribute doa &&
					typeof(IOptionsEntry).IsAssignableFrom(doa.Handler))
				result = CreateDynamicOption(prop, doa.Handler);
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
		/// The format string to use when rendering this option, or null if none was supplied.
		/// </summary>
		public string Format { get; }

		public virtual string Name => nameof(OptionsEntry);

		public event PUIDelegates.OnRealize OnRealize;

		/// <summary>
		/// The option title on screen.
		/// </summary>
		public string Title { get; protected set; }

		/// <summary>
		/// The tool tip to display.
		/// </summary>
		public string Tooltip { get; protected set; }

		/// <summary>
		/// The current value selected by the user.
		/// </summary>
		public abstract object Value { get; set; }

		protected OptionsEntry(string field, IOptionSpec attr) {
			if (attr == null)
				throw new ArgumentNullException(nameof(attr));
			Field = field;
			Format = attr.Format;
			Title = attr.Title ?? throw new ArgumentException("attr.Title is null");
			Tooltip = attr.Tooltip;
			Category = attr.Category;
		}

		public GameObject Build() {
			var comp = GetUIComponent();
			OnRealize?.Invoke(comp);
			return comp;
		}

		public int CompareTo(OptionsEntry other) {
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			return string.Compare(Category, other.Category, StringComparison.
				CurrentCultureIgnoreCase);
		}

		/// <summary>
		/// Adds the line item entry for this options entry.
		/// </summary>
		/// <param name="parent">The location to add this entry.</param>
		/// <param name="row">The layout row index to use. If updated, the row index will
		/// continue to count up from the new value.</param>
		public virtual void CreateUIEntry(PGridPanel parent, ref int row) {
			CreateDefaultUIEntry(this, parent, row, this);
		}

		/// <summary>
		/// Retrieves the UI component which can alter this setting. It should be sized
		/// properly to display any of the valid settings. The actual value will be set after
		/// the component is realized.
		/// </summary>
		/// <returns>The UI component to display.</returns>
		public abstract GameObject GetUIComponent();

		public virtual void ReadFrom(object settings) {
			if (Field != null && settings != null)
				try {
					var prop = settings.GetType().GetProperty(Field, INSTANCE_PUBLIC);
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

		public virtual void WriteTo(object settings) {
			if (Field != null && settings != null)
				try {
					var prop = settings.GetType().GetProperty(Field, INSTANCE_PUBLIC);
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
