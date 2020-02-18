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

using Harmony;
using KMod;
using PeterHan.PLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Utility methods for Debug Not Included.
	/// </summary>
	internal static class DebugUtils {
		/// <summary>
		/// Binding flags used to find declared methods in a class of any visibility.
		/// </summary>
		private const BindingFlags DEC_METHODS = BindingFlags.Instance | BindingFlags.Static |
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		
		/// <summary>
		/// The pattern to match for generic types.
		/// </summary>
		private static readonly Regex GENERIC_TYPE = new Regex(@"([^<\[]+)[<\[](.+)[>\]]",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Prestores a list of shorthand types used by Mono in stack traces.
		/// </summary>
		private static readonly IDictionary<string, Type> SHORTHAND_TYPES =
			new Dictionary<string, Type> {
				{ "bool", typeof(bool) }, { "byte", typeof(byte) }, { "sbyte", typeof(sbyte) },
				{ "char", typeof(char) }, { "decimal", typeof(decimal) },
				{ "double", typeof(double) }, { "float", typeof(float) },
				{ "int", typeof(int) }, { "uint", typeof(uint) }, { "long", typeof(long) },
				{ "ulong", typeof(ulong) }, { "short", typeof(short) },
				{ "ushort", typeof(ushort) }, { "object", typeof(object) },
				{ "string", typeof(string) }
			};

		/// <summary>
		/// This method was adapted from Mono at https://github.com/mono/mono/blob/master/mcs/class/corlib/System.Diagnostics/StackTrace.cs
		/// which is available under the MIT License.
		/// </summary>
		internal static void AppendMethod(StringBuilder message, MethodBase method) {
			var declaringType = method.DeclaringType;
			// If this method was declared in a generic type, choose the right method to log
			if (declaringType.IsGenericType && !declaringType.IsGenericTypeDefinition) {
				declaringType = declaringType.GetGenericTypeDefinition();
				foreach (var m in declaringType.GetMethods(DEC_METHODS))
					if (m.MetadataToken == method.MetadataToken) {
						method = m;
						break;
					}
			}
			// Type and name
			message.Append(declaringType.ToString());
			message.Append(".");
			message.Append(method.Name);
			// If the method itself is generic, use its definition
			if (method.IsGenericMethod && (method is MethodInfo methodInfo)) {
				if (!method.IsGenericMethodDefinition)
					method = methodInfo.GetGenericMethodDefinition();
				message.Append("[");
				message.Append(method.GetGenericArguments().Join());
				message.Append("]");
			}
			// Parameters
			message.Append(" (");
			var parameters = method.GetParameters();
			for (int i = 0; i < parameters.Length; ++i) {
				var paramType = parameters[i].ParameterType;
				string name = parameters[i].Name;
				if (i > 0)
					message.Append(", ");
				// Parameter type, use the definition if possible
				if (paramType.IsGenericType && !paramType.IsGenericTypeDefinition)
					paramType = paramType.GetGenericTypeDefinition();
				message.Append(paramType.ToString());
				// Parameter name
				if (!string.IsNullOrEmpty(name)) {
					message.Append(" ");
					message.Append(name);
				}
			}
			message.Append(")");
		}

		/// <summary>
		/// Finds the best match for the specified method parameters types.
		/// </summary>
		/// <param name="candidates">The methods with the required name.</param>
		/// <param name="paramTypes">The parameter types that must be matched.</param>
		/// <returns>The best match found, or null if all matches sucked.</returns>
		internal static MethodBase BestEffortMatch(IEnumerable<MethodBase> candidates,
				Type[] paramTypes) {
			MethodBase method = null;
			ParameterInfo[] parameters;
			int nArgs = paramTypes.Length, bestMatch = -1;
			foreach (var candidate in candidates)
				// Argument count must match
				if ((parameters = candidate.GetParameters()).Length == nArgs) {
					int matched = 0;
					// Count types which match exactly
					for (int i = 0; i < nArgs; i++)
						if (paramTypes[i] == parameters[i].ParameterType)
							matched++;
					if (matched > bestMatch) {
						bestMatch = matched;
						method = candidate;
					}
				}
			return method;
		}

		/// <summary>
		/// Gets the mod that most recently appeared on the call stack in the provided
		/// exception.
		/// </summary>
		/// <param name="e">The exception thrown.</param>
		/// <returns>The mod that is most likely at fault, or null if no mods appear on the stack.</returns>
		internal static Mod GetFirstModOnCallStack(this Exception e) {
			Mod mod = null;
			if (e != null) {
				var stackTrace = new StackTrace(e);
				var registry = ModDebugRegistry.Instance;
				int n = stackTrace.FrameCount;
				// Search for first method that has a mod in the registry
				for (int i = 0; i < n && mod == null; i++) {
					var method = stackTrace.GetFrame(i)?.GetMethod();
					if (method != null)
						mod = registry.OwnerOfType(method.DeclaringType)?.Mod;
				}
			}
			return mod;
		}

		/// <summary>
		/// Gets information about what patched the method.
		/// </summary>
		/// <param name="method">The method to check.</param>
		/// <param name="message">The location where the message will be stored.</param>
		internal static void GetPatchInfo(MethodBase method, StringBuilder message) {
			var patches = ModDebugRegistry.Instance.DebugInstance.GetPatchInfo(method);
			if (patches != null) {
				GetPatchInfo(patches.Prefixes, "Prefixed", message);
				GetPatchInfo(patches.Postfixes, "Postfixed", message);
				GetPatchInfo(patches.Transpilers, "Transpiled", message);
			}
		}

		/// <summary>
		/// Gets information about patches on a method.
		/// </summary>
		/// <param name="patches">The patches applied to the method.</param>
		/// <param name="verb">The verb to describe these patches.</param>
		/// <param name="message">The location where the message will be stored.</param>
		private static void GetPatchInfo(IEnumerable<Patch> patches, string verb,
				StringBuilder message) {
			var registry = ModDebugRegistry.Instance;
			ModDebugInfo info;
			foreach (var patch in patches) {
				string owner = patch.owner;
				var method = patch.patch;
				// Try to resolve to the friendly mod name
				if ((info = registry.GetDebugInfo(owner)) != null)
					owner = info.ModName;
				message.AppendFormat("    {4} by {0}[{1:D}] from {2}.{3}", owner, patch.index,
					method.DeclaringType.FullName, method.Name, verb);
				message.AppendLine();
			}
		}

		/// <summary>
		/// Checks for a mod in a list of mod events.
		/// </summary>
		/// <param name="events">The mod events which occurred.</param>
		/// <param name="mod">The mod for which to look.</param>
		/// <returns>true if that mod is in the list, or false otherwise.</returns>
		internal static bool IncludesMod(this IEnumerable<Event> events, Label mod) {
			bool includes = false;
			if (events != null)
				foreach (var evt in events)
					if (mod.Match(evt.mod)) {
						includes = true;
						break;
					}
			return includes;
		}

		/// <summary>
		/// Returns whether the type is included in the base game assemblies.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns>true if it is included in the base game, or false otherwise.</returns>
		internal static bool IsBaseGameType(this Type type) {
			bool isBase = false;
			if (type != null) {
				var assembly = type.Assembly;
				isBase = assembly == typeof(Mod).Assembly || assembly == typeof(Tag).Assembly;
			}
			return isBase;
		}

		/// <summary>
		/// Finds the type for the given name using the method that Unity/Mono uses to report
		/// types internally.
		/// </summary>
		/// <param name="typeName">The type name.</param>
		/// <returns>The types for each type name, or null if no type could be resolved.</returns>
		internal static Type GetTypeByUnityName(this string typeName) {
			Type type = null;
			if (!string.IsNullOrEmpty(typeName) && !SHORTHAND_TYPES.TryGetValue(typeName,
					out type)) {
				// Generic type?
				var match = GENERIC_TYPE.Match(typeName);
				if (match.Success) {
					var parameters = match.Groups[2].Value.Split(',');
					int nParams = parameters.Length;
					// Convert parameters to types
					var paramTypes = new Type[nParams];
					for (int i = 0; i < nParams; i++)
						paramTypes[i] = parameters[i].GetTypeByUnityName();
					// Genericize it
					var baseType = AccessTools.TypeByName(match.Groups[1].Value);
					if (baseType != null && baseType.IsGenericTypeDefinition)
						type = baseType.MakeGenericType(paramTypes);
					else
						type = baseType;
				} else
					type = AccessTools.TypeByName(typeName);
			}
			return type;
		}
	}
}
