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

namespace PeterHan.PLib.Options {
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
	public sealed class OptionAttribute : Attribute, IOptionSpec {
		public string Category { get; }

		public string Format { get; set; }

		public string Title { get; }

		public string Tooltip { get; }

		/// <summary>
		/// Denotes a mod option field. Can also be used on members of an Enum type to give
		/// them a friendly display name.
		/// 
		/// This overload will take the option strings from STRINGS, using the namespace of the
		/// declaring type and the name of the property. A type declared in the MyName.
		/// MyNamespace namespace with a property named TestProperty will get the title
		/// STRINGS.MYNAME.MYNAMESPACE.OPTIONS.TESTPROPERTY.NAME, the tooltip
		/// STRINGS.MYNAME.MYNAMESPACE.OPTIONS.TESTPROPERTY.TOOLTIP, and the category
		/// STRINGS.MYNAME.MYNAMESPACE.OPTIONS.TESTPROPERTY.CATEGORY.
		/// </summary>
		public OptionAttribute() {
			Format = null;
			Title = null;
			Tooltip = null;
			Category = null;
		}

		/// <summary>
		/// Denotes a mod option field. Can also be used on members of an Enum type to give
		/// them a friendly display name.
		/// </summary>
		/// <param name="title">The field title to display.</param>
		/// <param name="tooltip">The tool tip for the field.</param>
		/// <param name="category">The category to use, or null for the default category.</param>
		public OptionAttribute(string title, string tooltip = null, string category = null) {
			if (string.IsNullOrEmpty(title))
				throw new ArgumentNullException(nameof(title));
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