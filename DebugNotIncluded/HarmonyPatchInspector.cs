/*
 * Copyright 2024 Peter Han
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
		/// Assemblies beginning with these strings will not be included in checks.
		/// </summary>
		private static readonly string[] DISALLOW_ASSEMBLIES = new[] {
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
		/// Checks the patch parameters of the method against the intended types.
		/// </summary>
		/// <param name="method">The method to inspect.</param>
		/// <param name="target">The target method it is patching.</param>
		private static void CheckPatchParameters(MethodBase method, MethodInfo target) {
			string patchName = (method.DeclaringType?.FullName ?? "") + "." + method.Name;
			var declaringType = target.DeclaringType;
			string targetName = (declaringType?.FullName ?? "") + "." + target.Name;
			foreach (var parameter in method.GetParameters()) {
				string name = parameter.Name;
				var pt = parameter.ParameterType;
				if (name == "__result") {
					// Result of method
					var rt = target.ReturnType;
					if (!IsAssignable(rt, pt))
						PUtil.LogWarning("Method {0} return type of {1} does not match patch {2} expected return type of {3}".
							F(targetName, rt.FullName, patchName, pt.FullName));
				} else if (name == "__instance") {
					// Instance type
					if (declaringType == null)
						PUtil.LogWarning("Patch {0} expects an instance type, but method {1} does not have a declaring type".
							F(patchName, target.Name));
					else if (target.IsStatic)
						PUtil.LogWarning("Patch {0} expects an instance type, but method {1} is static".
							F(patchName, targetName));
					else if (!IsAssignable(declaringType, pt))
						PUtil.LogWarning("Method {0} does not match patch {1} expected instance type of {2}".
							F(targetName, patchName, pt.FullName));
				} else if (name.StartsWith("___") && declaringType != null) {
					string fn = name.Substring(3);
					// Field names
					var targetField = declaringType.GetField(fn, PPatchTools.BASE_FLAGS |
						BindingFlags.Static | BindingFlags.Instance);
					if (targetField == null)
						PUtil.LogWarning("Patch {0} references a field named {1} not found on type {2}".
							F(patchName, fn, declaringType.FullName));
					else if (!IsAssignable(targetField.FieldType, pt))
						PUtil.LogWarning("Patch {0} references field {1} expecting type {2}, but actual type is {3}".
							F(patchName, fn, pt.FullName, targetField.FieldType.FullName));
				} else
					// Try to match parameters (Harmony should scream at these?)
					foreach (var mp in target.GetParameters())
						if (mp.Name == name) {
							if (!IsAssignable(mp.ParameterType, pt))
								PUtil.LogWarning("Method {0} parameter {1} has type {2}, but patch {3} parameter has type {4}".
									F(targetName, name, mp.ParameterType.FullName, patchName,
									pt.FullName));
							break;
						}
			}
		}

		/// <summary>
		/// Checks the specified type and all of its nested types for issues.
		/// </summary>
		/// <param name="type">The type to check.</param>
		internal static void CheckType(Type type) {
			HarmonyPatch harmonyAnnotation = null;
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			foreach (var annotation in type.GetCustomAttributes(true))
				if (annotation is HarmonyPatch hp)
					harmonyAnnotation = hp;
				else if (annotation.GetType().Name.Contains("ExcludeInspection"))
					return;
			try {
				if (harmonyAnnotation != null) {
					var info = harmonyAnnotation.info;
					var target = AccessTools.Method(info.declaringType, info.methodName,
						info.argumentTypes);
					if (target != null)
						// Check each patch for broken ___, __instance and __result parameters
						foreach (var method in type.GetMethods(ALL))
							if (IsHarmonyPatchMethod(method))
								CheckPatchParameters(method, target);
				} else
					// Patchy method names?
					foreach (var method in type.GetMethods(ALL))
						if (HARMONY_NAMES.Contains(method.Name)) {
							DebugLogger.LogWarning("Type " + type.FullName +
								" looks like a Harmony patch but does not have the annotation!");
							break;
						}
			} catch (TypeLoadException e) {
				DebugLogger.LogWarning("Unable to check type " + type.FullName);
				DebugLogger.LogException(e);
			} catch (AmbiguousMatchException e) {
				DebugLogger.LogWarning("Unable to check type " + type.FullName);
				DebugLogger.LogException(e);
			}
		}

		/// <summary>
		/// Gets a list of all loaded types in the game that are not on the blacklist.
		/// </summary>
		/// <returns>A list of all types to inspect.</returns>
		public static ICollection<Type> GetAllTypes() {
			var types = new List<Type>(256);
			var running = Assembly.GetExecutingAssembly();
			int bn = DISALLOW_ASSEMBLIES.Length;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				string name = assembly.FullName;
				// Exclude assemblies on the blacklist
				bool blacklist = false;
				for (int i = 0; i < bn && !blacklist; i++)
					blacklist = name.StartsWith(DISALLOW_ASSEMBLIES[i]);
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

		/// <summary>
		/// Checks to see if the patch parameter type can successfully target a type,
		/// respecting the use of ref to allow mutation.
		/// </summary>
		/// <param name="targetType">The target type as declared.</param>
		/// <param name="parameterType">The patch method's expected type.</param>
		/// <returns>true if the patch can apply against this type, or false otherwise.</returns>
		private static bool IsAssignable(Type targetType, Type parameterType) =>
			parameterType.IsAssignableFrom(targetType) || (parameterType.IsByRef &&
			(parameterType.GetElementType()?.IsAssignableFrom(targetType) ?? false));

		/// <summary>
		/// Checks to see if a method is a Harmony postfix or prefix method. Only valid on
		/// methods declared inside a properly annotated type.
		/// </summary>
		/// <param name="method">The method to inspect.</param>
		/// <returns>true if the method will be used as a Harmony postfix or prefix injection
		/// method, or false otherwise.</returns>
		private static bool IsHarmonyPatchMethod(MethodBase method) {
			string name = method.Name;
			bool isPatch = name.Contains("Prefix") || name.Contains("Postfix");
			var attributes = method.GetCustomAttributes();
			if (!isPatch)
				foreach (var attribute in attributes)
					if (attribute is HarmonyPostfix || attribute is HarmonyPrefix) {
						isPatch = true;
						break;
					}
			return isPatch;
		}
	}
}
