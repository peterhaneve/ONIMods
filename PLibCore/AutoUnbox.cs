/*
 * Copyright 2025 Peter Han
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

namespace PeterHan.PLib.Core {
	/// <summary>
	/// A helper class for migrating to boxed event types.
	/// </summary>
	/// <typeparam name="T">The value type to be unboxed.</typeparam>
	public static class AutoUnbox<T> where T : struct {
		// TODO Simpify when versions prior to U57-699077 no longer need to be supported
		private delegate object BoxData(T raw);

		private delegate T UnboxData(object boxed);

		/// <summary>
		/// If boxing is required, gets the data from the object.
		/// </summary>
		private static readonly BoxData DATA_BOX;

		/// <summary>
		/// If unboxing is required, gets the data from the object.
		/// </summary>
		private static readonly UnboxData DATA_UNBOX;

		static AutoUnbox() {
			Type boxedType;
			try {
				boxedType = typeof(Tag).Assembly.GetType("Boxed`1");
			} catch {
				// Avoid throwing in static initializer
				boxedType = null;
			}
			if (boxedType != null && boxedType.ContainsGenericParameters) {
				var specificType = boxedType.MakeGenericType(typeof(T));
				// New game versions
				DATA_UNBOX = specificType.CreateStaticDelegate<UnboxData>(nameof(Boxed<T>.
					Unbox), typeof(object));
				DATA_BOX = specificType.CreateStaticDelegate<BoxData>(nameof(Boxed<T>.Get),
					typeof(T));
			} else {
				DATA_BOX = null;
				DATA_UNBOX = null;
			}
		}

		/// <summary>
		/// Boxes the specified object.
		/// </summary>
		/// <param name="data">The data to box.</param>
		/// <returns>The boxed data for the Trigger method.</returns>
		public static object Box(T data) {
			object result;
			if (DATA_BOX != null)
				result = DATA_BOX.Invoke(data);
			else
				result = data;
			return result;
		}

		/// <summary>
		/// Unboxes the specified object.
		/// </summary>
		/// <param name="data">The data or boxed data.</param>
		/// <param name="result">The result of unboxing. Only valid if true is returned.</param>
		/// <returns>true if the data could be unboxed or directly converted, or false otherwise.</returns>
		public static bool Unbox(object data, out T result) {
			bool ok = true;
			if (DATA_UNBOX != null)
				result = DATA_UNBOX.Invoke(data);
			else if (data is T rawResult)
				result = rawResult;
			else {
				result = default;
				ok = false;
			}
			return ok;
		}
	}
}
