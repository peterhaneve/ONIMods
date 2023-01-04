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

using Klei.AI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeterHan.FastTrack.GamePatches {
	/// <summary>
	/// A hacked extension of Klei.AI.AttributeConverter to quickly look up other attributes.
	/// </summary>
	[SkipSaveFileSerialization]
	internal sealed class LookupAttributeConverter : AttributeConverterInstance {
		/// <summary>
		/// Gets the fast attribute converter lookup, or creates them if they do not exist.
		/// </summary>
		/// <param name="converters">The attribute converters to query.</param>
		/// <returns>The fake converter to use for looking them up.</returns>
		public static LookupAttributeConverter GetConverterLookup(
				AttributeConverters converters) {
			LookupAttributeConverter lookup = null;
			if (converters != null) {
				var convList = converters.converters;
				if (!(convList[0] is LookupAttributeConverter lol)) {
					lol = new LookupAttributeConverter(converters.gameObject, converters);
					convList.Insert(0, lol);
				}
				lookup = lol;
			}
			return lookup;
		}

		/// <summary>
		/// A cached lookup of attribute converter names to converters.
		/// </summary>
		private readonly IDictionary<string, AttributeConverterInstance> attrConverters;

		internal LookupAttributeConverter(GameObject go, AttributeConverters converters) :
				base(go, null, new AttributeInstance(go, LookupAttributeLevel.LOOKUP_ATTR)) {
			if (go == null)
				throw new ArgumentNullException(nameof(go));
			if (converters == null)
				throw new ArgumentNullException(nameof(converters));
			var convList = converters.converters;
			int n = convList.Count;
			attrConverters = new Dictionary<string, AttributeConverterInstance>(n);
			for (int i = 0; i < n; i++) {
				var converterInstance = convList[i];
				attrConverters.Add(converterInstance.converter.Id, converterInstance);
			}
		}

		/// <summary>
		/// Gets an attribute converter instance.
		/// </summary>
		/// <param name="converter">The attribute converter to look up.</param>
		/// <returns>The instance of that converter for this Duplicant.</returns>
		public AttributeConverterInstance Get(AttributeConverter converter) {
			if (converter == null || !attrConverters.TryGetValue(converter.Id,
					out var instance))
				instance = null;
			return instance;
		}

		/// <summary>
		/// Gets an attribute converter instance by its ID.
		/// </summary>
		/// <param name="id">The attribute converter's ID.</param>
		/// <returns>The instance of that converter ID for this Duplicant.</returns>
		public AttributeConverterInstance GetConverter(string id) {
			if (id == null || !attrConverters.TryGetValue(id, out var instance))
				instance = null;
			return instance;
		}
	}
}
