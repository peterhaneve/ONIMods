/*
 * Copyright 2022 Peter Han
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

using KSerialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

using TypeInfo = KSerialization.TypeInfo;

namespace PeterHan.FastSave {
	/// <summary>
	/// A faster version of SerializationTemplate that uses delegates to speed up saving.
	/// </summary>
	public sealed class FastSerializationTemplate {
		/// <summary>
		/// Denotes public fields and properties declared in the serialized class.
		/// </summary>
		private const BindingFlags MEMBER_FLAGS = BindingFlags.DeclaredOnly | BindingFlags.
			Instance | BindingFlags.Public;

		/// <summary>
		/// Retrieves the Klei serialization configuration for the given type.
		/// </summary>
		/// <param name="type">The type to search for attributes.</param>
		/// <returns>The value of the Serialization attribute found lowest (closest to System.
		/// Object) in the inheritance hierarchy, or OptOut if none were found.</returns>
		private static MemberSerialization GetSerializationConfig(Type type) {
			var result = MemberSerialization.Invalid;
			// Scan through base classes as well
			while (type != typeof(object)) {
				object[] attributes = type.GetCustomAttributes(typeof(SerializationConfig),
					false);
				for (int i = 0; i < attributes.Length; i++)
					if (attributes[i] is SerializationConfig sc) {
						// Check for conflicting attributes removed, it really should only
						// appear once per class
						result = sc.MemberSerialization;
						break;
					}
				type = type.BaseType;
			}
			if (result == MemberSerialization.Invalid)
				result = MemberSerialization.OptOut;
			return result;
		}

		/// <summary>
		/// Writes the type header to the stream.
		/// </summary>
		/// <param name="writer">The location where the header will be written.</param>
		/// <param name="type">The type to be encoded.</param>
		private static void WriteType(BinaryWriter writer, Type type) {
			var typeInfo = Helper.EncodeSerializationType(type);
			writer.Write((byte)typeInfo);
			if (type.IsGenericType) {
				var type_arguments = type.GetGenericArguments();
				int n = type_arguments.Length;
				if (Helper.IsUserDefinedType(typeInfo))
					writer.WriteKleiString(type.GetKTypeString());
				writer.Write((byte)n);
				for (int i = 0; i < n; i++)
					WriteType(writer, type_arguments[i]);
			} else if (Helper.IsArray(typeInfo))
				WriteType(writer, type.GetElementType());
			else if (type.IsEnum || Helper.IsUserDefinedType(typeInfo))
				writer.WriteKleiString(type.GetKTypeString());
		}

		/// <summary>
		/// The lazily initialized Klei serialization template.
		/// </summary>
		public SerializationTemplate KleiTemplate {
			get {
				if (kleiTemplate == null)
					kleiTemplate = new SerializationTemplate(targetType);
				return kleiTemplate;
			}
		}

		/// <summary>
		/// If non-null, invoked to write custom serialization data from the supplied object.
		/// </summary>
		private readonly MethodInfo customSerialize;

		/// <summary>
		/// The lazily initialized Klei template.
		/// </summary>
		private SerializationTemplate kleiTemplate;

		/// <summary>
		/// If non-null, invoked after serialization completes.
		/// </summary>
		private readonly MethodInfo onSerialized;

		/// <summary>
		/// If non-null, invoked before serialization starts.
		/// </summary>
		private readonly MethodInfo onSerializing;

		/// <summary>
		/// The fields to be serialized.
		/// </summary>
		internal readonly IDictionary<string, DeserializationFieldInfo> serializableFields;

		/// <summary>
		/// The properties to be serialized.
		/// </summary>
		internal readonly IDictionary<string, DeserializationPropertyInfo> serializableProperties;

		/// <summary>
		/// The type to be serialized.
		/// </summary>
		public readonly Type targetType;

		public FastSerializationTemplate(Type type) {
			kleiTemplate = null;
			serializableFields = new Dictionary<string, DeserializationFieldInfo>(32);
			serializableProperties = new Dictionary<string, DeserializationPropertyInfo>(32);
			targetType = type;
			type.GetSerializationMethods(typeof(OnSerializingAttribute),
				typeof(OnSerializedAttribute), typeof(CustomSerialize), out onSerializing,
				out onSerialized, out customSerialize);
			// Add fields as set by the serialization config
			var sc = GetSerializationConfig(type);
			if (sc == MemberSerialization.OptIn)
				while (type != typeof(object)) {
					AddOptInFields(type);
					AddOptInProperties(type);
					type = type.BaseType;
				}
			else if (sc == MemberSerialization.OptOut)
				while (type != typeof(object)) {
					AddPublicFields(type);
					AddPublicProperties(type);
					type = type.BaseType;
				}
		}

		/// <summary>
		/// Adds the public and private fields of the class which have explicitly included a
		/// [Serialize] attribute to serialization.
		/// </summary>
		/// <param name="type">The type to scan for fields.</param>
		private void AddOptInFields(Type type) {
			foreach (var field in type.GetFields(MEMBER_FLAGS | BindingFlags.NonPublic)) {
				object[] se = field.GetCustomAttributes(typeof(Serialize), false);
				if (se != null && se.Length > 0)
					AddValidField(field);
			}
		}

		/// <summary>
		/// Adds all public fields of the class to serialization.
		/// </summary>
		/// <param name="type">The type to scan for fields.</param>
		private void AddPublicFields(Type type) {
			foreach (var field in type.GetFields(MEMBER_FLAGS))
				AddValidField(field);
		}

		/// <summary>
		/// Adds a field to serialization if it does not have the [NonSerialized] attribute.
		/// </summary>
		/// <param name="field">The field to be serialized.</param>
		private void AddValidField(FieldInfo field) {
			object[] ns = field.GetCustomAttributes(typeof(NonSerializedAttribute), false);
			if (ns == null || ns.Length == 0)
				serializableFields.Add(field.Name, new DeserializationFieldInfo(Manager.
					GetTypeInfo(field.FieldType), field));
		}

		/// <summary>
		/// Adds the public and private properties of the class which have explicitly included
		/// a [Serialize] attribute to serialization.
		/// </summary>
		/// <param name="type">The type to scan for properties.</param>
		private void AddOptInProperties(Type type) {
			foreach (var property in type.GetProperties(MEMBER_FLAGS | BindingFlags.NonPublic))
			{
				object[] se = property.GetCustomAttributes(typeof(Serialize), false);
				if (se != null && se.Length > 0)
					AddValidProperty(property);
			}
		}

		/// <summary>
		/// Adds all public properties of the class to serialization.
		/// </summary>
		/// <param name="type">The type to scan for properties.</param>
		private void AddPublicProperties(Type type) {
			foreach (var property in type.GetProperties(MEMBER_FLAGS))
				AddValidProperty(property);
		}

		/// <summary>
		/// Adds a property to serialization if it does not have the [NonSerialized] attribute.
		/// </summary>
		/// <param name="property">The property to be serialized.</param>
		private void AddValidProperty(PropertyInfo property) {
			object[] ns = property.GetCustomAttributes(typeof(NonSerializedAttribute), false);
			// Ignore indexed properties
			if (property.GetIndexParameters().Length == 0 && (ns == null || ns.Length == 0) &&
					property.GetSetMethod() != null)
				serializableProperties.Add(property.Name, new DeserializationPropertyInfo(
					Manager.GetTypeInfo(property.PropertyType), property));
		}

		/// <summary>
		/// Serializes the data in the specified object to the stream.
		/// </summary>
		/// <param name="obj">The object to be serialized.</param>
		/// <param name="writer">The location where the data will be written.</param>
		public void SerializeData(object obj, BinaryWriter writer) {
			onSerializing?.Invoke(obj, null);
			foreach (var pair in serializableFields) {
				var sfield = pair.Value;
				try {
					FastSerializationManager.WriteValue(writer, sfield.targetType, sfield.
						GetValue(obj));
				} catch (Exception e) {
					Debug.LogErrorFormat("Error while serializing field {0} on template {1}: {2}",
						sfield.field.Name, targetType.Name, e.ToString());
					throw;
				}
			}
			foreach (var pair in serializableProperties) {
				var sprop = pair.Value;
				try {
					FastSerializationManager.WriteValue(writer, sprop.targetType, sprop.
						GetValue(obj));
				} catch (Exception e) {
					Debug.LogErrorFormat("Error while serializing property {0} on template {1}: {2}",
						sprop.property.Name, targetType.Name, e.ToString());
					throw;
				}
			}
			customSerialize?.Invoke(obj, new object[] { writer });
			onSerialized?.Invoke(obj, null);
		}

		/// <summary>
		/// Serializes the template required to deserialize this type later, even if its
		/// fields or properties have changed.
		/// </summary>
		/// <param name="writer">The location where the template will be written.</param>
		public void SerializeTemplate(BinaryWriter writer) {
			writer.Write(serializableFields.Count);
			writer.Write(serializableProperties.Count);
			foreach (var pair in serializableFields) {
				var sfield = pair.Value;
				writer.WriteKleiString(sfield.field.Name);
				WriteType(writer, sfield.field.FieldType);
			}
			foreach (var pair in serializableProperties) {
				var sprop = pair.Value;
				writer.WriteKleiString(sprop.property.Name);
				WriteType(writer, sprop.property.PropertyType);
			}
		}

		public override string ToString() {
			string result = "Template: " + targetType.FullName + "\n";
			foreach (var f in serializableFields)
				result = result + "\t" + f.ToString() + "\n";
			foreach (var p in serializableProperties)
				result = result + "\t" + p.ToString() + "\n";
			return result;
		}
	}
}
