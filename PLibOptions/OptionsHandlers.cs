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

using System;
using System.Collections.Generic;
using System.Reflection;
using PeterHan.PLib.Core;
using PeterHan.PLib.Detours;
using UnityEngine;

namespace PeterHan.PLib.Options {
	/// <summary>
	/// Registers types to options entry classes that can handle them.
	/// </summary>
	public static class OptionsHandlers {
		private delegate IOptionsEntry CreateOption(string field, IOptionSpec spec);

		private delegate IOptionsEntry CreateOptionType(string field, IOptionSpec spec,
			Type fieldType);

		private delegate IOptionsEntry CreateOptionLimit(string field, IOptionSpec spec,
			LimitAttribute limit);

		/// <summary>
		/// Maps types to the constructor delegate that can create an options entry for them.
		/// </summary>
		private static readonly IDictionary<Type, Delegate> OPTIONS_HANDLERS =
			new Dictionary<Type, Delegate>(64);

		/// <summary>
		/// Adds a custom type to handle all options entries of a specific type. The change
		/// will only affect this mod's options.
		/// </summary>
		/// <param name="optionType">The property type to be handled.</param>
		/// <param name="handlerType">The type which will handle all option attributes of
		/// this type. It must subclass from IOptionsEntry and have a constructor of the
		/// signature HandlerType(string, IOptionSpec).</param>
		public static void AddOptionClass(Type optionType, Type handlerType) {
			if (optionType != null && handlerType != null && !OPTIONS_HANDLERS.
					ContainsKey(optionType) && typeof(IOptionsEntry).IsAssignableFrom(
					handlerType)) {
				var constructors = handlerType.GetConstructors();
				int n = constructors.Length;
				for (int i = 0; i < n; i++) {
					var del = CreateDelegate(handlerType, constructors[i]);
					if (del != null) {
						OPTIONS_HANDLERS[optionType] = del;
						break;
					}
				}
			}
		}

		/// <summary>
		/// If a candidate options entry constructor is valid, creates a delegate which can
		/// call the constructor.
		/// </summary>
		/// <param name="constructor">The constructor to wrap.</param>
		/// <param name="handlerType">The type which will handle all option attributes of this type.</param>
		/// <returns>If the constructor can be used to create an options entry, a delegate
		/// which calls the constructor using one of the delegate types declared in this class;
		/// otherwise, null.</returns>
		private static Delegate CreateDelegate(Type handlerType, ConstructorInfo constructor) {
			var param = constructor.GetParameters();
			int n = param.Length;
			Delegate result = null;
			// Must begin with string, IOptionsSpec
			if (n > 1 && param[0].ParameterType.IsAssignableFrom(typeof(string)) &&
					param[1].ParameterType.IsAssignableFrom(typeof(IOptionSpec))) {
				switch (n) {
				case 2:
					result = constructor.Detour<CreateOption>();
					break;
				case 3:
					var extraType = param[2].ParameterType;
					if (extraType.IsAssignableFrom(typeof(LimitAttribute)))
						result = constructor.Detour<CreateOptionLimit>();
					else if (extraType.IsAssignableFrom(typeof(Type)))
						result = constructor.Detour<CreateOptionType>();
					break;
				default:
					PUtil.LogWarning("Constructor on options handler type " + handlerType +
						" cannot be constructed by OptionsHandlers");
					break;
				}
			}
			return result;
		}
		
		/// <summary>
		/// Creates an options entry wrapper for the specified property.
		/// </summary>
		/// <param name="info">The property to wrap.</param>
		/// <param name="spec">The option title and tool tip.</param>
		/// <returns>An options wrapper, or null if none can handle this type.</returns>
		public static IOptionsEntry FindOptionClass(IOptionSpec spec, PropertyInfo info) {
			IOptionsEntry entry = null;
			if (spec != null && info != null) {
				var type = info.PropertyType;
				string field = info.Name;
				if (type.IsEnum)
					// Enumeration type
					entry = new SelectOneOptionsEntry(field, spec, type);
				else if (OPTIONS_HANDLERS.TryGetValue(type, out var handler)) {
					if (handler is CreateOption createOption)
						entry = createOption.Invoke(field, spec);
					else if (handler is CreateOptionLimit createOptionLimit)
						entry = createOptionLimit.Invoke(field, spec, info.
							GetCustomAttribute<LimitAttribute>());
					else if (handler is CreateOptionType createOptionType)
						entry = createOptionType.Invoke(field, spec, type);
				}
			}
			return entry;
		}

		/// <summary>
		/// Adds the predefined options classes.
		/// </summary>
		internal static void InitPredefinedOptions() {
			if (OPTIONS_HANDLERS.Count < 1) {
				AddOptionClass(typeof(bool), typeof(CheckboxOptionsEntry));
				AddOptionClass(typeof(int), typeof(IntOptionsEntry));
				AddOptionClass(typeof(int?), typeof(NullableIntOptionsEntry));
				AddOptionClass(typeof(float), typeof(FloatOptionsEntry));
				AddOptionClass(typeof(float?), typeof(NullableFloatOptionsEntry));
				AddOptionClass(typeof(Color32), typeof(Color32OptionsEntry));
				AddOptionClass(typeof(Color), typeof(ColorOptionsEntry));
				AddOptionClass(typeof(string), typeof(StringOptionsEntry));
				AddOptionClass(typeof(Action<object>), typeof(ButtonOptionsEntry));
				AddOptionClass(typeof(LocText), typeof(TextBlockOptionsEntry));
			}
		}
	}
}
