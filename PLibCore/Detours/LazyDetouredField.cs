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
	/// Stores delegates used to read and write fields or properties. This version is lazy and
	/// only calculates the destination when it is first used.
	/// 
	/// This class is not thread safe.
	/// </summary>
	/// <typeparam name="P">The containing type of the field or property.</typeparam>
	/// <typeparam name="T">The element type of the field or property.</typeparam>
	internal sealed class LazyDetouredField<P, T> : IDetouredField<P, T> {
		/// <summary>
		/// Invoke to get the field/property value.
		/// </summary>
		public Func<P, T> Get {
			get {
				Initialize();
				return getter;
			}
		}

		/// <summary>
		/// The field name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Invoke to set the field/property value.
		/// </summary>
		public Action<P, T> Set {
			get {
				Initialize();
				return setter;
			}
		}

		/// <summary>
		/// The function to get the field value.
		/// </summary>
		private Func<P, T> getter;

		/// <summary>
		/// The function to set the field value.
		/// </summary>
		private Action<P, T> setter;

		/// <summary>
		/// The target type.
		/// </summary>
		private readonly Type type;

		internal LazyDetouredField(Type type, string name) {
			this.type = type ?? throw new ArgumentNullException(nameof(type));
			Name = name ?? throw new ArgumentNullException(nameof(name));
			getter = null;
			setter = null;
		}

		/// <summary>
		/// Initializes the getter and setter functions immediately if necessary.
		/// </summary>
		public void Initialize() {
			if (getter == null && setter == null) {
				var dt = PDetours.DetourField<P, T>(Name);
				getter = dt.Get;
				setter = dt.Set;
			}
		}

		public override string ToString() {
			return string.Format("LazyDetouredField[type={1},name={0}]", Name, type.FullName);
		}
	}
}
