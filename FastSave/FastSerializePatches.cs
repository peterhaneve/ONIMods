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

using HarmonyLib;
using KSerialization;
using PeterHan.PLib.Core;
using System;
using System.IO;

namespace PeterHan.FastSave {
	internal static class FastSerializePatches {
		/// <summary>
		/// Applied to Deserializer to use the faster DeserializationMapping classes.
		/// </summary>
		[HarmonyPatch]
		public static class Deserializer_Deserialize_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			internal static System.Reflection.MethodBase TargetMethod() {
				// Cannot make an out type in an annotation :/
				return typeof(Deserializer).GetMethodSafe(nameof(Deserializer.Deserialize),
					true, typeof(Type), typeof(IReader), typeof(object).MakeByRefType());
			}

			/// <summary>
			/// Applied before Deserialize runs.
			/// </summary>
			internal static bool Prefix(Type type, IReader reader, out object result,
					ref bool __result) {
				var mapping = FastSerializationManager.GetFastDeserializationMapping(type);
				try {
					object obj = mapping.CreateInstance();
					mapping.Deserialize(obj, reader);
					result = obj;
				} catch (Exception e) {
					Debug.LogErrorFormat("Exception occurred while attempting to deserialize into object of type {0}.\n{1}",
						type.ToString(), e.ToString());
					throw;
				}
				__result = true;
				return false;
			}
		}

		/// <summary>
		/// Applied to Deserializer to use the faster DeserializationMapping classes.
		/// </summary>
		[HarmonyPatch(typeof(Deserializer), nameof(Deserializer.DeserializeTypeless),
			typeof(Type), typeof(object), typeof(IReader))]
		public static class Deserializer_DeserializeTypeless_Long_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before DeserializeTypeless runs.
			/// </summary>
			internal static bool Prefix(Type type, object obj, IReader reader,
					ref bool __result) {
				var mapping = FastSerializationManager.GetFastDeserializationMapping(type);
				try {
					mapping.Deserialize(obj, reader);
				} catch (Exception e) {
					Debug.LogErrorFormat("Exception occurred while attempting to deserialize object {0}({1}).\n{2}",
						obj, obj.GetType(), e.ToString());
					throw;
				}
				__result = true;
				return false;
			}
		}

		/// <summary>
		/// Applied to Deserializer to use the faster DeserializationMapping classes.
		/// </summary>
		[HarmonyPatch(typeof(Deserializer), nameof(Deserializer.DeserializeTypeless),
			typeof(object), typeof(IReader))]
		public static class Deserializer_DeserializeTypeless_Short_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before DeserializeTypeless runs.
			/// </summary>
			internal static bool Prefix(object obj, IReader reader, ref bool __result) {
				Type type = obj.GetType();
				var mapping = FastSerializationManager.GetFastDeserializationMapping(type);
				try {
					mapping.Deserialize(obj, reader);
				} catch (Exception e) {
					Debug.LogErrorFormat("Exception occurred while attempting to deserialize object {0}({1}).\n{2}",
						obj, obj.GetType(), e.ToString());
					throw;
				}
				__result = true;
				return false;
			}
		}

		/// <summary>
		/// Applied to Helper to replace the serialization procedure with a much faster version
		/// using the fast templates.
		/// </summary>
		[HarmonyPatch(typeof(Helper), "WriteValue")]
		public static class Helper_WriteValue_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before WriteValue runs.
			/// </summary>
			internal static bool Prefix(BinaryWriter writer, TypeInfo type_info, object value) {
				FastSerializationManager.WriteValue(writer, type_info, value);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to clear our cache when the Klei cache is cleared.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.Clear))]
		public static class Manager_Clear_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before Clear runs.
			/// </summary>
			internal static bool Prefix() {
				FastSerializationManager.ClearSmart();
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to read the list of serializable types into the database in
		/// this class.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.DeserializeDirectory))]
		public static class Manager_DeserializeDirectory_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before DeserializeDirectory runs.
			/// </summary>
			internal static bool Prefix(IReader reader) {
				FastSerializationManager.DeserializeDirectory(reader);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to replace deserialization mappings with ones that actually are
		/// fast.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.GetDeserializationMapping), typeof(Type))]
		public static class Manager_GetDeserializationMapping_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before GetDeserializationMapping runs.
			/// </summary>
			internal static bool Prefix(Type type, ref DeserializationMapping __result) {
				__result = FastSerializationManager.GetKleiDeserializationMapping(type);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to ensure that anything requesting a deserialization template
		/// uses this method (should have no references left, but for future proofing).
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.GetDeserializationTemplate), typeof(Type))]
		public static class Manager_GetDeserializationTemplate_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before GetDeserializationTemplate runs.
			/// </summary>
			internal static bool Prefix(Type type, ref DeserializationTemplate __result) {
				__result = FastSerializationManager.GetDeserializationTemplate(type);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to replace serialization templates with references to this class.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.GetSerializationTemplate), typeof(Type))]
		public static class Manager_GetSerializationTemplate_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before GetSerializationTemplate runs.
			/// </summary>
			internal static bool Prefix(Type type, ref SerializationTemplate __result) {
				__result = FastSerializationManager.GetKleiSerializationTemplate(type);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to short circuit a tiny bit of overhead when checking for
		/// deserialization mappings.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.HasDeserializationMapping))]
		public static class Manager_HasDeserializationMapping_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before HasDeserializationMapping runs.
			/// </summary>
			internal static bool Prefix(Type type, ref bool __result) {
				__result = FastSerializationManager.HasDeserializationMapping(type);
				return false;
			}
		}

		/// <summary>
		/// Applied to Manager to write the list of serializable types using the database in
		/// this class.
		/// </summary>
		[HarmonyPatch(typeof(Manager), nameof(Manager.SerializeDirectory))]
		public static class Manager_SerializeDirectory_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before SerializeDirectory runs.
			/// </summary>
			internal static bool Prefix(BinaryWriter writer) {
				FastSerializationManager.SerializeDirectory(writer);
				return false;
			}
		}

		/// <summary>
		/// Applied to Serializer to use the faster versions for serialization.
		/// </summary>
		[HarmonyPatch(typeof(Serializer), nameof(Serializer.Serialize), typeof(object),
			typeof(BinaryWriter))]
		public static class Serializer_Serialize_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before Serialize runs.
			/// </summary>
			internal static bool Prefix(object obj, BinaryWriter writer) {
				var type = obj.GetType();
				var template = FastSerializationManager.GetFastSerializationTemplate(type);
				writer.WriteKleiString(type.GetKTypeString());
				template.SerializeData(obj, writer);
				return false;
			}
		}

		/// <summary>
		/// Applied to Serializer to use the faster versions for serialization.
		/// </summary>
		[HarmonyPatch(typeof(Serializer), nameof(Serializer.SerializeTypeless), typeof(object),
			typeof(BinaryWriter))]
		public static class Serializer_SerializeTypeless_Patch {
			internal static bool Prepare() {
				return FastSaveOptions.Instance.DelegateSave;
			}

			/// <summary>
			/// Applied before SerializeTypeless runs.
			/// </summary>
			internal static bool Prefix(object obj, BinaryWriter writer) {
				var type = obj.GetType();
				var template = FastSerializationManager.GetFastSerializationTemplate(type);
				template.SerializeData(obj, writer);
				return false;
			}
		}
	}
}
