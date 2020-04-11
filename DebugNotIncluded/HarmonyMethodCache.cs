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

using PeterHan.PLib;
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
		/// remade on each different stack frame object.
		/// </summary>
		private static readonly MethodInfo GET_INTERNAL_METHOD_NAME = typeof(StackFrame).
			GetMethodSafe("GetInternalMethodName", false);

		/// <summary>
		/// The cache of methods.
		/// </summary>
		private readonly IDictionary<string, ICollection<MethodBase>> cache;

		internal HarmonyMethodCache() {
			var patched = ModDebugRegistry.Instance.DebugInstance.GetPatchedMethods();
			cache = new Dictionary<string, ICollection<MethodBase>>(256);
			// Cache the patched methods
			foreach (var method in patched) {
				string key = method.DeclaringType.FullName + "." + method.Name;
				if (!cache.TryGetValue(key, out ICollection<MethodBase> methods))
					cache[key] = methods = new List<MethodBase>(4);
				methods.Add(method);
			}
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
				if (match.Success && !string.IsNullOrEmpty(mName = match.Groups[1].Value) &&
						cache.TryGetValue(mName, out ICollection<MethodBase> methods)) {
					var typeNames = match.Groups[2].Value.Split(',');
					int nTypes = typeNames.Length;
					// Convert each type, strip the spaces between them
					var paramTypes = new Type[nTypes];
					for (int i = 0; i < nTypes; i++)
						paramTypes[i] = typeNames[i].Trim().GetTypeByUnityName();
					method = DebugUtils.BestEffortMatch(methods, paramTypes);
				}
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
