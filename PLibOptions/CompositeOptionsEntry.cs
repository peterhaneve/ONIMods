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
using PeterHan.PLib.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// An options entry that encapsulates other options. The category annotation on those
	/// objects will be ignored, and the category of the Option attribute on the property
	/// that declared those options (to avoid infinite loops) will be used instead.
	/// 
	/// <b>This object is not in the scene graph.</b> Any events in OnRealize will never be
	/// invoked, and it is never "built".
	/// </summary>
	internal class CompositeOptionsEntry : OptionsEntry {
		/// <summary>
		/// Creates an options entry wrapper for the specified property, iterating its internal
		/// fields to create sub-options if needed (recursively).
		/// </summary>
		/// <param name="info">The property to wrap.</param>
		/// <param name="spec">The option title and tool tip.</param>
		/// <param name="depth">The current depth of iteration to avoid infinite loops.</param>
		/// <returns>An options wrapper, or null if no inner properties are themselves options.</returns>
		internal static CompositeOptionsEntry Create(IOptionSpec spec, PropertyInfo info,
				int depth) {
			var type = info.PropertyType;
			var composite = new CompositeOptionsEntry(info.Name, spec, type);
			// Skip static properties if they exist
			foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.
					Instance)) {
				var entry = TryCreateEntry(prop, depth + 1);
				if (entry != null)
					composite.AddField(prop, entry);
			}
			return composite.ChildCount > 0 ? composite : null;
		}

		/// <summary>
		/// Reports the number of options contained inside this one.
		/// </summary>
		public int ChildCount {
			get {
				return subOptions.Count;
			}
		}

		public override object Value {
			get {
				return value;
			}
			set {
				if (value != null && targetType.IsAssignableFrom(value.GetType()))
					this.value = value;
			}
		}

		/// <summary>
		/// The options encapsulated in this object.
		/// </summary>
		protected readonly IDictionary<PropertyInfo, IOptionsEntry> subOptions;

		/// <summary>
		/// The type of the encapsulated object.
		/// </summary>
		protected readonly Type targetType;

		/// <summary>
		/// The object thus wrapped.
		/// </summary>
		protected object value;

		public CompositeOptionsEntry(string field, IOptionSpec spec, Type fieldType) :
				base(field, spec) {
			subOptions = new Dictionary<PropertyInfo, IOptionsEntry>(16);
			targetType = fieldType ?? throw new ArgumentNullException(nameof(fieldType));
			value = OptionsDialog.CreateOptions(fieldType);
		}

		/// <summary>
		/// Adds an options entry object that operates on Option fields of the encapsulated
		/// object.
		/// </summary>
		/// <param name="info">The property that is wrapped.</param>
		/// <param name="entry">The entry to add.</param>
		public void AddField(PropertyInfo info, IOptionsEntry entry) {
			if (entry == null)
				throw new ArgumentNullException(nameof(entry));
			if (info == null)
				throw new ArgumentNullException(nameof(info));
			subOptions.Add(info, entry);
		}

		public override void CreateUIEntry(PGridPanel parent, ref int row) {
			int i = row;
			bool first = true;
			parent.AddOnRealize(WhenRealized);
			// Render each sub-entry - order is always Add Spec, Create Entry, Increment Row
			foreach (var pair in subOptions) {
				if (!first) {
					i++;
					parent.AddRow(new GridRowSpec());
				}
				pair.Value.CreateUIEntry(parent, ref i);
				first = false;
			}
			row = i;
		}

		public override GameObject GetUIComponent() {
			// Will not be invoked
			return new GameObject("Empty");
		}

		public override void ReadFrom(object settings) {
			base.ReadFrom(settings);
			foreach (var pair in subOptions)
				pair.Value.ReadFrom(value);
		}

		public override string ToString() {
			return "{1}[field={0},title={2},children=[{3}]]".F(Field, GetType().Name, Title,
				subOptions.Join());
		}

		/// <summary>
		/// Updates the child objects for the first time when the panel is realized.
		/// </summary>
		private void WhenRealized(GameObject _) {
			foreach (var pair in subOptions)
				pair.Value.ReadFrom(value);
		}

		public override void WriteTo(object settings) {
			foreach (var pair in subOptions)
				// Cannot detour as the types are not known at compile time, and delegates
				// bake in the target object which changes upon each update
				pair.Value.WriteTo(value);
			base.WriteTo(settings);
		}
	}
}
