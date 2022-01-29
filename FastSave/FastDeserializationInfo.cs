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
using System.Runtime.Serialization;

namespace PeterHan.FastSave {
	/// <summary>
	/// Stores information required to deserialize a particular object field or property.
	/// </summary>
	internal class FastDeserializationInfo {
		/// <summary>
		/// Reads an array, which is encoded the same way for lists and plain old arrays.
		/// </summary>
		/// <param name="typeInfo">The array type to read.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		/// <param name="optimizePOD">If true, plain-old-data (POD) arrays will be deserialized
		/// using a faster method. Only can be used if they were serialized using the fast method.</param>
		/// <returns>The deserialized array, or null if it was empty.</returns>
		private static Array ReadArray(TypeInfo typeInfo, IReader reader, bool optimizePOD) {
			int n = reader.ReadInt32();
			Array array = null;
			if (n >= 0) {
				var elementType = typeInfo.subTypes[0];
				array = Array.CreateInstance(elementType.type, n);
				if (Helper.IsPOD(elementType.info) && optimizePOD)
					ReadArrayFast(array, elementType, reader);
				else if (Helper.IsValueType(elementType.info)) {
					var template = FastSerializationManager.GetFastDeserializationMapping(
						elementType.type);
					object element = template.CreateInstance();
					for (int i = 0; i < n; i++) {
						template.Deserialize(element, reader);
						array.SetValue(element, i);
					}
				} else
					for (int i = 0; i < n; i++)
						array.SetValue(ReadValue(elementType, reader, null), i);
			}
			return array;
		}

		/// <summary>
		/// Directly copies an array of primitive types from the input stream.
		/// </summary>
		/// <param name="destination">The location where the data will be stored.</param>
		/// <param name="elementType">The element type of the array.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		private static void ReadArrayFast(Array destination, TypeInfo elementType, IReader reader) {
			int length = destination.Length, bytesToCopy;
			switch (elementType.info) {
			case SerializationTypeInfo.SByte:
			case SerializationTypeInfo.Byte:
				bytesToCopy = length;
				break;
			case SerializationTypeInfo.Int16:
			case SerializationTypeInfo.UInt16:
				bytesToCopy = length * 2;
				break;
			case SerializationTypeInfo.Int32:
			case SerializationTypeInfo.UInt32:
			case SerializationTypeInfo.Single:
				bytesToCopy = length * 4;
				break;
			case SerializationTypeInfo.Int64:
			case SerializationTypeInfo.UInt64:
			case SerializationTypeInfo.Double:
				bytesToCopy = length * 8;
				break;
			default:
				throw new ArgumentException("Unknown array element type: " + elementType.info);
			}
			Buffer.BlockCopy(reader.RawBytes(), reader.Position, destination, 0, bytesToCopy);
			reader.SkipBytes(bytesToCopy);
		}

		/// <summary>
		/// Reads a basic value from the stream.
		/// </summary>
		/// <param name="typeInfo">The type to be deserialized.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		/// <param name="baseValue">The default value for this field.</param>
		/// <returns>The deserialized value.</returns>
		internal static object ReadValue(TypeInfo typeInfo, IReader reader, object baseValue) {
			object result = null;
			int n;
			var info = typeInfo.info & SerializationTypeInfo.VALUE_MASK;
			switch (info) {
			case SerializationTypeInfo.UserDefined:
				n = reader.ReadInt32();
				if (n >= 0) {
					var itemType = typeInfo.type;
					if (baseValue == null)
						result = ConstructorDelegator.CreateInstance(itemType);
					else
						result = baseValue;
					FastSerializationManager.GetFastDeserializationMapping(itemType).
						Deserialize(result, reader);
				}
				break;
			case SerializationTypeInfo.SByte:
				result = reader.ReadSByte();
				break;
			case SerializationTypeInfo.Byte:
				result = reader.ReadByte();
				break;
			case SerializationTypeInfo.Boolean:
				result = reader.ReadByte() == 1;
				break;
			case SerializationTypeInfo.Int16:
				result = reader.ReadInt16();
				break;
			case SerializationTypeInfo.UInt16:
				result = reader.ReadUInt16();
				break;
			case SerializationTypeInfo.Int32:
				result = reader.ReadInt32();
				break;
			case SerializationTypeInfo.UInt32:
				result = reader.ReadUInt32();
				break;
			case SerializationTypeInfo.Int64:
				result = reader.ReadInt64();
				break;
			case SerializationTypeInfo.UInt64:
				result = reader.ReadUInt64();
				break;
			case SerializationTypeInfo.Single:
				result = reader.ReadSingle();
				break;
			case SerializationTypeInfo.Double:
				result = reader.ReadDouble();
				break;
			case SerializationTypeInfo.String:
				result = reader.ReadKleiString();
				break;
			case SerializationTypeInfo.Enumeration:
				result = Enum.ToObject(typeInfo.type, reader.ReadInt32());
				break;
			case SerializationTypeInfo.Vector2I:
				result = reader.ReadVector2I();
				break;
			case SerializationTypeInfo.Vector2:
				result = reader.ReadVector2();
				break;
			case SerializationTypeInfo.Vector3:
				result = reader.ReadVector3();
				break;
			case SerializationTypeInfo.Array:
				reader.ReadInt32();
				result = ReadArray(typeInfo, reader, true);
				break;
			case SerializationTypeInfo.Pair:
				n = reader.ReadInt32();
				if (n >= 0) {
					TypeInfo keyType = typeInfo.subTypes[0], valueType = typeInfo.subTypes[1];
					object key = ReadValue(keyType, reader, null);
					object value = ReadValue(valueType, reader, null);
					result = KeyValuePairDelegator.GetDelegates(keyType.type, valueType.type).
						CreateInstance(key, value);
				}
				break;
			case SerializationTypeInfo.Dictionary:
				reader.ReadInt32();
				n = reader.ReadInt32();
				if (n >= 0) {
					// Preallocate dictionary to correct size
					result = DictionaryDelegator.CreateInstance(typeInfo.
						genericInstantiationType, n);
					var dict = result as System.Collections.IDictionary;
					var keyType = typeInfo.subTypes[0];
					var valueType = typeInfo.subTypes[1];
					var values = ListPool<object, FastDeserializationMapping>.Allocate();
					for (int i = 0; i < n; i++)
						values.Add(ReadValue(valueType, reader, null));
					for (int i = 0; i < n; i++)
						dict.Add(ReadValue(keyType, reader, null), values[i]);
					values.Recycle();
				}
				break;
			case SerializationTypeInfo.HashSet:
				reader.ReadInt32();
				result = ReadArray(typeInfo, reader, false);
				if (result != null)
					result = CollectionDelegator.CreateInstance(typeInfo.
						genericInstantiationType, result);
				break;
			case SerializationTypeInfo.List:
			case SerializationTypeInfo.Queue:
				reader.ReadInt32();
				result = ReadArray(typeInfo, reader, true);
				if (result != null)
					// Due to how POD lists/queues are encoded, must go through a temporary
					// array to make best usage of ReadArrayFast, sorry memory usage
					result = CollectionDelegator.CreateInstance(typeInfo.
						genericInstantiationType, result);
				break;
			case SerializationTypeInfo.Colour:
				result = reader.ReadColour();
				break;
			default:
				throw new ArgumentException("Unknown type " + info);
			}
			return result;
		}

		/// <summary>
		/// The field or property type to be deserialized.
		/// </summary>
		public readonly TypeInfo targetType;

		/// <summary>
		/// If false, this data field is invalid and should be skipped.
		/// </summary>
		public readonly bool valid;

		public FastDeserializationInfo(bool valid, TypeInfo target) {
			this.valid = valid;
			targetType = target;
		}

		/// <summary>
		/// Reads and deserializes a value from the specified stream.
		/// </summary>
		/// <param name="obj">The default value of the object.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		public virtual void Read(object obj, IReader reader) {
			if (!valid) {
				int skipBytes;
				// If type is unknown, discard the information
				var valueInfo = targetType.info & SerializationTypeInfo.VALUE_MASK;
				switch (valueInfo) {
				case SerializationTypeInfo.Array:
				case SerializationTypeInfo.Dictionary:
				case SerializationTypeInfo.List:
				case SerializationTypeInfo.HashSet:
				case SerializationTypeInfo.Queue:
					skipBytes = reader.ReadInt32();
					// If it has elements
					if (reader.ReadInt32() > -1)
						reader.SkipBytes(skipBytes);
					break;
				case SerializationTypeInfo.Pair:
				case SerializationTypeInfo.UserDefined:
					skipBytes = reader.ReadInt32();
					if (skipBytes > 0)
						reader.SkipBytes(skipBytes);
					break;
				default:
					SkipValue(valueInfo, reader);
					break;
				}
			}
		}

		/// <summary>
		/// Skips a value from the input stream, advancing the read pointer but storing no
		/// actual data.
		/// </summary>
		/// <param name="typeInfo">The type of the element to be skipped.</param>
		/// <param name="reader">The stream containing serialized data.</param>
		protected void SkipValue(SerializationTypeInfo typeInfo, IReader reader) {
			int length;
			switch (typeInfo) {
			case SerializationTypeInfo.SByte:
			case SerializationTypeInfo.Byte:
			case SerializationTypeInfo.Boolean:
				reader.SkipBytes(1);
				return;
			case SerializationTypeInfo.Int16:
			case SerializationTypeInfo.UInt16:
				reader.SkipBytes(2);
				return;
			case SerializationTypeInfo.Int32:
			case SerializationTypeInfo.UInt32:
			case SerializationTypeInfo.Single:
			case SerializationTypeInfo.Enumeration:
				reader.SkipBytes(4);
				return;
			case SerializationTypeInfo.Int64:
			case SerializationTypeInfo.UInt64:
			case SerializationTypeInfo.Double:
			case SerializationTypeInfo.Vector2I:
			case SerializationTypeInfo.Vector2:
				reader.SkipBytes(8);
				return;
			case SerializationTypeInfo.String:
				length = reader.ReadInt32();
				if (length > 0)
					reader.SkipBytes(length);
				return;
			case SerializationTypeInfo.Vector3:
				reader.SkipBytes(12);
				return;
			case SerializationTypeInfo.Colour:
				reader.SkipBytes(4);
				return;
			}
			throw new ArgumentException("Unhandled type for skipping: " + typeInfo);
		}

		public override string ToString() {
			return string.Format("DeserializationInfo[valid={0},type={1}]", valid,
				targetType.type.FullName);
		}
	}
}
