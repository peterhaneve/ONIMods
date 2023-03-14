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

using PeterHan.PLib.Core;
using System;

namespace PeterHan.PLib {
	/// <summary>
	/// An attribute placed on an option property for a class used as mod options in order to
	/// make PLib use a custom options handler. The type used for the handler must inherit
	/// from IOptionsEntry.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class DynamicOptionAttribute : Attribute {
		/// <summary>
		/// The option category.
		/// </summary>
		public string Category { get; }

		/// <summary>
		/// The option handler.
		/// </summary>
		public Type Handler { get; }

		/// <summary>
		/// Denotes a mod option field.
		/// </summary>
		/// <param name="type">The type that will handle this dynamic option.</param>
		/// <param name="category">The category to use, or null for the default category.</param>
		public DynamicOptionAttribute(Type type, string category = null) {
			Category = category;
			Handler = type ?? throw new ArgumentNullException(nameof(type));
		}

		public override string ToString() {
			return "DynamicOption[handler={0},category={1}]".F(Handler.FullName, Category);
		}
	}
}
