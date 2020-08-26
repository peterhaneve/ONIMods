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
using System;

namespace PeterHan.PLib {
	/// <summary>
	/// An attribute placed on an option property or enum value for a class used as mod options
	/// in order to denote the display title and other options.
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
			string title = "", tt = "", cat = "", format = null;
			if (attr.GetType().Name == typeof(OptionAttribute).Name) {
				var trAttr = Traverse.Create(attr);
				try {
					title = trAttr.GetProperty<string>(nameof(Title));
					tt = trAttr.GetProperty<string>(nameof(Tooltip)) ?? "";
					cat = trAttr.GetProperty<string>(nameof(Category)) ?? "";
					format = trAttr.GetProperty<string>(nameof(Format));
				} catch (Exception e) {
					PUtil.LogExcWarn(e);
				}
			}
			return string.IsNullOrEmpty(title) ? null : new OptionAttribute(title, tt, cat) {
				Format = format
			};
		}

		/// <summary>
		/// The option category.
		/// </summary>
		public string Category { get; }

		/// <summary>
		/// The format string to use when displaying this option value. Only applicable for
		/// some types of options.
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// The option title.
		/// </summary>
		public string Title { get; }

		/// <summary>
		/// The option description tooltip.
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