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

using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace PeterHan.FastSave {
	/// <summary>
	/// Pools delegates to constructors for key value pairs, and delegates to get their keys
	/// and values after creation.
	/// </summary>
	public static class KeyValuePairDelegator {
		private delegate object ConstructDelegate(object key, object value);

		/// <summary>
		/// Stores delegates for KeyValuePairs from their types.
		/// </summary>
		private static readonly IDictionary<PairHashKey, KeyValuePairDelegates> PAIR_TYPES =
			new Dictionary<PairHashKey, KeyValuePairDelegates>(512);

		/// <summary>
		/// The fast version of Activator.CreateInstance that caches delegates for very
		/// high performance.
		/// </summary>
		/// <param name="keyType">The key type to construct.</param>
		/// <param name="valueType">The value type to construct.</param>
		/// <returns>An instance of KeyValuePair&lt;keyType, valueType&gt;.</returns>
		public static KeyValuePairDelegates GetDelegates(Type keyType, Type valueType) {
			if (keyType == null)
				throw new ArgumentNullException(nameof(keyType));
			if (valueType == null)
				throw new ArgumentNullException(nameof(valueType));
			var lookup = new PairHashKey(keyType, valueType);
			if (!PAIR_TYPES.TryGetValue(lookup, out KeyValuePairDelegates delegates))
				PAIR_TYPES.Add(lookup, delegates = new KeyValuePairDelegates(keyType,
					valueType));
			return delegates;
		}

		/// <summary>
		/// The key type used for the lookup dictionary. Like KeyValuePair but has a useful
		/// GetHashCode and Equals.
		/// </summary>
		private struct PairHashKey {
			/// <summary>
			/// The pair's key type.
			/// </summary>
			public readonly Type keyType;

			/// <summary>
			/// The pair's value type.
			/// </summary>
			public readonly Type valueType;

			public PairHashKey(Type keyType, Type valueType) {
				this.keyType = keyType;
				this.valueType = valueType;
			}

			public override bool Equals(object obj) {
				return obj is PairHashKey other && other.keyType == keyType && other.
					valueType == valueType;
			}

			public override int GetHashCode() {
				return keyType.GetHashCode() ^ valueType.GetHashCode();
			}

			public override string ToString() {
				return "<" + keyType.FullName + ", " + valueType.FullName + ">";
			}
		}

		/// <summary>
		/// Stores delegates for a KVP's constructors and Key/Value properties.
		/// </summary>
		public sealed class KeyValuePairDelegates {
			/// <summary>
			/// A delegate to invoke the two argument constructor.
			/// </summary>
			private readonly ConstructDelegate construct;

			/// <summary>
			/// A delegate to retrieve the key property.
			/// </summary>
			private readonly DeserializationPropertyInfo.GetValueDelegate getKey;

			/// <summary>
			/// A delegate to retrieve the value property.
			/// </summary>
			private readonly DeserializationPropertyInfo.GetValueDelegate getValue;

			/// <summary>
			/// The type to construct with generic parameters filled in.
			/// </summary>
			private readonly Type targetType;

			public KeyValuePairDelegates(Type keyType, Type valueType) {
				if (keyType == null)
					throw new ArgumentNullException(nameof(keyType));
				if (valueType == null)
					throw new ArgumentNullException(nameof(valueType));
				targetType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
				construct = GenerateConstructor(keyType, valueType);
				if (construct == null)
					throw new ArgumentException("No constructor found for " + keyType.
						FullName + ", " + valueType.FullName);
				getKey = DeserializationPropertyInfo.GenerateGetter(targetType.GetProperty(
					nameof(KeyValuePair<int, int>.Key)));
				if (getKey == null)
					throw new ArgumentException("No key found for " + keyType.FullName);
				getValue = DeserializationPropertyInfo.GenerateGetter(targetType.GetProperty(
					nameof(KeyValuePair<int, int>.Value)));
				if (getValue == null)
					throw new ArgumentException("No value found for " + valueType.FullName);
			}

			/// <summary>
			/// Creates an instance of this pair.
			/// </summary>
			/// <param name="key">The key of the pair.</param>
			/// <param name="value">The value of the pair.</param>
			/// <returns>An instance of KeyValuePair holding these objects.</returns>
			public object CreateInstance(object key, object value) {
				return construct.Invoke(key, value);
			}

			/// <summary>
			/// Creates a constructor delegate.
			/// </summary>
			/// <param name="keyType">The type of the pair's key.</param>
			/// <param name="valueType">The type of the pair's value.</param>
			/// <returns>The delegate which can construct this object.</returns>
			private ConstructDelegate GenerateConstructor(Type keyType, Type valueType) {
				var constructorToUse = targetType.GetConstructor(PPatchTools.BASE_FLAGS |
					BindingFlags.Instance, null, new Type[] { keyType, valueType }, null);
				ConstructDelegate result;
				if (constructorToUse == null)
					result = null;
				else {
					var constructor = new DynamicMethod("Construct", typeof(object), new Type[]
					{
						typeof(object), typeof(object)
					}, targetType, true);
					var generator = constructor.GetILGenerator();
					// Push the arguments
					generator.Emit(OpCodes.Ldarg_0);
					if (keyType.IsValueType)
						generator.Emit(OpCodes.Unbox_Any, keyType);
					generator.Emit(OpCodes.Ldarg_1);
					if (valueType.IsValueType)
						generator.Emit(OpCodes.Unbox_Any, valueType);
					generator.Emit(OpCodes.Newobj, constructorToUse);
					// Box it
					generator.Emit(OpCodes.Box, targetType);
					generator.Emit(OpCodes.Ret);
					result = constructor.CreateDelegate(typeof(ConstructDelegate)) as
						ConstructDelegate;
				}
				return result;
			}

			/// <summary>
			/// Gets a key from a boxed pair.
			/// </summary>
			/// <param name="pair">The pair to inspect.</param>
			/// <returns>The key of that pair, boxed if necessary.</returns>
			public object GetKey(object pair) {
				return getKey.Invoke(pair);
			}

			/// <summary>
			/// Gets a value from a boxed pair.
			/// </summary>
			/// <param name="pair">The pair to inspect.</param>
			/// <returns>The value of that pair, boxed if necessary.</returns>
			public object GetValue(object pair) {
				return getValue.Invoke(pair);
			}
		}
	}
}
