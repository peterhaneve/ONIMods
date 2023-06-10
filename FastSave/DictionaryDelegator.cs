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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace PeterHan.FastSave {
	/// <summary>
	/// Pools delegates to constructors that accept an int argument in order to churn out types
	/// very quickly.
	/// </summary>
	public static class DictionaryDelegator {
		private delegate object ConstructDelegate(int capacity);

		/// <summary>
		/// Constructs objects from their type.
		/// </summary>
		private static readonly IDictionary<Type, ConstructDelegate> CONSTRUCTORS =
			new Dictionary<Type, ConstructDelegate>(512);

		/// <summary>
		/// The fast version of Activator.CreateInstance that uses the int constructor
		/// and caches delegates for very high performance.
		/// </summary>
		/// <param name="type">The type to construct.</param>
		/// <param name="capacity">The size to pre-allocate.</param>
		/// <returns>An instance of that type.</returns>
		public static object CreateInstance(Type type, int capacity) {
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (!CONSTRUCTORS.TryGetValue(type, out var constructor))
				CONSTRUCTORS.Add(type, constructor = GenerateConstructor(type));
			return (constructor == null) ? FormatterServices.GetUninitializedObject(
				type) : constructor.Invoke(capacity);
		}

		/// <summary>
		/// Creates a constructor delegate.
		/// </summary>
		/// <param name="type">The type for which to create a delegate.</param>
		/// <returns>The delegate which can construct this object.</returns>
		private static ConstructDelegate GenerateConstructor(Type type) {
			var constructorToUse = type.GetConstructor(PPatchTools.BASE_FLAGS |
				BindingFlags.Instance, null, new[] { typeof(int) }, null);
			ConstructDelegate result;
			if (constructorToUse == null)
				result = null;
			else {
				var constructor = new DynamicMethod("Construct", typeof(object), new[] {
					typeof(int)
				}, type, true);
				var generator = constructor.GetILGenerator();
				generator.Emit(OpCodes.Ldarg_0);
				generator.Emit(OpCodes.Newobj, constructorToUse);
				generator.Emit(OpCodes.Ret);
				result = constructor.CreateDelegate(typeof(ConstructDelegate)) as
					ConstructDelegate;
			}
			return result;
		}
	}
}
