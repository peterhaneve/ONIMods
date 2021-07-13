/*
 * Copyright 2021 Peter Han
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
	/// Pools delegates to default constructors in order to churn out types very quickly.
	/// Ideally this would go in Manager.TypeInfo but fields cannot be added to classes
	/// retroactively.
	/// </summary>
	public static class ConstructorDelegator {
		private delegate object ConstructDelegate();

		/// <summary>
		/// Constructs objects from their type.
		/// </summary>
		private static readonly IDictionary<Type, ConstructDelegate> CONSTRUCTORS =
			new Dictionary<Type, ConstructDelegate>(512);

		/// <summary>
		/// The fast version of Activator.CreateInstance that uses the default constructor,
		/// returns uninitialized objects if none is available, and caches delegates for very
		/// high performance.
		/// </summary>
		/// <param name="type">The type to construct.</param>
		/// <returns>An instance of that type.</returns>
		public static object CreateInstance(Type type) {
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			if (!CONSTRUCTORS.TryGetValue(type, out ConstructDelegate constructor))
				CONSTRUCTORS.Add(type, constructor = GenerateConstructor(type));
			return (constructor == null) ? FormatterServices.GetUninitializedObject(
				type) : constructor.Invoke();
		}

		/// <summary>
		/// Creates a constructor delegate.
		/// </summary>
		/// <param name="type">The type for which to create a delegate.</param>
		/// <returns>The delegate which can construct this object.</returns>
		private static ConstructDelegate GenerateConstructor(Type type) {
			var defaultConstructor = type.GetConstructor(PPatchTools.BASE_FLAGS |
				BindingFlags.Instance, null, Type.EmptyTypes, null);
			ConstructDelegate result;
			if (defaultConstructor == null)
				result = null;
			else {
				var constructor = new DynamicMethod("Construct", typeof(object), Type.
					EmptyTypes, type, true);
				var generator = constructor.GetILGenerator();
				generator.Emit(OpCodes.Newobj, defaultConstructor);
				if (type.IsValueType)
					generator.Emit(OpCodes.Box, type);
				generator.Emit(OpCodes.Ret);
				result = constructor.CreateDelegate(typeof(ConstructDelegate)) as
					ConstructDelegate;
			}
			return result;
		}
	}
}
