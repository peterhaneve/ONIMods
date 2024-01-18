/*
 * Copyright 2024 Peter Han
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
	/// Allows rapid deserialization of object fields.
	/// </summary>
	internal sealed class DeserializationFieldInfo : FastDeserializationInfo {
		private delegate object GetValueDelegate(object obj);

		private delegate void SetValueDelegate(object obj, object value);

		/// <summary>
		/// The field to be deserialized.
		/// </summary>
		public readonly FieldInfo field;

		/// <summary>
		/// The field's getter as a delegate.
		/// </summary>
		private readonly GetValueDelegate getValue;

		/// <summary>
		/// The field's setter as a delegate.
		/// </summary>
		private readonly SetValueDelegate setValue;

		public DeserializationFieldInfo(TypeInfo target, FieldInfo field) : base(
				target.type != null, target) {
			this.field = field ?? throw new ArgumentNullException(nameof(field));
			if (valid) {
				targetType.BuildGenericArgs();
				getValue = GenerateGetter();
				setValue = GenerateSetter();
				if (getValue == null)
					throw new ArgumentException(string.Format("Cannot create field getter: {0}.{1}",
						field.DeclaringType?.FullName, field.Name));
				if (setValue == null)
					throw new ArgumentException(string.Format("Cannot create field setter: {0}.{1}",
						field.DeclaringType?.FullName, field.Name));
			}
		}

		/// <summary>
		/// Creates a field getter delegate.
		/// </summary>
		/// <returns>The delegate which can get this field, boxing the value if necessary.</returns>
		private GetValueDelegate GenerateGetter() {
			Type ft = field.FieldType, tt = field.DeclaringType;
			if (tt == null || ft == null)
				throw new ArgumentException("Field has no declaring type");
			var getter = new DynamicMethod("Get_" + field.Name, typeof(object), new Type[] {
				typeof(object)
			}, tt, true);
			var generator = getter.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			if (tt.IsValueType)
				generator.Emit(OpCodes.Unbox, tt);
			generator.Emit(OpCodes.Ldfld, field);
			// Box it if it is a value type (struct, int, enum...)
			if (ft.IsValueType)
				generator.Emit(OpCodes.Box, ft);
			generator.Emit(OpCodes.Ret);
			return getter.CreateDelegate(typeof(GetValueDelegate)) as GetValueDelegate;
		}

		/// <summary>
		/// Creates a property setter delegate.
		/// </summary>
		/// <returns>The delegate which can set this property, unboxing the value if necessary.</returns>
		private SetValueDelegate GenerateSetter() {
			Type ft = field.FieldType, tt = field.DeclaringType;
			if (tt == null || ft == null)
				throw new ArgumentException("Field has no declaring type");
			var setter = new DynamicMethod("Set_" + field.Name, null, new Type[] {
				typeof(object), typeof(object)
			}, tt, true);
			var generator = setter.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			if (tt.IsValueType)
				generator.Emit(OpCodes.Unbox, tt);
			generator.Emit(OpCodes.Ldarg_1);
			// Unbox it if it is a value type (struct, int, enum...)
			if (ft.IsValueType)
				generator.Emit(OpCodes.Unbox_Any, ft);
			generator.Emit(OpCodes.Stfld, field);
			generator.Emit(OpCodes.Ret);
			return setter.CreateDelegate(typeof(SetValueDelegate)) as SetValueDelegate;
		}

		public object GetValue(object obj) {
			return getValue.Invoke(obj);
		}

		public override void Read(object obj, IReader reader) {
			if (valid) {
				try {
					setValue.Invoke(obj, ReadValue(targetType, reader, getValue.Invoke(obj)));
				} catch (Exception e) {
					Debug.LogErrorFormat("Exception when deserializing field {0} on object {1}({2}).\n{3}",
						field, obj, obj.GetType(), e.ToString());
					throw;
				}
			} else
				base.Read(obj, reader);
		}

		public override string ToString() {
			return string.Format("DeserializationFieldInfo[type={0},field={1}]", targetType.
				type.FullName, field.Name);
		}
	}
}
