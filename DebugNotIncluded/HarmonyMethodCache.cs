/*
 * Copyright 2021 Peter Han
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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Helps debug patches on Harmony methods by winnowing them out of a stack trace.
	/// </summary>
	internal sealed class HarmonyMethodCache {
		/// <summary>
		/// The pattern to match for looking up dynamic methods.
		/// 
		/// GeneratedBuildings.LoadGeneratedBuildings_Patch6(System.Collections.Generic.List`1&lt;System.Type&gt;)
		/// </summary>
		private static readonly Regex DYNAMIC_METHOD = new Regex(
			@"\(wrapper dynamic-method\) ([^\(\)]+?)_Patch[0-9]*\(([^\(\)]*)\)",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// The method to call to get the "internal method name" of a stack frame. Used if the
		/// method is a dynamic or other native method.
		/// 
		/// Note that making this a delegate gains nothing, because the delegate has to be
		/// remade on each different stack frame object. Detouring is not worth it as the
		/// performance overhead only occurs when the game is crashing anyways.
		/// </summary>
		private static readonly MethodInfo GET_INTERNAL_METHOD_NAME = typeof(StackFrame).
			GetMethodSafe("GetInternalMethodName", false);

		/// <summary>
		/// The cache of methods.
		/// </summary>
		private readonly IDictionary<string, ICollection<MethodBase>> cache;

		internal HarmonyMethodCache() {
			var patched = HarmonyLib.Harmony.GetAllPatchedMethods();
			cache = new Dictionary<string, ICollection<MethodBase>>(512);
			// Cache the patched methods
			foreach (var method in patched) {
				string key = method.DeclaringType.FullName + "." + method.Name;
				if (!cache.TryGetValue(key, out ICollection<MethodBase> methods))
					cache[key] = methods = new List<MethodBase>(16);
				methods.Add(method);
			}
		}

		/// <summary>
		/// Parses the (wrapper dynamic-method) internal name into a method that Harmony can
		/// target.
		/// </summary>
		/// <param name="methodName">The method name as reported by Mono.</param>
		/// <param name="methodParams">The parameter types.</param>
		/// <returns>The method that it represents, or null if no matching method could be found.</returns>
		internal MethodBase ParseDynamicMethod(string methodName, string methodParams) {
			MethodBase method = null;
			// Harmony 2 will double up the type name
			int firstDot = methodName.IndexOf('.');
			if (firstDot > 0 && firstDot < methodName.Length - 1) {
				string prefix = methodName.Substring(0, firstDot), suffix = methodName.
					Substring(firstDot + 1);
				if (suffix.StartsWith(prefix, StringComparison.InvariantCulture))
					methodName = suffix;
			}
			if (cache.TryGetValue(methodName, out ICollection<MethodBase> methods)) {
				var typeNames = methodParams.Split(',');
				int nTypes = typeNames.Length;
				// Convert each type, strip the spaces between them
				var paramTypes = new Type[nTypes];
				for (int i = 0; i < nTypes; i++)
					paramTypes[i] = typeNames[i].Trim().GetTypeByUnityName();
				method = DebugUtils.BestEffortMatch(methods, paramTypes);
			}
			return method;
		}

		/// <summary>
		/// Handles stack frames with missing method information by attempting to parse the
		/// internal method name and look it up in Harmony.
		/// </summary>
		/// <param name="frame">The stack frame to inspect.</param>
		/// <param name="hmf">The cache of Harmony methods.</param>
		/// <param name="message">The location to put the message.</param>
		/// <returns>The method information that was parsed from Harmony, or null if none could be.</returns>
		internal MethodBase ParseInternalName(StackFrame frame, StringBuilder message) {
			string internalName = null, mName;
			MethodBase method = null;
			try {
				// If this call fails, it should not crash the whole method
				internalName = GET_INTERNAL_METHOD_NAME?.Invoke(frame, null) as string;
			} catch (TargetInvocationException) { }
			if (!string.IsNullOrEmpty(internalName)) {
				var match = DYNAMIC_METHOD.Match(internalName);
				// Group 0 is the whole string
				if (match.Success && !string.IsNullOrEmpty(mName = match.Groups[1].Value))
					method = ParseDynamicMethod(mName, match.Groups[2].Value);
				if (method == null) {
					// All else fails, log the internal name
					message.Append("  at ");
					message.AppendLine(internalName);
				}
			}
			return method;
		}

		public override string ToString() {
			return "HarmonyMethodFinder ({0:D} methods)".F(cache.Count);
		}
	}
}
