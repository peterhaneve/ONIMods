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

using KSerialization;
using System;
using System.Collections.Generic;

namespace PeterHan.FastSave {
	/// <summary>
	/// A much, much faster version of KSerialization.DeserializationMapping that uses
	/// delegates and some dodgy casting instead of reflection where possible.
	/// </summary>
	public sealed class FastDeserializationMapping {
		/// <summary>
		/// Stores a list of all deserializable members (fields and properties).
		/// </summary>
		private readonly IList<FastDeserializationInfo> members;

		/// <summary>
		/// The template to use for deserialization.
		/// </summary>
		private readonly DeserializationTemplate template;

		/// <summary>
		/// The type that is being deserialized.
		/// </summary>
		private readonly Type targetType;

		public FastDeserializationMapping(DeserializationTemplate inTemplate,
				FastSerializationTemplate outTemplate) {
			targetType = outTemplate.targetType;
			template = inTemplate;
			members = new List<FastDeserializationInfo>(16);
			// Dictionaries look up faster than lists!
			var fields = outTemplate.serializableFields;
			var properties = outTemplate.serializableProperties;
			foreach (var memberInfo in inTemplate.serializedMembers) {
				FastDeserializationInfo dinfo;
				string name = memberInfo.name;
				var typeInfo = memberInfo.typeInfo;
				if (fields.TryGetValue(name, out var outField) && typeInfo.Equals(outField.
						targetType))
					dinfo = outField;
				else if (properties.TryGetValue(name, out var outProperty) && typeInfo.Equals(
						outProperty.targetType))
					dinfo = outProperty;
				else
					dinfo = new FastDeserializationInfo(false, typeInfo);
				if (typeInfo.type == null)
					Debug.LogWarningFormat("Tried to deserialize field '{0}' on type {1} but it no longer exists",
						name, inTemplate.typeName);
				members.Add(dinfo);
			}
		}

		/// <summary>
		/// Creates an instance of the mapped type.
		/// </summary>
		/// <returns>An instance of that type.</returns>
		public object CreateInstance() {
			return ConstructorDelegator.CreateInstance(targetType);
		}

		/// <summary>
		/// Deserializes the specified object from the input stream.
		/// </summary>
		/// <param name="obj">The default value of the object.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		public void Deserialize(object obj, IReader reader) {
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			template.onDeserializing?.Invoke(obj, null);
			foreach (var dinfo in members)
				dinfo.Read(obj, reader);
			template.customDeserialize?.Invoke(obj, new object[] { reader });
			template.onDeserialized?.Invoke(obj, null);
		}
	}
}
