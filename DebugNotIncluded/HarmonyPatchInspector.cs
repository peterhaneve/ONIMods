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

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PeterHan.DebugNotIncluded {
	/// <summary>
	/// Inspects all local and dev mods, looking at my feeble attempts to write code for this
	/// game and pointing out gotchas, some of which only fail on Mac/Linux.
	/// </summary>
	public static class HarmonyPatchInspector {
		/// <summary>
		/// Assemblies beginning with these strings will be blacklisted from checks.
		/// </summary>
		private static readonly string[] BLACKLIST_ASSEMBLIES = new string[] {
			"mscorlib", "System", "Assembly-CSharp", "UnityEngine", "Harmony", "Newtonsoft"
		};

		/// <summary>
		/// Methods usually used in Harmony annotated patches.
		/// </summary>
		private static readonly ISet<string> HARMONY_NAMES = new HashSet<string>() {
			"Prefix", "Postfix", "TargetMethod", "Transpiler", "TargetMethods"
		};

		/// <summary>
		/// Denotes all bindable objects to inspect private types.
		/// </summary>
		private const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.Static | BindingFlags.Instance | BindingFlags.GetProperty |
			BindingFlags.SetProperty;

		/// <summary>
		/// Checks all types currently loaded for issues.
		/// </summary>
		public static void Check() {
			foreach (var type in GetAllTypes())
				CheckType(type);
		}

		/// <summary>
		/// Checks a declared Harmony annotation and warns if an inherited method is being
		/// patched, which "works" on Windows but fails on Mac OS X / Linux.
		/// </summary>
		/// <param name="target">The annotation to check.</param>
		/// <param name="patcher">The type which created the patch</param>
		internal static void CheckHarmonyMethod(HarmonyMethod target, Type patcher) {
			var targetType = target.declaringType;
			string name = target.methodName;
			const BindingFlags ONLY_DEC = ALL | BindingFlags.DeclaredOnly;
			if (targetType != null && !string.IsNullOrEmpty(name))
				try {
					PropertyInfo info;
					string targetName = targetType.FullName + "." + name;
					switch (target.methodType) {
					case MethodType.Normal:
						var argTypes = target.argumentTypes;
						// If no argument types, provide what we can
						if (((argTypes == null) ? targetType.GetMethod(name, ONLY_DEC) :
								targetType.GetMethod(name, ONLY_DEC, null, argTypes, null)) ==
								null)
							DebugLogger.LogWarning("Patch {0} targets inherited method {1}",
								patcher.FullName, targetName);
						break;
					case MethodType.Setter:
						info = targetType.GetProperty(name, ONLY_DEC);
						if (info?.GetSetMethod(true) == null)
							DebugLogger.LogWarning("Patch {0} targets inherited property {1}",
								patcher.FullName, targetName);
						break;
					case MethodType.Getter:
						info = targetType.GetProperty(name, ONLY_DEC);
						if (info?.GetGetMethod(true) == null)
							DebugLogger.LogWarning("Patch {0} targets inherited property {1}",
								patcher.FullName, targetName);
						break;
					default:
						break;
					}
				} catch (AmbiguousMatchException) { }
		}

		/// <summary>
		/// Checks the specified type and all of its nested types for issues.
		/// </summary>
		/// <param name="type">The type to check.</param>
		internal static void CheckType(Type type) {
			if (type == null)
				throw new ArgumentNullException("type");
			bool isAnnotated = false, hasMethods = false;
			foreach (var annotation in type.GetCustomAttributes(true))
				if (annotation is HarmonyPatch patch) {
					isAnnotated = true;
					break;
				}
			// Patchy method names?
			foreach (var method in type.GetMethods(ALL))
				if (HARMONY_NAMES.Contains(method.Name)) {
					hasMethods = true;
					break;
				}
			if (hasMethods && !isAnnotated)
				DebugLogger.LogWarning("Type " + type.FullName +
					" looks like a Harmony patch but does not have the annotation!");
		}

		/// <summary>
		/// Gets a list of all loaded types in the game that are not on the blacklist.
		/// </summary>
		/// <returns>A list of all types to inspect.</returns>
		public static IEnumerable<Type> GetAllTypes() {
			var types = new List<Type>(256);
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				string name = assembly.FullName;
				// Exclude assemblies on the blacklist
				bool blacklist = false;
				foreach (string entry in BLACKLIST_ASSEMBLIES)
					if (name.StartsWith(entry)) {
						blacklist = true;
						break;
					}
				if (!blacklist)
					try {
						// This will fail when used with Ony's mod manager
						types.AddRange(assembly.GetTypes());
					} catch (ReflectionTypeLoadException e) {
						HandleTypeLoadExceptions(e, assembly, types);
					}
			}
			return types;
		}

		/// <summary>
		/// Logs exceptions where some types in an assembly fail to load, and adds the types
		/// that did load to the list anyways.
		/// </summary>
		/// <param name="exception">The exception thrown.</param>
		/// <param name="assembly">The assembly that failed to fully load.</param>
		/// <param name="types">The location to store types that did load.</param>
		private static void HandleTypeLoadExceptions(ReflectionTypeLoadException exception,
				Assembly assembly, ICollection<Type> types) {
			var failedTypes = exception?.Types;
			DebugLogger.LogError("Error when loading types from " + assembly.FullName);
			if (failedTypes != null) {
				int n = failedTypes.Length;
				for (int i = 0; i < n; i++) {
					var type = failedTypes[i];
					var thrown = exception.LoaderExceptions[i];
					if (type != null)
						types.Add(type);
					else if (thrown != null)
						DebugLogger.LogException(thrown);
				}
			}
		}
	}
}
