/*
 * Copyright 2021 Peter Han
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
using System.Reflection;

namespace PeterHan.PLib {
	/// <summary>
	/// An attribute placed on an option property or enum value for a class used as mod options
	/// in order to denote the display title and other options.
	/// 
	/// Options attributes will be recursively searched if a custom type is used for a property
	/// with this attribute. If fields in that type have Option attributes, they will be
	/// displayed under the category of their parent option (ignoring their own category
	/// declaration).
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false,
		Inherited = true)]
	public sealed class OptionAttribute : Attribute {
		/// <summary>
		/// Creates an OptionAttribute using an object from another mod.
		/// </summary>
		/// <param name="attr">The attribute from the other mod.</param>
		/// <returns>An OptionAttribute object with the values from that object, where
		/// possible to retrieve; or null if none could be obtained.</returns>
		internal static OptionAttribute CreateFrom(object attr) {
			string title = null, tt = "", cat = "", format = null;
			var type = attr.GetType();
			if (type.Name == typeof(OptionAttribute).Name) {
				try {
					var info = type.GetPropertySafe<string>(nameof(Title), false);
					if (info != null)
						title = info.GetValue(attr, null) as string;
					info = type.GetPropertySafe<string>(nameof(Tooltip), false);
					if (info != null)
						tt = (info.GetValue(attr, null) as string) ?? "";
					info = type.GetPropertySafe<string>(nameof(Category), false);
					if (info != null)
						cat = (info.GetValue(attr, null) as string) ?? "";
					info = type.GetPropertySafe<string>(nameof(Format), false);
					if (info != null)
						format = info.GetValue(attr, null) as string;
				} catch (TargetInvocationException e) {
					// Other mod's error
					PUtil.LogExcWarn(e.GetBaseException());
				} catch (AmbiguousMatchException e) {
					// Other mod's error
					PUtil.LogExcWarn(e);
				} catch (FieldAccessException e) {
					// Other mod's error
					PUtil.LogExcWarn(e);
				}
			}
			return (title == null) ? null : new OptionAttribute(title, tt, cat) {
				Format = format
			};
		}

		/// <summary>
		/// The option category. Ignored and replaced with the parent option's category if
		/// this option is part of a custom grouped type.
		/// </summary>
		public string Category { get; }

		/// <summary>
		/// The format string to use when displaying this option value. Only applicable for
		/// some types of options.
		/// 
		/// <b>Warning</b>: Attribute may have issues on nested classes that are used as custom
		/// grouped options. To mitigate, try declaring the custom class in a non-nested
		/// context (i.e. not declared inside another class).
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// The option title. Ignored for fields which are displayed as custom grouped types
		/// types of other options.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The option description tooltip. Ignored for fields which are displayed as custom
		/// grouped types of other options.
		/// </summary>
		public string Tooltip { get; }

		/// <summary>
		/// Denotes a mod option field. Can also be used on members of an Enum type to give
		/// them a friendly display name.
		/// </summary>
		/// <param name="title">The field title to display.</param>
		/// <param name="tooltip">The tool tip for the field.</param>
		public OptionAttribute(string title, string tooltip = null) : this(title, tooltip, null) { }

		/// <summary>
		/// Denotes a mod option field. Can also be used on members of an Enum type to give
		/// them a friendly display name.
		/// </summary>
		/// <param name="title">The field title to display.</param>
		/// <param name="tooltip">The tool tip for the field.</param>
		/// <param name="category">The category to use, or null for the default category.</param>
		public OptionAttribute(string title, string tooltip, string category = null) {
			if (string.IsNullOrEmpty(title))
				throw new ArgumentNullException("title");
			Category = category;
			Format = null;
			Title = title;
			Tooltip = tooltip;
		}

		public override string ToString() {
			return Title;
		}
	}
}