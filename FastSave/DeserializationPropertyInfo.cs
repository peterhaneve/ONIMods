/*
 * Copyright 2026 Peter Han
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
using System.Reflection;
using System.Reflection.Emit;

using TypeInfo = KSerialization.TypeInfo;

namespace PeterHan.FastSave {
	/// <summary>
	/// Allows rapid deserialization of object properties.
	/// </summary>
	internal sealed class DeserializationPropertyInfo : FastDeserializationInfo {
		/// <summary>
		/// The delegate type generated for retrieving properties from an object.
		/// </summary>
		/// <param name="obj">The object from which the property will be retrieved.</param>
		/// <returns>The value of the property.</returns>
		public delegate object GetValueDelegate(object obj);

		/// <summary>
		/// The delegate type generated for changing properties of an object.
		/// </summary>
		/// <param name="obj">The object whose property will be modified.</param>
		/// <param name="value">The new value of the property.</param>
		public delegate void SetValueDelegate(object obj, object value);

		/// <summary>
		/// Creates a property getter delegate.
		/// </summary>
		/// <param name="property">The property to get.</param>
		/// <returns>A delegate which can get this property, boxing the value if necessary.</returns>
		public static GetValueDelegate GenerateGetter(PropertyInfo property) {
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			Type pt = property.PropertyType, tt = property.DeclaringType;
			if (tt == null || pt == null)
				throw new ArgumentException("Property has no declaring type");
			var getProperty = property.GetGetMethod(true);
			var getter = new DynamicMethod("Get_" + property.Name, typeof(object), new Type[] {
				typeof(object)
			}, tt, true);
			var generator = getter.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			if (tt.IsValueType)
				generator.Emit(OpCodes.Unbox, tt);
			generator.Emit(OpCodes.Call, getProperty);
			// Box it if it is a value type (struct, int, enum...)
			if (pt.IsValueType)
				generator.Emit(OpCodes.Box, pt);
			generator.Emit(OpCodes.Ret);
			return getter.CreateDelegate(typeof(GetValueDelegate)) as GetValueDelegate;
		}

		/// <summary>
		/// Creates a property setter delegate.
		/// </summary>
		/// <param name="property">The property to set.</param>
		/// <returns>A delegate which can set this property, unboxing the value if necessary.</returns>
		public static SetValueDelegate GenerateSetter(PropertyInfo property) {
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			Type pt = property.PropertyType, tt = property.DeclaringType;
			if (tt == null || pt == null)
				throw new ArgumentException("Property has no declaring type");
			var setProperty = property.GetSetMethod(true);
			var setter = new DynamicMethod("Set_" + property.Name, null, new Type[] {
				typeof(object), typeof(object)
			}, tt, true);
			var generator = setter.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			if (tt.IsValueType)
				generator.Emit(OpCodes.Unbox, tt);
			generator.Emit(OpCodes.Ldarg_1);
			// Unbox it if it is a value type (struct, int, enum...)
			if (pt.IsValueType)
				generator.Emit(OpCodes.Unbox_Any, pt);
			generator.Emit(OpCodes.Call, setProperty);
			generator.Emit(OpCodes.Ret);
			return setter.CreateDelegate(typeof(SetValueDelegate)) as SetValueDelegate;
		}

		/// <summary>
		/// The property's getter as a delegate.
		/// </summary>
		private readonly GetValueDelegate getValue;

		/// <summary>
		/// The property to be deserialized.
		/// </summary>
		public readonly PropertyInfo property;

		/// <summary>
		/// The property's setter as a delegate.
		/// </summary>
		private readonly SetValueDelegate setValue;

		public DeserializationPropertyInfo(TypeInfo target, PropertyInfo property) : base(
				target.type != null, target) {
			this.property = property ?? throw new ArgumentNullException(nameof(property));
			if (valid) {
				targetType.BuildGenericArgs();
				getValue = GenerateGetter(property);
				setValue = GenerateSetter(property);
				if (getValue == null)
					throw new ArgumentException(string.Format("Cannot create property getter: {0}.{1}",
						property.DeclaringType?.FullName, property.Name));
				if (setValue == null)
					throw new ArgumentException(string.Format("Cannot create property setter: {0}.{1}",
						property.DeclaringType?.FullName, property.Name));
			}
		}

		public object GetValue(object obj) {
			return getValue.Invoke(obj);
		}

		public override void Read(object obj, IReader reader) {
			if (valid) {
				try {
					setValue.Invoke(obj, ReadValue(targetType, reader, getValue.Invoke(obj)));
				} catch (Exception e) {
					Debug.LogErrorFormat("Exception occurred while attempting to deserialize property {0} on object {1}({2}).\n{3}",
						property, obj, obj.GetType(), e.ToString());
					throw;
				}
			} else
				base.Read(obj, reader);
		}

		public override string ToString() {
			return string.Format("DeserializationPropertyInfo[type={0},property={1}]",
				targetType.type.FullName, property.Name);
		}
	}
}
