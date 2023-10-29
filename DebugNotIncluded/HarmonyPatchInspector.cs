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
using PeterHan.PLib.Core;
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
			"mscorlib", "System", "Assembly-CSharp", "Unity", "0Harmony", "Newtonsoft",
			"Mono", "ArabicSupport", "I18N", "Ionic", "com.rlabrecque.steamworks.net", "ImGui",
			"FMOD", "LibNoise", "Harmony", "Anonymously Hosted DynamicMethods Assembly",
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
		private const BindingFlags ALL = PPatchTools.BASE_FLAGS | BindingFlags.Static |
			BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty;

		/// <summary>
		/// Checks all types currently loaded for issues.
		/// </summary>
		public static void Check() {
			var types = GetAllTypes();
			DebugLogger.LogDebug("Inspecting {0:D} types".F(types.Count));
			foreach (var type in types)
				CheckType(type);
		}

		/// <summary>
		/// Checks the specified type and all of its nested types for issues.
		/// </summary>
		/// <param name="type">The type to check.</param>
		internal static void CheckType(Type type) {
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			bool isAnnotated = false;
			foreach (var annotation in type.GetCustomAttributes(true))
				if (annotation is HarmonyPatch) {
					isAnnotated = true;
					break;
				}
			if (!isAnnotated) {
				// Patchy method names?
				foreach (var method in type.GetMethods(ALL))
					if (HARMONY_NAMES.Contains(method.Name)) {
						DebugLogger.LogWarning("Type " + type.FullName +
							" looks like a Harmony patch but does not have the annotation!");
						break;
					}
			}
		}

		/// <summary>
		/// Gets a list of all loaded types in the game that are not on the blacklist.
		/// </summary>
		/// <returns>A list of all types to inspect.</returns>
		public static ICollection<Type> GetAllTypes() {
			var types = new List<Type>(256);
			var running = Assembly.GetExecutingAssembly();
			int bn = BLACKLIST_ASSEMBLIES.Length;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				string name = assembly.FullName;
				// Exclude assemblies on the blacklist
				bool blacklist = false;
				for (int i = 0; i < bn && !blacklist; i++)
					blacklist = name.StartsWith(BLACKLIST_ASSEMBLIES[i]);
				if (!blacklist)
					try {
						InspectAssembly(assembly, running, types);
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
				var tlExceptions = exception.LoaderExceptions;
				int n = failedTypes.Length, maxExceptions = tlExceptions.Length;
				for (int i = 0; i < n; i++) {
					var type = failedTypes[i];
					var thrown = i < maxExceptions ? tlExceptions[i] : null;
					if (type != null)
						types.Add(type);
					else if (thrown != null)
						DebugLogger.LogException(thrown);
				}
			}
		}

		/// <summary>
		/// Inspects the specified assembly and reports a list of types to check.
		/// </summary>
		/// <param name="assembly">The assembly to inspect.</param>
		/// <param name="running">The currently running assembly.</param>
		/// <param name="types">The location where the found types will be stored.</param>
		private static void InspectAssembly(Assembly assembly, Assembly running,
				ICollection<Type> types) {
			// This will fail when used with Ony's mod manager
			var asmTypes = assembly.GetTypes();
			int n = asmTypes.Length;
			for (int i = 0; i < n; i++) {
				var candidate = asmTypes[i];
				string typeName = candidate.FullName;
				// If the type is a PLib type, skip it
				if (assembly == running || string.IsNullOrEmpty(typeName) ||
						!typeName.StartsWith("PeterHan.PLib."))
					types.Add(candidate);
			}
		}
	}
}
