/*
 * Copyright 2020 Peter Han
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

namespace PeterHan.PLib {
	/// <summary>
	/// A method information object that automatically handles optional parameters. Klei likes
	/// to add these, but they are not binary compatible, so this method allows handling both
	/// cases.
	/// 
	/// Consider using PDetours in cases where performance is critical.
	/// </summary>
	public sealed class OptionalMethodInfo<T> {
		/// <summary>
		/// Creates a wrapper for a method with potentially optional parameters.
		/// </summary>
		/// <param name="type">The type containing the method.</param>
		/// <param name="name">The method name.</param>
		/// <param name="isStatic">true to match static methods, or false to match instance
		/// methods.</param>
		/// <param name="argumentTypes">The types of the intended arguments.</param>
		/// <returns>The specified method, or null if no clear match was found.</returns>
		public static OptionalMethodInfo<T> Create(Type type, string name, bool isStatic,
				params Type[] argumentTypes) {
			if (type == null)
				throw new ArgumentNullException("type");
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
				(isStatic ? BindingFlags.Static : BindingFlags.Instance));
			OptionalMethodInfo<T> info = null;
			if (methods != null) {
				var match = FindBestMatch(methods, name, argumentTypes);
				if (match != null)
					info = new OptionalMethodInfo<T>(match, argumentTypes.Length);
			}
			return info;
		}

		/// <summary>
		/// Finds the best method match for the specified method, allowing for optional
		/// arguments on the end.
		/// </summary>
		/// <param name="methods">The candidate methods to check.</param>
		/// <param name="name">The required method name.</param>
		/// <param name="argumentTypes">The argument types required.</param>
		/// <returns>A method with exactly those arguments, and possibly additional arguments
		/// that have default values; or null if no matching method or more than one matching
		/// method is found.</returns>
		private static MethodInfo FindBestMatch(MethodInfo[] methods, string name,
				Type[] argumentTypes) {
			int nArgs = argumentTypes.Length;
			bool multiple = false;
			MethodInfo result = null;
			// Get all methods with that name
			foreach (var cand in methods)
				if (cand.Name == name) {
					var parameters = cand.GetParameters();
					int n = parameters.Length;
					bool match = n >= nArgs;
					for (int i = 0; i < nArgs && match; i++)
						match = parameters[i].ParameterType == argumentTypes[i];
					if (match) {
						if (n == nArgs) {
							// Exact match
							result = cand;
							multiple = false;
							break;
						} else if (parameters[nArgs + 1].IsOptional) {
							// Further parameters by index must be optional anyways
							if (result != null)
								multiple = true;
							result = cand;
						}
					}
				}
			if (multiple)
				result = null;
			return result;
		}

		/// <summary>
		/// The number of parameters that are expected to be supplied.
		/// </summary>
		private readonly int knownParams;

		/// <summary>
		/// The method to be called.
		/// </summary>
		private readonly MethodInfo method;

		/// <summary>
		/// The template objects (default parameter values), with null in the leading positions
		/// where real arguments will be filled.
		/// </summary>
		private readonly object[] template;

		private OptionalMethodInfo(MethodInfo method, int knownParams) {
			var parameters = method.GetParameters();
			int n = parameters.Length;
			template = new object[n];
			for (int i = knownParams; i < n; i++)
				template[i] = parameters[i].DefaultValue;
			this.knownParams = knownParams;
			this.method = method;
		}

		/// <summary>
		/// Calls the target method, automatically filling in optional parameters.
		/// 
		/// This method can be slow, as it uses reflection.
		/// </summary>
		/// <param name="context">The object to use as the instance parameter, or null when
		/// used on a static method.</param>
		/// <param name="arguments">The arguments to use.</param>
		/// <returns>The return value of the method.</returns>
		/// <exception cref="ArgumentException">If the argument count is invalid or the types
		/// are not convertible to the expected argument types of the method.</exception>
		/// <exception cref="TargetInvocationException">If the method throws an exception, it
		/// will be wrapped in a TargetInvocationException.</exception>
		/// <exception cref="TargetException">If context is null for an instance method or is
		/// of the wrong type for that class.</exception>
		public T Invoke(object context, params object[] arguments) {
			int nProvided = (arguments == null) ? 0 : arguments.Length, actualParams =
				template.Length;
			if (nProvided != knownParams)
				throw new ArgumentException("Invalid parameter count - supplied {0:D}, expected {1:D}".
					F(nProvided, knownParams));
			object[] toUse;
			if (actualParams == nProvided)
				// Bypass template and invoke directly
				toUse = arguments;
			else {
				toUse = new object[actualParams];
				Array.Copy(template, toUse, actualParams);
				if (arguments != null)
					Array.Copy(arguments, toUse, nProvided);
			}
			return (method.Invoke(context, toUse) is T actualValue) ? actualValue : default;
		}

		/// <summary>
		/// Substitutes the generic argument parameters for the type parameters of a method.
		/// </summary>
		/// <param name="arguments">The generic types to be used.</param>
		/// <returns>A new method info representing the specific instance of the generic method.</returns>
		/// <exception cref="ArgumentException">If the generic arguments do not match the method.</exception>
		public OptionalMethodInfo<T> MakeGenericMethod(params Type[] arguments) {
			return new OptionalMethodInfo<T>(method.MakeGenericMethod(arguments), knownParams);
		}

		public override string ToString() {
			return "{0}.{1}({2})".F(method.DeclaringType.FullName, method.Name, method.
				GetParameterTypes().Join(", "));
		}
	}
}
