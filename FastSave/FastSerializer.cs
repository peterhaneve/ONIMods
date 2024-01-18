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

using KSerialization;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PeterHan.FastSave {
	/// <summary>
	/// A much faster implementation of Helper.WriteValue.
	/// </summary>
	internal static partial class FastSerializationManager {
		/// <summary>
		/// Writes the length of a completed object to the specified stream.
		/// </summary>
		/// <param name="writer">The stream where the length will be serialized.</param>
		/// <param name="lengthPos">The offset in the stream where the length will be written
		/// as a signed 32 bit integer.</param>
		/// <param name="basePos">The offset in the stream where the variable length object began.</param>
		private static void WriteLength(BinaryWriter writer, long lengthPos, long basePos) {
			long endPos = writer.BaseStream.Position;
			writer.BaseStream.Position = lengthPos;
			writer.Write((int)(endPos - basePos));
			writer.BaseStream.Position = endPos;
		}

		/// <summary>
		/// Serializes a value to the specified stream.
		/// </summary>
		/// <param name="writer">The stream where the data will be serialized.</param>
		/// <param name="typeInfo">The type to be serialized.</param>
		/// <param name="data">The data to serialize.</param>
		public static void WriteValue(BinaryWriter writer, TypeInfo typeInfo, object data) {
			switch (typeInfo.info & SerializationTypeInfo.VALUE_MASK) {
			case SerializationTypeInfo.UserDefined:
				if (data != null) {
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					long basePos = writer.BaseStream.Position;
					GetFastSerializationTemplate(typeInfo.type).SerializeData(data, writer);
					WriteLength(writer, startPos, basePos);
				} else
					writer.Write(-1);
				break;
			case SerializationTypeInfo.SByte:
				writer.Write((sbyte)data);
				break;
			case SerializationTypeInfo.Byte:
				writer.Write((byte)data);
				break;
			case SerializationTypeInfo.Boolean:
				writer.Write((byte)((data is bool which && which) ? 1 : 0));
				break;
			case SerializationTypeInfo.Int16:
				writer.Write((short)data);
				break;
			case SerializationTypeInfo.UInt16:
				writer.Write((ushort)data);
				break;
			case SerializationTypeInfo.Int32:
				writer.Write((int)data);
				break;
			case SerializationTypeInfo.UInt32:
				writer.Write((uint)data);
				break;
			case SerializationTypeInfo.Int64:
				writer.Write((long)data);
				break;
			case SerializationTypeInfo.UInt64:
				writer.Write((ulong)data);
				break;
			case SerializationTypeInfo.Single:
				writer.WriteSingleFast((float)data);
				break;
			case SerializationTypeInfo.Double:
				writer.Write((double)data);
				break;
			case SerializationTypeInfo.String:
				writer.WriteKleiString((string)data);
				break;
			case SerializationTypeInfo.Enumeration:
				writer.Write((int)data);
				break;
			case SerializationTypeInfo.Vector2I:
				if (data is Vector2I vec) {
					writer.Write(vec.x);
					writer.Write(vec.y);
				} else {
					writer.Write(0);
					writer.Write(0);
				}
				break;
			case SerializationTypeInfo.Vector2:
				if (data is Vector2 vector2) {
					writer.WriteSingleFast(vector2.x);
					writer.WriteSingleFast(vector2.y);
				} else {
					writer.WriteSingleFast(0.0f);
					writer.WriteSingleFast(0.0f);
				}
				break;
			case SerializationTypeInfo.Vector3:
				if (data is Vector3 vector3) {
					writer.WriteSingleFast(vector3.x);
					writer.WriteSingleFast(vector3.y);
					writer.WriteSingleFast(vector3.z);
				} else
					for (int i = 0; i < 3; i++)
						writer.WriteSingleFast(0.0f);
				break;
			case SerializationTypeInfo.Array:
				if (data is Array array) {
					var elementType = typeInfo.subTypes[0];
					int n = array.Length;
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					writer.Write(n);
					long basePos = writer.BaseStream.Position;
					if (Helper.IsPOD(elementType.info))
						WriteArrayPOD(writer, elementType, array);
					else if (Helper.IsValueType(elementType.info)) {
						var template = GetFastSerializationTemplate(elementType.type);
						for (int i = 0; i < n; i++)
							template.SerializeData(array.GetValue(i), writer);
					} else
						for (int i = 0; i < n; i++)
							WriteValue(writer, elementType, array.GetValue(i));
					WriteLength(writer, startPos, basePos);
				} else {
					writer.Write(0);
					writer.Write(-1);
				}
				break;
			case SerializationTypeInfo.Pair:
				if (data != null) {
					TypeInfo keyType = typeInfo.subTypes[0], valueType = typeInfo.subTypes[1];
					var delegator = KeyValuePairDelegator.GetDelegates(keyType.type,
						valueType.type);
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					long basePos = writer.BaseStream.Position;
					WriteValue(writer, keyType, delegator.GetKey(data));
					WriteValue(writer, valueType, delegator.GetValue(data));
					WriteLength(writer, startPos, basePos);
				} else {
					writer.Write(4);
					writer.Write(-1);
				}
				break;
			case SerializationTypeInfo.Dictionary:
				if (data is IDictionary dict) {
					TypeInfo keyType = typeInfo.subTypes[0], valueType = typeInfo.subTypes[1];
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					writer.Write(dict.Count);
					long basePos = writer.BaseStream.Position;
					foreach (object value in dict.Values)
						WriteValue(writer, valueType, value);
					foreach (object key in dict.Keys)
						WriteValue(writer, keyType, key);
					WriteLength(writer, startPos, basePos);
				} else {
					writer.Write(0);
					writer.Write(-1);
				}
				break;
			case SerializationTypeInfo.List:
			case SerializationTypeInfo.Queue:
				if (data is ICollection list) {
					var elementType = typeInfo.subTypes[0];
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					writer.Write(list.Count);
					long basePos = writer.BaseStream.Position;
					if (Helper.IsPOD(elementType.info))
						WriteListPOD(writer, elementType, list);
					else if (Helper.IsValueType(elementType.info)) {
						var template = GetFastSerializationTemplate(elementType.type);
						foreach (object element in list)
							template.SerializeData(element, writer);
					} else
						foreach (object element in list)
							WriteValue(writer, elementType, element);
					WriteLength(writer, startPos, basePos);
				} else {
					writer.Write(0);
					writer.Write(-1);
				}
				break;
			case SerializationTypeInfo.HashSet:
				if (data is IEnumerable enumerable) {
					var elementType = typeInfo.subTypes[0];
					long startPos = writer.BaseStream.Position;
					writer.Write(0);
					writer.Write(0);
					long basePos = writer.BaseStream.Position;
					int n = 0;
					// No special case handling for POD for hash sets
					if (Helper.IsValueType(elementType.info)) {
						var template = GetFastSerializationTemplate(elementType.type);
						foreach (object element in enumerable) {
							template.SerializeData(element, writer);
							n++;
						}
					} else
						foreach (object element in enumerable) {
							WriteValue(writer, elementType, element);
							n++;
						}
					// Element count must be written along with the length
					long endPos = writer.BaseStream.Position;
					writer.BaseStream.Position = startPos;
					writer.Write((int)(endPos - basePos));
					writer.Write(n);
					writer.BaseStream.Position = endPos;
				} else {
					writer.Write(0);
					writer.Write(-1);
				}
				break;
			case SerializationTypeInfo.Colour:
				if (data is Color color) {
					writer.Write((byte)(color.r * 255f));
					writer.Write((byte)(color.g * 255f));
					writer.Write((byte)(color.b * 255f));
					writer.Write((byte)(color.a * 255f));
				} else
					for (int i = 0; i < 4; i++)
						writer.Write((byte)0);
				break;
			default:
				throw new ArgumentException("Unable to serialize type: " + typeInfo.type.
					FullName);
			}
		}

		/// <summary>
		/// Serializes an array of primitive types to the output stream.
		/// </summary>
		/// <param name="writer">The stream where the data will be serialized.</param>
		/// <param name="elementType">The element type to be serialized.</param>
		/// <param name="array">The data to serialize.</param>
		private static void WriteArrayPOD(BinaryWriter writer, TypeInfo elementType,
				Array array) {
			int n = array.Length;
			switch (elementType.info) {
			case SerializationTypeInfo.SByte:
				if (array is sbyte[] arraySByte)
					for (int i = 0; i < n; i++)
						writer.Write(arraySByte[i]);
				break;
			case SerializationTypeInfo.Byte:
				if (array is byte[] arrayByte)
					writer.Write(arrayByte);
				break;
			case SerializationTypeInfo.Int16:
				if (array is short[] arrayShort)
					for (int i = 0; i < n; i++)
						writer.Write(arrayShort[i]);
				break;
			case SerializationTypeInfo.UInt16:
				if (array is ushort[] arrayUShort)
					for (int i = 0; i < n; i++)
						writer.Write(arrayUShort[i]);
				break;
			case SerializationTypeInfo.Int32:
				if (array is int[] arrayInt)
					for (int i = 0; i < n; i++)
						writer.Write(arrayInt[i]);
				break;
			case SerializationTypeInfo.UInt32:
				if (array is uint[] arrayUInt)
					for (int i = 0; i < n; i++)
						writer.Write(arrayUInt[i]);
				break;
			case SerializationTypeInfo.Int64:
				if (array is long[] arrayLong)
					for (int i = 0; i < n; i++)
						writer.Write(arrayLong[n]);
				break;
			case SerializationTypeInfo.UInt64:
				if (array is ulong[] arrayULong)
					for (int i = 0; i < n; i++)
						writer.Write(arrayULong[i]);
				break;
			case SerializationTypeInfo.Single:
				if (array is float[] arrayFloat)
					for (int i = 0; i < n; i++)
						writer.WriteSingleFast(arrayFloat[i]);
				break;
			case SerializationTypeInfo.Double:
				if (array is double[] arrayDouble)
					for (int i = 0; i < n; i++)
						writer.Write(arrayDouble[i]);
				break;
			default:
				throw new ArgumentException("Unknown array element type: " + elementType.info);
			}
		}

		/// <summary>
		/// Serializes a collection of primitive types to the output stream.
		/// </summary>
		/// <param name="writer">The stream where the data will be serialized.</param>
		/// <param name="elementType">The element type to be serialized.</param>
		/// <param name="collection">The data to serialize.</param>
		private static void WriteListPOD(BinaryWriter writer, TypeInfo elementType,
				ICollection collection) {
			switch (elementType.info) {
			case SerializationTypeInfo.SByte:
				foreach (object element in collection)
					writer.Write((sbyte)element);
				break;
			case SerializationTypeInfo.Byte:
				foreach (object element in collection)
					writer.Write((byte)element);
				break;
			case SerializationTypeInfo.Int16:
				foreach (object element in collection)
					writer.Write((short)element);
				break;
			case SerializationTypeInfo.UInt16:
				foreach (object element in collection)
					writer.Write((ushort)element);
				break;
			case SerializationTypeInfo.Int32:
				foreach (object element in collection)
					writer.Write((int)element);
				break;
			case SerializationTypeInfo.UInt32:
				foreach (object element in collection)
					writer.Write((uint)element);
				break;
			case SerializationTypeInfo.Int64:
				foreach (object element in collection)
					writer.Write((long)element);
				break;
			case SerializationTypeInfo.UInt64:
				foreach (object element in collection)
					writer.Write((ulong)element);
				break;
			case SerializationTypeInfo.Single:
				foreach (object element in collection)
					writer.WriteSingleFast((float)element);
				break;
			case SerializationTypeInfo.Double:
				foreach (object element in collection)
					writer.Write((double)element);
				break;
			default:
				throw new ArgumentException("Unknown element type: " + elementType.info);
			}
		}
	}
}
