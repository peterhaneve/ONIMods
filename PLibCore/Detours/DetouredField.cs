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

namespace PeterHan.PLib.Detours {
	/// <summary>
	/// Stores delegates used to read and write fields or properties.
	/// </summary>
	/// <typeparam name="P">The containing type of the field or property.</typeparam>
	/// <typeparam name="T">The element type of the field or property.</typeparam>
	internal sealed class DetouredField<P, T> : IDetouredField<P, T> {
		/// <summary>
		/// Invoke to get the field/property value.
		/// </summary>
		public Func<P, T> Get { get; }

		/// <summary>
		/// The field name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Invoke to set the field/property value. Null if the field is const or readonly.
		/// </summary>
		public Action<P, T> Set { get; }

		internal DetouredField(string name, Func<P, T> get, Action<P, T> set) {
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Get = get;
			Set = set;
		}

		public override string ToString() {
			return string.Format("DetouredField[name={0}]", Name);
		}
	}
}
