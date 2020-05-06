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
	/// An attribute placed on an option property for a class used as mod options in order to
	/// make PLib use a custom options handler.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class DynamicOptionAttribute : Attribute {
		/// <summary>
		/// Creates a DynamicOptionAttribute using an object from another mod.
		/// </summary>
		/// <param name="attr">The attribute from the other mod.</param>
		/// <returns>A DynamicOptionAttribute object with the values from that object, where
		/// possible to retrieve; or null if none could be obtained.</returns>
		internal static DynamicOptionAttribute CreateFrom(object attr) {
			Type handler = null;
			string category = null;
			if (attr.GetType().Name == typeof(DynamicOptionAttribute).Name) {
				var trAttr = Traverse.Create(attr);
				try {
					handler = trAttr.GetProperty<Type>(nameof(Handler));
					category = trAttr.GetProperty<string>(nameof(Category));
				} catch (Exception e) {
					PUtil.LogExcWarn(e);
				}
			}
			return (handler == null) ? null : new DynamicOptionAttribute(handler, category);
		}

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
			Handler = type ?? throw new ArgumentNullException("type");
		}

		public override string ToString() {
			return "DynamicOption[handler={0},category={1}]".F(Handler.FullName, Category);
		}
	}
}