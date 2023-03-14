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
	/// Stores a detoured method, only performing the expensive reflection when the detour is
	/// first used.
	/// 
	/// This class is not thread safe.
	/// <typeparam name="D">The delegate type to be used to call the detour.</typeparam>
	/// </summary>
	public sealed class DetouredMethod<D> where D : Delegate {
		/// <summary>
		/// Emulates the ability of Delegate.Invoke to actually call the method.
		/// </summary>
		public D Invoke {
			get {
				Initialize();
				return delg;
			}
		}

		/// <summary>
		/// The method name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// The delegate method which will be called.
		/// </summary>
		private D delg;

		/// <summary>
		/// The target type.
		/// </summary>
		private readonly Type type;

		internal DetouredMethod(Type type, string name) {
			this.type = type ?? throw new ArgumentNullException(nameof(type));
			Name = name ?? throw new ArgumentNullException(nameof(name));
			delg = null;
		}

		/// <summary>
		/// Initializes the getter and setter functions immediately if necessary.
		/// </summary>
		public void Initialize() {
			if (delg == null)
				delg = PDetours.Detour<D>(type, Name);
		}

		public override string ToString() {
			return string.Format("LazyDetouredMethod[type={1},name={0}]", Name, type.FullName);
		}
	}
}
