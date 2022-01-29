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

namespace PeterHan.FastSave {
	/// <summary>
	/// A faster version of KSerialization.Manager which dispenses with the useless type name
	/// to manager dictionaries and adds a proper hash code to the mappings.
	/// </summary>
	internal static partial class FastSerializationManager {
		// Base dictionaries were not thread safe, so neither will these be
		private static readonly IDictionary<Type, FastDeserializationTemplate>
			DESERIALIZATION_TEMPLATES = new Dictionary<Type, FastDeserializationTemplate>(512);

		private static readonly IDictionary<FastDeserializationTemplate, FastDeserializationMapping>
			MAPPINGS = new Dictionary<FastDeserializationTemplate, FastDeserializationMapping>(512);

		private static readonly IDictionary<FastDeserializationTemplate, DeserializationMapping>
			MAPPINGS_LEGACY = new Dictionary<FastDeserializationTemplate, DeserializationMapping>(512);

		private static readonly IDictionary<Type, FastSerializationTemplate>
			SERIALIZATION_TEMPLATES = new Dictionary<Type, FastSerializationTemplate>(512);

		private static readonly IDictionary<Type, FastSerializationTemplate>
			SERIALIZATION_TEMPLATES_ACTIVE = new Dictionary<Type, FastSerializationTemplate>(512);

		/// <summary>
		/// Clears all cached serialization information.
		/// </summary>
		public static void Clear() {
			Helper.ClearTypeInfoMask();
			DESERIALIZATION_TEMPLATES.Clear();
			MAPPINGS_LEGACY.Clear();
			MAPPINGS.Clear();
			SERIALIZATION_TEMPLATES.Clear();
			SERIALIZATION_TEMPLATES_ACTIVE.Clear();
		}

		/// <summary>
		/// Instead of clearing all serialization information, marks the unused ones as clean,
		/// and only serializes dirty information in the next pass.
		/// </summary>
		public static void ClearSmart() {
			Helper.ClearTypeInfoMask();
			// No need to clear the serialization templates or type infos, as those are only
			// based on the types currently in the appdomain which do not change (we hope).
			// Clear the mappings and deserialization templates as they could change from
			// save to save.
			DESERIALIZATION_TEMPLATES.Clear();
			SERIALIZATION_TEMPLATES_ACTIVE.Clear();
			MAPPINGS_LEGACY.Clear();
			MAPPINGS.Clear();
		}

		/// <summary>
		/// Deserializes the list of deserialization templates from the specified stream.
		/// The old ones must be cleared as different saves might have different directories
		/// for the same classes.
		/// </summary>
		/// <param name="reader">The location to read the templates.</param>
		public static void DeserializeDirectory(IReader reader) {
			int n = reader.ReadInt32();
			ClearSmart();
			for (int i = 0; i < n; i++) {
				string typeName = reader.ReadKleiString();
				try {
					// Sure the template does not get stored for unknown types, but it was dead
					// anyways in the Klei version
					var template = new FastDeserializationTemplate(typeName, reader);
					var type = Manager.GetType(typeName);
					if (type != null)
						DESERIALIZATION_TEMPLATES[type] = template;
				} catch (Exception e) {
					DebugUtil.LogErrorArgs("Error deserializing template " + typeName + ": " +
						e.Message, e.StackTrace);
					throw;
				}
			}
		}

		/// <summary>
		/// Gets the cached deseralization template for the specified type.
		/// </summary>
		/// <param name="type">The type to look up.</param>
		/// <returns>The existing template for that type, or null if none is cached.</returns>
		internal static FastDeserializationTemplate GetDeserializationTemplate(Type type) {
			DESERIALIZATION_TEMPLATES.TryGetValue(type, out FastDeserializationTemplate
				template);
			return template;
		}

		/// <summary>
		/// Gets the fast deserialization mapping for the specified type.
		/// </summary>
		/// <param name="type">The type to look up.</param>
		/// <returns>The deserialization mapping for that type.</returns>
		public static FastDeserializationMapping GetFastDeserializationMapping(Type type) {
			var dtemplate = GetDeserializationTemplate(type);
			if (dtemplate == null)
				throw new ArgumentException("Tried to deserialize a class named: " +
					type.GetKTypeString() + " but no such class exists");
			var stemplate = GetFastSerializationTemplate(type);
			if (stemplate == null)
				throw new ArgumentException("Tried to deserialize into a class named: " +
					type.GetKTypeString() + " but no such class exists");
			if (!MAPPINGS.TryGetValue(dtemplate, out FastDeserializationMapping mapping)) {
				mapping = new FastDeserializationMapping(dtemplate, stemplate);
				MAPPINGS.Add(dtemplate, mapping);
			}
			return mapping;
		}

		/// <summary>
		/// Gets the serialization template for the specified type.
		/// </summary>
		/// <param name="type">The type to look up.</param>
		/// <returns>The template for that type.</returns>
		public static FastSerializationTemplate GetFastSerializationTemplate(Type type) {
			if (type == null)
				throw new ArgumentNullException(nameof(type),
					"Invalid type encountered when serializing");
			if (!SERIALIZATION_TEMPLATES_ACTIVE.TryGetValue(type, out FastSerializationTemplate
					template)) {
				if (!SERIALIZATION_TEMPLATES.TryGetValue(type, out FastSerializationTemplate
						cached)) {
					cached = new FastSerializationTemplate(type);
					SERIALIZATION_TEMPLATES.Add(type, cached);
				}
				SERIALIZATION_TEMPLATES_ACTIVE.Add(type, template = cached);
			}
			return template;
		}

		/// <summary>
		/// Gets the deserialization mapping for the specified type.
		/// </summary>
		/// <param name="type">The type to look up.</param>
		/// <returns>The deserialization mapping for that type.</returns>
		public static DeserializationMapping GetKleiDeserializationMapping(Type type) {
			var dtemplate = GetDeserializationTemplate(type);
			if (dtemplate == null)
				throw new ArgumentException("Tried to deserialize a class named: " +
					type.GetKTypeString() + " but no such class exists");
			var stemplate = GetKleiSerializationTemplate(type);
			if (stemplate == null)
				throw new ArgumentException("Tried to deserialize into a class named: " +
					type.GetKTypeString() + " but no such class exists");
			if (!MAPPINGS_LEGACY.TryGetValue(dtemplate, out DeserializationMapping mapping)) {
				mapping = new DeserializationMapping(dtemplate, stemplate);
				MAPPINGS_LEGACY.Add(dtemplate, mapping);
			}
			return mapping;
		}

		/// <summary>
		/// Gets the serialization template for the specified type.
		/// </summary>
		/// <param name="type">The type to look up.</param>
		/// <returns>The template for that type.</returns>
		public static SerializationTemplate GetKleiSerializationTemplate(Type type) {
			return GetFastSerializationTemplate(type).KleiTemplate;
		}

		/// <summary>
		/// Checks to see if there is a known serialization mapping for the specified type.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if it has a serialization mapping, or false otherwise.</returns>
		public static bool HasDeserializationMapping(Type type) {
			return GetDeserializationTemplate(type) != null && GetFastSerializationTemplate(
				type) != null;
		}

		/// <summary>
		/// Serializes the list of serialization templates to the specified stream.
		/// </summary>
		/// <param name="writer">The stream where the templates will be serialized.</param>
		public static void SerializeDirectory(BinaryWriter writer) {
			writer.Write(SERIALIZATION_TEMPLATES_ACTIVE.Count);
			foreach (var pair in SERIALIZATION_TEMPLATES_ACTIVE) {
				string type = pair.Key.GetKTypeString();
				try {
					writer.WriteKleiString(type);
					pair.Value.SerializeTemplate(writer);
				} catch (Exception e) {
					DebugUtil.LogErrorArgs("Error serializing template " + type + ": " + e.
						Message, e.StackTrace);
				}
			}
		}

		/// <summary>
		/// A version of DeserializationTemplate that actually can be put in a Dictionary.
		/// </summary>
		internal sealed class FastDeserializationTemplate : DeserializationTemplate {
			public FastDeserializationTemplate(string template_type_name, IReader reader) :
				base(template_type_name, reader) { }

			public override bool Equals(object obj) {
				return obj is DeserializationTemplate other && other.typeName == typeName;
			}

			public override int GetHashCode() {
				return typeName.GetHashCode();
			}

			public override string ToString() {
				return string.Format("FastDeserializationTemplate[type={0}]", typeName);
			}
		}
	}
}
